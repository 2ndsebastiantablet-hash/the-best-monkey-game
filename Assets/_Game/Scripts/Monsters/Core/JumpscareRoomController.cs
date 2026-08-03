using System.Collections;
using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class JumpscareRoomController : MonoBehaviour
    {
        [SerializeField] private Transform playerAnchor;
        [SerializeField] private Transform monsterAnchor;
        [SerializeField] private AudioSource centeredAudio;
        [SerializeField, Range(1.5f, 2.5f)] private float scareDuration = 2f;
        [SerializeField, Range(0.05f, 0.4f)] private float fadeDuration = 0.16f;

        public Transform PlayerAnchor => playerAnchor;

        public void Configure(Transform player, Transform monster, AudioSource audio)
        {
            playerAnchor = player;
            monsterAnchor = monster;
            centeredAudio = audio;
            centeredAudio.spatialBlend = 0f;
            centeredAudio.playOnAwake = false;
            centeredAudio.volume = 0.85f;
        }

        public IEnumerator Run(MonsterBrain killer, PlayerDeathController player)
        {
            yield return player.FadeToBlack(fadeDuration);
            player.MoveAndLockAt(playerAnchor, monsterAnchor);

            GameObject scareVisual = Instantiate(killer.VisualRoot.gameObject, monsterAnchor.position, monsterAnchor.rotation, monsterAnchor);
            scareVisual.name = $"{killer.MonsterId}_JumpscareVisual";
            scareVisual.transform.localPosition = Vector3.zero;
            scareVisual.transform.localRotation = Quaternion.identity;
            DisableGameplayComponents(scareVisual);
            if (centeredAudio != null && killer.GetComponentInChildren<MonsterJumpscareController>() is MonsterJumpscareController jump && jump.Scream != null)
            {
                centeredAudio.clip = jump.Scream;
                centeredAudio.Play();
            }

            yield return player.FadeFromBlack(fadeDuration);
            float elapsed = 0f;
            Vector3 initial = scareVisual.transform.localPosition;
            while (elapsed < scareDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float intensity = Mathf.Lerp(0.025f, 0.08f, elapsed / scareDuration);
                scareVisual.transform.localPosition = initial + Random.insideUnitSphere * intensity + Vector3.forward * Mathf.Lerp(0f, 0.22f, elapsed / scareDuration);
                scareVisual.transform.localRotation = Quaternion.Euler(Random.insideUnitSphere * intensity * 220f);
                yield return null;
            }

            yield return player.FadeToBlack(fadeDuration);
            if (centeredAudio != null) centeredAudio.Stop();
            Destroy(scareVisual);
            player.RestoreAfterJumpscare();
            killer.ResumeAfterJumpscare();
            yield return player.FadeFromBlack(fadeDuration);
        }

        private static void DisableGameplayComponents(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }
        }
    }
}
