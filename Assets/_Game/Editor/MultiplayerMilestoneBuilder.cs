#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame.Monsters;
using TheBestMonkeyGame.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheBestMonkeyGame.Editor
{
    public static class MultiplayerMilestoneBuilder
    {
        public const string MainMenuPath = "Assets/_Game/Scenes/MainMenu.unity";
        public const string LobbyPath = "Assets/_Game/Scenes/MultiplayerLobby.unity";
        public const string NetworkPlayerPath = "Assets/_Game/Prefabs/Multiplayer/NetworkVRPlayer.prefab";
        public const string BootstrapPath = "Assets/_Game/Prefabs/Multiplayer/GameBootstrap.prefab";
        private const string VrPlayerPath = "Assets/_Game/Prefabs/VRPlayer.prefab";
        private const string UiMaterialPath = "Assets/_Game/Materials/Multiplayer/UIAccent.mat";
        private const string RemoteMaterialPath = "Assets/_Game/Materials/Multiplayer/RemotePlayer.mat";
        private const int UiLayer = 5;

        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color Navy = new(0.025f, 0.055f, 0.095f, 0.97f);
        private static readonly Color Panel = new(0.055f, 0.105f, 0.16f, 0.97f);
        private static readonly Color Accent = new(0.12f, 0.72f, 0.93f, 1f);
        private static readonly Color Ink = new(0.9f, 0.97f, 1f, 1f);

        [MenuItem("Tools/The Best Monkey Game/Multiplayer/Build Milestone One")]
        public static void Build()
        {
            try
            {
                EnsureFolders();
                Material uiMaterial = CreateMaterial(UiMaterialPath, Accent);
                Material remoteMaterial = CreateMaterial(RemoteMaterialPath, Color.white);
                GameObject networkPlayer = BuildNetworkPlayer(uiMaterial, remoteMaterial);
                GameObject bootstrap = BuildBootstrap(networkPlayer);
                BuildMainMenu(bootstrap, uiMaterial);
                BuildLobby(uiMaterial);
                ConfigureBuildSettings();
                RestoreXrPreloadedAssets();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateMilestone();
                Debug.Log("MULTIPLAYER_MILESTONE_BUILD_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/The Best Monkey Game/Multiplayer/Validate Milestone One")]
        public static void ValidateMilestone()
        {
            GameObject vrPlayer = RequireAsset<GameObject>(VrPlayerPath);
            GameObject networkPlayer = RequireAsset<GameObject>(NetworkPlayerPath);
            GameObject bootstrap = RequireAsset<GameObject>(BootstrapPath);
            RequireAsset<SceneAsset>(MainMenuPath);
            RequireAsset<SceneAsset>(LobbyPath);

            if (networkPlayer.GetComponent<NetworkObject>() == null || networkPlayer.GetComponent<NetworkVRPlayer>() == null || networkPlayer.GetComponent<NetworkPlayerIdentity>() == null)
                throw new InvalidOperationException("NetworkVRPlayer is missing required network components.");
            if (bootstrap.GetComponent<NetworkManager>() == null || bootstrap.GetComponent<UnityTransport>() == null || bootstrap.GetComponent<GameBootstrap>() == null)
                throw new InvalidOperationException("GameBootstrap prefab is incomplete.");
            if (vrPlayer.GetComponent<NetworkObject>() != null)
                throw new InvalidOperationException("The single-player VRPlayer prefab must not contain networking.");

            string[] scenes = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray();
            if (scenes.Length < 3 || scenes[0] != MainMenuPath || !scenes.Contains(RevisionBootstrap.MainScenePath) || !scenes.Contains(LobbyPath))
                throw new InvalidOperationException("Build scenes are not configured with MainMenu first and both gameplay modes enabled.");

            Scene lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            try
            {
                if (lobby.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonsterBrain>(true)).Any())
                    throw new InvalidOperationException("MultiplayerLobby must not contain monsters.");
            }
            finally { EditorSceneManager.CloseScene(lobby, true); }

            Debug.Log($"MULTIPLAYER_MILESTONE_VALIDATION_SUCCESS buildScenes={scenes.Length} singlePlayerPrefabUnchanged=true networkTick={MultiplayerConstants.PoseSendRate}");
        }

        [MenuItem("Tools/The Best Monkey Game/Multiplayer/Build Android Quest")]
        public static void BuildAndroidQuest()
        {
            try
            {
                ValidateMilestone();
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                Directory.CreateDirectory("Build");
                BuildPlayerOptions options = new()
                {
                    scenes = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray(),
                    locationPathName = "Build/TheBestMonkeyGame-Multiplayer.apk",
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException($"Android build failed with {report.summary.totalErrors} errors.");
                Debug.Log($"MULTIPLAYER_ANDROID_BUILD_SUCCESS bytes={report.summary.totalSize} warnings={report.summary.totalWarnings} errors={report.summary.totalErrors}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/The Best Monkey Game/Multiplayer/Validate Profile Persistence")]
        public static void ValidateProfilePersistence()
        {
            GameObject firstObject = new("ProfilePersistenceTestA");
            PlayerProfileService first = firstObject.AddComponent<PlayerProfileService>();
            first.Initialize();
            LocalPlayerProfile original = first.Current.Clone();
            LocalPlayerProfile test = new()
            {
                DisplayName = "<b>  Test\nMonkey  </b>",
                ColorIndex = 3,
                Turning = TurningMode.Smooth,
                SnapTurnAngle = 55f,
                SmoothTurnSpeed = 125f,
                MasterVolume = 0.65f,
                SoundEffectVolume = 0.45f
            };
            first.Save(test);
            UnityEngine.Object.DestroyImmediate(firstObject);

            GameObject secondObject = new("ProfilePersistenceTestB");
            PlayerProfileService second = secondObject.AddComponent<PlayerProfileService>();
            second.Initialize();
            LocalPlayerProfile loaded = second.Current;
            if (loaded.DisplayName.Contains("<") || loaded.DisplayName.Contains("\n") || loaded.DisplayName.Length > PlayerProfileService.MaxDisplayNameLength ||
                loaded.ColorIndex != 3 || loaded.Turning != TurningMode.Smooth || Mathf.Abs(loaded.SnapTurnAngle - 55f) > 0.01f ||
                Mathf.Abs(loaded.SmoothTurnSpeed - 125f) > 0.01f || Mathf.Abs(loaded.MasterVolume - 0.65f) > 0.01f ||
                Mathf.Abs(loaded.SoundEffectVolume - 0.45f) > 0.01f)
            {
                throw new InvalidOperationException("Local profile did not sanitize, save, and reload correctly.");
            }
            if (MultiplayerSessionService.NormalizeRoomCode("  abC123  ") != "ABC123" ||
                !MultiplayerSessionService.IsValidRoomCode("ABC123") || MultiplayerSessionService.IsValidRoomCode("BAD CODE"))
            {
                throw new InvalidOperationException("Room code normalization or validation failed.");
            }
            second.Save(original);
            UnityEngine.Object.DestroyImmediate(secondObject);
            Debug.Log("MULTIPLAYER_PROFILE_PERSISTENCE_SUCCESS sanitized=true reload=true snapDefault=45 smoothDefault=90 roomCodeValidation=true");
        }

        private static GameObject BuildNetworkPlayer(Material rayMaterial, Material remoteMaterial)
        {
            GameObject root = new("NetworkVRPlayer");
            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            NetworkPlayerIdentity identity = root.AddComponent<NetworkPlayerIdentity>();
            NetworkVRPlayer networkPlayer = root.AddComponent<NetworkVRPlayer>();

            GameObject vrPrefab = RequireAsset<GameObject>(VrPlayerPath);
            GameObject localRoot = (GameObject)PrefabUtility.InstantiatePrefab(vrPrefab);
            localRoot.name = "LocalPlayerRoot";
            localRoot.transform.SetParent(root.transform, false);
            Transform head = RequireChildRecursive(localRoot.transform, "Main Camera");
            Transform left = RequireChildRecursive(localRoot.transform, "Left Controller Target");
            Transform right = RequireChildRecursive(localRoot.transform, "Right Controller Target");

            VRTurningController turning = localRoot.GetComponent<VRTurningController>();
            if (turning == null) turning = localRoot.AddComponent<VRTurningController>();
            turning.Configure(localRoot.transform, head);
            DevelopmentPoseSimulator simulator = localRoot.AddComponent<DevelopmentPoseSimulator>();
            simulator.Configure(head, left, right);
            AddControllerRay(right.gameObject, rayMaterial);
            localRoot.SetActive(false);

            GameObject remoteRoot = new("RemoteVisualRoot");
            remoteRoot.transform.SetParent(root.transform, false);
            GameObject remoteHead = CreateRemotePart(remoteRoot.transform, "HeadVisual", PrimitiveType.Sphere, new Vector3(0f, 0.9f, 0f), Vector3.one * 0.27f, remoteMaterial);
            GameObject remoteLeft = CreateRemotePart(remoteRoot.transform, "LeftHandVisual", PrimitiveType.Sphere, new Vector3(-0.32f, 0.55f, 0.28f), Vector3.one * 0.17f, remoteMaterial);
            GameObject remoteRight = CreateRemotePart(remoteRoot.transform, "RightHandVisual", PrimitiveType.Sphere, new Vector3(0.32f, 0.55f, 0.28f), Vector3.one * 0.17f, remoteMaterial);
            Renderer[] renderers = remoteRoot.GetComponentsInChildren<Renderer>(true);
            networkPlayer.Configure(localRoot, remoteRoot, head, left, right, remoteHead.transform, remoteLeft.transform, remoteRight.transform, renderers, identity);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildBootstrap(GameObject networkPlayer)
        {
            GameObject root = new("GameBootstrap");
            GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();
            UnityServicesInitializer services = root.AddComponent<UnityServicesInitializer>();
            PlayerAuthenticationService authentication = root.AddComponent<PlayerAuthenticationService>();
            PlayerProfileService profile = root.AddComponent<PlayerProfileService>();
            MultiplayerErrorPresenter presenter = root.AddComponent<MultiplayerErrorPresenter>();
            NetworkManager manager = root.AddComponent<NetworkManager>();
            UnityTransport transport = root.AddComponent<UnityTransport>();
            TheBestMonkeyGame.Multiplayer.NetworkConnectionManager connection = root.AddComponent<TheBestMonkeyGame.Multiplayer.NetworkConnectionManager>();
            MultiplayerSceneCoordinator scenes = root.AddComponent<MultiplayerSceneCoordinator>();
            MultiplayerSessionService sessions = root.AddComponent<MultiplayerSessionService>();
            RoomPermissionService permissions = root.AddComponent<RoomPermissionService>();
            root.AddComponent<MultiplayerPlayModeAutoLauncher>();

            manager.NetworkConfig = new NetworkConfig
            {
                ProtocolVersion = 1,
                NetworkTransport = transport,
                PlayerPrefab = networkPlayer,
                TickRate = 30,
                EnableSceneManagement = true,
                ForceSamePrefabs = true,
                ConnectionApproval = true,
                EnableTimeResync = true,
                TimeResyncInterval = 30,
                LoadSceneTimeOut = 45,
                SpawnTimeout = 15f
            };
            connection.Configure(manager, transport);
            scenes.Configure(connection);
            sessions.Configure(services, authentication, connection, scenes, presenter);
            permissions.Configure(sessions, authentication, connection);
            bootstrap.Configure(services, authentication, profile, sessions, connection, scenes, presenter, permissions);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BootstrapPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static void BuildMainMenu(GameObject bootstrapPrefab, Material rayMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject bootstrap = (GameObject)PrefabUtility.InstantiatePrefab(bootstrapPrefab, scene);
            bootstrap.name = "GameBootstrap";
            CreateSafeRoom("MenuEnvironment", new Color(0.035f, 0.075f, 0.11f), new Color(0.06f, 0.15f, 0.21f), 10f, 7f);
            CreateLighting(new Color(0.35f, 0.58f, 0.74f));
            CreateEventSystem();
            CreateMenuRig(rayMaterial);

            Canvas canvas = CreateWorldCanvas("MainMenuCanvas", new Vector3(0f, 1.05f, 3.25f), new Vector2(900f, 720f));
            Image backdrop = canvas.gameObject.AddComponent<Image>();
            backdrop.color = Navy;
            MainMenuController controller = canvas.gameObject.AddComponent<MainMenuController>();

            GameObject home = CreatePanel(canvas.transform, "HomePanel");
            CreateText(home.transform, "THE BEST MONKEY GAME", new Vector2(0f, 265f), new Vector2(820f, 80f), 46, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            CreateText(home.transform, "PRIVATE MULTIPLAYER TEST  •  NETWORK v1", new Vector2(0f, 215f), new Vector2(820f, 42f), 18, TextAnchor.MiddleCenter, Accent);
            Button join = CreateButton(home.transform, "JOIN ROOM", new Vector2(0f, 110f), new Vector2(430f, 66f));
            Button single = CreateButton(home.transform, "SINGLE PLAYER", new Vector2(0f, 25f), new Vector2(430f, 66f));
            Button settings = CreateButton(home.transform, "SETTINGS", new Vector2(0f, -60f), new Vector2(430f, 66f));
            Button quit = CreateButton(home.transform, "QUIT", new Vector2(0f, -145f), new Vector2(430f, 66f));
            CreateText(home.transform, "Single Player remains the original map and monsters.", new Vector2(0f, -250f), new Vector2(760f, 40f), 17, TextAnchor.MiddleCenter, new Color(0.65f, 0.78f, 0.84f));

            GameObject room = CreatePanel(canvas.transform, "PrivateRoomPanel");
            CreateText(room.transform, "PRIVATE ROOM", new Vector2(0f, 265f), new Vector2(820f, 65f), 40, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            CreateText(room.transform, "Enter the same 4-12 character code on both players.", new Vector2(0f, 210f), new Vector2(820f, 40f), 18, TextAnchor.MiddleCenter, new Color(0.68f, 0.82f, 0.9f));
            InputField roomCode = CreateInput(room.transform, "RoomCode", "ROOM CODE", new Vector2(0f, 130f), new Vector2(460f, 62f), 12);
            Button connect = CreateButton(room.transform, "JOIN / CREATE", new Vector2(0f, 50f), new Vector2(460f, 62f));
            Button localHost = CreateButton(room.transform, "EDITOR HOST", new Vector2(-125f, -40f), new Vector2(230f, 52f));
            Button localClient = CreateButton(room.transform, "EDITOR CLIENT", new Vector2(125f, -40f), new Vector2(230f, 52f));
            Text status = CreateText(room.transform, "Ready", new Vector2(0f, -105f), new Vector2(760f, 36f), 18, TextAnchor.MiddleCenter, Accent);
            Text error = CreateText(room.transform, string.Empty, new Vector2(0f, -150f), new Vector2(760f, 65f), 17, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.32f));
            Button roomBack = CreateButton(room.transform, "BACK", new Vector2(0f, -245f), new Vector2(300f, 56f));

            GameObject settingsPanel = CreatePanel(canvas.transform, "SettingsPanel");
            SettingsPanelController settingsController = settingsPanel.AddComponent<SettingsPanelController>();
            CreateText(settingsPanel.transform, "SETTINGS", new Vector2(0f, 305f), new Vector2(820f, 55f), 38, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            CreateLabel(settingsPanel.transform, "PLAYER NAME", -360f, 235f);
            InputField nameInput = CreateInput(settingsPanel.transform, "DisplayName", "MONKEY", new Vector2(80f, 235f), new Vector2(500f, 50f), PlayerProfileService.MaxDisplayNameLength);
            CreateLabel(settingsPanel.transform, "PLAYER COLOR", -360f, 170f);
            Image preview = CreateImage(settingsPanel.transform, "ColorPreview", new Vector2(-200f, 170f), new Vector2(46f, 46f), Color.white);
            Button[] paletteButtons = new Button[6];
            for (int index = 0; index < paletteButtons.Length; index++)
            {
                paletteButtons[index] = CreateButton(settingsPanel.transform, string.Empty, new Vector2(-120f + index * 68f, 170f), new Vector2(52f, 46f));
                paletteButtons[index].image.color = bootstrapPrefab.GetComponent<PlayerProfileService>().GetColor(index);
            }
            CreateLabel(settingsPanel.transform, "TURNING", -360f, 105f);
            Dropdown modeData = CreateHiddenTurningDropdown(settingsPanel.transform);
            Button snapMode = CreateButton(settingsPanel.transform, "SNAP", new Vector2(-40f, 105f), new Vector2(210f, 48f));
            Button smoothMode = CreateButton(settingsPanel.transform, "SMOOTH", new Vector2(195f, 105f), new Vector2(210f, 48f));
            Slider snapSlider = CreateSettingSlider(settingsPanel.transform, "SnapAngle", "SNAP ANGLE", 15f, 90f, 5f, new Vector2(30f, 35f), out Text snapValue);
            Slider smoothSlider = CreateSettingSlider(settingsPanel.transform, "SmoothSpeed", "SMOOTH SPEED", 30f, 180f, 1f, new Vector2(30f, -40f), out Text smoothValue);
            Slider masterSlider = CreateSettingSlider(settingsPanel.transform, "MasterVolume", "MASTER VOLUME", 0f, 1f, 0.05f, new Vector2(30f, -115f), out Text masterValue);
            Slider effectsSlider = CreateSettingSlider(settingsPanel.transform, "EffectsVolume", "SFX VOLUME", 0f, 1f, 0.05f, new Vector2(30f, -190f), out Text effectsValue);
            Button settingsBack = CreateButton(settingsPanel.transform, "SAVE & BACK", new Vector2(0f, -285f), new Vector2(350f, 54f));
            settingsController.Configure(nameInput, modeData, snapSlider, smoothSlider, masterSlider, effectsSlider, snapValue, smoothValue, masterValue, effectsValue, paletteButtons, preview, settingsBack, snapMode, smoothMode);

            controller.Configure(home, room, settingsPanel, join, single, settings, quit, connect, roomBack, roomCode, status, error, settingsController, localHost, localClient);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        private static void BuildLobby(Material rayMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject spawnerObject = new("LobbyPlayerSpawner");
            spawnerObject.AddComponent<NetworkObject>();
            LobbyPlayerSpawner spawner = spawnerObject.AddComponent<LobbyPlayerSpawner>();
            spawner.Configure(RequireAsset<GameObject>(NetworkPlayerPath));
            CreateSafeRoom("WaitingRoomEnvironment", new Color(0.045f, 0.075f, 0.095f), new Color(0.1f, 0.2f, 0.24f), 12f, 8f);
            CreateLighting(new Color(0.4f, 0.7f, 0.78f));
            CreateEventSystem();

            Canvas canvas = CreateWorldCanvas("LobbyCanvas", new Vector3(0f, 1.1f, 4.2f), new Vector2(900f, 680f));
            canvas.gameObject.AddComponent<Image>().color = Navy;
            MultiplayerLobbyUI lobbyUi = canvas.gameObject.AddComponent<MultiplayerLobbyUI>();
            CreateText(canvas.transform, "MULTIPLAYER WAITING ROOM", new Vector2(0f, 270f), new Vector2(840f, 65f), 38, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            Text code = CreateText(canvas.transform, "ROOM: ----", new Vector2(0f, 215f), new Vector2(840f, 42f), 25, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            CreateText(canvas.transform, "CONNECTED PLAYERS", new Vector2(-205f, 145f), new Vector2(400f, 40f), 20, TextAnchor.MiddleLeft, new Color(0.68f, 0.82f, 0.9f));
            Text players = CreateText(canvas.transform, "Connecting player...", new Vector2(-205f, 20f), new Vector2(400f, 220f), 23, TextAnchor.UpperLeft, Ink);
            Image divider = CreateImage(canvas.transform, "Divider", new Vector2(0f, 25f), new Vector2(3f, 270f), Accent);
            Text info = CreateText(canvas.transform, "Head and hands are synchronized.\n\nThe host is an ordinary player.\nNo monsters or match gameplay are active yet.", new Vector2(220f, 20f), new Vector2(360f, 220f), 20, TextAnchor.UpperLeft, new Color(0.74f, 0.86f, 0.9f));
            Button start = CreateButton(canvas.transform, "START MATCH  •  COMING NEXT", new Vector2(0f, -155f), new Vector2(500f, 58f));
            start.interactable = false;
            Button leave = CreateButton(canvas.transform, "LEAVE ROOM", new Vector2(0f, -230f), new Vector2(360f, 58f));
            Text status = CreateText(canvas.transform, "Waiting room ready", new Vector2(0f, -292f), new Vector2(800f, 32f), 17, TextAnchor.MiddleCenter, Accent);
            Text error = CreateText(canvas.transform, string.Empty, new Vector2(0f, -325f), new Vector2(800f, 32f), 16, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.32f));
            lobbyUi.Configure(code, players, status, error, leave, start);

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "CenterPedestal";
            pedestal.transform.SetPositionAndRotation(new Vector3(0f, 0.12f, 0.5f), Quaternion.identity);
            pedestal.transform.localScale = new Vector3(1.4f, 0.12f, 1.4f);
            pedestal.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Assets/_Game/Materials/Multiplayer/LobbyPedestal.mat", new Color(0.08f, 0.35f, 0.42f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LobbyPath);
        }

        private static void CreateMenuRig(Material rayMaterial)
        {
            GameObject vrPrefab = RequireAsset<GameObject>(VrPlayerPath);
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(vrPrefab);
            rig.name = "MenuLocalPlayerRoot";
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Player locomotion = rig.GetComponent<Player>();
            if (locomotion != null) locomotion.enabled = false;
            PlayerRespawn respawn = rig.GetComponent<PlayerRespawn>();
            if (respawn != null) respawn.enabled = false;
            Rigidbody body = rig.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            foreach (Collider collider in rig.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Transform right = RequireChildRecursive(rig.transform, "Right Controller Target");
            AddControllerRay(right.gameObject, rayMaterial);
            Transform head = RequireChildRecursive(rig.transform, "Main Camera");
            Transform left = RequireChildRecursive(rig.transform, "Left Controller Target");
            DevelopmentPoseSimulator simulator = rig.AddComponent<DevelopmentPoseSimulator>();
            simulator.Configure(head, left, right);
        }

        private static void AddControllerRay(GameObject controller, Material material)
        {
            LineRenderer line = controller.GetComponent<LineRenderer>();
            if (line == null) line = controller.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.startWidth = 0.008f;
            line.endWidth = 0.003f;
            line.useWorldSpace = true;
            line.numCapVertices = 3;
            line.startColor = Accent;
            line.endColor = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
            VRControllerRaycaster ray = controller.GetComponent<VRControllerRaycaster>();
            if (ray == null) ray = controller.AddComponent<VRControllerRaycaster>();
            ray.Configure(line);
        }

        private static GameObject CreateRemotePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        private static void CreateSafeRoom(string name, Color floorColor, Color wallColor, float size, float height)
        {
            GameObject root = new(name);
            Material floor = CreateMaterial($"Assets/_Game/Materials/Multiplayer/{name}Floor.mat", floorColor);
            Material wall = CreateMaterial($"Assets/_Game/Materials/Multiplayer/{name}Wall.mat", wallColor);
            CreateCube(root.transform, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(size, 0.2f, size), floor);
            CreateCube(root.transform, "BackWall", new Vector3(0f, height * 0.5f, size * 0.5f), new Vector3(size, height, 0.2f), wall);
            CreateCube(root.transform, "FrontWall", new Vector3(0f, height * 0.5f, -size * 0.5f), new Vector3(size, height, 0.2f), wall);
            CreateCube(root.transform, "LeftWall", new Vector3(-size * 0.5f, height * 0.5f, 0f), new Vector3(0.2f, height, size), wall);
            CreateCube(root.transform, "RightWall", new Vector3(size * 0.5f, height * 0.5f, 0f), new Vector3(0.2f, height, size), wall);
            CreateCube(root.transform, "Ceiling", new Vector3(0f, height, 0f), new Vector3(size, 0.15f, size), wall);
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static void CreateLighting(Color ambient)
        {
            GameObject lightObject = new("Soft Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.85f;
            light.color = new Color(0.72f, 0.9f, 1f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.025f, 0.06f, 0.08f);
            RenderSettings.fogDensity = 0.012f;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new("LocalEventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateWorldCanvas(string name, Vector3 position, Vector2 size)
        {
            GameObject canvasObject = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = UiLayer;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.position = position;
            rect.rotation = Quaternion.identity;
            rect.localScale = Vector3.one * 0.0022f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;
            return canvas;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = new(name, typeof(RectTransform));
            panel.layer = UiLayer;
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return panel;
        }

        private static Text CreateText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            GameObject item = new("Text", typeof(RectTransform), typeof(Text));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Text text = item.GetComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void CreateLabel(Transform parent, string value, float x, float y)
        {
            CreateText(parent, value, new Vector2(x, y), new Vector2(250f, 42f), 17, TextAnchor.MiddleLeft, new Color(0.63f, 0.8f, 0.87f), FontStyle.Bold);
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size)
        {
            GameObject item = new(label.Replace(" ", string.Empty) + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider), typeof(VRRayTarget));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = item.GetComponent<Image>();
            image.color = Panel;
            Button button = item.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Panel;
            colors.highlightedColor = new Color(0.08f, 0.34f, 0.46f, 1f);
            colors.pressedColor = Accent;
            colors.selectedColor = new Color(0.07f, 0.28f, 0.38f, 1f);
            colors.disabledColor = new Color(0.1f, 0.14f, 0.17f, 0.7f);
            button.colors = colors;
            BoxCollider collider = item.GetComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 18f);
            collider.isTrigger = true;
            item.GetComponent<VRRayTarget>().Configure(button);
            CreateText(item.transform, label, Vector2.zero, size, 20, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            return button;
        }

        private static InputField CreateInput(Transform parent, string name, string placeholderValue, Vector2 position, Vector2 size, int characterLimit)
        {
            GameObject item = new(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(BoxCollider), typeof(VRRayTarget));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            item.GetComponent<Image>().color = new Color(0.015f, 0.035f, 0.055f, 1f);
            Text text = CreateText(item.transform, string.Empty, Vector2.zero, new Vector2(size.x - 34f, size.y - 8f), 23, TextAnchor.MiddleCenter, Ink);
            Text placeholder = CreateText(item.transform, placeholderValue, Vector2.zero, new Vector2(size.x - 34f, size.y - 8f), 21, TextAnchor.MiddleCenter, new Color(0.35f, 0.55f, 0.64f));
            InputField input = item.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = characterLimit;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Alphanumeric;
            BoxCollider collider = item.GetComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 18f);
            collider.isTrigger = true;
            item.GetComponent<VRRayTarget>().Configure(input);
            return input;
        }

        private static Slider CreateSettingSlider(Transform parent, string name, string label, float min, float max, float step, Vector2 position, out Text valueText)
        {
            CreateText(parent, label, new Vector2(-265f, position.y), new Vector2(220f, 38f), 16, TextAnchor.MiddleLeft, new Color(0.63f, 0.8f, 0.87f), FontStyle.Bold);
            GameObject item = new(name, typeof(RectTransform), typeof(Slider), typeof(BoxCollider), typeof(VRRayTarget));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(390f, 42f);
            rect.anchoredPosition = position;

            Image background = CreateImage(item.transform, "Background", Vector2.zero, new Vector2(390f, 12f), new Color(0.08f, 0.18f, 0.22f));
            Image fill = CreateImage(item.transform, "Fill", new Vector2(-195f, 0f), new Vector2(390f, 12f), Accent);
            RectTransform fillRect = fill.rectTransform;
            fillRect.pivot = new Vector2(0f, 0.5f);
            Image handle = CreateImage(item.transform, "Handle", Vector2.zero, new Vector2(28f, 28f), Ink);
            Slider slider = item.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.fillRect = fillRect;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            BoxCollider collider = item.GetComponent<BoxCollider>();
            collider.size = new Vector3(410f, 48f, 18f);
            collider.isTrigger = true;
            item.GetComponent<VRRayTarget>().Configure(slider);
            valueText = CreateText(parent, string.Empty, new Vector2(310f, position.y), new Vector2(170f, 38f), 16, TextAnchor.MiddleRight, Ink);
            return slider;
        }

        private static Dropdown CreateHiddenTurningDropdown(Transform parent)
        {
            GameObject item = new("TurningModeData", typeof(RectTransform), typeof(Dropdown));
            item.transform.SetParent(parent, false);
            Dropdown dropdown = item.GetComponent<Dropdown>();
            dropdown.options = new List<Dropdown.OptionData> { new("Snap"), new("Smooth") };
            item.SetActive(false);
            return dropdown;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject item = new(name, typeof(RectTransform), typeof(Image));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = item.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Material CreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(RevisionBootstrap.MainScenePath, true),
                new EditorBuildSettingsScene(LobbyPath, true),
                new EditorBuildSettingsScene(RevisionBootstrap.TestScenePath, true)
            };
        }

        private static void RestoreXrPreloadedAssets()
        {
            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length == 0) return;
            SerializedObject settings = new(settingsAssets[0]);
            SerializedProperty preloaded = settings.FindProperty("preloadedAssets");
            UnityEngine.Object xrSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");
            if (preloaded == null || xrSettings == null) return;
            bool exists = Enumerable.Range(0, preloaded.arraySize).Any(index => preloaded.GetArrayElementAtIndex(index).objectReferenceValue == xrSettings);
            if (!exists)
            {
                preloaded.InsertArrayElementAtIndex(preloaded.arraySize);
                preloaded.GetArrayElementAtIndex(preloaded.arraySize - 1).objectReferenceValue = xrSettings;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Missing required asset: {path}");
            return asset;
        }

        private static Transform RequireChildRecursive(Transform root, string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) if (item.name == name) return item;
            throw new InvalidOperationException($"{root.name} is missing child {name}.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game/Prefabs/Multiplayer");
            EnsureFolder("Assets/_Game/Materials/Multiplayer");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
    }
}
#endif
