using UnityEngine;
using UnityEngine.AI;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class StatueBrain : MonsterBrain
    {
        [SerializeField, Range(20f, 60f)] private float awarenessRadius = 48f;
        [SerializeField, Range(8f, 30f)] private float directSightRange = 22f;
        [SerializeField, Range(10f, 45f)] private float directLookAngle = 25f;
        [SerializeField, Range(0.5f, 4f)] private float escapeConfirmation = 2f;
        [SerializeField] private float[] teleportIntervals = { 1.5f, 1.1f, 0.8f, 0.5f };
        [SerializeField] private float[] teleportDistances = { 18f, 13f, 9f, 6f, 3f };

        private int teleportStage;
        private float unwatchedTime;
        private float outsideAwarenessTime;
        private float nextAwarenessCheck;
        private bool watched;

        public float AwarenessRadius => awarenessRadius;
        public float DirectSightRange => directSightRange;
        public float DirectLookAngle => directLookAngle;
        public float[] TeleportIntervals => teleportIntervals;
        public float[] TeleportDistances => teleportDistances;
        public override float MinimumSpawnDistance => 35f;

        public void ConfigureStatue(float awareness, float directSight, float lookAngle, float escapeTime)
        {
            awarenessRadius = awareness;
            directSightRange = directSight;
            directLookAngle = lookAngle;
            escapeConfirmation = escapeTime;
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
                    navigation.TickRoaming();
                    if (Time.time >= nextAwarenessCheck)
                    {
                        nextAwarenessCheck = Time.time + 0.2f;
                        if (Vector3.Distance(transform.position, playerHead.position) <= awarenessRadius)
                        {
                            ChangeState(MonsterState.Alerted);
                        }
                    }
                    break;
                case MonsterState.Alerted:
                    navigation.StopImmediately();
                    if (Vector3.Distance(transform.position, playerHead.position) > awarenessRadius)
                    {
                        outsideAwarenessTime += Time.deltaTime;
                        if (outsideAwarenessTime >= escapeConfirmation) RelocateAndRoam();
                    }
                    else
                    {
                        outsideAwarenessTime = 0f;
                        if (perception.EvaluateSight(directSightRange, 100f)) ChangeState(MonsterState.Special);
                    }
                    break;
                case MonsterState.Special:
                    TickGazeTeleport();
                    break;
            }
        }

        private void TickGazeTeleport()
        {
            float distance = Vector3.Distance(transform.position, playerHead.position);
            if (distance > awarenessRadius)
            {
                outsideAwarenessTime += Time.deltaTime;
                if (outsideAwarenessTime >= escapeConfirmation) RelocateAndRoam();
                return;
            }
            outsideAwarenessTime = 0f;

            bool currentlyWatched = EvaluateWatched(watched ? directLookAngle + 5f : directLookAngle);
            if (currentlyWatched)
            {
                watched = true;
                unwatchedTime = 0f;
                navigation.StopImmediately();
                animationController.SetFrozen(true);
                return;
            }

            watched = false;
            animationController.SetFrozen(true);
            unwatchedTime += Time.deltaTime;
            float interval = teleportIntervals[Mathf.Min(teleportStage, teleportIntervals.Length - 1)];
            if (unwatchedTime >= interval && TryTeleportCloser())
            {
                unwatchedTime = 0f;
                teleportStage = Mathf.Min(teleportStage + 1, teleportDistances.Length - 1);
                audioController.PlayTeleport();
            }
        }

        private bool EvaluateWatched(float angle)
        {
            Vector3 target = transform.position + Vector3.up * 0.95f;
            Vector3 delta = target - playerHead.position;
            float distance = delta.magnitude;
            if (distance < 0.01f || Vector3.Angle(playerHead.forward, delta) > angle) return false;
            return !Physics.Raycast(playerHead.position, delta / distance, distance, perception.ObstructionMask, QueryTriggerInteraction.Ignore);
        }

        private bool TryTeleportCloser()
        {
            float desiredDistance = teleportDistances[Mathf.Min(teleportStage, teleportDistances.Length - 1)];
            Vector3 flatForward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up).normalized;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = Random.Range(75f, 285f);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * flatForward;
                Vector3 candidate = playerHead.position + direction * desiredDistance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                Vector3 toCandidate = hit.position - playerHead.position;
                if (Vector3.Angle(flatForward, Vector3.ProjectOnPlane(toCandidate, Vector3.up)) < 48f) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 0.34f, hit.position + Vector3.up * 1.65f, 0.26f, perception.ObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                if (!navigation.Warp(hit.position)) continue;
                transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(playerHead.position - transform.position, Vector3.up), Vector3.up);
                Physics.SyncTransforms();
                return true;
            }
            return false;
        }

        private void RelocateAndRoam()
        {
            ChangeState(MonsterState.Resetting);
            MonsterSpawnPoint safe = MonsterSpawnPoint.FindSafePoint(playerHead, MinimumSpawnDistance, perception.ObstructionMask);
            if (safe != null) navigation.Warp(safe.transform.position);
            ResetBehaviorMemory();
            audioController.PlayRelocation();
            ChangeState(MonsterState.Roaming);
        }

        protected override void ResetBehaviorMemory()
        {
            teleportStage = 0;
            unwatchedTime = 0f;
            outsideAwarenessTime = 0f;
            nextAwarenessCheck = 0f;
            watched = false;
            lastKnownPlayerPosition = Vector3.zero;
        }

        protected override void OnStateChanged(MonsterState previous, MonsterState next)
        {
            switch (next)
            {
                case MonsterState.Roaming:
                    animationController.SetFrozen(false);
                    animationController.SetLocomotionSpeed(0.85f);
                    audioController.PlayRoaming();
                    break;
                case MonsterState.Alerted:
                    navigation.StopImmediately();
                    animationController.SetFrozen(true);
                    audioController.PlaySpecial();
                    break;
                case MonsterState.Special:
                    navigation.StopImmediately();
                    animationController.SetFrozen(true);
                    audioController.PlayAggro();
                    teleportStage = 0;
                    unwatchedTime = 0f;
                    break;
                case MonsterState.Killing:
                    audioController.StopAll();
                    animationController.SetFrozen(true);
                    break;
            }
        }
    }
}
