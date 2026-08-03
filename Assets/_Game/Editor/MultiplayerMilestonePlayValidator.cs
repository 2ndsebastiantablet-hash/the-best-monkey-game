#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using TheBestMonkeyGame.Monsters;
using TheBestMonkeyGame.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Editor
{
    [InitializeOnLoad]
    public static class MultiplayerMilestonePlayValidator
    {
        private const string ActiveKey = "TBMG.MultiplayerValidation.Active";
        private const string PhaseKey = "TBMG.MultiplayerValidation.Phase";
        private const string ErrorsKey = "TBMG.MultiplayerValidation.Errors";
        private const string DetailsKey = "TBMG.MultiplayerValidation.Details";
        private const string FramesKey = "TBMG.MultiplayerValidation.Frames";
        private static bool localStartRequested;
        private static bool lobbyValidated;
        private static bool leaveRequested;

        static MultiplayerMilestonePlayValidator()
        {
            if (SessionState.GetBool(ActiveKey, false)) Attach();
        }

        [MenuItem("Tools/The Best Monkey Game/Multiplayer/Validate Local Play Mode")]
        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(PhaseKey, "starting");
            SessionState.SetInt(ErrorsKey, 0);
            SessionState.SetString(DetailsKey, string.Empty);
            SessionState.SetInt(FramesKey, 0);
            localStartRequested = false;
            lobbyValidated = false;
            leaveRequested = false;
            Attach();
            try
            {
                MultiplayerMilestoneBuilder.ValidateMilestone();
                EditorSceneManager.OpenScene(MultiplayerMilestoneBuilder.MainMenuPath, OpenSceneMode.Single);
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
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) RecordError($"{type}: {condition}\n{stackTrace}");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(PhaseKey, "playing");
                SessionState.SetInt(FramesKey, 0);
                localStartRequested = false;
                lobbyValidated = false;
                leaveRequested = false;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) SessionState.SetString(PhaseKey, "exiting");
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(PhaseKey, string.Empty) == "exiting") Finish();
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            int frames = SessionState.GetInt(FramesKey, 0) + 1;
            SessionState.SetInt(FramesKey, frames);

            if (frames == 20) ValidateMenuRuntime();
            if (frames >= 35 && !localStartRequested)
            {
                localStartRequested = true;
                BeginLocalHost();
            }
            if (!lobbyValidated && SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene)
            {
                NetworkVRPlayer player = UnityEngine.Object.FindFirstObjectByType<NetworkVRPlayer>();
                if (player != null && player.IsSpawned)
                {
                    ValidateLobbyRuntime(player);
                    lobbyValidated = true;
                }
            }
            if (lobbyValidated && frames > 160 && !leaveRequested)
            {
                leaveRequested = true;
                LeaveLocalRoom();
            }
            if (leaveRequested && frames > 220 && SceneManager.GetActiveScene().name == MultiplayerConstants.MainMenuScene)
            {
                if (GameBootstrap.Instance.Connection.IsListening) RecordError("NetworkManager was still listening after Leave Room.");
                EditorApplication.ExitPlaymode();
            }
            else if (frames > 600)
            {
                RecordError($"Timed out during local multiplayer validation in scene {SceneManager.GetActiveScene().name}.");
                EditorApplication.ExitPlaymode();
            }
        }

        private static async void BeginLocalHost()
        {
            try
            {
                if (GameBootstrap.Instance == null) throw new InvalidOperationException("GameBootstrap did not initialize in MainMenu.");
                await GameBootstrap.Instance.Sessions.StartLocalHostAsync();
            }
            catch (Exception exception) { RecordError("Local host start failed: " + exception); }
        }

        private static async void LeaveLocalRoom()
        {
            try { await GameBootstrap.Instance.Sessions.LeaveAsync(); }
            catch (Exception exception) { RecordError("Local leave failed: " + exception); }
        }

        private static void ValidateMenuRuntime()
        {
            GameBootstrap bootstrap = GameBootstrap.Instance;
            if (bootstrap == null) { RecordError("MainMenu has no persistent GameBootstrap."); return; }
            LocalPlayerProfile profile = bootstrap.Profile.Current;
            if (profile == null || profile.Turning != TurningMode.Snap || Mathf.Abs(profile.SnapTurnAngle - 45f) > 0.01f || Mathf.Abs(profile.SmoothTurnSpeed - 90f) > 0.01f)
                RecordError($"Profile defaults invalid: turn={profile?.Turning}, snap={profile?.SnapTurnAngle}, smooth={profile?.SmoothTurnSpeed}.");
            if (UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length != 1)
                RecordError("MainMenu must have exactly one active local EventSystem.");
            if (UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera => camera.isActiveAndEnabled) != 1)
                RecordError("MainMenu must have exactly one active camera.");
        }

        private static void ValidateLobbyRuntime(NetworkVRPlayer player)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(camera => camera.isActiveAndEnabled).ToArray();
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(listener => listener.isActiveAndEnabled).ToArray();
            if (cameras.Length != 1 || listeners.Length != 1) RecordError($"Lobby owner has cameras={cameras.Length}, audioListeners={listeners.Length}; expected exactly one each.");
            if (!player.IsOwner || player.Identity == null || player.Identity.DisplayName != GameBootstrap.Instance.Profile.Current.DisplayName)
                RecordError("Local player ownership or synchronized display name is invalid.");
            Transform local = player.transform.Find("LocalPlayerRoot");
            Transform remote = player.transform.Find("RemoteVisualRoot");
            if (local == null || remote == null || !local.gameObject.activeSelf || remote.gameObject.activeSelf)
                RecordError("Owner/remote visual split is invalid for the local player.");
            if (local == null || local.GetComponent<VRTurningController>() == null || local.GetComponent<DevelopmentPoseSimulator>() == null)
                RecordError("Local network player is missing turning or editor pose simulation.");
            if (UnityEngine.Object.FindObjectsByType<MonsterBrain>(FindObjectsSortMode.None).Length != 0)
                RecordError("A monster was present in the multiplayer waiting room.");
            if (UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length != 1)
                RecordError("MultiplayerLobby must have exactly one active local EventSystem.");
            Debug.Log($"MULTIPLAYER_LOCAL_RUNTIME_CHECK owner={player.OwnerClientId} name={player.Identity.DisplayName} color={player.Identity.ColorIndex} cameras={cameras.Length} listeners={listeners.Length}");
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
                Debug.Log("MULTIPLAYER_LOCAL_PLAYMODE_SUCCESS host=1 clients=0 ownership=true poseSimulation=true leaveCleanup=true errors=0");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"MULTIPLAYER_LOCAL_PLAYMODE_FAILED errors={errors}{details}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
