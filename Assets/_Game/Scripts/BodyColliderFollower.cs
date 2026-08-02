using UnityEngine;

namespace TheBestMonkeyGame
{
    /// <summary>Keeps the rigidbody's body capsule underneath the tracked headset.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class BodyColliderFollower : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private CapsuleCollider bodyCollider;
        [SerializeField] private float floorClearance = 0.08f;
        [SerializeField] private float minimumHeight = 0.45f;
        [SerializeField] private float maximumHeight = 2.0f;

        public void Configure(Transform trackedHead, CapsuleCollider capsule)
        {
            head = trackedHead;
            bodyCollider = capsule;
        }

        private void LateUpdate()
        {
            if (head == null || bodyCollider == null)
            {
                return;
            }

            Vector3 localHead = transform.parent.InverseTransformPoint(head.position);
            float height = Mathf.Clamp(localHead.y - floorClearance, minimumHeight, maximumHeight);
            transform.localPosition = new Vector3(localHead.x, floorClearance, localHead.z);
            bodyCollider.height = height;
            bodyCollider.center = new Vector3(0f, height * 0.5f, 0f);
        }
    }
}
