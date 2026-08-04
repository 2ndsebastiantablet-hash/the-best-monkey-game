using Unity.Netcode;
using UnityEngine;
using System.Linq;

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
            ServerRestoreLobbyPlayers();
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
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, false);
        }

        public void ServerRestoreLobbyPlayers()
        {
            if (!IsServer) return;
            NetworkPlayerMatchState[] players = FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None)
                .Where(item => item.IsSpawned)
                .OrderBy(item => item.OwnerClientId)
                .ToArray();
            Vector3[] points =
            {
                new(-1.8f, 0.05f, -1.2f), new(1.8f, 0.05f, -1.2f),
                new(-1.8f, 0.05f, -3.2f), new(1.8f, 0.05f, -3.2f)
            };
            for (int i = 0; i < players.Length && i < points.Length; i++)
                players[i].ServerRestoreForLobby(points[i], Quaternion.identity);
        }
    }
}
