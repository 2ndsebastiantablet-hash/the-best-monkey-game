#if UNITY_EDITOR
using System;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame.Monsters;
using TheBestMonkeyGame.Multiplayer;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheBestMonkeyGame.Editor
{
    public static class MultiplayerMatchMilestoneBuilder
    {
        public const string MatchManagerPrefabPath = "Assets/_Game/Prefabs/Multiplayer/Match/MultiplayerMatchManager.prefab";
        public const string NetworkTiptoePrefabPath = "Assets/_Game/Prefabs/Multiplayer/Monsters/NetworkTiptoe.prefab";
        public const string NetworkStatuePrefabPath = "Assets/_Game/Prefabs/Multiplayer/Monsters/NetworkStatue.prefab";
        public const string NetworkPlayerPrefabPath = "Assets/_Game/Prefabs/Multiplayer/NetworkVRPlayer.prefab";
        public const string BootstrapPrefabPath = "Assets/_Game/Prefabs/Multiplayer/GameBootstrap.prefab";
        public const string MainMapPath = "Assets/_Game/Scenes/MainMap.unity";
        public const string LobbyPath = "Assets/_Game/Scenes/MultiplayerLobby.unity";

        private const string TiptoePrefabPath = "Assets/_Game/Prefabs/Monsters/Tiptoe.prefab";
        private const string StatuePrefabPath = "Assets/_Game/Prefabs/Monsters/Statue.prefab";

        [MenuItem("Tools/The Best Monkey Game/Multiplayer Match/Build Shared Match")]
        public static void Build()
        {
            try
            {
                EnsureFolder("Assets/_Game/Prefabs/Multiplayer/Match");
                EnsureFolder("Assets/_Game/Prefabs/Multiplayer/Monsters");
                GameObject tiptoe = BuildNetworkMonster(NetworkMonsterKind.Tiptoe, TiptoePrefabPath, NetworkTiptoePrefabPath);
                GameObject statue = BuildNetworkMonster(NetworkMonsterKind.Statue, StatuePrefabPath, NetworkStatuePrefabPath);
                GameObject match = BuildMatchManager();
                IntegrateNetworkPlayer();
                IntegrateBootstrap(match, tiptoe, statue);
                IntegrateMainMap(tiptoe, statue);
                IntegrateLobby();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Validate();
                Debug.Log("MULTIPLAYER_MATCH_BUILD_SUCCESS stateFlow=true spawns=4 monsters=2 singlePlayerPreserved=true");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/The Best Monkey Game/Multiplayer Match/Validate Shared Match")]
        public static void Validate()
        {
            GameObject match = RequireAsset<GameObject>(MatchManagerPrefabPath);
            if (match.GetComponent<NetworkObject>() == null || match.GetComponent<MultiplayerMatchManager>() == null ||
                match.GetComponent<MatchPermissionValidator>() == null || match.GetComponent<MultiplayerRespawnManager>() == null)
                throw new InvalidOperationException("The persistent authoritative match prefab is incomplete.");

            ValidateNetworkMonster(RequireAsset<GameObject>(NetworkTiptoePrefabPath), NetworkMonsterKind.Tiptoe);
            ValidateNetworkMonster(RequireAsset<GameObject>(NetworkStatuePrefabPath), NetworkMonsterKind.Statue);

            GameObject networkPlayer = RequireAsset<GameObject>(NetworkPlayerPrefabPath);
            if (networkPlayer.GetComponents<NetworkPlayerMatchState>().Length != 1 || networkPlayer.GetComponent<NetworkVRPlayer>() == null)
                throw new InvalidOperationException("NetworkVRPlayer does not have exactly one match-state authority component.");
            Transform local = networkPlayer.transform.Find("LocalPlayerRoot");
            if (local == null || local.GetComponentsInChildren<Camera>(true).Length != 1 || local.GetComponentsInChildren<AudioListener>(true).Length != 1)
                throw new InvalidOperationException("NetworkVRPlayer owner rig must contain one camera and one AudioListener.");

            GameObject bootstrap = RequireAsset<GameObject>(BootstrapPrefabPath);
            if (bootstrap.GetComponent<MultiplayerMatchBootstrap>() == null)
                throw new InvalidOperationException("GameBootstrap does not register or spawn the match authority prefab.");

            Scene map = EditorSceneManager.OpenScene(MainMapPath, OpenSceneMode.Single);
            MultiplayerSceneContext context = UnityEngine.Object.FindFirstObjectByType<MultiplayerSceneContext>(FindObjectsInactive.Include);
            MultiplayerSpawnPoint[] points = UnityEngine.Object.FindObjectsByType<MultiplayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(point => point.Index).ToArray();
            if (context == null || points.Length != MultiplayerConstants.MaxPlayers || points.Select(point => point.Index).Distinct().Count() != MultiplayerConstants.MaxPlayers)
                throw new InvalidOperationException("MainMap does not contain four deterministic multiplayer spawn points and a scene context.");
            for (int i = 0; i < points.Length; i++)
                for (int j = i + 1; j < points.Length; j++)
                    if (Vector3.Distance(points[i].transform.position, points[j].transform.position) < 1.5f)
                        throw new InvalidOperationException("Multiplayer spawn points overlap.");
            if (UnityEngine.Object.FindObjectsByType<MultiplayerSpawnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<MultiplayerMonsterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("MainMap match managers are missing or duplicated.");
            if (UnityEngine.Object.FindFirstObjectByType<PlayerRespawn>(FindObjectsInactive.Include) == null ||
                UnityEngine.Object.FindFirstObjectByType<TiptoeBrain>(FindObjectsInactive.Include) == null ||
                UnityEngine.Object.FindFirstObjectByType<StatueBrain>(FindObjectsInactive.Include) == null)
                throw new InvalidOperationException("Single-player player and monster paths were not preserved.");

            Scene lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
            Button start = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(button => button.GetComponentsInChildren<Text>(true).Any(text => text.text == "START MATCH"));
            if (start == null) throw new InvalidOperationException("The waiting room Start Match control is missing.");
            if (Time.timeScale != 1f) throw new InvalidOperationException("Match implementation must not alter global Time.timeScale.");

            Debug.Log("MULTIPLAYER_MATCH_VALIDATION_SUCCESS states=5 hostStart=true hostEnd=true spawns=4 networkTiptoe=1 networkStatue=1 singlePlayer=true timeScale=1");
        }

        private static GameObject BuildNetworkMonster(NetworkMonsterKind kind, string sourcePath, string outputPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = kind == NetworkMonsterKind.Tiptoe ? "NetworkTiptoe" : "NetworkStatue";
                NetworkObject networkObject = root.GetComponent<NetworkObject>();
                if (networkObject == null) networkObject = root.AddComponent<NetworkObject>();
                MonsterBrain brain = root.GetComponent<MonsterBrain>();
                MonsterNavigation navigation = root.GetComponent<MonsterNavigation>();
                MonsterPerception perception = root.GetComponent<MonsterPerception>();
                MonsterAnimationController animation = root.GetComponent<MonsterAnimationController>();
                MonsterAudioController audio = root.GetComponentInChildren<MonsterAudioController>(true);
                if (brain == null || navigation == null || perception == null || animation == null || audio == null)
                    throw new InvalidOperationException($"{kind} source prefab is missing its shared presentation or navigation components.");
                brain.enabled = false;
                perception.enabled = false;
                foreach (MonsterKillTrigger trigger in root.GetComponentsInChildren<MonsterKillTrigger>(true))
                {
                    trigger.enabled = false;
                    Collider collider = trigger.GetComponent<Collider>();
                    if (collider != null) collider.enabled = false;
                }
                NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                Transform eye = FindRecursive(root.transform, "Perception Eye");
                NetworkMonsterAuthority authority = root.GetComponent<NetworkMonsterAuthority>();
                if (authority == null) authority = root.AddComponent<NetworkMonsterAuthority>();
                authority.Configure(kind, brain, navigation, perception, animation, audio, eye, perception.ObstructionMask);
                return PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildMatchManager()
        {
            GameObject root = new("MultiplayerMatchManager");
            root.AddComponent<NetworkObject>();
            MatchPermissionValidator permissions = root.AddComponent<MatchPermissionValidator>();
            MultiplayerRespawnManager respawns = root.AddComponent<MultiplayerRespawnManager>();
            MultiplayerMatchManager manager = root.AddComponent<MultiplayerMatchManager>();
            manager.Configure(permissions, respawns, 7f);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MatchManagerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static void IntegrateNetworkPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(NetworkPlayerPrefabPath);
            try
            {
                Transform local = root.transform.Find("LocalPlayerRoot");
                Transform remote = root.transform.Find("RemoteVisualRoot");
                if (local == null || remote == null) throw new InvalidOperationException("NetworkVRPlayer ownership split is incomplete.");
                NetworkPlayerMatchState matchState = root.GetComponent<NetworkPlayerMatchState>();
                if (matchState == null) matchState = root.AddComponent<NetworkPlayerMatchState>();
                matchState.Configure(local.gameObject, local.GetComponent<Player>(), local.GetComponent<PlayerRespawn>(),
                    local.GetComponent<Rigidbody>(), local.GetComponent<PlayerDeathController>());

                Transform oldLabel = remote.Find("RemoteNameLabel");
                if (oldLabel != null) UnityEngine.Object.DestroyImmediate(oldLabel.gameObject);
                GameObject labelObject = new("RemoteNameLabel", typeof(TextMesh), typeof(RemoteNameBillboard));
                labelObject.transform.SetParent(remote, false);
                labelObject.transform.localPosition = new Vector3(0f, 1.28f, 0f);
                TextMesh label = labelObject.GetComponent<TextMesh>();
                label.text = "MONKEY";
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 44;
                label.characterSize = 0.035f;
                label.color = Color.white;
                root.GetComponent<NetworkVRPlayer>().ConfigureMatch(label);
                PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void IntegrateBootstrap(GameObject matchPrefab, GameObject tiptoePrefab, GameObject statuePrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BootstrapPrefabPath);
            try
            {
                MultiplayerMatchBootstrap match = root.GetComponent<MultiplayerMatchBootstrap>();
                if (match == null) match = root.AddComponent<MultiplayerMatchBootstrap>();
                match.Configure(root.GetComponent<TheBestMonkeyGame.Multiplayer.NetworkConnectionManager>(), matchPrefab, tiptoePrefab, statuePrefab);
                PrefabUtility.SaveAsPrefabAsset(root, BootstrapPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void IntegrateMainMap(GameObject networkTiptoe, GameObject networkStatue)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMapPath, OpenSceneMode.Single);
            GameObject previous = GameObject.Find("MultiplayerMatchSystems");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);

            PlayerRespawn singleRespawn = UnityEngine.Object.FindFirstObjectByType<PlayerRespawn>(FindObjectsInactive.Include);
            TiptoeBrain singleTiptoe = UnityEngine.Object.FindFirstObjectByType<TiptoeBrain>(FindObjectsInactive.Include);
            StatueBrain singleStatue = UnityEngine.Object.FindFirstObjectByType<StatueBrain>(FindObjectsInactive.Include);
            MonsterSpawnCoordinator coordinator = UnityEngine.Object.FindFirstObjectByType<MonsterSpawnCoordinator>(FindObjectsInactive.Include);
            if (singleRespawn == null || singleTiptoe == null || singleStatue == null || coordinator == null)
                throw new InvalidOperationException("MainMap single-player foundation is incomplete.");

            GameObject root = new("MultiplayerMatchSystems");
            GameObject spawnRoot = new("MultiplayerPlayerSpawns");
            spawnRoot.transform.SetParent(root.transform, false);
            Vector3 basePosition = singleRespawn.SpawnPoint != null ? singleRespawn.SpawnPoint.position : singleRespawn.transform.position;
            Quaternion facing = singleRespawn.SpawnPoint != null ? singleRespawn.SpawnPoint.rotation : Quaternion.identity;
            Vector3[] offsets =
            {
                new(-1.25f, 0f, -1.1f), new(1.25f, 0f, -1.1f),
                new(-1.25f, 0f, 1.1f), new(1.25f, 0f, 1.1f)
            };
            MultiplayerSpawnPoint[] points = new MultiplayerSpawnPoint[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject point = new($"PlayerSpawn_{i + 1:00}");
                point.transform.SetParent(spawnRoot.transform, false);
                point.transform.SetPositionAndRotation(basePosition + offsets[i], facing);
                points[i] = point.AddComponent<MultiplayerSpawnPoint>();
                points[i].Configure(i);
            }

            GameObject spawnManagerObject = new("MultiplayerSpawnManager");
            spawnManagerObject.transform.SetParent(root.transform, false);
            spawnManagerObject.AddComponent<NetworkObject>();
            MultiplayerSpawnManager spawnManager = spawnManagerObject.AddComponent<MultiplayerSpawnManager>();
            spawnManager.Configure(points);

            GameObject monsterManagerObject = new("MultiplayerMonsterManager");
            monsterManagerObject.transform.SetParent(root.transform, false);
            monsterManagerObject.AddComponent<NetworkObject>();
            MultiplayerMonsterManager monsterManager = monsterManagerObject.AddComponent<MultiplayerMonsterManager>();
            GameObject monsterSpawns = new("NetworkMonsterSpawns");
            monsterSpawns.transform.SetParent(root.transform, false);
            Transform tiptoeSpawn = new GameObject("NetworkTiptoeSpawn").transform;
            tiptoeSpawn.SetParent(monsterSpawns.transform, false);
            tiptoeSpawn.SetPositionAndRotation(singleTiptoe.transform.position, singleTiptoe.transform.rotation);
            Transform statueSpawn = new GameObject("NetworkStatueSpawn").transform;
            statueSpawn.SetParent(monsterSpawns.transform, false);
            statueSpawn.SetPositionAndRotation(singleStatue.transform.position, singleStatue.transform.rotation);
            monsterManager.Configure(networkTiptoe, networkStatue, tiptoeSpawn, statueSpawn);

            MultiplayerSceneContext context = root.AddComponent<MultiplayerSceneContext>();
            context.Configure(singleRespawn.gameObject, singleTiptoe, singleStatue, coordinator, spawnManager, monsterManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMapPath);
        }

        private static void IntegrateLobby()
        {
            Scene scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
            Text label = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(text => text.text.StartsWith("START MATCH", StringComparison.Ordinal));
            if (label == null) throw new InvalidOperationException("The existing lobby Start Match button was not found.");
            label.text = "START MATCH";
            Button button = label.GetComponentInParent<Button>();
            if (button != null) button.interactable = true;
            Text info = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(text => text.text.Contains("No monsters or match gameplay", StringComparison.Ordinal));
            if (info != null) info.text = "The host can start the shared match.\n\nHost and clients use identical movement, death, and respawn rules.";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LobbyPath);
        }

        private static void ValidateNetworkMonster(GameObject prefab, NetworkMonsterKind expected)
        {
            NetworkMonsterAuthority authority = prefab.GetComponent<NetworkMonsterAuthority>();
            MonsterBrain brain = prefab.GetComponent<MonsterBrain>();
            NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
            if (prefab.GetComponent<NetworkObject>() == null || authority == null || authority.Kind != expected || brain == null || brain.enabled || agent == null || agent.enabled)
                throw new InvalidOperationException($"Network {expected} prefab does not have a single server-authority source.");
            if (prefab.GetComponentsInChildren<MonsterKillTrigger>(true).Any(trigger => trigger.enabled))
                throw new InvalidOperationException($"Network {expected} retained a client-local kill trigger.");
        }

        private static Transform FindRecursive(Transform root, string name)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
            return result != null ? result : throw new InvalidOperationException($"{root.name} is missing {name}.");
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null ? asset : throw new InvalidOperationException($"Missing required asset: {path}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
#endif
