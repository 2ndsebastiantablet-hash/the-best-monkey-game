using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerPlayModeAutoLauncher : MonoBehaviour
    {
        private IEnumerator Start()
        {
#if UNITY_EDITOR
            yield return new WaitUntil(() => GameBootstrap.Instance != null && SceneManager.GetActiveScene().name == MultiplayerConstants.MainMenuScene);
            yield return new WaitForSecondsRealtime(0.75f);
            string[] tags = Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags();
            bool host = System.Array.IndexOf(tags, "AutoHost") >= 0;
            bool client = System.Array.IndexOf(tags, "AutoClient") >= 0;
            bool matchCycle = System.Array.IndexOf(tags, "AutoMatchCycle") >= 0;
            if (!host && !client) yield break;
            if (host && client)
            {
                Debug.LogError("A Multiplayer Play Mode player cannot use both AutoHost and AutoClient tags.");
                yield break;
            }

            LocalPlayerProfile profile = GameBootstrap.Instance.Profile.Current.Clone();
            profile.DisplayName = host ? "Editor Host" : "Editor Client";
            profile.ColorIndex = host ? 0 : 1;
            GameBootstrap.Instance.Profile.Save(profile);
            if (host) _ = GameBootstrap.Instance.Sessions.StartLocalHostAsync();
            else _ = GameBootstrap.Instance.Sessions.StartLocalClientAsync();
            if (matchCycle) StartCoroutine(host ? RunHostMatchCycle() : ObserveClientMatchCycle());
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static IEnumerator RunHostMatchCycle()
        {
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene &&
                MultiplayerMatchManager.Instance != null && MultiplayerMatchManager.Instance.IsHostLocal &&
                Object.FindObjectsByType<NetworkVRPlayer>(FindObjectsSortMode.None).Any(player => player.IsOwner && player.IsSpawned));
            yield return new WaitForSecondsRealtime(2f);
            MultiplayerMatchManager.Instance.RequestStartMatch();
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == MultiplayerConstants.SinglePlayerScene &&
                MultiplayerMatchManager.Instance != null && MultiplayerMatchManager.Instance.State == MultiplayerMatchState.Playing);
            Debug.Log($"MULTIPLAYER_MPPM_MATCH_ACTIVE role=host players={Object.FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None).Length}");
            yield return new WaitUntil(() => MultiplayerMatchManager.Instance == null || MultiplayerMatchManager.Instance.GraceRemaining <= 0f);
            yield return new WaitForSecondsRealtime(2f);
            MultiplayerMatchManager.Instance?.RequestEndMatch();
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene &&
                MultiplayerMatchManager.Instance != null && MultiplayerMatchManager.Instance.State == MultiplayerMatchState.Waiting);
            Debug.Log("MULTIPLAYER_MPPM_MATCH_CYCLE_SUCCESS role=host returned=true connected=true");
        }

        private static IEnumerator ObserveClientMatchCycle()
        {
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == MultiplayerConstants.SinglePlayerScene &&
                MultiplayerMatchManager.Instance != null && MultiplayerMatchManager.Instance.State == MultiplayerMatchState.Playing);
            NetworkPlayerMatchState owner = Object.FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None).FirstOrDefault(player => player.IsOwner);
            Debug.Log($"MULTIPLAYER_MPPM_MATCH_ACTIVE role=client ownerSpawn={owner?.SpawnIndex ?? -1} players={Object.FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None).Length}");
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == MultiplayerConstants.LobbyScene &&
                MultiplayerMatchManager.Instance != null && MultiplayerMatchManager.Instance.State == MultiplayerMatchState.Waiting);
            Debug.Log("MULTIPLAYER_MPPM_MATCH_CYCLE_SUCCESS role=client returned=true connected=true");
        }
#endif
    }
}
