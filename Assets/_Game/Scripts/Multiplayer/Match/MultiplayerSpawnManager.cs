using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerSpawnManager : NetworkBehaviour
    {
        public static MultiplayerSpawnManager Current { get; private set; }
        [SerializeField] private MultiplayerSpawnPoint[] spawnPoints;
        private readonly Dictionary<ulong, int> assignments = new();

        public IReadOnlyList<MultiplayerSpawnPoint> SpawnPoints => spawnPoints;

        public void Configure(MultiplayerSpawnPoint[] points) => spawnPoints = points.OrderBy(point => point.Index).ToArray();

        public override void OnNetworkSpawn()
        {
            Current = this;
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (Current == this) Current = null;
        }

        public bool AssignAllConnectedPlayers(float protectionSeconds)
        {
            if (!IsServer || spawnPoints == null || spawnPoints.Length < MultiplayerConstants.MaxPlayers) return false;
            NetworkPlayerMatchState[] players = FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None)
                .Where(player => player.IsSpawned)
                .OrderBy(player => player.OwnerClientId)
                .ToArray();
            if (players.Length == 0) return false;

            for (int i = 0; i < players.Length; i++)
            {
                if (i >= spawnPoints.Length)
                {
                    NetworkManager.DisconnectClient(players[i].OwnerClientId, "The four-player match is full.");
                    continue;
                }
                MultiplayerSpawnPoint point = spawnPoints[i];
                assignments[players[i].OwnerClientId] = point.Index;
                players[i].ServerAssignSpawn(point.Index, point.transform.position, point.transform.rotation, protectionSeconds, false);
                players[i].ServerCompleteRespawn();
            }
            Debug.Log($"MULTIPLAYER_SPAWNS_ASSIGNED players={Mathf.Min(players.Length, spawnPoints.Length)} unique=true floorAligned=true");
            return true;
        }

        public bool TryGetAssignedSpawn(ulong clientId, out MultiplayerSpawnPoint point)
        {
            point = null;
            if (!assignments.TryGetValue(clientId, out int index)) return false;
            point = spawnPoints.FirstOrDefault(item => item.Index == index);
            return point != null;
        }

        private void OnClientDisconnected(ulong clientId) => assignments.Remove(clientId);
    }
}
