using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame
{
    /// <summary>
    /// Applies one explicit floor correction to the pose-space parent beneath the
    /// floor-aligned XR Origin. Camera and controller local poses remain OpenXR-owned.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public sealed class VRFloorHeightCalibration : MonoBehaviour
    {
        public const float DefaultPlayerFloorOffset = -1.45f;

        [SerializeField] private Transform poseSpace;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private PlayerRespawn respawn;
        [SerializeField, Range(-2f, 1f)] private float playerFloorOffset = DefaultPlayerFloorOffset;
        [SerializeField, Range(1f, 4f)] private float calibrationHoldSeconds = 2f;
        [SerializeField, Range(0f, 0.15f)] private float desiredHandClearance = 0.02f;
        [SerializeField, Range(2f, 10f)] private float recalibrationCooldown = 4f;

        private InputDevice leftDevice;
        private InputDevice rightDevice;
        private float heldTime;
        private float cooldownUntil;

        public float PlayerFloorOffset => playerFloorOffset;
        public Transform PoseSpace => poseSpace;

        public void Configure(
            Transform space,
            Transform left,
            Transform right,
            PlayerRespawn playerRespawn,
            float offset = DefaultPlayerFloorOffset)
        {
            poseSpace = space;
            leftHand = left;
            rightHand = right;
            respawn = playerRespawn;
            playerFloorOffset = Mathf.Clamp(offset, -2f, 1f);
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
            if (heldTime < calibrationHoldSeconds) return;

            RecalibrateFromCurrentHands();
            heldTime = 0f;
            cooldownUntil = Time.unscaledTime + recalibrationCooldown;
        }

        public void SetPlayerFloorOffset(float offset)
        {
            playerFloorOffset = Mathf.Clamp(offset, -2f, 1f);
            ApplyOffset();
            respawn?.StabilizeAfterCalibration();
        }

        public void RecalibrateFromCurrentHands()
        {
            if (poseSpace == null || leftHand == null || rightHand == null) return;

            float floorY = transform.position.y;
            float lowestHandY = Mathf.Min(leftHand.position.y, rightHand.position.y);
            float correction = floorY + desiredHandClearance - lowestHandY;
            playerFloorOffset = Mathf.Clamp(playerFloorOffset + correction, -2f, 1f);
            ApplyOffset();
            respawn?.StabilizeAfterCalibration();
        }

        private void ApplyOffset()
        {
            if (poseSpace == null) return;
            poseSpace.localPosition = new Vector3(0f, playerFloorOffset, 0f);
            poseSpace.localRotation = Quaternion.identity;
            poseSpace.localScale = Vector3.one;
        }
    }
}
