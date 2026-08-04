using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Multiplayer
{
    [RequireComponent(typeof(MatchPermissionValidator), typeof(MultiplayerRespawnManager))]
    public sealed class MultiplayerMatchManager : NetworkBehaviour
    {
        public static MultiplayerMatchManager Instance { get; private set; }

        [SerializeField] private MatchPermissionValidator permissions;
        [SerializeField] private MultiplayerRespawnManager respawns;
        [SerializeField, Range(3f, 15f)] private float startupGraceDuration = 7f;
        [SerializeField, Range(10f, 60f)] private float transitionTimeout = 45f;

        private readonly NetworkVariable<MultiplayerMatchState> state = new(
            MultiplayerMatchState.Waiting, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> monsterActivationTime = new(
            0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> matchSequence = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool transitionInProgress;
        private float transitionDeadline;

        public MultiplayerMatchState State => state.Value;
        public double MonsterActivationTime => monsterActivationTime.Value;
        public float StartupGraceDuration => startupGraceDuration;
        public uint MatchSequence => matchSequence.Value;
        public bool IsHostLocal => IsSpawned && NetworkManager != null && NetworkManager.IsHost && GameBootstrap.Instance != null && GameBootstrap.Instance.Sessions.IsHost;
        public bool MonsterKillsAllowed => IsServer && state.Value == MultiplayerMatchState.Playing && NetworkManager.ServerTime.Time >= monsterActivationTime.Value && !transitionInProgress;
        public bool AllowsNewConnections => state.Value == MultiplayerMatchState.Waiting && !transitionInProgress;
        public float GraceRemaining => NetworkManager == null || !NetworkManager.IsListening ? 0f : Mathf.Max(0f, (float)(monsterActivationTime.Value - NetworkManager.ServerTime.Time));

        public event Action<MultiplayerMatchState> StateChanged;

        public void Configure(MatchPermissionValidator validator, MultiplayerRespawnManager respawnManager, float startupGrace = 7f)
        {
            permissions = validator;
            respawns = respawnManager;
            startupGraceDuration = startupGrace;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            state.OnValueChanged += OnStateValueChanged;
            if (IsServer)
            {
                state.Value = MultiplayerMatchState.Waiting;
                NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            }
            StateChanged?.Invoke(state.Value);
            Debug.Log($"MATCH_MANAGER_SPAWNED server={IsServer} state={state.Value}");
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateValueChanged;
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsServer || !transitionInProgress || Time.realtimeSinceStartup < transitionDeadline) return;
            Debug.LogError($"MATCH_TRANSITION_TIMEOUT state={state.Value}; returning connected players to the waiting room.");
            transitionInProgress = false;
            BeginLobbyReturn(true);
        }

        public void RequestStartMatch()
        {
            if (!IsSpawned) return;
            RequestStartMatchRpc();
        }

        public void RequestEndMatch()
        {
            if (!IsSpawned) return;
            RequestEndMatchRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestStartMatchRpc(RpcParams rpcParams = default)
        {
            ulong requester = rpcParams.Receive.SenderClientId;
            if (transitionInProgress || !permissions.ValidateHostRequest(requester, "start", state.Value, MultiplayerMatchState.Waiting)) return;
            if (NetworkManager.ConnectedClientsIds.Count < 1)
            {
                Debug.LogWarning("MATCH_START_REJECTED: no connected player exists.");
                return;
            }

            transitionInProgress = true;
            transitionDeadline = Time.realtimeSinceStartup + transitionTimeout;
            state.Value = MultiplayerMatchState.Starting;
            matchSequence.Value++;
            monsterActivationTime.Value = 0d;
            GameBootstrap.Instance?.Presenter.SetStatus("Starting match...");
            _ = GameBootstrap.Instance?.Sessions.PublishMatchStateAsync(MultiplayerMatchState.Starting);
            SceneEventProgressStatus result = NetworkManager.SceneManager.LoadScene(MultiplayerConstants.SinglePlayerScene, LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started)
            {
                transitionInProgress = false;
                state.Value = MultiplayerMatchState.Waiting;
                Debug.LogError($"MATCH_START_FAILED sceneStatus={result}");
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestEndMatchRpc(RpcParams rpcParams = default)
        {
            ulong requester = rpcParams.Receive.SenderClientId;
            if (transitionInProgress || !permissions.ValidateHostRequest(requester, "end", state.Value, MultiplayerMatchState.Playing)) return;
            state.Value = MultiplayerMatchState.Ending;
            respawns.CancelAllRespawns();
            MultiplayerMonsterManager.Current?.ServerStopAndDespawn();
            BeginLobbyReturn(false);
        }

        public void ServerEndMatchForValidation()
        {
            if (!IsServer || state.Value != MultiplayerMatchState.Playing || transitionInProgress) return;
            state.Value = MultiplayerMatchState.Ending;
            respawns.CancelAllRespawns();
            MultiplayerMonsterManager.Current?.ServerStopAndDespawn();
            BeginLobbyReturn(false);
        }

        private void BeginLobbyReturn(bool recovery)
        {
            if (!IsServer) return;
            transitionInProgress = true;
            transitionDeadline = Time.realtimeSinceStartup + transitionTimeout;
            state.Value = MultiplayerMatchState.ReturningToLobby;
            monsterActivationTime.Value = double.PositiveInfinity;
            GameBootstrap.Instance?.Presenter.SetStatus(recovery ? "Recovering waiting room..." : "Returning to waiting room...");
            _ = GameBootstrap.Instance?.Sessions.PublishMatchStateAsync(MultiplayerMatchState.ReturningToLobby);
            SceneEventProgressStatus result = NetworkManager.SceneManager.LoadScene(MultiplayerConstants.LobbyScene, LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started)
            {
                transitionInProgress = false;
                Debug.LogError($"MATCH_RETURN_FAILED sceneStatus={result}");
            }
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (!IsServer || sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;
            if (sceneEvent.ClientsThatTimedOut != null)
            {
                foreach (ulong clientId in sceneEvent.ClientsThatTimedOut.Where(id => id != NetworkManager.ServerClientId))
                    NetworkManager.DisconnectClient(clientId, "Scene loading timed out. Please rejoin after the match returns to the lobby.");
            }

            if (sceneEvent.SceneName == MultiplayerConstants.SinglePlayerScene && state.Value == MultiplayerMatchState.Starting)
                StartCoroutine(FinalizeMatchStart());
            else if (sceneEvent.SceneName == MultiplayerConstants.LobbyScene && state.Value == MultiplayerMatchState.ReturningToLobby)
                StartCoroutine(FinalizeLobbyReturn());
        }

        private IEnumerator FinalizeMatchStart()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((MultiplayerSpawnManager.Current == null || MultiplayerMonsterManager.Current == null) && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (MultiplayerSpawnManager.Current == null || !MultiplayerSpawnManager.Current.AssignAllConnectedPlayers(startupGraceDuration + 1f))
            {
                Debug.LogError("MATCH_START_FAILED: multiplayer spawn manager did not become ready.");
                BeginLobbyReturn(true);
                yield break;
            }

            monsterActivationTime.Value = NetworkManager.ServerTime.Time + startupGraceDuration;
            MultiplayerMonsterManager.Current?.ServerEnsureMonsters();
            state.Value = MultiplayerMatchState.Playing;
            transitionInProgress = false;
            GameBootstrap.Instance?.Presenter.SetStatus($"Match starts in {startupGraceDuration:0}...");
            _ = GameBootstrap.Instance?.Sessions.PublishMatchStateAsync(MultiplayerMatchState.Playing);
            Debug.Log($"MATCH_STARTED sequence={matchSequence.Value} players={NetworkManager.ConnectedClientsIds.Count} grace={startupGraceDuration:F1}");
        }

        private IEnumerator FinalizeLobbyReturn()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            LobbyPlayerSpawner lobby = null;
            while (lobby == null && Time.realtimeSinceStartup < deadline)
            {
                lobby = FindFirstObjectByType<LobbyPlayerSpawner>();
                yield return null;
            }
            lobby?.ServerRestoreLobbyPlayers();
            state.Value = MultiplayerMatchState.Waiting;
            monsterActivationTime.Value = 0d;
            transitionInProgress = false;
            GameBootstrap.Instance?.Presenter.SetStatus("Waiting room ready");
            _ = GameBootstrap.Instance?.Sessions.PublishMatchStateAsync(MultiplayerMatchState.Waiting);
            Debug.Log($"MATCH_RETURNED_TO_LOBBY sequence={matchSequence.Value} connected={NetworkManager.ConnectedClientsIds.Count}");
        }

        private void OnStateValueChanged(MultiplayerMatchState previous, MultiplayerMatchState current)
        {
            StateChanged?.Invoke(current);
        }
    }
}
