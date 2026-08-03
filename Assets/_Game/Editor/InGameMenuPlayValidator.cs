#if UNITY_EDITOR
using System;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame.Multiplayer;
using TheBestMonkeyGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Editor
{
    [InitializeOnLoad]
    public static class InGameMenuPlayValidator
    {
        private const string ActiveKey = "TBMG.InGameMenuValidation.Active";
        private const string PhaseKey = "TBMG.InGameMenuValidation.Phase";
        private const string ErrorsKey = "TBMG.InGameMenuValidation.Errors";
        private const string DetailsKey = "TBMG.InGameMenuValidation.Details";
        private const string FramesKey = "TBMG.InGameMenuValidation.Frames";
        private static bool opened;
        private static bool openValidated;
        private static bool closed;
        private static bool closeValidated;
        private static bool leaveStarted;

        static InGameMenuPlayValidator()
        {
            if (SessionState.GetBool(ActiveKey, false)) Attach();
        }

        [MenuItem("Tools/The Best Monkey Game/In-Game Menu/Validate Single Player Play Mode")]
        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(PhaseKey, "starting");
            SessionState.SetInt(ErrorsKey, 0);
            SessionState.SetString(DetailsKey, string.Empty);
            SessionState.SetInt(FramesKey, 0);
            ResetFlags();
            Attach();
            try
            {
                InGameMenuMilestoneBuilder.Validate();
                EditorSceneManager.OpenScene(RevisionBootstrap.MainScenePath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                RecordError(exception.ToString());
                Finish();
            }
        }

        private static void Attach()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void Detach()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Application.logMessageReceived -= OnLog;
        }

        private static void ResetFlags()
        {
            opened = false;
            openValidated = false;
            closed = false;
            closeValidated = false;
            leaveStarted = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(PhaseKey, "playing");
                SessionState.SetInt(FramesKey, 0);
                ResetFlags();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) SessionState.SetString(PhaseKey, "exiting");
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(PhaseKey, string.Empty) == "exiting") Finish();
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                RecordError($"{type}: {condition}\n{stackTrace}");
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            int frames = SessionState.GetInt(FramesKey, 0) + 1;
            SessionState.SetInt(FramesKey, frames);

            InGameMenuController menu = UnityEngine.Object.FindFirstObjectByType<InGameMenuController>();
            if (SceneManager.GetActiveScene().name == MultiplayerConstants.SinglePlayerScene)
            {
                if (frames >= 45 && !opened && menu != null)
                {
                    Rigidbody body = menu.GetComponentInParent<Rigidbody>();
                    if (body != null) body.linearVelocity = new Vector3(3f, 1f, 0f);
                    menu.SetOpen(true);
                    opened = true;
                }
                if (opened && !openValidated && frames >= 48)
                {
                    ValidateOpen(menu);
                    openValidated = true;
                }
                if (openValidated && !closed && frames >= 72)
                {
                    menu.SetOpen(false);
                    closed = true;
                }
                if (closed && !closeValidated && frames >= 76)
                {
                    ValidateClosed(menu);
                    closeValidated = true;
                }
                if (closeValidated && !leaveStarted && frames >= 100)
                {
                    menu.SetOpen(true);
                    _ = menu.LeaveGameAsync();
                    leaveStarted = true;
                }
            }
            else if (leaveStarted && SceneManager.GetActiveScene().name == MultiplayerConstants.MainMenuScene)
            {
                ValidateMainMenuReturn();
                EditorApplication.ExitPlaymode();
            }

            if (frames > 600)
            {
                RecordError($"In-game menu validation timed out in {SceneManager.GetActiveScene().name}.");
                EditorApplication.ExitPlaymode();
            }
        }

        private static void ValidateOpen(InGameMenuController menu)
        {
            if (menu == null) { RecordError("InGameMenuController disappeared while opening."); return; }
            Player player = menu.GetComponentInParent<Player>();
            Rigidbody body = menu.GetComponentInParent<Rigidbody>();
            VRTurningController[] turners = player.GetComponents<VRTurningController>();
            XRTrackedPose[] tracking = player.GetComponentsInChildren<XRTrackedPose>(true);
            VRControllerRaycaster[] rays = player.GetComponentsInChildren<VRControllerRaycaster>(true).Where(item => item.enabled).ToArray();
            if (!menu.IsOpen) RecordError("Menu did not enter its open state.");
            if (menu.ActionPath != InGameMenuMilestoneBuilder.MenuActionPath || menu.BindingPath != InGameMenuMilestoneBuilder.MenuBindingPath)
                RecordError($"Unexpected input action {menu.ActionPath} / {menu.BindingPath}.");
            if (player.enabled || !player.disableMovement) RecordError("GorillaLocomotion remained active while the menu was open.");
            if (body == null || !body.isKinematic || body.linearVelocity.sqrMagnitude > 0.0001f || body.angularVelocity.sqrMagnitude > 0.0001f)
                RecordError("Rigidbody movement was not fully suspended and cleared.");
            if (turners.Length != 1 || turners[0].enabled) RecordError("Turning remained active while the menu was open.");
            if (tracking.Length < 3 || tracking.Any(item => !item.enabled)) RecordError("Head/controller tracking was disabled by the menu.");
            if (rays.Length != 2) RecordError($"Expected two local controller rays while open; found {rays.Length}.");
            if (UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length != 1)
                RecordError("The menu did not maintain exactly one local EventSystem.");
            if (Mathf.Abs(Time.timeScale - 1f) > 0.0001f) RecordError("The menu changed global Time.timeScale.");
            Debug.Log($"IN_GAME_MENU_OPEN_RUNTIME movementBlocked={!player.enabled && player.disableMovement} tracking={tracking.Length} rays={rays.Length} timeScale={Time.timeScale:0.0}");
        }

        private static void ValidateClosed(InGameMenuController menu)
        {
            if (menu == null) { RecordError("InGameMenuController disappeared while resuming."); return; }
            Player player = menu.GetComponentInParent<Player>();
            Rigidbody body = menu.GetComponentInParent<Rigidbody>();
            VRTurningController turning = player.GetComponent<VRTurningController>();
            int rays = player.GetComponentsInChildren<VRControllerRaycaster>(true).Count(item => item.enabled);
            if (menu.IsOpen) RecordError("Resume did not close the menu.");
            if (!player.enabled || player.disableMovement) RecordError("Resume did not restore GorillaLocomotion.");
            if (body == null || body.isKinematic || body.linearVelocity.sqrMagnitude > 0.0001f || body.angularVelocity.sqrMagnitude > 0.0001f)
                RecordError("Resume left stale velocity or an incorrect Rigidbody state.");
            if (turning == null || !turning.enabled) RecordError("Resume did not restore turning.");
            if (rays != 0) RecordError($"Controller menu rays remained active after Resume ({rays}).");
            if (Mathf.Abs(Time.timeScale - 1f) > 0.0001f) RecordError("Resume observed a modified Time.timeScale.");
            Debug.Log($"IN_GAME_MENU_RESUME_RUNTIME movementRestored={player.enabled && !player.disableMovement} staleVelocity={body.linearVelocity.magnitude:0.000} rays={rays}");
        }

        private static void ValidateMainMenuReturn()
        {
            int cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(item => item.isActiveAndEnabled);
            int listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(item => item.isActiveAndEnabled);
            int systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length;
            if (cameras != 1 || listeners != 1 || systems != 1)
                RecordError($"MainMenu return has cameras={cameras}, listeners={listeners}, EventSystems={systems}.");
            Debug.Log($"IN_GAME_MENU_SINGLEPLAYER_LEAVE_RUNTIME mainMenu=true cameras={cameras} listeners={listeners} eventSystems={systems}");
        }

        private static void RecordError(string message)
        {
            SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
            SessionState.SetString(DetailsKey, SessionState.GetString(DetailsKey, string.Empty) + "\n" + message);
        }

        private static void Finish()
        {
            int errors = SessionState.GetInt(ErrorsKey, 0);
            string details = SessionState.GetString(DetailsKey, string.Empty);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetString(PhaseKey, "done");
            Detach();
            if (errors == 0)
            {
                Debug.Log("IN_GAME_MENU_SINGLEPLAYER_PLAYMODE_SUCCESS toggle=true resume=true leave=true movement=true tracking=true staleVelocity=false timeScaleUnchanged=true errors=0");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"IN_GAME_MENU_SINGLEPLAYER_PLAYMODE_FAILED errors={errors}{details}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
