using System.Collections;
using GorillaLocomotion;
using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    [RequireComponent(typeof(Rigidbody), typeof(PlayerRespawn))]
    public sealed class PlayerDeathController : MonoBehaviour
    {
        [SerializeField] private Player locomotion;
        [SerializeField] private PlayerRespawn respawn;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Renderer[] handRenderers;
        [SerializeField] private Renderer fadeOverlay;
        [SerializeField, Range(0f, 0.5f)] private float fadeDuration = 0.12f;

        private bool deathActive;
        private MaterialPropertyBlock fadeProperties;

        public bool DeathActive => deathActive;

        public void Configure(
            Player player,
            PlayerRespawn playerRespawn,
            Rigidbody playerBody,
            Renderer[] hands,
            Renderer overlay)
        {
            locomotion = player;
            respawn = playerRespawn;
            body = playerBody;
            handRenderers = hands;
            fadeOverlay = overlay;
            fadeProperties = new MaterialPropertyBlock();
            SetFade(0f);
        }

        private void OnEnable()
        {
            RestoreSafePlayerState();
        }

        public void BeginDeath(MonsterBrain killer)
        {
            if (deathActive || killer == null || respawn == null || respawn.IsSpawnProtected) return;
            deathActive = true;
            StartCoroutine(RespawnAfterDeath(killer));
        }

        private IEnumerator RespawnAfterDeath(MonsterBrain killer)
        {
            locomotion.disableMovement = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            if (fadeOverlay != null && fadeDuration > 0f)
            {
                yield return FadeToBlack(fadeDuration);
            }

            // PlayerSpawn is a floor point. PlayerRespawn moves only the rigidbody
            // root, then rebuilds tracked hand/head history on the following frame.
            respawn.Respawn(3f);
            killer.ResetAfterPlayerDeath();

            float timeout = Time.unscaledTime + 1f;
            while (respawn.IsResetting && Time.unscaledTime < timeout) yield return null;

            RestoreSafePlayerState();
            if (fadeOverlay != null && fadeDuration > 0f)
            {
                yield return FadeFromBlack(fadeDuration);
            }

            deathActive = false;
        }

        public IEnumerator FadeToBlack(float duration)
        {
            fadeOverlay.enabled = true;
            for (float elapsed = 0f; elapsed < duration; elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 90f))
            {
                SetFade(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            SetFade(1f);
        }

        public IEnumerator FadeFromBlack(float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 90f))
            {
                SetFade(1f - Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            SetFade(0f);
            fadeOverlay.enabled = false;
        }

        private void RestoreSafePlayerState()
        {
            if (body != null)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (locomotion != null) locomotion.disableMovement = false;
            if (handRenderers != null)
            {
                foreach (Renderer hand in handRenderers)
                {
                    if (hand != null) hand.enabled = true;
                }
            }
        }

        private void SetFade(float alpha)
        {
            if (fadeOverlay == null) return;
            if (fadeProperties == null) fadeProperties = new MaterialPropertyBlock();
            fadeOverlay.GetPropertyBlock(fadeProperties);
            Color color = Color.black;
            color.a = alpha;
            fadeProperties.SetColor("_Color", color);
            fadeProperties.SetColor("_BaseColor", color);
            fadeOverlay.SetPropertyBlock(fadeProperties);
        }
    }
}
