using UnityEngine;

namespace TheBestMonkeyGame
{
    /// <summary>Positions the collider-free temporary body below the tracked head.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class GorillaVisualRig : MonoBehaviour
    {
        [SerializeField] private Transform trackedHead;
        [SerializeField] private Transform bodyVisual;
        [SerializeField] private Vector3 bodyLocalOffset;
        [SerializeField] private float yawSharpness = 16f;

        public void Configure(Transform head, Transform body, Vector3 localOffset)
        {
            trackedHead = head;
            bodyVisual = body;
            bodyLocalOffset = localOffset;
        }

        private void LateUpdate()
        {
            if (trackedHead == null || bodyVisual == null)
            {
                return;
            }

            Vector3 planarForward = Vector3.ProjectOnPlane(trackedHead.forward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                planarForward = bodyVisual.forward;
            }

            Quaternion targetYaw = Quaternion.LookRotation(planarForward.normalized, Vector3.up);
            Vector3 groundPosition = new Vector3(trackedHead.position.x, transform.position.y, trackedHead.position.z);
            bodyVisual.position = groundPosition + targetYaw * bodyLocalOffset;
            float blend = 1f - Mathf.Exp(-yawSharpness * Time.deltaTime);
            bodyVisual.rotation = Quaternion.Slerp(bodyVisual.rotation, targetYaw, blend);
        }
    }
}
