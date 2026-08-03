using GorillaLocomotion;
using UnityEngine;

namespace TheBestMonkeyGame
{
    [ExecuteAlways]
    public sealed class PlayerFloorDebugGizmo : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform poseSpace;
        [SerializeField] private CapsuleCollider bodyCollider;
        [SerializeField] private Player locomotion;

        public void Configure(
            Transform spawn,
            Transform origin,
            Transform calibratedPoseSpace,
            CapsuleCollider body,
            Player player)
        {
            spawnPoint = spawn;
            xrOrigin = origin;
            poseSpace = calibratedPoseSpace;
            bodyCollider = body;
            locomotion = player;
        }

        private void OnDrawGizmos()
        {
            Vector3 floorPoint = spawnPoint != null ? spawnPoint.position : transform.position;
            Vector3 originPoint = xrOrigin != null ? xrOrigin.position : transform.position;
            Vector3 expectedTrackedFloor = poseSpace != null ? poseSpace.position : originPoint;

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(floorPoint, 0.06f);
            Gizmos.DrawLine(floorPoint, floorPoint + Vector3.up * 0.35f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(originPoint, 0.09f);
            Gizmos.DrawLine(floorPoint, originPoint);

            Gizmos.color = new Color(1f, 0.55f, 0f);
            Gizmos.DrawWireSphere(expectedTrackedFloor, 0.075f);
            Gizmos.DrawLine(originPoint, expectedTrackedFloor);

            if (bodyCollider != null)
            {
                Vector3 bodyBottom = new Vector3(
                    bodyCollider.bounds.center.x,
                    bodyCollider.bounds.min.y,
                    bodyCollider.bounds.center.z);
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(bodyBottom, 0.045f);
            }

            if (locomotion != null && locomotion.headCollider != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.55f);
                Gizmos.DrawWireSphere(locomotion.headCollider.transform.position, locomotion.maxArmLength);
                Gizmos.DrawLine(locomotion.headCollider.transform.position, floorPoint);
            }
        }
    }
}
