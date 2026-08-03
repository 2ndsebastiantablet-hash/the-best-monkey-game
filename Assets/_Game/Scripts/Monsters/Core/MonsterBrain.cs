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
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0f)] private float startupGracePeriod = 7f;

        protected Transform playerRoot;
        protected Transform playerHead;
        protected PlayerRespawn playerRespawn;
        protected float stateEnteredAt;
        protected Vector3 lastKnownPlayerPosition;

        private MonsterSpawnPoint lastSpawnPoint;

        public MonsterState State { get; private set; } = MonsterState.Dormant;
        public string MonsterId => gameObject.name;
        public Transform VisualRoot => visualRoot;
        public MonsterNavigation Navigation => navigation;
        public MonsterPerception Perception => perception;
        public float StartupGracePeriod => startupGracePeriod;
        public virtual float MinimumSpawnDistance => 30f;

        public virtual void ConfigureShared(
            MonsterNavigation monsterNavigation,
            MonsterPerception monsterPerception,
            MonsterAnimationController monsterAnimation,
            MonsterAudioController monsterAudio,
            Transform monsterVisual,
            float gracePeriod = 7f)
        {
            navigation = monsterNavigation;
            perception = monsterPerception;
            animationController = monsterAnimation;
            audioController = monsterAudio;
            visualRoot = monsterVisual;
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

        public void PlaceAtStartup(MonsterSpawnPoint point)
        {
            if (point == null) return;
            lastSpawnPoint = point;
            navigation.Warp(point.transform.position);
            navigation.StopImmediately();
            ResetBehaviorMemory();
            SetKillTriggersArmed(true);
            State = MonsterState.Dormant;
            stateEnteredAt = Time.time;
        }

        public bool TryBeginKill(Collider playerCollider)
        {
            if (State == MonsterState.Dormant || State == MonsterState.Killing || State == MonsterState.Resetting ||
                playerRespawn == null || playerRespawn.IsSpawnProtected)
            {
                return false;
            }
            if (playerCollider.GetComponentInParent<PlayerRespawn>() != playerRespawn) return false;

            PlayerDeathController death = playerRoot.GetComponent<PlayerDeathController>();
            if (death == null || death.DeathActive) return false;

            ChangeState(MonsterState.Killing);
            navigation.StopImmediately();
            SetKillTriggersArmed(false);
            death.BeginDeath(this);
            return true;
        }

        public virtual void ResetAfterPlayerDeath()
        {
            ChangeState(MonsterState.Resetting);
            navigation.StopImmediately();
            MonsterSpawnPoint safe = MonsterSpawnPoint.FindSafePoint(
                playerHead,
                MinimumSpawnDistance,
                perception != null ? perception.ObstructionMask : 0,
                lastSpawnPoint,
                transform);
            if (safe != null)
            {
                navigation.Warp(safe.transform.position);
                lastSpawnPoint = safe;
            }

            ResetBehaviorMemory();
            SetKillTriggersArmed(true);
            ChangeState(MonsterState.Roaming);
        }

        public void ActivateImmediatelyForDevelopment()
        {
            if (State == MonsterState.Dormant) ChangeState(MonsterState.Roaming);
        }

        protected abstract void TickState();
        protected virtual void ResetBehaviorMemory() { }
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

        private void SetKillTriggersArmed(bool armed)
        {
            foreach (MonsterKillTrigger trigger in GetComponentsInChildren<MonsterKillTrigger>(true))
            {
                trigger.SetArmed(armed);
            }
        }

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
