using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerRespawnManager : NetworkBehaviour
    {
        public static MultiplayerRespawnManager Instance { get; private set; }
        [SerializeField, Range(1f, 8f)] private float spawnProtectionDuration = 3f;
        [SerializeField, Range(0.1f, 1f)] private float respawnDelay = 0.3f;
        private readonly Dictionary<ulong, Coroutine> activeRespawns = new();

        public float SpawnProtectionDuration => spawnProtectionDuration;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            CancelAllRespawns();
            if (Instance == this) Instance = null;
        }

        public bool TryKill(NetworkPlayerMatchState victim, NetworkMonsterAuthority killer)
        {
            MultiplayerMatchManager match = MultiplayerMatchManager.Instance;
            if (!IsServer || victim == null || killer == null || match == null || !match.MonsterKillsAllowed || !victim.ServerBeginDeath()) return false;
            if (activeRespawns.ContainsKey(victim.OwnerClientId)) return false;
            activeRespawns[victim.OwnerClientId] = StartCoroutine(RespawnPlayer(victim, killer));
            return true;
        }

        public void CancelAllRespawns()
        {
            foreach (Coroutine routine in activeRespawns.Values.Where(item => item != null)) StopCoroutine(routine);
            activeRespawns.Clear();
            if (!IsServer) return;
            foreach (NetworkPlayerMatchState player in FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None))
                if (player.IsSpawned) player.ServerCompleteRespawn();
        }

        private IEnumerator RespawnPlayer(NetworkPlayerMatchState victim, NetworkMonsterAuthority killer)
        {
            killer.ServerOnKill(victim.OwnerClientId);
            yield return new WaitForSecondsRealtime(respawnDelay);
            if (victim != null && victim.IsSpawned && MultiplayerSpawnManager.Current != null &&
                MultiplayerSpawnManager.Current.TryGetAssignedSpawn(victim.OwnerClientId, out MultiplayerSpawnPoint point))
            {
                victim.ServerAssignSpawn(point.Index, point.transform.position, point.transform.rotation, spawnProtectionDuration, true);
                yield return new WaitForSecondsRealtime(0.3f);
                if (victim != null && victim.IsSpawned) victim.ServerCompleteRespawn();
            }
            if (victim != null) activeRespawns.Remove(victim.OwnerClientId);
            killer?.ServerClearInvalidTarget();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!activeRespawns.TryGetValue(clientId, out Coroutine routine)) return;
            if (routine != null) StopCoroutine(routine);
            activeRespawns.Remove(clientId);
        }
    }
}
