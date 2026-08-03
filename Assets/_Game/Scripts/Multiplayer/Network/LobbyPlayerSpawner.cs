using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class LobbyPlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject networkPlayerPrefab;

        public void Configure(GameObject prefab) => networkPlayerPrefab = prefab;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds) SpawnFor(clientId);
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        private void OnClientConnected(ulong clientId) => SpawnFor(clientId);

        private void SpawnFor(ulong clientId)
        {
            if (!IsServer || networkPlayerPrefab == null || !NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject != null) return;
            GameObject instance = Instantiate(networkPlayerPrefab);
            instance.name = $"NetworkVRPlayer_{clientId}";
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }
    }
}
