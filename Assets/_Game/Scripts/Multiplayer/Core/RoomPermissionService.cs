using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class RoomPermissionService : MonoBehaviour
    {
        [SerializeField] private MultiplayerSessionService sessions;
        [SerializeField] private PlayerAuthenticationService authentication;
        [SerializeField] private NetworkConnectionManager connection;

        public void Configure(MultiplayerSessionService sessionService, PlayerAuthenticationService auth, NetworkConnectionManager network)
        {
            sessions = sessionService;
            authentication = auth;
            connection = network;
        }

        public bool CanManageRoom(ulong requestingClientId)
        {
            NetworkManager manager = connection != null ? connection.Manager : null;
            if (manager == null || !manager.IsServer || requestingClientId != NetworkManager.ServerClientId) return false;
            if (sessions == null || sessions.ActiveSession == null) return manager.IsHost;
            return sessions.ActiveSession.IsHost && sessions.ActiveSession.Host == authentication.PlayerId;
        }
    }
}
