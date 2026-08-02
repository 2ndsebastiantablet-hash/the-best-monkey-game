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
