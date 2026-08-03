using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerSceneCoordinator : MonoBehaviour
    {
        [SerializeField] private NetworkConnectionManager connection;

        public void Configure(NetworkConnectionManager manager) => connection = manager;

        public async Task EnterLobbyAsHostAsync()
        {
            NetworkManager manager = connection != null ? connection.Manager : null;
            if (manager == null || !manager.IsServer) throw new InvalidOperationException("Only the room host can load the shared lobby.");
            if (SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene) return;
            SceneEventProgressStatus result = manager.SceneManager.LoadScene(MultiplayerConstants.LobbyScene, LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started) throw new InvalidOperationException($"Lobby scene load did not start ({result}).");
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != MultiplayerConstants.LobbyScene)
            {
                if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Timed out while loading the multiplayer lobby.");
                await Task.Yield();
            }
        }

        public void ReturnToMenu()
        {
            if (SceneManager.GetActiveScene().name != MultiplayerConstants.MainMenuScene)
            {
                SceneManager.LoadScene(MultiplayerConstants.MainMenuScene, LoadSceneMode.Single);
            }
        }

        public void LoadSinglePlayer()
        {
            SceneManager.LoadScene(MultiplayerConstants.SinglePlayerScene, LoadSceneMode.Single);
        }
    }
}
