using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerLobbyUI : MonoBehaviour
    {
        [SerializeField] private Text roomCode;
        [SerializeField] private Text playerList;
        [SerializeField] private Text status;
        [SerializeField] private Text error;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button startButton;
        private float nextRefresh;

        public void Configure(Text roomCodeText, Text playersText, Text statusText, Text errorText, Button leave, Button start)
        {
            roomCode = roomCodeText; playerList = playersText; status = statusText; error = errorText;
            leaveButton = leave; startButton = start;
        }

        private void Start()
        {
            leaveButton.onClick.AddListener(Leave);
            startButton.interactable = false;
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.Presenter.StatusChanged += OnStatus;
                GameBootstrap.Instance.Presenter.ErrorChanged += OnError;
                OnStatus("Waiting room ready");
                OnError(GameBootstrap.Instance.Presenter.Error);
                roomCode.text = "ROOM: " + (string.IsNullOrEmpty(GameBootstrap.Instance.Sessions.CurrentRoomCode) ? "LOCAL" : GameBootstrap.Instance.Sessions.CurrentRoomCode);
            }
            RefreshPlayers();
        }

        private void OnDestroy()
        {
            leaveButton?.onClick.RemoveListener(Leave);
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.Presenter.StatusChanged -= OnStatus;
                GameBootstrap.Instance.Presenter.ErrorChanged -= OnError;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            RefreshPlayers();
        }

        private void RefreshPlayers()
        {
            NetworkVRPlayer[] players = FindObjectsByType<NetworkVRPlayer>(FindObjectsSortMode.None);
            var lines = players.Where(item => item.IsSpawned).OrderBy(item => item.OwnerClientId).Select(item =>
            {
                string name = item.Identity == null ? "Connecting..." : item.Identity.DisplayName;
                return item.IsRoomHost ? $"★ {name}  (HOST)" : $"• {name}";
            });
            playerList.text = players.Length == 0 ? "Connecting player..." : string.Join("\n", lines);
        }

        private async void Leave()
        {
            leaveButton.interactable = false;
            if (GameBootstrap.Instance != null) await GameBootstrap.Instance.Sessions.LeaveAsync();
        }

        private void OnStatus(string value) { if (status != null) status.text = value; }
        private void OnError(string value) { if (error != null) error.text = value; }
    }
}
