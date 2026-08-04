using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MatchPermissionValidator : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float requestCooldown = 1f;
        private readonly Dictionary<ulong, double> nextAllowedRequest = new();
        private readonly Dictionary<ulong, double> nextRejectLog = new();

        public bool ValidateHostRequest(ulong clientId, string action, MultiplayerMatchState current, MultiplayerMatchState required)
        {
            NetworkManager manager = NetworkManager.Singleton;
            double now = manager != null && manager.IsListening ? manager.ServerTime.Time : Time.realtimeSinceStartupAsDouble;
            if (manager == null || !manager.IsServer)
            {
                Reject(clientId, action, "server authority is unavailable", now);
                return false;
            }
            if (nextAllowedRequest.TryGetValue(clientId, out double allowedAt) && now < allowedAt)
            {
                Reject(clientId, action, "request rate limit", now);
                return false;
            }
            nextAllowedRequest[clientId] = now + requestCooldown;
            if (GameBootstrap.Instance == null || !GameBootstrap.Instance.Permissions.CanManageRoom(clientId))
            {
                Reject(clientId, action, "requester is not the authenticated room host", now);
                return false;
            }
            if (current != required)
            {
                Reject(clientId, action, $"match state is {current}, expected {required}", now);
                return false;
            }
            return true;
        }

        private void Reject(ulong clientId, string action, string reason, double now)
        {
            if (nextRejectLog.TryGetValue(clientId, out double logAt) && now < logAt) return;
            nextRejectLog[clientId] = now + 2d;
            Debug.LogWarning($"MATCH_REQUEST_REJECTED client={clientId} action={action} reason={reason}");
        }
    }
}
