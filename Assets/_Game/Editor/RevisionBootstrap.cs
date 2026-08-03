#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Editor
{
    public static class RevisionBootstrap
    {
        public const string PlayerPrefabPath = "Assets/_Game/Prefabs/VRPlayer.prefab";
        public const string MapPrefabPath = "Assets/_Game/Prefabs/Environment/GiggleFartsMap.prefab";
        public const string MainScenePath = "Assets/_Game/Scenes/MainMap.unity";
        public const string TestScenePath = "Assets/_Game/Scenes/LocomotionTest.unity";
        public const float MapScaleMultiplier = 2.85f;
        public const float MeasuredDoorwayHeight = 2.05f;
        public const float MeasuredCorridorCeilingHeight = 2.63f;
        public const float MeasuredLowWallHeight = 1.03f;

        private const string MapSourcePath = "Assets/ThirdParty/Map/giggle_farts_map.glb";
        private const string LeftHandMaterialPath = "Assets/_Game/Materials/LeftHand.mat";
        private const string RightHandMaterialPath = "Assets/_Game/Materials/RightHand.mat";
        private const int LocomotionLayer = 8;
        private const int PlayerLayer = 9;

        [MenuItem("Tools/The Best Monkey Game/Build Scale And Player Revision")]
        public static void BuildRevision()
        {
            try
            {
                BuildPlayer();
                GameObject mapPrefab = BuildMapPrefab();
                BuildMainScene(mapPrefab);
                ConfigureBuildScenes();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"SCALE_PLAYER_REVISION_BUILD_SUCCESS mapScale={MapScaleMultiplier:F2} " +
                    $"doorwayHeight={MeasuredDoorwayHeight:F2} sphereHandsVisible=true playerModel=false");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        [MenuItem("Tools/The Best Monkey Game/Build Revision Android Smoke Test")]
        public static void BuildAndroidSmokeTest()
        {
            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                Directory.CreateDirectory("Build");
                EditorUserBuildSettings.buildAppBundle = false;

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { MainScenePath, TestScenePath },
                    locationPathName = "Build/TheBestMonkeyGame.apk",
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.Development
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException($"Android build failed with {report.summary.totalErrors} errors.");
                }

                Debug.Log(
                    $"REVISION_ANDROID_BUILD_SUCCESS bytes={report.summary.totalSize} " +
                    $"warnings={report.summary.totalWarnings} errors={report.summary.totalErrors}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void BuildPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(item.gameObject);
                }
                RemoveComponentByTypeName(root, "GorillaVisualRig");

                Transform tracking = RequireChild(root.transform, "TrackingSpace", "XR Origin");
                Transform cameraTransform = RequireChild(tracking, "Head", "Main Camera");
                Transform leftController = RequireChild(tracking, "LeftController", "Left Controller Target");
                Transform rightController = RequireChild(tracking, "RightController", "Right Controller Target");
                Transform bodyTransform = RequireChild(root.transform, "BodyCollider", "Body Collider");
                Transform locomotionObjects = RequireChild(root.transform, "HandCollisionFollowers", "GorillaLocomotion");
                Transform leftHand = RequireChild(locomotionObjects, "LeftHand", "Left Hand Sphere");
                Transform rightHand = RequireChild(locomotionObjects, "RightHand", "Right Hand Sphere");

                root.name = "VRPlayer";
                root.transform.localScale = Vector3.one;
                tracking.name = "XR Origin";
                tracking.localPosition = Vector3.zero;
                tracking.localRotation = Quaternion.identity;
                tracking.localScale = Vector3.one;

                cameraTransform.name = "Main Camera";
                cameraTransform.localPosition = new Vector3(0f, 0.25f, 0f);
                cameraTransform.localScale = Vector3.one;
                leftController.name = "Left Controller Target";
                leftController.localPosition = new Vector3(-0.22f, 0.22f, 0.18f);
                leftController.localScale = Vector3.one;
                rightController.name = "Right Controller Target";
                rightController.localPosition = new Vector3(0.22f, 0.22f, 0.18f);
                rightController.localScale = Vector3.one;

                XRFloorTrackingOrigin floorOrigin = root.GetComponent<XRFloorTrackingOrigin>();
                if (floorOrigin == null)
                {
                    floorOrigin = root.AddComponent<XRFloorTrackingOrigin>();
                }
                floorOrigin.Configure(tracking, 0f);

                bodyTransform.name = "Body Collider";
                bodyTransform.localScale = Vector3.one;
                CapsuleCollider bodyCollider = bodyTransform.GetComponent<CapsuleCollider>();
                if (bodyCollider == null)
                {
                    throw new InvalidOperationException("VRPlayer is missing its body CapsuleCollider.");
                }
                bodyCollider.height = 0.45f;
                bodyCollider.center = new Vector3(0f, 0.225f, 0f);
                BodyColliderFollower bodyFollower = bodyTransform.GetComponent<BodyColliderFollower>();
                if (bodyFollower == null)
                {
                    bodyFollower = bodyTransform.gameObject.AddComponent<BodyColliderFollower>();
                }
                bodyFollower.Configure(cameraTransform, bodyCollider);
                bodyFollower.FloorClearance = 0.015f;

                SphereCollider headCollider = MoveHeadColliderToNamedChild(cameraTransform);

                Transform visuals = root.transform.Find("Visuals");
                if (visuals != null)
                {
                    UnityEngine.Object.DestroyImmediate(visuals.gameObject);
                }

                locomotionObjects.name = "GorillaLocomotion";
                locomotionObjects.localScale = Vector3.one;
                ConfigureVisibleHand(leftHand, "Left Hand Sphere", LeftHandMaterialPath, new Vector3(-0.22f, 0.22f, 0.18f));
                ConfigureVisibleHand(rightHand, "Right Hand Sphere", RightHandMaterialPath, new Vector3(0.22f, 0.22f, 0.18f));

                Player player = root.GetComponent<Player>();
                if (player == null)
                {
                    throw new InvalidOperationException("VRPlayer is missing GorillaLocomotion.Player.");
                }
                player.headCollider = headCollider;
                player.bodyCollider = bodyCollider;
                player.leftHandTransform = leftController;
                player.rightHandTransform = rightController;
                player.leftHandFollower = leftHand;
                player.rightHandFollower = rightHand;
                player.maxArmLength = 1.5f;

                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                if (cameras.Length != 1 || cameras[0].transform != cameraTransform)
                {
                    throw new InvalidOperationException($"VRPlayer must contain exactly one tracked camera; found {cameras.Length}.");
                }
                cameras[0].tag = "MainCamera";

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SphereCollider MoveHeadColliderToNamedChild(Transform cameraTransform)
        {
            Transform colliderTransform = cameraTransform.Find("Head Collider");
            if (colliderTransform == null)
            {
                GameObject colliderObject = new GameObject("Head Collider");
                colliderObject.layer = PlayerLayer;
                colliderTransform = colliderObject.transform;
                colliderTransform.SetParent(cameraTransform, false);
            }
            colliderTransform.localPosition = Vector3.zero;
            colliderTransform.localRotation = Quaternion.identity;
            colliderTransform.localScale = Vector3.one;

            SphereCollider oldCollider = cameraTransform.GetComponent<SphereCollider>();
            SphereCollider headCollider = colliderTransform.GetComponent<SphereCollider>();
            if (headCollider == null)
            {
                headCollider = colliderTransform.gameObject.AddComponent<SphereCollider>();
            }
            headCollider.radius = oldCollider != null ? oldCollider.radius : 0.14f;
            headCollider.center = oldCollider != null ? oldCollider.center : Vector3.zero;
            headCollider.isTrigger = false;
            headCollider.enabled = true;
            if (oldCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(oldCollider);
            }
            return headCollider;
        }

        private static void ConfigureVisibleHand(Transform hand, string name, string materialPath, Vector3 fallbackPosition)
        {
            hand.name = name;
            hand.localPosition = fallbackPosition;
            hand.localRotation = Quaternion.identity;
            hand.localScale = Vector3.one * 0.11f;
            hand.gameObject.layer = PlayerLayer;

            while (hand.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(hand.GetChild(0).gameObject);
            }

            SphereCollider sphere = hand.GetComponent<SphereCollider>();
            MeshRenderer renderer = hand.GetComponent<MeshRenderer>();
            MeshFilter filter = hand.GetComponent<MeshFilter>();
            if (sphere == null || renderer == null || filter == null)
            {
                throw new InvalidOperationException($"{name} must retain its original sphere collider and renderer.");
            }
            sphere.enabled = true;
            sphere.isTrigger = true;
            renderer.enabled = true;
            Material handMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (handMaterial == null)
            {
                throw new InvalidOperationException($"Missing hand material: {materialPath}");
            }
            renderer.sharedMaterial = handMaterial;
        }

        private static GameObject BuildMapPrefab()
        {
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(MapSourcePath);
            if (imported == null)
            {
                throw new InvalidOperationException("The GLB is not imported as a GameObject. Confirm glTFast resolved.");
            }

            GameObject root = new GameObject("GiggleFartsMap");
            GameObject source = (GameObject)PrefabUtility.InstantiatePrefab(imported);
            source.name = "ImportedGLB";
            source.transform.SetParent(root.transform, false);
            source.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            source.transform.localScale = Vector3.one;
            Physics.SyncTransforms();

            Bounds unscaledBounds = CalculateRendererBounds(root);
            float planarSpan = Mathf.Max(unscaledBounds.size.x, unscaledBounds.size.z);
            if (planarSpan < 0.001f)
            {
                throw new InvalidOperationException("Imported map has invalid planar bounds.");
            }
            source.transform.localScale = Vector3.one * (30f / planarSpan);
            Physics.SyncTransforms();
            Bounds normalizedBounds = CalculateRendererBounds(root);
            source.transform.position += new Vector3(-normalizedBounds.center.x, -normalizedBounds.min.y, -normalizedBounds.center.z);
            Physics.SyncTransforms();

            int colliderCount = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                int triangles = mesh == null ? 0 : mesh.triangles.Length / 3;
                if (triangles < 100)
                {
                    foreach (Collider oldCollider in filter.GetComponents<Collider>())
                    {
                        UnityEngine.Object.DestroyImmediate(oldCollider);
                    }
                    continue;
                }

                foreach (Collider oldCollider in filter.GetComponents<Collider>())
                {
                    UnityEngine.Object.DestroyImmediate(oldCollider);
                }
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = false;
                collider.isTrigger = false;

                Surface surface = filter.GetComponent<Surface>();
                if (surface == null)
                {
                    surface = filter.gameObject.AddComponent<Surface>();
                }
                surface.slipPercentage = colliderCount == 0 ? 0.015f : 0.01f;
                filter.gameObject.layer = LocomotionLayer;
                filter.gameObject.tag = "LocomotionSurface";
                colliderCount++;
            }

            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                item.gameObject.isStatic = true;
                if (item.GetComponent<Collider>() == null)
                {
                    item.gameObject.layer = LocomotionLayer;
                }
            }
            if (colliderCount != 4)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException($"Expected 4 substantial map meshes, generated {colliderCount} colliders.");
            }

            root.transform.localScale = Vector3.one * MapScaleMultiplier;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildMainScene(GameObject mapPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, scene);
            map.name = "GiggleFartsMap";
            Physics.SyncTransforms();

            Bounds mapBounds = CalculateRendererBounds(map);
            SpawnCandidate candidate = FindSpawn(mapBounds);
            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = candidate.position;
            Vector3 towardCenter = Vector3.ProjectOnPlane(mapBounds.center - candidate.position, Vector3.up);
            spawn.transform.rotation = towardCenter.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(towardCenter.normalized, Vector3.up)
                : Quaternion.identity;

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            player.transform.localScale = Vector3.one;
            player.GetComponent<PlayerRespawn>().SpawnPoint = spawn.transform;

            GameObject reset = new GameObject("FallResetArea");
            reset.layer = PlayerLayer;
            reset.transform.position = new Vector3(mapBounds.center.x, mapBounds.min.y - 8f, mapBounds.center.z);
            BoxCollider resetCollider = reset.AddComponent<BoxCollider>();
            resetCollider.size = new Vector3(mapBounds.size.x + 30f, 1f, mapBounds.size.z + 30f);
            resetCollider.isTrigger = true;
            reset.AddComponent<FallResetVolume>();

            CreateLighting(mapBounds);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            Debug.Log(
                $"MAIN_MAP_RESCALED spawn={spawn.transform.position:F3} bounds={mapBounds.size:F2} " +
                $"mapScale={map.transform.localScale.x:F2}");
        }

        private static SpawnCandidate FindSpawn(Bounds bounds)
        {
            List<SpawnCandidate> candidates = new List<SpawnCandidate>();
            float rayY = bounds.max.y + 8f;
            float xExtent = bounds.extents.x * 0.38f;
            float zExtent = bounds.extents.z * 0.38f;

            for (int xIndex = -5; xIndex <= 5; xIndex++)
            {
                for (int zIndex = -5; zIndex <= 5; zIndex++)
                {
                    float x = bounds.center.x + xExtent * xIndex / 5f;
                    float z = bounds.center.z + zExtent * zIndex / 5f;
                    RaycastHit[] hits = Physics.RaycastAll(
                        new Vector3(x, rayY, z), Vector3.down, bounds.size.y + 16f,
                        1 << LocomotionLayer, QueryTriggerInteraction.Ignore);

                    foreach (RaycastHit hit in hits.OrderBy(item => item.point.y))
                    {
                        if (hit.normal.y < 0.92f)
                        {
                            continue;
                        }
                        bool obstructed = Physics.CheckCapsule(
                            hit.point + Vector3.up * 0.27f,
                            hit.point + Vector3.up * 1.75f,
                            0.2f,
                            1 << LocomotionLayer,
                            QueryTriggerInteraction.Ignore);
                        float centerDistance = Vector2.Distance(
                            new Vector2(hit.point.x, hit.point.z),
                            new Vector2(bounds.center.x, bounds.center.z));
                        candidates.Add(new SpawnCandidate(
                            hit.point,
                            centerDistance + Mathf.Abs(hit.point.y) * 0.25f + (obstructed ? 100f : 0f)));
                        break;
                    }
                }
            }
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("Could not find an upward-facing map floor for PlayerSpawn.");
            }
            return candidates.OrderBy(candidate => candidate.score).First();
        }

        private static void CreateLighting(Bounds mapBounds)
        {
            GameObject lightObject = new GameObject("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/TestSkybox.mat");
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.52f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.32f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.14f);
            QualitySettings.shadowDistance = Mathf.Max(120f, mapBounds.extents.magnitude * 2f);
        }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(TestScenePath, true)
            };
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"{root.name} has no renderers.");
            }
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static Transform RequireChild(Transform root, params string[] names)
        {
            foreach (string name in names)
            {
                Transform direct = root.Find(name);
                if (direct != null)
                {
                    return direct;
                }
            }
            throw new InvalidOperationException($"Missing required child under {root.name}: {string.Join(" or ", names)}");
        }

        private static void RemoveComponentByTypeName(GameObject gameObject, string typeName)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == typeName)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        private readonly struct SpawnCandidate
        {
            public readonly Vector3 position;
            public readonly float score;
            public SpawnCandidate(Vector3 position, float score)
            {
                this.position = position;
                this.score = score;
            }
        }
    }
}
#endif
