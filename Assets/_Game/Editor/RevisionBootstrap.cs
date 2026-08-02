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

        private const string ModelSourcePath = "Assets/ThirdParty/GorillaModel/Source/Gorilla_tag.stl";
        private const string BodyMeshPath = "Assets/ThirdParty/GorillaModel/GorillaTagBody.asset";
        private const string LeftHandMeshPath = "Assets/ThirdParty/GorillaModel/GorillaTagLeftHand.asset";
        private const string RightHandMeshPath = "Assets/ThirdParty/GorillaModel/GorillaTagRightHand.asset";
        private const string ModelMaterialPath = "Assets/ThirdParty/GorillaModel/GorillaVisual.mat";
        private const string MapSourcePath = "Assets/ThirdParty/Map/giggle_farts_map.glb";
        private const int LocomotionLayer = 8;
        private const int PlayerLayer = 9;

        [MenuItem("Tools/The Best Monkey Game/Build Player And Main Map Revision")]
        public static void BuildRevision()
        {
            try
            {
                EnsureFolder("Assets/_Game/Prefabs/Player");
                EnsureFolder("Assets/_Game/Prefabs/Environment");
                EnsureFolder("Assets/ThirdParty/GorillaModel");

                StlData source = ReadBinaryStl(ModelSourcePath);
                Mesh bodyMesh = SaveMesh(BodyMeshPath, BuildMesh(source, HandRegion.All, "GorillaTagBody"));
                Mesh leftHandMesh = SaveMesh(LeftHandMeshPath, BuildMesh(source, HandRegion.Left, "GorillaTagLeftHand"));
                Mesh rightHandMesh = SaveMesh(RightHandMeshPath, BuildMesh(source, HandRegion.Right, "GorillaTagRightHand"));
                Material visualMaterial = CreateVisualMaterial();

                BuildPlayer(bodyMesh, leftHandMesh, rightHandMesh, visualMaterial);
                GameObject mapPrefab = BuildMapPrefab();
                BuildMainScene(mapPrefab);
                ConfigureBuildScenes();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("REVISION_BUILD_SUCCESS playerFloorOffset=0 mapColliders=4 visualHands=source-derived");
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

                Debug.Log($"REVISION_ANDROID_BUILD_SUCCESS bytes={report.summary.totalSize} warnings={report.summary.totalWarnings} errors={report.summary.totalErrors}");
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

        private static void BuildPlayer(Mesh bodyMesh, Mesh leftHandMesh, Mesh rightHandMesh, Material material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform trackingSpace = RequireChild(root.transform, "TrackingSpace");
                Transform head = RequireChild(trackingSpace, "Head");
                Transform leftController = RequireChild(trackingSpace, "LeftController");
                Transform rightController = RequireChild(trackingSpace, "RightController");
                Transform leftPhysicsHand = RequireChild(root.transform, "HandCollisionFollowers/LeftHand");
                Transform rightPhysicsHand = RequireChild(root.transform, "HandCollisionFollowers/RightHand");

                trackingSpace.localPosition = Vector3.zero;
                head.localPosition = new Vector3(0f, 0.25f, 0f);
                leftController.localPosition = new Vector3(-0.22f, 0.22f, 0.18f);
                rightController.localPosition = new Vector3(0.22f, 0.22f, 0.18f);
                leftPhysicsHand.localPosition = leftController.localPosition;
                rightPhysicsHand.localPosition = rightController.localPosition;

                XRFloorTrackingOrigin floorOrigin = root.GetComponent<XRFloorTrackingOrigin>();
                if (floorOrigin == null)
                {
                    floorOrigin = root.AddComponent<XRFloorTrackingOrigin>();
                }
                floorOrigin.Configure(trackingSpace, 0f);

                BodyColliderFollower bodyFollower = root.GetComponentInChildren<BodyColliderFollower>(true);
                if (bodyFollower != null)
                {
                    bodyFollower.FloorClearance = 0.015f;
                }

                SetPhysicsHandInvisible(leftPhysicsHand);
                SetPhysicsHandInvisible(rightPhysicsHand);

                Transform oldVisuals = root.transform.Find("Visuals");
                if (oldVisuals != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldVisuals.gameObject);
                }
                RemoveComponent<GorillaVisualRig>(root);

                GameObject visuals = new GameObject("Visuals");
                visuals.layer = PlayerLayer;
                visuals.transform.SetParent(root.transform, false);

                GameObject bodyAnchor = new GameObject("GorillaBody");
                bodyAnchor.layer = PlayerLayer;
                bodyAnchor.transform.SetParent(visuals.transform, false);
                GameObject bodyModel = CreateMeshVisual(bodyAnchor.transform, "CompleteTemporaryModel", bodyMesh, material);
                bodyModel.transform.localScale = Vector3.one * 0.025f;

                CreateHandVisual(leftPhysicsHand, "LeftVisualHand_SourceMesh", leftHandMesh, material);
                CreateHandVisual(rightPhysicsHand, "RightVisualHand_SourceMesh", rightHandMesh, material);

                GorillaVisualRig visualRig = root.AddComponent<GorillaVisualRig>();
                visualRig.Configure(head, bodyAnchor.transform, Vector3.zero);

                Player locomotion = root.GetComponent<Player>();
                if (locomotion == null)
                {
                    throw new InvalidOperationException("VRPlayer is missing GorillaLocomotion.Player.");
                }
                locomotion.maxArmLength = 1.5f;

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetPhysicsHandInvisible(Transform physicsHand)
        {
            SphereCollider sphere = physicsHand.GetComponent<SphereCollider>();
            if (sphere == null)
            {
                throw new InvalidOperationException($"{physicsHand.name} lost its authoritative SphereCollider.");
            }
            sphere.enabled = true;
            Renderer renderer = physicsHand.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private static void CreateHandVisual(Transform physicsHand, string name, Mesh mesh, Material material)
        {
            Transform existing = physicsHand.Find(name);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject visual = CreateMeshVisual(physicsHand, name, mesh, material);
            // The authoritative primitive is scaled to 0.11. Counter-scale so the
            // source hand keeps its intended 2.5 cm-per-source-unit dimensions.
            visual.transform.localScale = Vector3.one * (0.025f / physicsHand.localScale.x);
        }

        private static GameObject CreateMeshVisual(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject visual = new GameObject(name);
            visual.layer = PlayerLayer;
            visual.transform.SetParent(parent, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return visual;
        }

        private static GameObject BuildMapPrefab()
        {
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(MapSourcePath);
            if (imported == null)
            {
                throw new InvalidOperationException(
                    "The GLB is not imported as a GameObject. Confirm com.unity.cloud.gltfast resolved, then retry after import finishes.");
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
            // The GLB contains an embedded Sketchfab/FBX unit transform. Normalize
            // the finished hierarchy to an explicit, Gorilla-Tag-sized 30 m arena.
            source.transform.localScale = Vector3.one * (30f / planarSpan);
            Physics.SyncTransforms();
            Bounds bounds = CalculateRendererBounds(root);
            source.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            Physics.SyncTransforms();

            int colliderCount = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                int triangles = mesh == null ? 0 : mesh.triangles.Length / 3;
                // The last two eight-triangle meshes are decorative cards. Colliding
                // the four substantial material meshes covers all playable geometry.
                if (triangles < 100)
                {
                    continue;
                }

                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                }
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

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
                if (child.GetComponent<Collider>() == null)
                {
                    child.gameObject.layer = LocomotionLayer;
                }
            }

            if (colliderCount != 4)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException($"Expected 4 substantial map meshes, generated {colliderCount} colliders.");
            }

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
            spawn.transform.position = candidate.position + Vector3.up * 0.02f;
            Vector3 towardCenter = Vector3.ProjectOnPlane(mapBounds.center - candidate.position, Vector3.up);
            spawn.transform.rotation = towardCenter.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(towardCenter.normalized, Vector3.up)
                : Quaternion.identity;

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            player.GetComponent<PlayerRespawn>().SpawnPoint = spawn.transform;

            GameObject reset = new GameObject("FallResetArea");
            reset.layer = PlayerLayer;
            reset.transform.position = new Vector3(mapBounds.center.x, mapBounds.min.y - 6f, mapBounds.center.z);
            BoxCollider resetCollider = reset.AddComponent<BoxCollider>();
            resetCollider.size = new Vector3(mapBounds.size.x + 20f, 1f, mapBounds.size.z + 20f);
            resetCollider.isTrigger = true;
            reset.AddComponent<FallResetVolume>();

            CreateLighting();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            Debug.Log($"MAIN_MAP_CREATED spawn={spawn.transform.position:F3} bounds={mapBounds.size:F2}");
        }

        private static SpawnCandidate FindSpawn(Bounds bounds)
        {
            List<SpawnCandidate> candidates = new List<SpawnCandidate>();
            float rayY = bounds.max.y + 5f;
            float xExtent = bounds.extents.x * 0.38f;
            float zExtent = bounds.extents.z * 0.38f;

            for (int xIndex = -5; xIndex <= 5; xIndex++)
            {
                for (int zIndex = -5; zIndex <= 5; zIndex++)
                {
                    float x = bounds.center.x + xExtent * xIndex / 5f;
                    float z = bounds.center.z + zExtent * zIndex / 5f;
                    RaycastHit[] hits = Physics.RaycastAll(
                        new Vector3(x, rayY, z), Vector3.down, bounds.size.y + 10f,
                        1 << LocomotionLayer, QueryTriggerInteraction.Ignore);

                    foreach (RaycastHit hit in hits.OrderBy(item => item.point.y))
                    {
                        if (hit.normal.y < 0.92f)
                        {
                            continue;
                        }

                        Vector3 bottom = hit.point + Vector3.up * 0.27f;
                        Vector3 top = hit.point + Vector3.up * 1.35f;
                        bool obstructed = Physics.CheckCapsule(
                            bottom, top, 0.2f, 1 << LocomotionLayer, QueryTriggerInteraction.Ignore);

                        float centerDistance = Vector2.Distance(
                            new Vector2(hit.point.x, hit.point.z),
                            new Vector2(bounds.center.x, bounds.center.z));
                        float clearancePenalty = obstructed ? 100f : 0f;
                        candidates.Add(new SpawnCandidate(
                            hit.point,
                            centerDistance + Mathf.Abs(hit.point.y) * 0.25f + clearancePenalty));
                        break;
                    }
                }
            }

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("Could not find a flat, head-clear map spawn point.");
            }
            return candidates.OrderBy(candidate => candidate.score).First();
        }

        private static void CreateLighting()
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
        }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(TestScenePath, true)
            };
        }

        private static Material CreateVisualMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ModelMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = "GorillaVisual", color = new Color(0.04f, 0.34f, 0.82f) };
                material.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(material, ModelMaterialPath);
            }
            return material;
        }

        private static StlData ReadBinaryStl(string assetPath)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (bytes.Length < 84)
            {
                throw new InvalidDataException("Gorilla STL is too short to be a binary STL.");
            }

            uint triangleCount = BitConverter.ToUInt32(bytes, 80);
            if (bytes.Length < 84L + triangleCount * 50L)
            {
                throw new InvalidDataException("Gorilla STL triangle data is truncated.");
            }

            List<StlTriangle> triangles = new List<StlTriangle>((int)triangleCount);
            for (int index = 0; index < triangleCount; index++)
            {
                int offset = 84 + index * 50 + 12;
                Vector3 a = ReadVector(bytes, offset);
                Vector3 b = ReadVector(bytes, offset + 12);
                Vector3 c = ReadVector(bytes, offset + 24);
                triangles.Add(new StlTriangle(a, b, c));
            }
            return new StlData(triangles);
        }

        private static Vector3 ReadVector(byte[] bytes, int offset)
        {
            return new Vector3(
                BitConverter.ToSingle(bytes, offset),
                BitConverter.ToSingle(bytes, offset + 4),
                BitConverter.ToSingle(bytes, offset + 8));
        }

        private static Mesh BuildMesh(StlData source, HandRegion region, string name)
        {
            List<StlTriangle> selected = source.triangles.Where(triangle => IncludeTriangle(triangle, source, region)).ToList();
            if (selected.Count == 0)
            {
                throw new InvalidOperationException($"No STL triangles selected for {region}.");
            }

            List<Vector3> vertices = new List<Vector3>(selected.Count * 3);
            List<int> indices = new List<int>(selected.Count * 3);
            foreach (StlTriangle triangle in selected)
            {
                vertices.Add(ConvertStlPoint(triangle.a));
                vertices.Add(ConvertStlPoint(triangle.b));
                vertices.Add(ConvertStlPoint(triangle.c));
                int start = vertices.Count - 3;
                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
            }

            Bounds meshBounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                meshBounds.Encapsulate(vertex);
            }
            Vector3 center = region == HandRegion.All
                ? new Vector3(meshBounds.center.x, meshBounds.min.y, meshBounds.center.z)
                : meshBounds.center;
            for (int index = 0; index < vertices.Count; index++)
            {
                vertices[index] -= center;
            }

            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool IncludeTriangle(StlTriangle triangle, StlData source, HandRegion region)
        {
            if (region == HandRegion.All)
            {
                return true;
            }

            Vector3 centroid = (triangle.a + triangle.b + triangle.c) / 3f;
            if (centroid.z > 13f)
            {
                return false;
            }
            return region == HandRegion.Left ? centroid.y < source.handSplitY : centroid.y >= source.handSplitY;
        }

        private static Vector3 ConvertStlPoint(Vector3 source)
        {
            // Thingiverse STL: Z up, Y across the shoulders. This cyclic axis
            // permutation is right-handed and therefore preserves triangle winding.
            return new Vector3(source.y, source.z, source.x);
        }

        private static Mesh SaveMesh(string path, Mesh generated)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
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

        private static Transform RequireChild(Transform root, string path)
        {
            Transform child = root.Find(path);
            if (child == null)
            {
                throw new InvalidOperationException($"Missing required prefab transform: {path}");
            }
            return child;
        }

        private static void RemoveComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        private enum HandRegion { All, Left, Right }

        private readonly struct StlTriangle
        {
            public readonly Vector3 a;
            public readonly Vector3 b;
            public readonly Vector3 c;
            public StlTriangle(Vector3 a, Vector3 b, Vector3 c) { this.a = a; this.b = b; this.c = c; }
        }

        private sealed class StlData
        {
            public readonly List<StlTriangle> triangles;
            public readonly float handSplitY;
            public StlData(List<StlTriangle> triangles)
            {
                this.triangles = triangles;
                float minY = triangles.SelectMany(t => new[] { t.a.y, t.b.y, t.c.y }).Min();
                float maxY = triangles.SelectMany(t => new[] { t.a.y, t.b.y, t.c.y }).Max();
                handSplitY = (minY + maxY) * 0.5f;
            }
        }

        private readonly struct SpawnCandidate
        {
            public readonly Vector3 position;
            public readonly float score;
            public SpawnCandidate(Vector3 position, float score) { this.position = position; this.score = score; }
        }
    }
}
#endif
