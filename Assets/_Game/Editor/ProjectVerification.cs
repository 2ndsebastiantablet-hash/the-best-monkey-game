#if UNITY_EDITOR
using System;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame;
using TheBestMonkeyGame.Monsters;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
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
            if (frames == 300)
            {
                UnityEngine.Object.FindFirstObjectByType<TiptoeBrain>()?.ActivateImmediatelyForDevelopment();
                UnityEngine.Object.FindFirstObjectByType<StatueBrain>()?.ActivateImmediatelyForDevelopment();
            }
            if (frames == 420)
            {
                ValidateRuntimeMonsters();
            }
            if (frames == 480)
            {
                PlayerDeathController death = UnityEngine.Object.FindFirstObjectByType<PlayerDeathController>();
                TiptoeBrain tiptoe = UnityEngine.Object.FindFirstObjectByType<TiptoeBrain>();
                death?.BeginDeath(tiptoe);
            }
            if (frames == 620)
            {
                ValidateDeathRespawnRecovery();
            }
            if (frames >= 720)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private static void ValidateRuntimeMonsters()
        {
            TiptoeBrain tiptoe = UnityEngine.Object.FindFirstObjectByType<TiptoeBrain>();
            StatueBrain statue = UnityEngine.Object.FindFirstObjectByType<StatueBrain>();
            Player player = UnityEngine.Object.FindFirstObjectByType<Player>();
            PlayerRespawn respawn = player != null ? player.GetComponent<PlayerRespawn>() : null;
            PlayerDeathController death = player != null ? player.GetComponent<PlayerDeathController>() : null;
            if (tiptoe == null || statue == null || tiptoe.State == MonsterState.Dormant || statue.State == MonsterState.Dormant ||
                !tiptoe.Navigation.Agent.isOnNavMesh || !statue.Navigation.Agent.isOnNavMesh || player == null || player.disableMovement)
            {
                RecordError(
                    $"Runtime monster activation failed: Tiptoe={tiptoe?.State}, Statue={statue?.State}, " +
                    $"TiptoeOnNavMesh={tiptoe?.Navigation?.Agent?.isOnNavMesh}, StatueOnNavMesh={statue?.Navigation?.Agent?.isOnNavMesh}, " +
                    $"movementDisabled={player?.disableMovement}, resetting={respawn?.IsResetting}, deathActive={death?.DeathActive}, " +
                    $"playerPosition={player?.transform.position}, velocity={player?.PlayerRigidBody?.linearVelocity}.");
            }
        }

        private static void ValidateDeathRespawnRecovery()
        {
            Player player = UnityEngine.Object.FindFirstObjectByType<Player>();
            PlayerRespawn respawn = player != null ? player.GetComponent<PlayerRespawn>() : null;
            PlayerDeathController death = player != null ? player.GetComponent<PlayerDeathController>() : null;
            TiptoeBrain tiptoe = UnityEngine.Object.FindFirstObjectByType<TiptoeBrain>();
            Renderer[] handRenderers = player != null
                ? player.transform.Find("GorillaLocomotion").GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();

            if (player == null || respawn == null || death == null || tiptoe == null || death.DeathActive ||
                respawn.IsResetting || !respawn.IsSpawnProtected || player.disableMovement ||
                Vector3.Distance(player.transform.position, respawn.SpawnPoint.position) > 0.05f ||
                Vector3.Distance(tiptoe.transform.position, player.transform.position) < tiptoe.MinimumSpawnDistance ||
                handRenderers.Length < 2 || handRenderers.Any(renderer => !renderer.enabled))
            {
                RecordError(
                    $"Death/respawn recovery failed: deathActive={death?.DeathActive}, resetting={respawn?.IsResetting}, " +
                    $"protected={respawn?.IsSpawnProtected}, movementDisabled={player?.disableMovement}, " +
                    $"playerPosition={player?.transform.position}, spawnPosition={respawn?.SpawnPoint?.position}, " +
                    $"tiptoeDistance={(player != null && tiptoe != null ? Vector3.Distance(tiptoe.transform.position, player.transform.position) : -1f):F2}, " +
                    $"visibleHands={handRenderers.Count(renderer => renderer.enabled)}.");
            }
        }

        private static void ValidateProjectStructure()
        {
            ValidatePlayerPrefab();
            ValidateArchitecturalMeasurements();

            Scene scene = EditorSceneManager.OpenScene(RevisionBootstrap.MainScenePath, OpenSceneMode.Single);
            Player[] players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Surface[] surfaces = UnityEngine.Object.FindObjectsByType<Surface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject map = GameObject.Find("GiggleFartsMap");
            GameObject spawn = GameObject.Find("PlayerSpawn");
            if (!scene.IsValid() || players.Length != 1 || Camera.main == null || map == null || spawn == null)
            {
                throw new InvalidOperationException(
                    $"Main scene invalid: players={players.Length}, camera={Camera.main != null}, map={map != null}, spawn={spawn != null}.");
            }

            Collider[] mapColliders = map.GetComponentsInChildren<Collider>(true);
            MeshCollider[] meshColliders = mapColliders.OfType<MeshCollider>().ToArray();
            if (surfaces.Length != 4 || mapColliders.Length != 4 || meshColliders.Length != 4 ||
                meshColliders.Any(collider => collider.convex || collider.isTrigger))
            {
                throw new InvalidOperationException(
                    $"Map collision invalid: surfaces={surfaces.Length}, allColliders={mapColliders.Length}, meshColliders={meshColliders.Length}.");
            }
            if (surfaces.Any(surface => surface.gameObject.layer != 8 || surface.gameObject.tag != "LocomotionSurface"))
            {
                throw new InvalidOperationException("Every collidable map mesh must use the Locomotion layer and LocomotionSurface tag.");
            }

            Vector3 mapScale = map.transform.localScale;
            if (!Approximately(mapScale, Vector3.one * RevisionBootstrap.MapScaleMultiplier))
            {
                throw new InvalidOperationException($"Complete map root scale is {mapScale}, expected uniform {RevisionBootstrap.MapScaleMultiplier:F2}.");
            }
            Bounds bounds = CalculateRendererBounds(map);
            if (bounds.size.x < 99f || bounds.size.x > 102f || bounds.size.z < 104f || bounds.size.z > 107f ||
                bounds.size.y < 5f || bounds.size.y > 5.4f)
            {
                throw new InvalidOperationException($"Rescaled map bounds are unexpected: {bounds.size:F3}.");
            }

            Player player = players[0];
            PlayerRespawn respawn = player.GetComponent<PlayerRespawn>();
            if (respawn == null || respawn.SpawnPoint != spawn.transform || !Approximately(player.transform.localScale, Vector3.one))
            {
                throw new InvalidOperationException("Player spawn wiring or root scale is invalid.");
            }
            if (Vector3.Distance(player.transform.position, spawn.transform.position) > 0.001f)
            {
                throw new InvalidOperationException("Player root is not placed exactly at PlayerSpawn.");
            }
            if (!Physics.Raycast(spawn.transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit floorHit, 1f, 1 << 8) ||
                Mathf.Abs(floorHit.point.y - spawn.transform.position.y) > 0.015f)
            {
                throw new InvalidOperationException(
                    $"PlayerSpawn is not on the map floor: spawnY={spawn.transform.position.y:F3}, hitY={floorHit.point.y:F3}.");
            }

            ValidateMonsterScene(player.transform);

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length < 2 || buildScenes[0].path != RevisionBootstrap.MainScenePath || !buildScenes[0].enabled ||
                buildScenes.Any(item => item.path == MonsterRevisionBootstrap.ExperimentalJumpscareScenePath))
            {
                throw new InvalidOperationException("MainMap must be the first enabled build scene.");
            }
            ValidateOpenXr();

            Debug.Log(
                $"SCALE_PLAYER_STRUCTURE_VALIDATION_SUCCESS mapScale={mapScale.x:F2} bounds={bounds.size:F2} " +
                $"doorway={RevisionBootstrap.MeasuredDoorwayHeight:F2} colliders={mapColliders.Length} spawn={spawn.transform.position:F3} " +
                $"floorOffset={VRFloorHeightCalibration.DefaultPlayerFloorOffset:F2} monsters=2 spawns={MonsterRevisionBootstrap.SpawnPointCount}");
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RevisionBootstrap.PlayerPrefabPath);
            if (prefab == null || prefab.GetComponent<Player>() == null || prefab.GetComponent<Rigidbody>() == null)
            {
                throw new InvalidOperationException("VRPlayer prefab is missing Player or Rigidbody.");
            }

            Transform tracking = prefab.transform.Find("XR Origin");
            Transform poseSpace = prefab.transform.Find("XR Origin/Tracking Space Offset");
            Transform cameraTransform = prefab.transform.Find("XR Origin/Tracking Space Offset/Main Camera");
            Transform headCollider = prefab.transform.Find("XR Origin/Tracking Space Offset/Main Camera/Head Collider");
            Transform leftController = prefab.transform.Find("XR Origin/Tracking Space Offset/Left Controller Target");
            Transform rightController = prefab.transform.Find("XR Origin/Tracking Space Offset/Right Controller Target");
            Transform body = prefab.transform.Find("Body Collider");
            Transform leftHand = prefab.transform.Find("GorillaLocomotion/Left Hand Sphere");
            Transform rightHand = prefab.transform.Find("GorillaLocomotion/Right Hand Sphere");
            if (tracking == null || poseSpace == null || cameraTransform == null || headCollider == null || leftController == null ||
                rightController == null || body == null || leftHand == null || rightHand == null)
            {
                throw new InvalidOperationException("Clean VRPlayer hierarchy is incomplete.");
            }

            Transform[] unscaled = { prefab.transform, tracking, poseSpace, cameraTransform, leftController, rightController, body };
            if (unscaled.Any(item => !Approximately(item.localScale, Vector3.one)))
            {
                throw new InvalidOperationException("VRPlayer, XR Origin, camera, controller targets, and collider objects must remain scale 1,1,1.");
            }
            XRFloorTrackingOrigin origin = prefab.GetComponent<XRFloorTrackingOrigin>();
            VRFloorHeightCalibration calibration = prefab.GetComponent<VRFloorHeightCalibration>();
            if (origin == null || origin.TrackingSpace != tracking || calibration == null || calibration.PoseSpace != poseSpace ||
                Mathf.Abs(calibration.PlayerFloorOffset - VRFloorHeightCalibration.DefaultPlayerFloorOffset) > 0.001f ||
                !Approximately(tracking.localPosition, Vector3.zero) ||
                Mathf.Abs(poseSpace.localPosition.y - VRFloorHeightCalibration.DefaultPlayerFloorOffset) > 0.001f ||
                Mathf.Abs(poseSpace.localPosition.x) > 0.001f || Mathf.Abs(poseSpace.localPosition.z) > 0.001f ||
                !Approximately(cameraTransform.localPosition, Vector3.zero))
            {
                throw new InvalidOperationException("Floor calibration must leave XR Origin and Main Camera at zero and apply only playerFloorOffset to the tracking-space adjustment parent.");
            }

            Camera[] cameras = prefab.GetComponentsInChildren<Camera>(true);
            XRTrackedPose headPose = cameraTransform.GetComponent<XRTrackedPose>();
            XRTrackedPose leftPose = leftController.GetComponent<XRTrackedPose>();
            XRTrackedPose rightPose = rightController.GetComponent<XRTrackedPose>();
            if (cameras.Length != 1 || cameras[0].transform != cameraTransform || cameras[0].tag != "MainCamera" ||
                headPose == null || headPose.Node != XRNode.Head ||
                leftPose == null || leftPose.Node != XRNode.LeftHand || rightPose == null || rightPose.Node != XRNode.RightHand)
            {
                throw new InvalidOperationException("Tracked camera/controller assignments are invalid or a duplicate camera exists.");
            }

            ValidateVisibleHand(leftHand, "Left Hand Sphere");
            ValidateVisibleHand(rightHand, "Right Hand Sphere");
            Renderer leftRenderer = leftHand.GetComponent<Renderer>();
            Renderer rightRenderer = rightHand.GetComponent<Renderer>();
            if (leftRenderer.sharedMaterial == rightRenderer.sharedMaterial || leftRenderer.sharedMaterial.color == rightRenderer.sharedMaterial.color)
            {
                throw new InvalidOperationException("Left and right hand spheres must be visually distinguishable.");
            }

            string[] forbiddenNames = { "Visuals", "GorillaBody", "CompleteTemporaryModel", "LeftVisualHand_SourceMesh", "RightVisualHand_SourceMesh" };
            if (prefab.GetComponentsInChildren<Transform>(true).Any(item => forbiddenNames.Contains(item.name)) ||
                prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(item => item != null && item.GetType().Name == "GorillaVisualRig"))
            {
                throw new InvalidOperationException("Temporary gorilla model objects or follow scripts remain in VRPlayer.");
            }

            Player locomotion = prefab.GetComponent<Player>();
            if (locomotion.headCollider != headCollider.GetComponent<SphereCollider>() ||
                locomotion.leftHandFollower != leftHand || locomotion.rightHandFollower != rightHand ||
                locomotion.leftHandTransform != leftController || locomotion.rightHandTransform != rightController ||
                Mathf.Abs(locomotion.maxArmLength - 1.5f) > 0.001f)
            {
                throw new InvalidOperationException("GorillaLocomotion tracked references or normal arm reach changed.");
            }
            if (prefab.GetComponent<PlayerDeathController>() == null || prefab.GetComponent<GorillaLocomotionDiagnostics>() == null ||
                prefab.GetComponent<PlayerFloorDebugGizmo>() == null || cameraTransform.Find("Development Fade Overlay") == null)
            {
                throw new InvalidOperationException("VRPlayer is missing the shared death controller or VR fade overlay.");
            }
        }

        private static void ValidateMonsterScene(Transform playerRoot)
        {
            TiptoeBrain[] tiptoes = UnityEngine.Object.FindObjectsByType<TiptoeBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            StatueBrain[] statues = UnityEngine.Object.FindObjectsByType<StatueBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            MonsterSpawnPoint[] spawns = UnityEngine.Object.FindObjectsByType<MonsterSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            JumpscareRoomController[] jumpscareRooms = UnityEngine.Object.FindObjectsByType<JumpscareRoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            MonsterJumpscareController[] jumpscareControllers = UnityEngine.Object.FindObjectsByType<MonsterJumpscareController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            MonsterSpawnCoordinator[] coordinators = UnityEngine.Object.FindObjectsByType<MonsterSpawnCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (tiptoes.Length != 1 || statues.Length != 1 || spawns.Length != MonsterRevisionBootstrap.SpawnPointCount ||
                surfaces.Length != 1 || surfaces[0].navMeshData == null || jumpscareRooms.Length != 0 ||
                jumpscareControllers.Length != 0 || coordinators.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Monster scene setup invalid: Tiptoe={tiptoes.Length}, Statue={statues.Length}, spawns={spawns.Length}, " +
                    $"surfaces={surfaces.Length}, jumpscareRooms={jumpscareRooms.Length}, jumpscareControllers={jumpscareControllers.Length}, coordinators={coordinators.Length}.");
            }

            float tiptoeHeight = CalculateRendererBounds(tiptoes[0].gameObject).size.y;
            float statueHeight = CalculateRendererBounds(statues[0].gameObject).size.y;
            if (tiptoeHeight < 1.65f || tiptoeHeight > 1.8f || statueHeight < 1.8f || statueHeight > 2f)
            {
                throw new InvalidOperationException($"Monster heights invalid: Tiptoe={tiptoeHeight:F3}, Statue={statueHeight:F3}.");
            }
            if (tiptoes[0].Navigation == null || statues[0].Navigation == null ||
                tiptoes[0].Navigation.Agent == null || statues[0].Navigation.Agent == null ||
                tiptoes[0].GetComponentInChildren<MonsterKillTrigger>(true) == null ||
                statues[0].GetComponentInChildren<MonsterKillTrigger>(true) == null)
            {
                throw new InvalidOperationException("Monster navigation or dedicated kill triggers are missing.");
            }
            Animator tiptoeAnimator = tiptoes[0].GetComponentInChildren<Animator>(true);
            Animator statueAnimator = statues[0].GetComponentInChildren<Animator>(true);
            if (tiptoeAnimator == null || statueAnimator == null || tiptoeAnimator.runtimeAnimatorController == null || statueAnimator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException("Imported monster animation controllers are missing.");
            }
            if (Vector3.Distance(tiptoes[0].transform.position, playerRoot.position) < 30f ||
                Vector3.Distance(statues[0].transform.position, playerRoot.position) < 35f ||
                Mathf.Abs(tiptoes[0].StartupGracePeriod - 7f) > 0.001f ||
                Mathf.Abs(statues[0].StartupGracePeriod - 7f) > 0.001f)
            {
                throw new InvalidOperationException("Monster starting distance or seven-second startup grace is invalid.");
            }
            Vector3 sightOrigin = playerRoot.position + Vector3.up * 1.35f;
            if (spawns.Any(point => Vector3.Distance(point.transform.position, playerRoot.position) < 35f) ||
                spawns.Select(point => point.Region).Distinct().Count() < 3 ||
                spawns.Any(point => !NavMesh.SamplePosition(point.transform.position, out _, 1.5f, NavMesh.AllAreas)) ||
                spawns.Any(point =>
                {
                    Vector3 delta = point.transform.position + Vector3.up - sightOrigin;
                    return !Physics.Raycast(sightOrigin, delta.normalized, delta.magnitude, 1 << 8, QueryTriggerInteraction.Ignore);
                }))
            {
                throw new InvalidOperationException("Monster spawn points are not all distant, hidden, region-distributed, and NavMesh-valid.");
            }
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(MonsterRevisionBootstrap.NavMeshDataPath) == null)
            {
                throw new InvalidOperationException("Committed NavMeshData asset is missing.");
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MonsterRevisionBootstrap.ExperimentalJumpscareScenePath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Experimental/Jumpscares/Prefabs/JumpscareRoom.prefab") == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Experimental/Jumpscares/Prefabs/TiptoeJumpscare.prefab") == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Experimental/Jumpscares/Prefabs/StatueJumpscare.prefab") == null)
            {
                throw new InvalidOperationException("Experimental jumpscare assets were not preserved outside the playable scene.");
            }
        }

        private static void ValidateVisibleHand(Transform hand, string label)
        {
            SphereCollider sphere = hand.GetComponent<SphereCollider>();
            MeshRenderer renderer = hand.GetComponent<MeshRenderer>();
            if (sphere == null || !sphere.enabled || !sphere.isTrigger || renderer == null || !renderer.enabled ||
                hand.GetComponents<Collider>().Length != 1 || hand.childCount != 0)
            {
                throw new InvalidOperationException($"{label} is not a single visible authoritative sphere hand.");
            }
        }

        private static void ValidateArchitecturalMeasurements()
        {
            if (RevisionBootstrap.MeasuredDoorwayHeight < 2.2f || RevisionBootstrap.MeasuredDoorwayHeight > 2.6f ||
                RevisionBootstrap.MeasuredCorridorCeilingHeight < 3f || RevisionBootstrap.MeasuredCorridorCeilingHeight > 4.5f ||
                RevisionBootstrap.MeasuredLowWallHeight < 1.1f || RevisionBootstrap.MeasuredLowWallHeight > 1.5f)
            {
                throw new InvalidOperationException(
                    $"Architectural measurements invalid: doorway={RevisionBootstrap.MeasuredDoorwayHeight:F2}, " +
                    $"ceiling={RevisionBootstrap.MeasuredCorridorCeilingHeight:F2}, lowWall={RevisionBootstrap.MeasuredLowWallHeight:F2}.");
            }
        }

        private static void ValidateOpenXr()
        {
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
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return Vector3.SqrMagnitude(a - b) < 0.000001f;
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
                Debug.Log("REVISION_PLAYMODE_VERIFICATION_SUCCESS frames=720 errors=0 runtimeMonsterChecks=true");
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
