using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerMonsterManager : NetworkBehaviour
    {
        public static MultiplayerMonsterManager Current { get; private set; }

        [SerializeField] private GameObject networkTiptoePrefab;
        [SerializeField] private GameObject networkStatuePrefab;
        [SerializeField] private Transform tiptoeSpawn;
        [SerializeField] private Transform statueSpawn;

        public void Configure(GameObject tiptoePrefab, GameObject statuePrefab, Transform tiptoeStart, Transform statueStart)
        {
            networkTiptoePrefab = tiptoePrefab;
            networkStatuePrefab = statuePrefab;
            tiptoeSpawn = tiptoeStart;
            statueSpawn = statueStart;
        }

        public override void OnNetworkSpawn() => Current = this;

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
        }

        public void ServerEnsureMonsters()
        {
            if (!IsServer) return;
            SpawnOne(NetworkMonsterKind.Tiptoe, networkTiptoePrefab, tiptoeSpawn);
            SpawnOne(NetworkMonsterKind.Statue, networkStatuePrefab, statueSpawn);
            Debug.Log("MULTIPLAYER_MONSTERS_READY tiptoe=1 statue=1 authority=server");
        }

        public void ServerStopAndDespawn()
        {
            if (!IsServer) return;
            foreach (NetworkMonsterAuthority monster in FindObjectsByType<NetworkMonsterAuthority>(FindObjectsSortMode.None))
            {
                if (!monster.IsSpawned) continue;
                monster.ServerStopForTransition();
                monster.NetworkObject.Despawn(true);
            }
        }

        private void SpawnOne(NetworkMonsterKind kind, GameObject prefab, Transform point)
        {
            if (prefab == null || point == null) return;
            if (FindObjectsByType<NetworkMonsterAuthority>(FindObjectsSortMode.None).Any(item => item.IsSpawned && item.Kind == kind)) return;
            GameObject instance = Instantiate(prefab, point.position, point.rotation);
            instance.name = kind == NetworkMonsterKind.Tiptoe ? "NetworkTiptoe" : "NetworkStatue";
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            networkObject.Spawn(true);
            instance.GetComponent<NetworkMonsterAuthority>().ServerInitializeAt(point.position, point.rotation);
        }
    }
}
