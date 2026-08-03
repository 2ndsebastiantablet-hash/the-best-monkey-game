using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class MonsterPerception : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField, Range(1f, 60f)] private float sightDistance = 30f;
        [SerializeField, Range(10f, 180f)] private float fieldOfView = 120f;
        [SerializeField, Range(0.05f, 0.5f)] private float detectionConfirmation = 0.15f;
        [SerializeField, Range(0.03f, 0.5f)] private float checkInterval = 0.12f;

        private Transform playerRoot;
        private Transform playerHead;
        private float visibleDuration;
        private float nextCheck;
        private bool directSight;

        public float SightDistance => sightDistance;
        public float FieldOfView => fieldOfView;
        public LayerMask ObstructionMask => obstructionMask;
        public bool HasDirectSight => directSight;
        public bool HasConfirmedSight => directSight && visibleDuration >= detectionConfirmation;

        public void Configure(Transform eyeTransform, LayerMask obstacles, float distance, float fov, float confirmation)
        {
            eye = eyeTransform;
            obstructionMask = obstacles;
            sightDistance = distance;
            fieldOfView = fov;
            detectionConfirmation = confirmation;
        }

        public void SetPlayer(Transform root, Transform head)
        {
            playerRoot = root;
            playerHead = head;
        }

        private void Update()
        {
            if (Time.time < nextCheck || playerHead == null) return;
            float elapsed = checkInterval;
            nextCheck = Time.time + checkInterval + Random.Range(0f, 0.025f);
            directSight = EvaluateSight(sightDistance, fieldOfView);
            visibleDuration = directSight ? visibleDuration + elapsed : 0f;
        }

        public bool EvaluateSight(float distance, float fov)
        {
            if (playerHead == null || eye == null) return false;
            Vector3 toTarget = playerHead.position - eye.position;
            float targetDistance = toTarget.magnitude;
            if (targetDistance > distance || targetDistance < 0.001f) return false;
            if (Vector3.Angle(eye.forward, toTarget) > fov * 0.5f) return false;
            return !Physics.Raycast(eye.position, toTarget / targetDistance, targetDistance, obstructionMask, QueryTriggerInteraction.Ignore);
        }
    }
}
