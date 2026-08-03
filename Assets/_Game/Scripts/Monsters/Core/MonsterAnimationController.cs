using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class MonsterAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Range(0.1f, 8f)] private float blendSharpness = 5f;
        private float targetPlaybackSpeed = 1f;
        private bool frozen;

        public Animator Animator => animator;

        public void Configure(Animator target)
        {
            animator = target;
            if (animator != null) animator.applyRootMotion = false;
        }

        private void Update()
        {
            if (animator == null) return;
            float target = frozen ? 0f : targetPlaybackSpeed;
            animator.speed = Mathf.MoveTowards(animator.speed, target, blendSharpness * Time.deltaTime);
        }

        public void SetLocomotionSpeed(float playbackSpeed)
        {
            targetPlaybackSpeed = Mathf.Max(0f, playbackSpeed);
        }

        public void SetFrozen(bool value)
        {
            frozen = value;
            if (value && animator != null) animator.speed = 0f;
        }
    }
}
