using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class MonsterJumpscareController : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private MonsterAudioController audioController;

        public Transform VisualRoot => visualRoot;
        public AudioClip Scream => audioController != null ? audioController.Scream : null;

        public void Configure(Transform visual, MonsterAudioController audio)
        {
            visualRoot = visual;
            audioController = audio;
        }
    }
}
