using TheBestMonkeyGame.Monsters;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    [DefaultExecutionOrder(-900)]
    public sealed class MultiplayerSceneContext : MonoBehaviour
    {
        public static MultiplayerSceneContext Current { get; private set; }

        [SerializeField] private GameObject singlePlayerRoot;
        [SerializeField] private TiptoeBrain singlePlayerTiptoe;
        [SerializeField] private StatueBrain singlePlayerStatue;
        [SerializeField] private MonsterSpawnCoordinator singlePlayerMonsterCoordinator;
        [SerializeField] private MultiplayerSpawnManager multiplayerSpawns;
        [SerializeField] private MultiplayerMonsterManager multiplayerMonsters;

        public MultiplayerSceneMode Mode { get; private set; }
        public bool IsMultiplayer => Mode != MultiplayerSceneMode.SinglePlayer;

        public void Configure(GameObject playerRoot, TiptoeBrain tiptoe, StatueBrain statue, MonsterSpawnCoordinator coordinator, MultiplayerSpawnManager spawns, MultiplayerMonsterManager monsters)
        {
            singlePlayerRoot = playerRoot;
            singlePlayerTiptoe = tiptoe;
            singlePlayerStatue = statue;
            singlePlayerMonsterCoordinator = coordinator;
            multiplayerSpawns = spawns;
            multiplayerMonsters = monsters;
        }

        private void Awake()
        {
            Current = this;
            NetworkManager manager = NetworkManager.Singleton;
            bool multiplayer = manager != null && manager.IsListening && (manager.IsClient || manager.IsServer);
            Mode = !multiplayer ? MultiplayerSceneMode.SinglePlayer : manager.IsHost ? MultiplayerSceneMode.MultiplayerHost : MultiplayerSceneMode.MultiplayerClient;

            if (singlePlayerRoot != null) singlePlayerRoot.SetActive(!multiplayer);
            if (singlePlayerTiptoe != null) singlePlayerTiptoe.gameObject.SetActive(!multiplayer);
            if (singlePlayerStatue != null) singlePlayerStatue.gameObject.SetActive(!multiplayer);
            // Keep the shared MonsterSystems root active because it also owns the baked
            // NavMeshSurface. Only the single-player startup coordinator is mode-specific.
            if (singlePlayerMonsterCoordinator != null) singlePlayerMonsterCoordinator.enabled = !multiplayer;
            if (multiplayerSpawns != null) multiplayerSpawns.gameObject.SetActive(multiplayer);
            if (multiplayerMonsters != null) multiplayerMonsters.gameObject.SetActive(multiplayer);
            Debug.Log($"SCENE_RUNTIME_MODE mode={Mode} singlePlayerRoot={singlePlayerRoot != null && singlePlayerRoot.activeSelf}");
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
