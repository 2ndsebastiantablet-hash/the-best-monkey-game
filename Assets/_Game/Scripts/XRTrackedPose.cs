using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame
{
    /// <summary>
    /// Applies an OpenXR device pose directly to a rig transform. No locomotion
    /// actions are read here: controller position and rotation are the only inputs.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class XRTrackedPose : MonoBehaviour
    {
        [SerializeField] private XRNode node = XRNode.Head;

        private InputDevice device;

        public XRNode Node
        {
            get => node;
            set => node = value;
        }

        private void OnEnable()
        {
            device = InputDevices.GetDeviceAtXRNode(node);
        }

        private void Update()
        {
            if (!device.isValid)
            {
                device = InputDevices.GetDeviceAtXRNode(node);
            }

#if UNITY_EDITOR
            // Headless/editor Play Mode has no OpenXR pose. Keep its simulation near
            // the floor without serializing a fake eye height onto the camera.
            if (!device.isValid)
            {
                transform.localPosition = node switch
                {
                    XRNode.Head => new Vector3(0f, 2.35f, 0f),
                    XRNode.LeftHand => new Vector3(-0.22f, 1.55f, 0.18f),
                    XRNode.RightHand => new Vector3(0.22f, 1.55f, 0.18f),
                    _ => transform.localPosition
                };
                transform.localRotation = Quaternion.identity;
                return;
            }
#endif

            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                transform.localPosition = position;
            }

            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.localRotation = rotation;
            }
        }
    }
}
