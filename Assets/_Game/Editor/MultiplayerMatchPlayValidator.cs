#if UNITY_EDITOR
using System;
using System.Linq;
using TheBestMonkeyGame.Monsters;
using TheBestMonkeyGame.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Editor
{
    [InitializeOnLoad]
    public static class MultiplayerMatchPlayValidator
    {
        private const string ActiveKey = "TBMG.MatchPlay.Active";
        private const string ErrorsKey = "TBMG.MatchPlay.Errors";
        private const string DetailsKey = "TBMG.MatchPlay.Details";
        private const string StartedKey = "TBMG.MatchPlay.Started";
        private static bool hostRequested;
        private static bool startRequested;
        private static bool matchValidated;
        private static bool deathRequested;
        private static bool sawRespawning;
        private static bool endRequested;
        private static bool leaveRequested;

        static MultiplayerMatchPlayValidator()
        {
            if (SessionState.GetBool(ActiveKey, false)) Attach();
        }

        [MenuItem("Tools/The Best Monkey Game/Multiplayer Match/Validate Local Match Play Mode")]
        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(ErrorsKey, 0);
            SessionState.SetString(DetailsKey, string.Empty);
            SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
            ResetFlags();
            Attach();
            try
            {
                MultiplayerMatchMilestoneBuilder.Validate();
                EditorSceneManager.OpenScene(MultiplayerMilestoneBuilder.MainMenuPath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                RecordError(exception.ToString());
                Finish();
            }
        }

        private static void ResetFlags()
        {
            hostRequested = false;
            startRequested = false;
            matchValidated = false;
            deathRequested = false;
            sawRespawning = false;
            endRequested = false;
            leaveRequested = false;
        }

        private static void Attach()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void Detach()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Application.logMessageReceived -= OnLog;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
                ResetFlags();
            }
            else if (change == PlayModeStateChange.EnteredEditMode) Finish();
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                RecordError($"{type}: {condition}\n{stackTrace}");
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            float elapsed = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0f);
            if (!hostRequested && elapsed > 0.5f)
            {
                hostRequested = true;
                StartHost();
            }

            MultiplayerMatchManager match = MultiplayerMatchManager.Instance;
            if (!startRequested && SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene && match != null && match.IsHostLocal)
            {
                NetworkVRPlayer owner = UnityEngine.Object.FindObjectsByType<NetworkVRPlayer>(FindObjectsSortMode.None).FirstOrDefault(player => player.IsOwner && player.IsSpawned);
                if (owner != null)
                {
                    MatchPermissionValidator validator = match.GetComponent<MatchPermissionValidator>();
                    if (validator == null || validator.ValidateHostRequest(ulong.MaxValue, "nonhost-start-validation", MultiplayerMatchState.Waiting, MultiplayerMatchState.Waiting))
                        RecordError("A disconnected non-host identity passed host-only validation.");
                    startRequested = true;
                    match.RequestStartMatch();
                }
            }

            if (!matchValidated && SceneManager.GetActiveScene().name == MultiplayerConstants.SinglePlayerScene && match != null && match.State == MultiplayerMatchState.Playing)
            {
                ValidateActiveMatch(match);
                matchValidated = true;
            }

            NetworkPlayerMatchState playerState = UnityEngine.Object.FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None).FirstOrDefault(player => player.IsOwner);
            if (matchValidated && !deathRequested && match != null && match.MonsterKillsAllowed && playerState != null && !playerState.IsProtected)
            {
                NetworkMonsterAuthority tiptoe = UnityEngine.Object.FindObjectsByType<NetworkMonsterAuthority>(FindObjectsSortMode.None).FirstOrDefault(monster => monster.Kind == NetworkMonsterKind.Tiptoe);
                deathRequested = true;
                if (tiptoe == null || MultiplayerRespawnManager.Instance == null || !MultiplayerRespawnManager.Instance.TryKill(playerState, tiptoe))
                    RecordError("Authoritative host-player death could not be initiated after startup protection.");
            }

            if (deathRequested && playerState != null && playerState.IsRespawning) sawRespawning = true;
            if (sawRespawning && !endRequested && playerState != null && playerState.IsAlive && !playerState.IsRespawning && playerState.IsProtected)
            {
                endRequested = true;
                match?.RequestEndMatch();
            }

            if (endRequested && !leaveRequested && SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene &&
                match != null && match.State == MultiplayerMatchState.Waiting)
            {
                if (!GameBootstrap.Instance.Connection.IsListening) RecordError("Room disconnected while returning from the match.");
                if (UnityEngine.Object.FindObjectsByType<NetworkVRPlayer>(FindObjectsSortMode.None).Count(player => player.IsSpawned) != 1)
                    RecordError("Lobby player list was not restored after returning from the match.");
                leaveRequested = true;
                LeaveRoom();
            }

            if (leaveRequested && SceneManager.GetActiveScene().name == MultiplayerConstants.MainMenuScene && !GameBootstrap.Instance.Connection.IsListening)
                EditorApplication.ExitPlaymode();
            else if (elapsed > 100f)
            {
                RecordError($"Local match validator timed out in {SceneManager.GetActiveScene().name}, state={match?.State}.");
                EditorApplication.ExitPlaymode();
            }
        }

        private static async void StartHost()
        {
            try { await GameBootstrap.Instance.Sessions.StartLocalHostAsync(); }
            catch (Exception exception) { RecordError("Local host start failed: " + exception); }
        }

        private static async void LeaveRoom()
        {
            try { await GameBootstrap.Instance.Sessions.LeaveAsync(); }
            catch (Exception exception) { RecordError("Local room leave failed: " + exception); }
        }

        private static void ValidateActiveMatch(MultiplayerMatchManager match)
        {
            MultiplayerSpawnPoint[] points = UnityEngine.Object.FindObjectsByType<MultiplayerSpawnPoint>(FindObjectsSortMode.None).OrderBy(point => point.Index).ToArray();
            if (points.Length != 4 || points.Select(point => point.Index).Distinct().Count() != 4) RecordError("Runtime match does not have four unique spawn points.");
            NetworkPlayerMatchState[] players = UnityEngine.Object.FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None);
            if (players.Length != 1 || players[0].SpawnIndex < 0 || !players[0].IsProtected) RecordError("Host did not receive a protected deterministic match spawn.");
            NetworkMonsterAuthority[] monsters = UnityEngine.Object.FindObjectsByType<NetworkMonsterAuthority>(FindObjectsSortMode.None);
            if (monsters.Count(monster => monster.Kind == NetworkMonsterKind.Tiptoe) != 1 || monsters.Count(monster => monster.Kind == NetworkMonsterKind.Statue) != 1)
                RecordError("Runtime match does not contain exactly one shared Tiptoe and Statue.");
            if (UnityEngine.Object.FindObjectsByType<TiptoeBrain>(FindObjectsSortMode.None).Any(brain => brain.isActiveAndEnabled) ||
                UnityEngine.Object.FindObjectsByType<StatueBrain>(FindObjectsSortMode.None).Any(brain => brain.isActiveAndEnabled))
                RecordError("Single-player monster brains remained active in multiplayer mode.");
            if (UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera => camera.isActiveAndEnabled) != 1 ||
                UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled) != 1)
                RecordError("Local multiplayer match does not have exactly one active camera and AudioListener.");
            if (match.GraceRemaining < 5f) RecordError("Synchronized startup grace was not established.");
            Debug.Log($"MULTIPLAYER_MATCH_RUNTIME_ACTIVE players={players.Length} monsters={monsters.Length} spawns={points.Length} grace={match.GraceRemaining:F1}");
        }

        private static void RecordError(string message)
        {
            int count = SessionState.GetInt(ErrorsKey, 0) + 1;
            string details = SessionState.GetString(DetailsKey, string.Empty);
            if (!details.Contains(message)) details += "\n" + message;
            SessionState.SetInt(ErrorsKey, count);
            SessionState.SetString(DetailsKey, details);
        }

        private static void Finish()
        {
            int errors = SessionState.GetInt(ErrorsKey, 0);
            string details = SessionState.GetString(DetailsKey, string.Empty);
            SessionState.SetBool(ActiveKey, false);
            Detach();
            if (errors == 0)
            {
                Debug.Log("MULTIPLAYER_MATCH_LOCAL_PLAYMODE_SUCCESS host=1 start=true load=true spawn=true monsters=2 hostDeath=true protection=true end=true returnConnected=true leaveCleanup=true errors=0");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"MULTIPLAYER_MATCH_LOCAL_PLAYMODE_FAILED errors={errors}{details}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
