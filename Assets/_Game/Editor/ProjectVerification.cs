#if UNITY_EDITOR
using System;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame;
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

        static ProjectVerification()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        [MenuItem("Tools/The Best Monkey Game/Verify Revision Play Mode")]
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
            SessionState.SetString(ErrorTextKey, SessionState.GetString(ErrorTextKey, string.Empty) + "\n" + message);
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
            int frames = SessionState.GetInt(FrameCountKey, 0) + 1;
            SessionState.SetInt(FrameCountKey, frames);
            if (frames >= 180)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private static void ValidateProjectStructure()
        {
            ValidatePlayerPrefab();

            Scene scene = EditorSceneManager.OpenScene(RevisionBootstrap.MainScenePath, OpenSceneMode.Single);
            Player[] players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Surface[] surfaces = UnityEngine.Object.FindObjectsByType<Surface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            MeshCollider[] mapColliders = surfaces.Select(item => item.GetComponent<MeshCollider>()).Where(item => item != null).ToArray();
            GameObject spawn = GameObject.Find("PlayerSpawn");

            if (!scene.IsValid() || players.Length != 1 || Camera.main == null || spawn == null)
            {
                throw new InvalidOperationException($"Main scene objects invalid: players={players.Length}, camera={Camera.main != null}, spawn={spawn != null}.");
            }
            if (surfaces.Length != 4 || mapColliders.Length != 4 || mapColliders.Any(collider => collider.convex || collider.isTrigger))
            {
                throw new InvalidOperationException($"Map collision invalid: surfaces={surfaces.Length}, meshColliders={mapColliders.Length}.");
            }
            if (surfaces.Any(surface => surface.gameObject.layer != 8 || surface.gameObject.tag != "LocomotionSurface"))
            {
                throw new InvalidOperationException("Every collidable map mesh must use Locomotion layer and LocomotionSurface tag.");
            }

            PlayerRespawn respawn = players[0].GetComponent<PlayerRespawn>();
            if (respawn == null || respawn.SpawnPoint != spawn.transform || Mathf.Abs(players[0].transform.position.y - spawn.transform.position.y) > 0.001f)
            {
                throw new InvalidOperationException("Player spawn assignment is inconsistent.");
            }

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length < 2 || buildScenes[0].path != RevisionBootstrap.MainScenePath || !buildScenes[0].enabled)
            {
                throw new InvalidOperationException("MainMap must be the first enabled build scene.");
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
                throw new InvalidOperationException("Meta Quest support or controller interaction profile is not enabled.");
            }

            Debug.Log($"REVISION_STRUCTURE_VALIDATION_SUCCESS surfaces={surfaces.Length} meshColliders={mapColliders.Length} spawn={spawn.transform.position:F3}");
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RevisionBootstrap.PlayerPrefabPath);
            if (prefab == null || prefab.GetComponent<Player>() == null || prefab.GetComponent<Rigidbody>() == null)
            {
                throw new InvalidOperationException("VRPlayer prefab is missing Player or Rigidbody.");
            }

            Transform tracking = prefab.transform.Find("TrackingSpace");
            Transform head = prefab.transform.Find("TrackingSpace/Head");
            Transform left = prefab.transform.Find("HandCollisionFollowers/LeftHand");
            Transform right = prefab.transform.Find("HandCollisionFollowers/RightHand");
            XRFloorTrackingOrigin origin = prefab.GetComponent<XRFloorTrackingOrigin>();
            if (tracking == null || head == null || origin == null || Mathf.Abs(tracking.localPosition.y) > 0.001f || head.localPosition.y > 0.3f || Mathf.Abs(origin.PlayerFloorOffset) > 0.001f)
            {
                throw new InvalidOperationException("Floor-origin player hierarchy is not calibrated to zero.");
            }

            ValidatePhysicsAndVisualHand(left, "LeftVisualHand_SourceMesh");
            ValidatePhysicsAndVisualHand(right, "RightVisualHand_SourceMesh");

            Transform body = prefab.transform.Find("Visuals/GorillaBody/CompleteTemporaryModel");
            if (body == null || body.GetComponent<MeshRenderer>() == null || body.GetComponent<Collider>() != null)
            {
                throw new InvalidOperationException("Complete collider-free temporary body visual is missing.");
            }
        }

        private static void ValidatePhysicsAndVisualHand(Transform hand, string visualName)
        {
            if (hand == null)
            {
                throw new InvalidOperationException("Authoritative hand transform is missing.");
            }
            SphereCollider sphere = hand.GetComponent<SphereCollider>();
            Renderer physicsRenderer = hand.GetComponent<Renderer>();
            Transform visual = hand.Find(visualName);
            if (sphere == null || !sphere.enabled || physicsRenderer == null || physicsRenderer.enabled ||
                visual == null || visual.GetComponent<MeshRenderer>() == null || visual.GetComponentInChildren<Collider>() != null)
            {
                throw new InvalidOperationException($"Physics/visual hand contract invalid for {hand.name}.");
            }
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
                Debug.Log("REVISION_PLAYMODE_VERIFICATION_SUCCESS frames=180 errors=0");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            else
            {
                Debug.LogError($"REVISION_PLAYMODE_VERIFICATION_FAILED errors={errors}{details}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
#endif
