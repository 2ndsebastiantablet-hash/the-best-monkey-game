using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class NetworkConnectionManager : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;

        public NetworkManager Manager => networkManager;
        public bool IsListening => networkManager != null && networkManager.IsListening;

        private void Awake()
        {
            if (networkManager != null) networkManager.ConnectionApprovalCallback = ApproveConnection;
        }

        private void OnDestroy()
        {
            if (networkManager != null && networkManager.ConnectionApprovalCallback == ApproveConnection)
                networkManager.ConnectionApprovalCallback = null;
        }

        public void Configure(NetworkManager manager, UnityTransport unityTransport)
        {
            networkManager = manager;
            transport = unityTransport;
        }

        public void PrepareOnlineConnection(string authenticatedPlayerId)
        {
            if (networkManager == null) return;
            string payload = MultiplayerConstants.NetworkVersion + "|" + (authenticatedPlayerId ?? string.Empty);
            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(payload);
        }

        public async Task StartLocalHostAsync()
        {
#if UNITY_EDITOR
            EnsureReady();
            if (networkManager.IsListening) return;
            transport.SetConnectionData("127.0.0.1", MultiplayerConstants.LocalTestPort, "0.0.0.0");
            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(MultiplayerConstants.NetworkVersion + "|local-host");
            if (!networkManager.StartHost()) throw new InvalidOperationException("The local host could not start.");
            await WaitForConnectionAsync();
#else
            await Task.FromException(new InvalidOperationException("Local test networking is editor-only."));
#endif
        }

        public async Task StartLocalClientAsync()
        {
#if UNITY_EDITOR
            EnsureReady();
            if (networkManager.IsListening) return;
            transport.SetConnectionData("127.0.0.1", MultiplayerConstants.LocalTestPort);
            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(MultiplayerConstants.NetworkVersion + "|local-client");
            if (!networkManager.StartClient()) throw new InvalidOperationException("The local client could not start.");
            await WaitForConnectionAsync();
#else
            await Task.FromException(new InvalidOperationException("Local test networking is editor-only."));
#endif
        }

        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
        }

        public void DisconnectRemoteClients(string reason)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            ulong[] remoteClients = networkManager.ConnectedClientsIds
                .Where(clientId => clientId != NetworkManager.ServerClientId)
                .ToArray();
            foreach (ulong clientId in remoteClients) networkManager.DisconnectClient(clientId, reason);
        }

        public async Task ShutdownAndWaitAsync(float timeoutSeconds = 3f)
        {
            if (networkManager == null || !networkManager.IsListening) return;
            networkManager.Shutdown();
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, timeoutSeconds);
            while (networkManager != null && networkManager.IsListening && Time.realtimeSinceStartup < deadline)
            {
                await Task.Yield();
            }
            if (networkManager != null && networkManager.IsListening)
            {
                Debug.LogWarning("NETWORK_SHUTDOWN_TIMEOUT: forcing the local scene transition while Netcode finishes cleanup.");
            }
        }

        private void EnsureReady()
        {
            if (networkManager == null || transport == null) throw new InvalidOperationException("The multiplayer bootstrap is missing its NetworkManager or UnityTransport.");
        }

        private static void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string payload = request.Payload == null ? string.Empty : Encoding.UTF8.GetString(request.Payload);
            bool compatible = payload.StartsWith(MultiplayerConstants.NetworkVersion + "|", StringComparison.Ordinal);
            bool waiting = MultiplayerMatchManager.Instance == null || MultiplayerMatchManager.Instance.AllowsNewConnections;
            bool capacity = NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClientsIds.Count < MultiplayerConstants.MaxPlayers;
            response.Approved = compatible && waiting && capacity;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = !compatible ? "Incompatible network version." :
                !waiting ? "A match is already in progress. Join after the room returns to the waiting room." :
                !capacity ? "This four-player room is full." : string.Empty;
        }

        private async Task WaitForConnectionAsync()
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (networkManager != null && !networkManager.IsConnectedClient)
            {
                if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Timed out while connecting to the local room.");
                await Task.Yield();
            }
        }
    }
}
