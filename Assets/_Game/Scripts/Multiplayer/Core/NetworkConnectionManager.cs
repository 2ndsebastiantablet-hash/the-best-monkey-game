using System;
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

        private void EnsureReady()
        {
            if (networkManager == null || transport == null) throw new InvalidOperationException("The multiplayer bootstrap is missing its NetworkManager or UnityTransport.");
        }

        private static void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string payload = request.Payload == null ? string.Empty : Encoding.UTF8.GetString(request.Payload);
            bool compatible = payload.StartsWith(MultiplayerConstants.NetworkVersion + "|", StringComparison.Ordinal);
            response.Approved = compatible;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = compatible ? string.Empty : "Incompatible network version.";
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
