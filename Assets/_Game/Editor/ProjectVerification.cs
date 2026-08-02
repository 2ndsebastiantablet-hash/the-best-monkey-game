#if UNITY_EDITOR
using System;
using System.Linq;
using GorillaLocomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace TheBestMonkeyGame.Editor
{
    [InitializeOnLoad]
    public static class ProjectVerification
    {
        private const string ActiveKey = "TBMG.Verification.Active";
        private const string PhaseKey = "TBMG.Verification.Phase";
        private const string ErrorCountKey = "TBMG.Verification.ErrorCount";
        private const string ErrorTextKey = "TBMG.Verification.ErrorText";
        private const string FrameCountKey = "TBMG.Verification.FrameCount";
        private const string ScenePath = "Assets/_Game/Scenes/LocomotionTest.unity";
        private const string PrefabPath = "Assets/_Game/Prefabs/VRPlayer.prefab";

        static ProjectVerification()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        [MenuItem("Tools/The Best Monkey Game/Verify Play Mode")]
        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(PhaseKey, "starting");
            SessionState.SetInt(ErrorCountKey, 0);
            SessionState.SetInt(FrameCountKey, 0);
            SessionState.SetString(ErrorTextKey, string.Empty);
            Attach();

            try
            {
                ValidateProjectStructure();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                RecordError($"{type}: {condition}\n{stackTrace}");
            }
        }

        private static void RecordError(string message)
        {
            SessionState.SetInt(ErrorCountKey, SessionState.GetInt(ErrorCountKey, 0) + 1);
            string prior = SessionState.GetString(ErrorTextKey, string.Empty);
            SessionState.SetString(ErrorTextKey, prior + "\n" + message);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(PhaseKey, "playing");
                SessionState.SetInt(FrameCountKey, 0);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetString(PhaseKey, "exiting");
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(PhaseKey, string.Empty) == "exiting")
            {
                Finish();
            }
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying)
            {
                return;
            }

            int frameCount = SessionState.GetInt(FrameCountKey, 0) + 1;
            SessionState.SetInt(FrameCountKey, frameCount);
            if (frameCount >= 180)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private static void ValidateProjectStructure()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<Player>() == null || prefab.GetComponent<Rigidbody>() == null)
            {
                throw new InvalidOperationException("VRPlayer prefab is missing required Player or Rigidbody components.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Surface[] surfaces = UnityEngine.Object.FindObjectsByType<Surface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Player[] players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!scene.IsValid() || surfaces.Length < 20 || players.Length != 1 || Camera.main == null)
            {
                throw new InvalidOperationException($"Scene validation failed: surfaces={surfaces.Length}, players={players.Length}, mainCamera={Camera.main != null}.");
            }

            XRGeneralSettings xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (xrSettings?.Manager == null || !xrSettings.Manager.activeLoaders.Any(loader => loader is UnityEngine.XR.OpenXR.OpenXRLoader))
            {
                throw new InvalidOperationException("Android OpenXR loader is not assigned.");
            }

            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            string[] enabledFeatures = openXr.GetFeatures().Where(feature => feature.enabled).Select(feature => feature.GetType().Name).ToArray();
            if (!enabledFeatures.Contains("MetaQuestFeature") || !enabledFeatures.Any(name => name.Contains("ControllerProfile")))
            {
                throw new InvalidOperationException("Meta Quest support or a controller interaction profile is not enabled.");
            }

            Debug.Log($"STRUCTURE_VALIDATION_SUCCESS surfaces={surfaces.Length} enabledOpenXRFeatures={string.Join(",", enabledFeatures)}");
        }

        private static void Finish()
        {
            int errors = SessionState.GetInt(ErrorCountKey, 0);
            string details = SessionState.GetString(ErrorTextKey, string.Empty);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetString(PhaseKey, "done");
            Detach();

            if (errors == 0)
            {
                Debug.Log("PLAYMODE_VERIFICATION_SUCCESS frames=180 errors=0");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"PLAYMODE_VERIFICATION_FAILED errors={errors}{details}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
