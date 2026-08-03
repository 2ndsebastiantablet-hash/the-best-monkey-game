using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    public enum MonsterState
    {
        Dormant,
        Roaming,
        Investigating,
        Alerted,
        Chasing,
        Searching,
        Special,
        Killing,
        Resetting
    }

    public abstract class MonsterBrain : MonoBehaviour
    {
        [SerializeField] protected MonsterNavigation navigation;
        [SerializeField] protected MonsterPerception perception;
        [SerializeField] protected MonsterAnimationController animationController;
        [SerializeField] protected MonsterAudioController audioController;
        [SerializeField] protected MonsterJumpscareController jumpscareController;
        [SerializeField, Min(0f)] private float startupGracePeriod = 5f;

        protected Transform playerRoot;
        protected Transform playerHead;
        protected PlayerRespawn playerRespawn;
        protected float stateEnteredAt;
        protected Vector3 lastKnownPlayerPosition;

        public MonsterState State { get; private set; } = MonsterState.Dormant;
        public string MonsterId => gameObject.name;
        public Transform VisualRoot => jumpscareController != null ? jumpscareController.VisualRoot : null;
        public MonsterNavigation Navigation => navigation;
        public MonsterPerception Perception => perception;

        public virtual void ConfigureShared(
            MonsterNavigation monsterNavigation,
            MonsterPerception monsterPerception,
            MonsterAnimationController monsterAnimation,
            MonsterAudioController monsterAudio,
            MonsterJumpscareController monsterJumpscare,
            float gracePeriod = 5f)
        {
            navigation = monsterNavigation;
            perception = monsterPerception;
            animationController = monsterAnimation;
            audioController = monsterAudio;
            jumpscareController = monsterJumpscare;
            startupGracePeriod = gracePeriod;
        }

        protected virtual void Awake()
        {
            ResolvePlayer();
        }

        protected virtual void OnEnable()
        {
            stateEnteredAt = Time.time;
            State = MonsterState.Dormant;
        }

        protected virtual void Update()
        {
            if (playerRoot == null) ResolvePlayer();
            if (State == MonsterState.Dormant && Time.time - stateEnteredAt >= startupGracePeriod)
            {
                ChangeState(MonsterState.Roaming);
            }
            TickState();
        }

        public bool TryBeginKill(Collider playerCollider)
        {
            if (State == MonsterState.Killing || State == MonsterState.Resetting || playerRespawn == null || playerRespawn.IsSpawnProtected)
            {
                return false;
            }
            if (playerCollider.GetComponentInParent<PlayerRespawn>() != playerRespawn)
            {
                return false;
            }

            ChangeState(MonsterState.Killing);
            navigation.StopImmediately();
            MonsterKillTrigger[] triggers = GetComponentsInChildren<MonsterKillTrigger>(true);
            foreach (MonsterKillTrigger trigger in triggers) trigger.SetArmed(false);
            PlayerDeathController death = playerRoot.GetComponent<PlayerDeathController>();
            if (death != null) death.BeginDeath(this);
            return true;
        }

        public virtual void ResetAfterJumpscare()
        {
            ChangeState(MonsterState.Resetting);
            Transform safe = MonsterSpawnPoint.FindSafePoint(playerHead, 20f, perception != null ? perception.ObstructionMask : 0);
            if (safe != null) navigation.Warp(safe.position);
            foreach (MonsterKillTrigger trigger in GetComponentsInChildren<MonsterKillTrigger>(true)) trigger.SetArmed(true);
            ChangeState(MonsterState.Roaming);
        }

        public void SuspendForJumpscare()
        {
            navigation.StopImmediately();
            enabled = false;
        }

        public void ActivateImmediatelyForDevelopment()
        {
            if (State == MonsterState.Dormant) ChangeState(MonsterState.Roaming);
        }

        public void ResumeAfterJumpscare()
        {
            enabled = true;
            ResetAfterJumpscare();
        }

        protected abstract void TickState();

        protected virtual void OnStateChanged(MonsterState previous, MonsterState next) { }

        protected void ChangeState(MonsterState next)
        {
            if (State == next) return;
            MonsterState previous = State;
            State = next;
            stateEnteredAt = Time.time;
            OnStateChanged(previous, next);
        }

        protected float StateTime => Time.time - stateEnteredAt;

        private void ResolvePlayer()
        {
            playerRespawn = FindFirstObjectByType<PlayerRespawn>();
            if (playerRespawn == null) return;
            playerRoot = playerRespawn.transform;
            Camera camera = playerRoot.GetComponentInChildren<Camera>(true);
            playerHead = camera != null ? camera.transform : playerRoot;
            perception?.SetPlayer(playerRoot, playerHead);
        }
    }
}
