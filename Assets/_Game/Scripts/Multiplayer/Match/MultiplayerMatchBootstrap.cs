using Unity.Netcode;
using UnityEngine;
using System.Linq;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerMatchBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkConnectionManager connection;
        [SerializeField] private GameObject matchManagerPrefab;
        [SerializeField] private GameObject networkTiptoePrefab;
        [SerializeField] private GameObject networkStatuePrefab;

        public void Configure(NetworkConnectionManager network, GameObject managerPrefab, GameObject tiptoePrefab, GameObject statuePrefab)
        {
            connection = network;
            matchManagerPrefab = managerPrefab;
            networkTiptoePrefab = tiptoePrefab;
            networkStatuePrefab = statuePrefab;
        }

        private void Awake()
        {
            NetworkManager manager = connection != null ? connection.Manager : null;
            Register(manager, matchManagerPrefab);
            Register(manager, networkTiptoePrefab);
            Register(manager, networkStatuePrefab);
        }

        private void Update()
        {
            NetworkManager manager = connection != null ? connection.Manager : null;
            if (manager == null || !manager.IsServer || !manager.IsListening || MultiplayerMatchManager.Instance != null || matchManagerPrefab == null) return;
            GameObject instance = Instantiate(matchManagerPrefab);
            instance.name = "MultiplayerMatchManager";
            instance.GetComponent<NetworkObject>().Spawn(false);
        }

        private static void Register(NetworkManager manager, GameObject prefab)
        {
            if (manager == null || prefab == null || manager.NetworkConfig.Prefabs.Prefabs.Any(item => item.Prefab == prefab)) return;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
        }
    }
}
