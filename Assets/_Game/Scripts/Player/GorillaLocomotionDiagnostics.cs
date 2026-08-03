using GorillaLocomotion;
using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame
{
    /// <summary>Opt-in inspector diagnostics. It produces no log traffic.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class GorillaLocomotionDiagnostics : MonoBehaviour
    {
        [SerializeField] private bool diagnosticsEnabled;
        [SerializeField] private Player locomotion;
        [SerializeField] private XRFloorTrackingOrigin floorOrigin;
        [SerializeField] private bool leftHandTouching;
        [SerializeField] private bool rightHandTouching;
        [SerializeField] private Vector3 rigidbodyVelocity;
        [SerializeField] private Vector3 calculatedAverageVelocity;
        [SerializeField] private bool movementDisabled;
        [SerializeField] private TrackingOriginModeFlags currentTrackingOriginMode;
        [SerializeField] private float playerRootHeightAboveFloor;

        public bool DiagnosticsEnabled
        {
            get => diagnosticsEnabled;
            set => diagnosticsEnabled = value;
        }

        public void Configure(Player player, XRFloorTrackingOrigin origin)
        {
            locomotion = player;
            floorOrigin = origin;
        }

        private void LateUpdate()
        {
            if (!diagnosticsEnabled || locomotion == null) return;

            leftHandTouching = locomotion.wasLeftHandTouching;
            rightHandTouching = locomotion.wasRightHandTouching;
            rigidbodyVelocity = locomotion.PlayerRigidBody != null
                ? locomotion.PlayerRigidBody.linearVelocity
                : Vector3.zero;
            calculatedAverageVelocity = locomotion.CalculatedVelocityAverage;
            movementDisabled = locomotion.disableMovement;
            currentTrackingOriginMode = floorOrigin != null
                ? floorOrigin.CurrentMode
                : TrackingOriginModeFlags.Unknown;

            int floorMask = locomotion.locomotionEnabledLayers.value;
            float rayLength = 20f;
            playerRootHeightAboveFloor = Physics.Raycast(
                transform.position + Vector3.up * 0.05f,
                Vector3.down,
                out RaycastHit hit,
                rayLength,
                floorMask,
                QueryTriggerInteraction.Ignore)
                ? transform.position.y - hit.point.y
                : float.PositiveInfinity;
        }
    }
}
