using System.Collections;
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
#else
            yield break;
#endif
        }
    }
}
