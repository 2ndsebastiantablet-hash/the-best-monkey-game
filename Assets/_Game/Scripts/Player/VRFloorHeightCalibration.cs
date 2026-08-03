using GorillaLocomotion;
using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame
{
    /// <summary>
    /// Applies a configurable Y correction to the XR tracking-space parent. The
    /// tracked camera and controllers remain fully driven by OpenXR beneath it.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public sealed class VRFloorHeightCalibration : MonoBehaviour
    {
        public const float DefaultVerticalOffset = -0.75f;

        [SerializeField] private Transform trackingSpace;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private PlayerRespawn respawn;
        [SerializeField, Range(-2f, 1f)] private float verticalOffset = DefaultVerticalOffset;
        [SerializeField, Range(1f, 4f)] private float calibrationHoldSeconds = 2f;
        [SerializeField, Range(0.03f, 0.3f)] private float desiredHandClearance = 0.12f;
        [SerializeField, Range(2f, 10f)] private float recalibrationCooldown = 4f;

        private InputDevice leftDevice;
        private InputDevice rightDevice;
        private float heldTime;
        private float cooldownUntil;

        public float VerticalOffset => verticalOffset;
        public Transform TrackingSpace => trackingSpace;

        public void Configure(
            Transform space,
            Transform left,
            Transform right,
            PlayerRespawn playerRespawn,
            float offset = DefaultVerticalOffset)
        {
            trackingSpace = space;
            leftHand = left;
            rightHand = right;
            respawn = playerRespawn;
            verticalOffset = Mathf.Clamp(offset, -2f, 1f);
            ApplyOffset();
        }

        private void OnEnable()
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            ApplyOffset();
        }

        private void Update()
        {
            ApplyOffset();
            if (Time.unscaledTime < cooldownUntil)
            {
                heldTime = 0f;
                return;
            }

            if (!leftDevice.isValid) leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!rightDevice.isValid) rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool leftPressed = leftDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool l) && l;
            bool rightPressed = rightDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool r) && r;

            if (!leftPressed || !rightPressed)
            {
                heldTime = 0f;
                return;
            }

            heldTime += Time.unscaledDeltaTime;
            if (heldTime >= calibrationHoldSeconds)
            {
                RecalibrateFromCurrentHands();
                heldTime = 0f;
                cooldownUntil = Time.unscaledTime + recalibrationCooldown;
            }
        }

        public void SetVerticalOffset(float offset)
        {
            verticalOffset = Mathf.Clamp(offset, -2f, 1f);
            ApplyOffset();
            respawn?.StabilizeAfterCalibration();
        }

        public void RecalibrateFromCurrentHands()
        {
            if (trackingSpace == null || leftHand == null || rightHand == null)
            {
                Debug.LogWarning("VR floor recalibration skipped because tracked hand references are unavailable.");
                return;
            }

            float virtualFloorY = transform.position.y;
            float lowestHandY = Mathf.Min(leftHand.position.y, rightHand.position.y);
            float correction = virtualFloorY + desiredHandClearance - lowestHandY;
            verticalOffset = Mathf.Clamp(verticalOffset + correction, -2f, 1f);
            ApplyOffset();
            respawn?.StabilizeAfterCalibration();
            Debug.Log($"VR_FLOOR_RECALIBRATED offset={verticalOffset:F3} handClearance={desiredHandClearance:F2}");
        }

        private void ApplyOffset()
        {
            if (trackingSpace != null)
            {
                trackingSpace.localPosition = new Vector3(0f, verticalOffset, 0f);
            }
        }
    }
}
