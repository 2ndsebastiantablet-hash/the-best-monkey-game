using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class MonsterAudioController : MonoBehaviour
    {
        [SerializeField] private AudioSource movementSource;
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioClip roamingMovement;
        [SerializeField] private AudioClip aggroCue;
        [SerializeField] private AudioClip chaseLoop;
        [SerializeField] private AudioClip searchCue;
        [SerializeField] private AudioClip specialCue;
        [SerializeField] private AudioClip teleportCue;
        [SerializeField] private AudioClip relocationCue;
        [SerializeField] private AudioClip scream;
        [SerializeField, Range(0f, 1f)] private float movementVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float cueVolume = 0.7f;

        public AudioClip Scream => scream;

        public void Configure(AudioSource movement, AudioSource oneShot, AudioClip placeholder)
        {
            movementSource = movement;
            oneShotSource = oneShot;
            roamingMovement = chaseLoop = aggroCue = searchCue = specialCue = teleportCue = relocationCue = scream = placeholder;
            ConfigureSpatial(movementSource, true);
            ConfigureSpatial(oneShotSource, false);
        }

        public void PlayRoaming() => PlayLoop(roamingMovement, movementVolume, 0.75f);
        public void PlayChase() => PlayLoop(chaseLoop, movementVolume, 1.35f);
        public void PlayAggro() => PlayOneShot(aggroCue, cueVolume);
        public void PlaySearch() => PlayOneShot(searchCue, cueVolume * 0.7f);
        public void PlaySpecial() => PlayOneShot(specialCue, cueVolume * 0.65f);
        public void PlayTeleport() => PlayOneShot(teleportCue, cueVolume * 0.65f);
        public void PlayRelocation() => PlayOneShot(relocationCue, cueVolume * 0.6f);

        public void StopAll()
        {
            if (movementSource != null) movementSource.Stop();
            if (oneShotSource != null) oneShotSource.Stop();
        }

        private void PlayLoop(AudioClip clip, float volume, float pitch)
        {
            if (movementSource == null || clip == null) return;
            if (movementSource.clip != clip || !movementSource.isPlaying)
            {
                movementSource.clip = clip;
                movementSource.loop = true;
                movementSource.Play();
            }
            movementSource.volume = volume;
            movementSource.pitch = pitch;
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (oneShotSource != null && clip != null) oneShotSource.PlayOneShot(clip, volume);
        }

        private static void ConfigureSpatial(AudioSource source, bool loop)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 35f;
            source.dopplerLevel = 0f;
        }
    }
}
