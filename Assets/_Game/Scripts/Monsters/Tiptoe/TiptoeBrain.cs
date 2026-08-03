using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class TiptoeBrain : MonsterBrain
    {
        [SerializeField, Range(3f, 7f)] private float roamSpeed = 5f;
        [SerializeField, Range(8f, 15f)] private float chaseSpeed = 11.5f;
        [SerializeField, Range(0.5f, 4f)] private float lostSightGrace = 2f;
        [SerializeField, Range(3f, 12f)] private float searchDuration = 6.5f;
        [SerializeField, Range(10f, 30f)] private float escapeDistance = 17.5f;

        private float lastSightTime;
        private float nextSearchMove;

        public float RoamSpeed => roamSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float LostSightGrace => lostSightGrace;
        public float SearchDuration => searchDuration;
        public float EscapeDistance => escapeDistance;

        public void ConfigureTiptoe(float roam, float chase, float lostGrace, float search, float escape)
        {
            roamSpeed = roam;
            chaseSpeed = chase;
            lostSightGrace = lostGrace;
            searchDuration = search;
            escapeDistance = escape;
        }

        protected override void TickState()
        {
            if (playerHead == null) return;
            switch (State)
            {
                case MonsterState.Dormant:
                    navigation.StopImmediately();
                    break;
                case MonsterState.Roaming:
                    navigation.SetSpeed(roamSpeed);
                    navigation.TickRoaming();
                    if (perception.HasConfirmedSight)
                    {
                        lastKnownPlayerPosition = playerHead.position;
                        lastSightTime = Time.time;
                        ChangeState(MonsterState.Chasing);
                    }
                    break;
                case MonsterState.Chasing:
                    navigation.SetSpeed(chaseSpeed);
                    if (perception.HasDirectSight)
                    {
                        lastKnownPlayerPosition = playerHead.position;
                        lastSightTime = Time.time;
                    }
                    navigation.MoveTo(lastKnownPlayerPosition);
                    if (!perception.HasDirectSight && Time.time - lastSightTime >= lostSightGrace && navigation.ReachedDestination)
                    {
                        ChangeState(MonsterState.Searching);
                    }
                    break;
                case MonsterState.Searching:
                    navigation.SetSpeed(roamSpeed * 1.15f);
                    if (perception.HasDirectSight)
                    {
                        lastKnownPlayerPosition = playerHead.position;
                        lastSightTime = Time.time;
                        ChangeState(MonsterState.Chasing);
                        break;
                    }
                    if (Time.time >= nextSearchMove)
                    {
                        Vector2 random = Random.insideUnitCircle * 7f;
                        navigation.MoveTo(lastKnownPlayerPosition + new Vector3(random.x, 0f, random.y), true);
                        nextSearchMove = Time.time + 1.2f;
                    }
                    if (StateTime >= searchDuration && Vector3.Distance(transform.position, playerHead.position) >= escapeDistance)
                    {
                        ChangeState(MonsterState.Roaming);
                    }
                    break;
            }
        }

        protected override void OnStateChanged(MonsterState previous, MonsterState next)
        {
            switch (next)
            {
                case MonsterState.Roaming:
                    audioController.PlayRoaming();
                    animationController.SetFrozen(false);
                    animationController.SetLocomotionSpeed(1.15f);
                    break;
                case MonsterState.Chasing:
                    audioController.PlayAggro();
                    audioController.PlayChase();
                    animationController.SetFrozen(false);
                    animationController.SetLocomotionSpeed(2.5f);
                    break;
                case MonsterState.Searching:
                    audioController.PlaySearch();
                    animationController.SetLocomotionSpeed(1.35f);
                    nextSearchMove = 0f;
                    break;
                case MonsterState.Killing:
                    audioController.StopAll();
                    animationController.SetFrozen(true);
                    break;
            }
        }
    }
}
