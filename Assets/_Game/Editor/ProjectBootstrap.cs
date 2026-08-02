#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace TheBestMonkeyGame.Editor
{
    public static class ProjectBootstrap
    {
        private const string ScenePath = "Assets/_Game/Scenes/LocomotionTest.unity";
        private const string PrefabPath = "Assets/_Game/Prefabs/VRPlayer.prefab";
        private const string MaterialFolder = "Assets/_Game/Materials";
        private const string PhysicsFolder = "Assets/_Game/Physics";
        private const int LocomotionLayer = 8;
        private const int PlayerLayer = 9;

        [MenuItem("Tools/The Best Monkey Game/Build Foundation")]
        public static void Build()
        {
            try
            {
                ConfigureProject();
                PhysicsMaterial surfacePhysics = CreatePhysicsMaterial();
                Material ground = CreateMaterial("Ground", new Color(0.16f, 0.32f, 0.19f));
                Material wall = CreateMaterial("Wall", new Color(0.28f, 0.38f, 0.49f));
                Material climb = CreateMaterial("Climbable", new Color(0.78f, 0.42f, 0.16f));
                Material platform = CreateMaterial("Platform", new Color(0.18f, 0.51f, 0.62f));
                Material obstacle = CreateMaterial("Obstacle", new Color(0.56f, 0.27f, 0.65f));
                Material leftHand = CreateMaterial("LeftHand", new Color(0.12f, 0.45f, 1f));
                Material rightHand = CreateMaterial("RightHand", new Color(1f, 0.2f, 0.16f));

                GameObject prefab = BuildPlayerPrefab(leftHand, rightHand);
                BuildScene(prefab, surfacePhysics, ground, wall, climb, platform, obstacle);
                ConfigureXr();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("FOUNDATION_BUILD_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/The Best Monkey Game/Build Android Smoke Test")]
        public static void BuildAndroidSmokeTest()
        {
            ConfigureProject();
            ConfigureXr();
            Directory.CreateDirectory("Build");
            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/TheBestMonkeyGame.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Android smoke build failed with {report.summary.totalErrors} errors.");
            }

            Debug.Log($"ANDROID_BUILD_SUCCESS bytes={report.summary.totalSize} warnings={report.summary.totalWarnings} errors={report.summary.totalErrors}");
            EditorApplication.Exit(0);
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "The Best Monkey Game";
            PlayerSettings.productName = "The Best Monkey Game";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.secondsebastiantablet.thebestmonkeygame");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;

            SetLayer(LocomotionLayer, "Locomotion");
            SetLayer(PlayerLayer, "Player");
            AddTag("LocomotionSurface");

            UnityEngine.Object[] projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (projectSettings.Length > 0)
            {
                SerializedObject serializedSettings = new SerializedObject(projectSettings[0]);
                SerializedProperty inputHandler = serializedSettings.FindProperty("activeInputHandler");
                if (inputHandler != null)
                {
                    inputHandler.intValue = 1;
                    serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        private static void ConfigureXr()
        {
            XRGeneralSettings generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null || generalSettings.Manager == null)
            {
                EnsureAssetFolder("Assets/XR");
                EnsureAssetFolder("Assets/XR/Settings");

                XRGeneralSettingsPerBuildTarget perBuildTarget;
                if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perBuildTarget) || perBuildTarget == null)
                {
                    perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perBuildTarget, "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuildTarget, true);
                }

                if (!perBuildTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                {
                    perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
                }

                generalSettings = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            }

            if (generalSettings == null || generalSettings.Manager == null)
            {
                throw new InvalidOperationException("Could not create XR Plug-in Management Android settings.");
            }

            generalSettings.InitManagerOnStart = true;
            bool assigned = XRPackageMetadataStore.AssignLoader(
                generalSettings.Manager,
                "UnityEngine.XR.OpenXR.OpenXRLoader",
                BuildTargetGroup.Android);

            if (!assigned && !generalSettings.Manager.activeLoaders.Any(loader => loader is UnityEngine.XR.OpenXR.OpenXRLoader))
            {
                throw new InvalidOperationException("Could not assign the OpenXR loader for Android.");
            }

            if (OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android) == null)
            {
                Type packageSettingsType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings"))
                    .FirstOrDefault(type => type != null);
                packageSettingsType?.GetMethod("GetOrCreateInstance")?.Invoke(null, null);
            }

            OpenXRSettings openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXrSettings == null)
            {
                throw new InvalidOperationException("OpenXR Android settings were not created.");
            }

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

            string[] desiredFeatures =
            {
                "MetaQuestFeature",
                "OculusTouchControllerProfile",
                "MetaQuestTouchPlusControllerProfile",
                "MetaQuestTouchProControllerProfile"
            };

            foreach (var feature in openXrSettings.GetFeatures())
            {
                if (feature != null && desiredFeatures.Contains(feature.GetType().Name))
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
            }

            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(generalSettings.Manager);
            EditorUtility.SetDirty(openXrSettings);
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject BuildPlayerPrefab(Material leftHandMaterial, Material rightHandMaterial)
        {
            GameObject root = new GameObject("VRPlayer");
            root.layer = PlayerLayer;

            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.useGravity = true;
            rigidbody.isKinematic = false;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            rigidbody.linearDamping = 0.05f;

            GameObject trackingSpace = CreateChild(root.transform, "TrackingSpace", Vector3.zero);

            GameObject head = CreateChild(trackingSpace.transform, "Head", new Vector3(0f, 1.65f, 0f));
            head.layer = PlayerLayer;
            head.AddComponent<XRTrackedPose>().Node = XRNode.Head;
            Camera camera = head.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            head.AddComponent<AudioListener>();
            SphereCollider headCollider = head.AddComponent<SphereCollider>();
            headCollider.radius = 0.14f;

            GameObject leftController = CreateChild(trackingSpace.transform, "LeftController", new Vector3(-0.35f, 1.2f, 0.35f));
            leftController.layer = PlayerLayer;
            leftController.AddComponent<XRTrackedPose>().Node = XRNode.LeftHand;

            GameObject rightController = CreateChild(trackingSpace.transform, "RightController", new Vector3(0.35f, 1.2f, 0.35f));
            rightController.layer = PlayerLayer;
            rightController.AddComponent<XRTrackedPose>().Node = XRNode.RightHand;

            GameObject bodyObject = CreateChild(root.transform, "BodyCollider", Vector3.zero);
            bodyObject.layer = PlayerLayer;
            CapsuleCollider bodyCollider = bodyObject.AddComponent<CapsuleCollider>();
            bodyCollider.direction = 1;
            bodyCollider.radius = 0.22f;
            bodyCollider.height = 1.55f;
            bodyCollider.center = new Vector3(0f, 0.775f, 0f);
            bodyObject.AddComponent<BodyColliderFollower>().Configure(head.transform, bodyCollider);

            GameObject followers = CreateChild(root.transform, "HandCollisionFollowers", Vector3.zero);
            GameObject leftFollower = CreateHand(followers.transform, "LeftHand", new Vector3(-0.35f, 1.2f, 0.35f), leftHandMaterial);
            GameObject rightFollower = CreateHand(followers.transform, "RightHand", new Vector3(0.35f, 1.2f, 0.35f), rightHandMaterial);

            Player player = root.AddComponent<Player>();
            player.headCollider = headCollider;
            player.bodyCollider = bodyCollider;
            player.leftHandTransform = leftController.transform;
            player.rightHandTransform = rightController.transform;
            player.leftHandFollower = leftFollower.transform;
            player.rightHandFollower = rightFollower.transform;
            player.velocityHistorySize = 10;
            player.maxArmLength = 1.5f;
            player.unStickDistance = 1f;
            player.velocityLimit = 0.8f;
            player.maxJumpSpeed = 6.5f;
            player.jumpMultiplier = 1.15f;
            player.minimumRaycastDistance = 0.055f;
            player.defaultSlideFactor = 0.03f;
            player.defaultPrecision = 0.995f;
            player.leftHandOffset = Vector3.zero;
            player.rightHandOffset = Vector3.zero;
            player.locomotionEnabledLayers = 1 << LocomotionLayer;
            player.disableMovement = false;

            root.AddComponent<PlayerRespawn>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateHand(Transform parent, string name, Vector3 localPosition, Material material)
        {
            GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hand.name = name;
            hand.layer = PlayerLayer;
            hand.transform.SetParent(parent, false);
            hand.transform.localPosition = localPosition;
            hand.transform.localScale = Vector3.one * 0.11f;
            hand.GetComponent<Renderer>().sharedMaterial = material;
            SphereCollider collider = hand.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            return hand;
        }

        private static void BuildScene(
            GameObject playerPrefab,
            PhysicsMaterial physicsMaterial,
            Material ground,
            Material wall,
            Material climb,
            Material platform,
            Material obstacle)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject environment = new GameObject("Environment");
            CreateSurface(environment.transform, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 24f), Vector3.zero, ground, physicsMaterial, 0.015f);
            CreateSurface(environment.transform, "NorthWall", new Vector3(0f, 2.5f, 12f), new Vector3(24f, 5.5f, 0.5f), Vector3.zero, wall, physicsMaterial, 0.01f);
            CreateSurface(environment.transform, "SouthWall", new Vector3(0f, 2.5f, -12f), new Vector3(24f, 5.5f, 0.5f), Vector3.zero, wall, physicsMaterial, 0.01f);
            CreateSurface(environment.transform, "EastWall", new Vector3(12f, 2.5f, 0f), new Vector3(0.5f, 5.5f, 24f), Vector3.zero, wall, physicsMaterial, 0.01f);
            CreateSurface(environment.transform, "WestWall", new Vector3(-12f, 2.5f, 0f), new Vector3(0.5f, 5.5f, 24f), Vector3.zero, wall, physicsMaterial, 0.01f);

            GameObject ramps = new GameObject("Ramps");
            ramps.transform.SetParent(environment.transform);
            CreateSurface(ramps.transform, "RampLeft", new Vector3(-5f, 0.85f, -2f), new Vector3(5f, 0.5f, 3f), new Vector3(0f, 0f, -18f), platform, physicsMaterial, 0.02f);
            CreateSurface(ramps.transform, "RampRight", new Vector3(5f, 1.15f, 1f), new Vector3(5.5f, 0.5f, 3f), new Vector3(0f, 0f, 23f), platform, physicsMaterial, 0.02f);

            GameObject platforms = new GameObject("Platforms");
            platforms.transform.SetParent(environment.transform);
            CreateSurface(platforms.transform, "LowPlatform", new Vector3(-5.8f, 1.5f, 3.5f), new Vector3(3.5f, 0.45f, 3.5f), Vector3.zero, platform, physicsMaterial, 0.01f);
            CreateSurface(platforms.transform, "HighPlatform", new Vector3(5.8f, 3.2f, 5.2f), new Vector3(4f, 0.45f, 4f), Vector3.zero, platform, physicsMaterial, 0.01f);
            CreateSurface(platforms.transform, "Bridge", new Vector3(0f, 2.35f, 6.5f), new Vector3(7.8f, 0.35f, 1.2f), Vector3.zero, platform, physicsMaterial, 0.015f);

            GameObject climbables = new GameObject("ClimbableWalls");
            climbables.transform.SetParent(environment.transform);
            CreateSurface(climbables.transform, "ClimbWallA", new Vector3(-8f, 2.25f, 6f), new Vector3(0.45f, 4.5f, 6f), Vector3.zero, climb, physicsMaterial, 0.001f);
            CreateSurface(climbables.transform, "ClimbWallB", new Vector3(8f, 2.75f, -4.5f), new Vector3(0.45f, 5.5f, 5f), Vector3.zero, climb, physicsMaterial, 0.001f);
            CreateSurface(climbables.transform, "ClimbSlab", new Vector3(0f, 2.2f, 9f), new Vector3(7f, 4.4f, 0.45f), Vector3.zero, climb, physicsMaterial, 0.001f);

            GameObject obstacles = new GameObject("ObstacleCourse");
            obstacles.transform.SetParent(environment.transform);
            for (int index = 0; index < 7; index++)
            {
                float x = -6f + index * 2f;
                float height = 0.35f + (index % 3) * 0.35f;
                CreateSurface(obstacles.transform, $"Step_{index + 1}", new Vector3(x, height * 0.5f, -7.5f), new Vector3(1.15f, height, 1.15f), Vector3.zero, obstacle, physicsMaterial, 0.01f);
            }

            CreateSurface(obstacles.transform, "BalanceBeam", new Vector3(0f, 1.15f, -4.5f), new Vector3(8f, 0.3f, 0.45f), Vector3.zero, obstacle, physicsMaterial, 0.012f);
            CreateSurface(obstacles.transform, "TallCube", new Vector3(-2.5f, 2f, 1.8f), new Vector3(1.5f, 4f, 1.5f), Vector3.zero, obstacle, physicsMaterial, 0.001f);
            CreateSurface(obstacles.transform, "MediumCube", new Vector3(0f, 1.25f, 2.1f), new Vector3(1.5f, 2.5f, 1.5f), Vector3.zero, obstacle, physicsMaterial, 0.001f);
            CreateSurface(obstacles.transform, "ShortCube", new Vector3(2.5f, 0.65f, 2.4f), new Vector3(1.5f, 1.3f, 1.5f), Vector3.zero, obstacle, physicsMaterial, 0.001f);

            GameObject spawn = new GameObject("SpawnPoint");
            spawn.transform.position = new Vector3(0f, 0.12f, -5.5f);
            spawn.transform.rotation = Quaternion.identity;

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            player.GetComponent<PlayerRespawn>().SpawnPoint = spawn.transform;

            GameObject reset = new GameObject("FallResetArea");
            reset.transform.position = new Vector3(0f, -8f, 0f);
            reset.layer = PlayerLayer;
            BoxCollider resetCollider = reset.AddComponent<BoxCollider>();
            resetCollider.size = new Vector3(60f, 1f, 60f);
            resetCollider.isTrigger = true;
            reset.AddComponent<FallResetVolume>();

            CreateLighting();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static GameObject CreateSurface(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation,
            Material material,
            PhysicsMaterial physicsMaterial,
            float slip)
        {
            GameObject surfaceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surfaceObject.name = name;
            surfaceObject.layer = LocomotionLayer;
            surfaceObject.tag = "LocomotionSurface";
            surfaceObject.transform.SetParent(parent);
            surfaceObject.transform.position = position;
            surfaceObject.transform.localScale = scale;
            surfaceObject.transform.eulerAngles = rotation;
            surfaceObject.GetComponent<Renderer>().sharedMaterial = material;
            surfaceObject.GetComponent<Collider>().sharedMaterial = physicsMaterial;
            surfaceObject.AddComponent<Surface>().slipPercentage = slip;
            GameObjectUtility.SetStaticEditorFlags(surfaceObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return surfaceObject;
        }

        private static void CreateLighting()
        {
            GameObject sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/TestSkybox.mat");
                if (skybox == null)
                {
                    skybox = new Material(skyShader) { name = "TestSkybox" };
                    skybox.SetColor("_SkyTint", new Color(0.34f, 0.5f, 0.72f));
                    skybox.SetFloat("_AtmosphereThickness", 0.8f);
                    AssetDatabase.CreateAsset(skybox, $"{MaterialFolder}/TestSkybox.mat");
                }
                RenderSettings.skybox = skybox;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.52f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.32f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.14f);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }
                material = new Material(shader) { name = name, color = color };
                material.SetFloat("_Smoothness", 0.2f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static PhysicsMaterial CreatePhysicsMaterial()
        {
            string path = $"{PhysicsFolder}/LocomotionSurface.physicMaterial";
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("LocomotionSurface")
                {
                    dynamicFriction = 0.6f,
                    staticFriction = 0.6f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static void SetLayer(int index, string name)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(index).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddTag(string tag)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int index = 0; index < tags.arraySize; index++)
            {
                if (tags.GetArrayElementAtIndex(index).stringValue == tag)
                {
                    return;
                }
            }
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
