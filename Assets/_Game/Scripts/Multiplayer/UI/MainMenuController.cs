using System;
using UnityEngine;
using UnityEngine.UI;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button createOrJoinButton;
        [SerializeField] private Button roomBackButton;
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private Text statusText;
        [SerializeField] private Text errorText;
        [SerializeField] private SettingsPanelController settingsController;
        [SerializeField] private Button localHostButton;
        [SerializeField] private Button localClientButton;

        private bool busy;

        public void Configure(GameObject home, GameObject room, GameObject settings, Button join, Button single, Button settingsOpen, Button quit, Button connect, Button back, InputField code, Text status, Text error, SettingsPanelController settingsPanelController, Button localHost, Button localClient)
        {
            homePanel = home; roomPanel = room; settingsPanel = settings;
            joinRoomButton = join; singlePlayerButton = single; settingsButton = settingsOpen; quitButton = quit;
            createOrJoinButton = connect; roomBackButton = back; roomCodeInput = code;
            statusText = status; errorText = error; settingsController = settingsPanelController;
            localHostButton = localHost; localClientButton = localClient;
        }

        private void Start()
        {
            ShowHome();
            Bind(true);
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.Presenter.StatusChanged += OnStatus;
                GameBootstrap.Instance.Presenter.ErrorChanged += OnError;
                OnStatus(GameBootstrap.Instance.Presenter.Status);
                OnError(GameBootstrap.Instance.Presenter.Error);
            }
#if !UNITY_EDITOR
            if (localHostButton != null) localHostButton.gameObject.SetActive(false);
            if (localClientButton != null) localClientButton.gameObject.SetActive(false);
#endif
        }

        private void OnDestroy()
        {
            Bind(false);
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.Presenter.StatusChanged -= OnStatus;
                GameBootstrap.Instance.Presenter.ErrorChanged -= OnError;
            }
        }

        private void Bind(bool add)
        {
            Change(joinRoomButton, OpenRoom, add);
            Change(singlePlayerButton, StartSinglePlayer, add);
            Change(settingsButton, OpenSettings, add);
            Change(quitButton, Quit, add);
            Change(createOrJoinButton, JoinOnline, add);
            Change(roomBackButton, ShowHome, add);
            Change(localHostButton, StartLocalHost, add);
            Change(localClientButton, StartLocalClient, add);
        }

        private static void Change(Button button, UnityEngine.Events.UnityAction action, bool add)
        {
            if (button == null) return;
            if (add) button.onClick.AddListener(action); else button.onClick.RemoveListener(action);
        }

        private void ShowHome()
        {
            homePanel.SetActive(true); roomPanel.SetActive(false); settingsPanel.SetActive(false);
            GameBootstrap.Instance?.Presenter.ClearError();
        }

        private void OpenRoom()
        {
            homePanel.SetActive(false); roomPanel.SetActive(true); settingsPanel.SetActive(false);
            GameBootstrap.Instance?.Presenter.ClearError();
        }

        private void OpenSettings()
        {
            homePanel.SetActive(false); roomPanel.SetActive(false); settingsPanel.SetActive(true);
            settingsController.Open(ShowHome);
        }

        private async void JoinOnline()
        {
            if (busy || GameBootstrap.Instance == null) return;
            busy = true;
            SetButtonsInteractable(false);
            try { await GameBootstrap.Instance.JoinOrCreateRoomAsync(roomCodeInput.text); }
            finally { busy = false; SetButtonsInteractable(true); }
        }

#if UNITY_EDITOR
        private async void StartLocalHost()
        {
            if (busy || GameBootstrap.Instance == null) return;
            busy = true;
            try { await GameBootstrap.Instance.Sessions.StartLocalHostAsync(); }
            catch (Exception exception) { GameBootstrap.Instance.Presenter.ShowError(exception.Message); }
            finally { busy = false; }
        }

        private async void StartLocalClient()
        {
            if (busy || GameBootstrap.Instance == null) return;
            busy = true;
            try { await GameBootstrap.Instance.Sessions.StartLocalClientAsync(); }
            catch (Exception exception) { GameBootstrap.Instance.Presenter.ShowError(exception.Message); }
            finally { busy = false; }
        }
#else
        private void StartLocalHost() { }
        private void StartLocalClient() { }
#endif

        private void StartSinglePlayer()
        {
            if (GameBootstrap.Instance == null) return;
            GameBootstrap.Instance.Connection.Shutdown();
            GameBootstrap.Instance.Scenes.LoadSinglePlayer();
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnStatus(string value) { if (statusText != null) statusText.text = value; }
        private void OnError(string value) { if (errorText != null) errorText.text = value; }

        private void SetButtonsInteractable(bool value)
        {
            if (createOrJoinButton != null) createOrJoinButton.interactable = value;
            if (roomBackButton != null) roomBackButton.interactable = value;
        }
    }
}
