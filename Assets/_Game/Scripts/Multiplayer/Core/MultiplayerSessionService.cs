using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerSessionService : MonoBehaviour
    {
        [SerializeField] private UnityServicesInitializer services;
        [SerializeField] private PlayerAuthenticationService authentication;
        [SerializeField] private NetworkConnectionManager connection;
        [SerializeField] private MultiplayerSceneCoordinator scenes;
        [SerializeField] private MultiplayerErrorPresenter presenter;

        private float nextRequestTime;
        private bool requestInProgress;
        private bool leaveInProgress;

        public ISession ActiveSession { get; private set; }
        public string CurrentRoomCode { get; private set; } = string.Empty;
        public bool IsOnlineRoom => ActiveSession != null;
        public bool IsHost => ActiveSession != null ? ActiveSession.IsHost : connection != null && connection.Manager != null && connection.Manager.IsHost;

        public event Action SessionChanged;

        public void Configure(UnityServicesInitializer initializer, PlayerAuthenticationService auth, NetworkConnectionManager network, MultiplayerSceneCoordinator coordinator, MultiplayerErrorPresenter errorPresenter)
        {
            services = initializer;
            authentication = auth;
            connection = network;
            scenes = coordinator;
            presenter = errorPresenter;
        }

        public async Task<bool> CreateOrJoinAsync(string rawCode)
        {
            string code = NormalizeRoomCode(rawCode);
            if (!IsValidRoomCode(code))
            {
                presenter.ShowError("Room codes must be 4-12 letters or numbers.");
                return false;
            }
            if (requestInProgress || Time.realtimeSinceStartup < nextRequestTime)
            {
                presenter.ShowError("Please wait a moment before trying again.");
                return false;
            }

            requestInProgress = true;
            nextRequestTime = Time.realtimeSinceStartup + MultiplayerConstants.JoinRequestCooldownSeconds;
            presenter.ClearError();
            try
            {
                presenter.SetStatus("Connecting to Unity services...");
                await services.InitializeAsync();
                presenter.SetStatus("Signing in...");
                await authentication.SignInAsync();
                connection.PrepareOnlineConnection(authentication.PlayerId);

                presenter.SetStatus("Creating or joining private room...");
                var options = new SessionOptions
                {
                    Name = "Private room " + code,
                    MaxPlayers = MultiplayerConstants.MaxPlayers,
                    IsPrivate = true,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        [MultiplayerConstants.NetworkVersionProperty] = new SessionProperty(MultiplayerConstants.NetworkVersion, VisibilityPropertyOptions.Member),
                        [MultiplayerConstants.CustomRoomCodeProperty] = new SessionProperty(code, VisibilityPropertyOptions.Member)
                    }
                }.WithRelayNetwork();

                string deterministicSessionId = BuildSessionId(code);
                ISession session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(deterministicSessionId, options);
                if (!TryReadProperty(session, MultiplayerConstants.NetworkVersionProperty, out string version) || version != MultiplayerConstants.NetworkVersion)
                {
                    await session.LeaveAsync();
                    throw new InvalidOperationException("This room was created by an incompatible game version.");
                }

                ActiveSession = session;
                CurrentRoomCode = code;
                Subscribe(session);
                SessionChanged?.Invoke();
                presenter.SetStatus(session.IsHost ? "Room created. Loading lobby..." : "Room joined. Loading lobby...");
                if (session.IsHost) await scenes.EnterLobbyAsHostAsync();
                return true;
            }
            catch (SessionException exception)
            {
                presenter.ShowError(ToFriendlyMessage(exception));
                await ResetAfterFailureAsync();
                return false;
            }
            catch (Exception exception)
            {
                presenter.ShowError(exception.Message);
                await ResetAfterFailureAsync();
                return false;
            }
            finally
            {
                requestInProgress = false;
            }
        }

        public async Task LeaveAsync(bool returnToMenu = true)
        {
            if (leaveInProgress) return;
            leaveInProgress = true;
            presenter.SetStatus("Leaving room...");
            ISession session = ActiveSession;
            bool wasHost = session != null ? session.IsHost : connection != null && connection.Manager != null && connection.Manager.IsHost;
            Unsubscribe(session);
            ActiveSession = null;
            CurrentRoomCode = string.Empty;
            try
            {
                if (wasHost)
                {
                    connection.DisconnectRemoteClients("The host ended the room.");
                    await Task.Yield();
                }
                if (session != null && session.IsMember)
                {
                    if (session.IsHost) await session.AsHost().DeleteAsync();
                    else await session.LeaveAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Room cleanup warning: {exception.Message}");
            }
            finally
            {
                await connection.ShutdownAndWaitAsync();
                SessionChanged?.Invoke();
                presenter.SetStatus("Ready");
                leaveInProgress = false;
                if (returnToMenu) scenes.ReturnToMenu();
            }
        }

        public async Task StartLocalHostAsync()
        {
#if UNITY_EDITOR
            presenter.ClearError();
            CurrentRoomCode = "LOCAL";
            await connection.StartLocalHostAsync();
            SessionChanged?.Invoke();
            await scenes.EnterLobbyAsHostAsync();
#else
            await Task.CompletedTask;
#endif
        }

        public async Task StartLocalClientAsync()
        {
#if UNITY_EDITOR
            presenter.ClearError();
            CurrentRoomCode = "LOCAL";
            await connection.StartLocalClientAsync();
            SessionChanged?.Invoke();
#else
            await Task.CompletedTask;
#endif
        }

        public static string NormalizeRoomCode(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        public static bool IsValidRoomCode(string value)
        {
            if (value.Length < 4 || value.Length > 12) return false;
            foreach (char c in value) if (!char.IsLetterOrDigit(c) || c > 127) return false;
            return true;
        }

        private static string BuildSessionId(string normalizedCode)
        {
            return MultiplayerConstants.NetworkVersion + "-" + normalizedCode;
        }

        private static bool TryReadProperty(ISession session, string key, out string value)
        {
            value = null;
            if (session?.Properties == null || !session.Properties.TryGetValue(key, out SessionProperty property)) return false;
            value = property.Value;
            return true;
        }

        private void Subscribe(ISession session)
        {
            if (session == null) return;
            session.Changed += OnSessionChanged;
            session.RemovedFromSession += OnRemoved;
            session.Deleted += OnDeleted;
        }

        private void Unsubscribe(ISession session)
        {
            if (session == null) return;
            session.Changed -= OnSessionChanged;
            session.RemovedFromSession -= OnRemoved;
            session.Deleted -= OnDeleted;
        }

        private void OnSessionChanged() => SessionChanged?.Invoke();
        private async void OnRemoved()
        {
            if (leaveInProgress) return;
            presenter.ShowError("The room was closed or you were disconnected.");
            await LeaveAsync();
        }

        private async void OnDeleted()
        {
            if (leaveInProgress) return;
            presenter.ShowError("The host ended the room.");
            await LeaveAsync();
        }

        private async Task ResetAfterFailureAsync()
        {
            ISession session = ActiveSession;
            Unsubscribe(session);
            ActiveSession = null;
            CurrentRoomCode = string.Empty;
            if (session != null && session.IsMember)
            {
                try { await session.LeaveAsync(); } catch { }
            }
            connection.Shutdown();
            SessionChanged?.Invoke();
        }

        private static string ToFriendlyMessage(SessionException exception)
        {
            return exception.Error switch
            {
                SessionError.RateLimitExceeded => "Unity services are busy. Please wait a moment and try again.",
                SessionError.SessionNotFound => "That room is no longer available.",
                SessionError.NotAuthorized => "Authentication expired. Please restart the game and try again.",
                SessionError.Forbidden => "You do not have permission to enter that room.",
                SessionError.InvalidParameter or SessionError.InvalidSessionIdentifier => "That room code is not valid.",
                SessionError.NetworkManagerStartFailed or SessionError.NetworkSetupFailed => "The room exists, but the network connection could not start.",
                _ => string.IsNullOrWhiteSpace(exception.Message) ? "Could not connect to the room." : exception.Message
            };
        }
    }
}
