using UnityEngine;
using UnityEngine.AI;

namespace TheBestMonkeyGame.Monsters
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class MonsterNavigation : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent agent;
        [SerializeField, Min(1f)] private float patrolRadius = 22f;
        [SerializeField, Range(0.1f, 2f)] private float repathInterval = 0.35f;
        [SerializeField, Range(0.5f, 4f)] private float stuckTimeout = 1.75f;
        [SerializeField, Range(0.01f, 0.3f)] private float progressThreshold = 0.08f;

        private NavMeshPath validationPath;
        private Vector3 lastPatrolDestination;
        private Vector3 lastProgressPosition;
        private float nextRepathTime;
        private float lastProgressTime;

        public NavMeshAgent Agent => agent;
        public bool ReachedDestination => agent != null && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f;

        public void Configure(float speed, float acceleration, float angularSpeed, float stoppingDistance)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            validationPath = new NavMeshPath();
            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.radius = 0.24f;
            agent.height = 1.55f;
            agent.baseOffset = 0f;
            agent.autoBraking = true;
            agent.autoRepath = true;
            agent.updateRotation = true;
            agent.updateUpAxis = true;
        }

        private void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
        }

        public void SetSpeed(float speed)
        {
            if (agent != null) agent.speed = speed;
        }

        public void TickRoaming()
        {
            if (!EnsureOnNavMesh()) return;
            TrackProgress();
            if (!agent.hasPath || ReachedDestination || IsStuck())
            {
                ChoosePatrolDestination();
            }
        }

        public bool MoveTo(Vector3 destination, bool force = false)
        {
            if (validationPath == null) validationPath = new NavMeshPath();
            if (!EnsureOnNavMesh() || (!force && Time.time < nextRepathTime)) return false;
            nextRepathTime = Time.time + repathInterval;
            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2.5f, agent.areaMask)) return false;
            if (!NavMesh.CalculatePath(transform.position, hit.position, agent.areaMask, validationPath) || validationPath.status != NavMeshPathStatus.PathComplete) return false;
            agent.isStopped = false;
            return agent.SetDestination(hit.position);
        }

        public void StopImmediately()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        public bool Warp(Vector3 position)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;
            bool result = agent.Warp(hit.position);
            if (result)
            {
                agent.isStopped = false;
                lastProgressPosition = hit.position;
                lastProgressTime = Time.time;
            }
            return result;
        }

        public bool TrySampleReachable(Vector3 candidate, float radius, out Vector3 position)
        {
            if (validationPath == null) validationPath = new NavMeshPath();
            position = default;
            if (!EnsureOnNavMesh() || !NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, agent.areaMask)) return false;
            if (!NavMesh.CalculatePath(transform.position, hit.position, agent.areaMask, validationPath) || validationPath.status != NavMeshPathStatus.PathComplete) return false;
            position = hit.position;
            return true;
        }

        private void ChoosePatrolDestination()
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 random = Random.insideUnitCircle * patrolRadius;
                Vector3 candidate = transform.position + new Vector3(random.x, 0f, random.y);
                if (!TrySampleReachable(candidate, 4f, out Vector3 sampled)) continue;
                if ((sampled - lastPatrolDestination).sqrMagnitude < 16f) continue;
                lastPatrolDestination = sampled;
                MoveTo(sampled, true);
                return;
            }
        }

        private bool EnsureOnNavMesh()
        {
            if (agent != null && agent.isOnNavMesh) return true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return agent.isOnNavMesh;
            }
            return false;
        }

        private void TrackProgress()
        {
            if ((transform.position - lastProgressPosition).sqrMagnitude >= progressThreshold * progressThreshold)
            {
                lastProgressPosition = transform.position;
                lastProgressTime = Time.time;
            }
        }

        private bool IsStuck()
        {
            return agent.hasPath && agent.velocity.sqrMagnitude < 0.01f && Time.time - lastProgressTime >= stuckTimeout;
        }
    }
}
