#if UNITY_EDITOR
using System;
using TheBestMonkeyGame.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Editor
{
    [InitializeOnLoad]
    public static class MultiplayerOnlineSmokeValidator
    {
        private const string ActiveKey = "TBMG.OnlineSmoke.Active";
        private const string PhaseKey = "TBMG.OnlineSmoke.Phase";
        private const string ErrorKey = "TBMG.OnlineSmoke.Error";
        private const string CodeKey = "TBMG.OnlineSmoke.Code";
        private static double startedAt;
        private static bool joinStarted;
        private static bool joinFinished;
        private static bool joinSucceeded;
        private static bool leaveStarted;

        static MultiplayerOnlineSmokeValidator()
        {
            if (SessionState.GetBool(ActiveKey, false)) Attach();
        }

        [MenuItem("Tools/The Best Monkey Game/Multiplayer/Run Online Services Smoke Test")]
        public static void Run()
        {
            string code = "T" + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x0FFFFFFF).ToString("X7");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(PhaseKey, "starting");
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(CodeKey, code);
            ResetRuntimeFlags();
            Attach();
            try
            {
                MultiplayerMilestoneBuilder.ValidateMilestone();
                EditorSceneManager.OpenScene(MultiplayerMilestoneBuilder.MainMenuPath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Fail(exception.ToString());
                Finish();
            }
        }

        private static void ResetRuntimeFlags()
        {
            startedAt = EditorApplication.timeSinceStartup;
            joinStarted = false;
            joinFinished = false;
            joinSucceeded = false;
            leaveStarted = false;
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

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) Fail($"{type}: {condition}\n{stackTrace}");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(PhaseKey, "playing");
                ResetRuntimeFlags();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) SessionState.SetString(PhaseKey, "exiting");
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(PhaseKey, string.Empty) == "exiting") Finish();
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            double elapsed = EditorApplication.timeSinceStartup - startedAt;
            if (!joinStarted && elapsed > 0.5)
            {
                joinStarted = true;
                BeginJoin();
            }
            if (joinFinished && !joinSucceeded)
            {
                EditorApplication.ExitPlaymode();
                return;
            }
            if (joinSucceeded && !leaveStarted && SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene)
            {
                NetworkVRPlayer player = UnityEngine.Object.FindFirstObjectByType<NetworkVRPlayer>();
                if (player != null && player.IsSpawned)
                {
                    ValidateOnlineRoom(player);
                    leaveStarted = true;
                    LeaveRoom();
                }
            }
            if (leaveStarted && SceneManager.GetActiveScene().name == MultiplayerConstants.MainMenuScene && !GameBootstrap.Instance.Connection.IsListening)
            {
                EditorApplication.ExitPlaymode();
                return;
            }
            if (elapsed > 90d)
            {
                Fail($"Online smoke test timed out in {SceneManager.GetActiveScene().name}: {GameBootstrap.Instance?.Presenter?.Error}");
                if (GameBootstrap.Instance?.Sessions?.ActiveSession != null) LeaveRoom();
                EditorApplication.ExitPlaymode();
            }
        }

        private static async void BeginJoin()
        {
            try
            {
                if (GameBootstrap.Instance == null) throw new InvalidOperationException("GameBootstrap did not initialize.");
                joinSucceeded = await GameBootstrap.Instance.Sessions.CreateOrJoinAsync(SessionState.GetString(CodeKey, string.Empty));
                joinFinished = true;
                if (!joinSucceeded) Fail(GameBootstrap.Instance.Presenter.Error);
            }
            catch (Exception exception)
            {
                joinFinished = true;
                joinSucceeded = false;
                Fail(exception.ToString());
            }
        }

        private static void ValidateOnlineRoom(NetworkVRPlayer player)
        {
            GameBootstrap bootstrap = GameBootstrap.Instance;
            string code = SessionState.GetString(CodeKey, string.Empty);
            if (bootstrap.Sessions.ActiveSession == null || !bootstrap.Sessions.ActiveSession.IsHost || bootstrap.Sessions.CurrentRoomCode != code)
                Fail("Online session did not preserve host status and the exact normalized custom room code.");
            if (!bootstrap.Authentication.IsSignedIn || !bootstrap.Services.IsReady)
                Fail("Unity Services or anonymous authentication was not ready in the online room.");
            if (!player.IsOwner || !bootstrap.Connection.IsListening)
                Fail("Online Relay host did not spawn an owned network VR player.");
            Debug.Log($"MULTIPLAYER_ONLINE_ROOM_CONNECTED codeLength={code.Length} host={bootstrap.Sessions.ActiveSession?.IsHost} playerSpawned={player.IsSpawned} auth=anonymous relay=true");
        }

        private static async void LeaveRoom()
        {
            try { await GameBootstrap.Instance.Sessions.LeaveAsync(); }
            catch (Exception exception) { Fail("Online leave failed: " + exception); }
        }

        private static void Fail(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) message = "Unknown online smoke-test failure.";
            string existing = SessionState.GetString(ErrorKey, string.Empty);
            if (!existing.Contains(message)) SessionState.SetString(ErrorKey, existing + "\n" + message);
        }

        private static void Finish()
        {
            string errors = SessionState.GetString(ErrorKey, string.Empty).Trim();
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetString(PhaseKey, "done");
            Detach();
            if (string.IsNullOrEmpty(errors))
            {
                Debug.Log("MULTIPLAYER_ONLINE_SMOKE_SUCCESS services=true anonymousAuth=true createOrJoin=true relay=true exactCustomCode=true leaveCleanup=true");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("MULTIPLAYER_ONLINE_SMOKE_FAILED\n" + errors);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
