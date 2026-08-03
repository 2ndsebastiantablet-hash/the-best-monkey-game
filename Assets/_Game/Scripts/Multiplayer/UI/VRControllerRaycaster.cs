using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame.Multiplayer
{
    [DefaultExecutionOrder(200)]
    public sealed class VRControllerRaycaster : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 12f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private LineRenderer line;
        [SerializeField] private XRNode controllerNode = XRNode.RightHand;

        private InputDevice device;
        private VRRayTarget hovered;
        private bool triggerWasPressed;

        public void Configure(LineRenderer lineRenderer, XRNode node = XRNode.RightHand, float distance = 12f)
        {
            line = lineRenderer;
            controllerNode = node;
            maxDistance = distance;
        }

        private void OnEnable()
        {
            device = InputDevices.GetDeviceAtXRNode(controllerNode);
            triggerWasPressed = false;
            if (line != null) line.enabled = true;
        }

        private void OnDisable()
        {
            hovered?.SetHovered(false);
            hovered = null;
            if (line != null) line.enabled = false;
        }

        private void Update()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            bool hitUi = Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactionMask, QueryTriggerInteraction.Collide);
            VRRayTarget next = hitUi ? hit.collider.GetComponentInParent<VRRayTarget>() : null;
            if (next != hovered)
            {
                hovered?.SetHovered(false);
                hovered = next;
                hovered?.SetHovered(true);
            }

            float distance = hitUi ? hit.distance : maxDistance;
            if (line != null)
            {
                line.positionCount = 2;
                line.SetPosition(0, transform.position);
                line.SetPosition(1, transform.position + transform.forward * distance);
            }

            if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(controllerNode);
            bool pressed = device.TryGetFeatureValue(CommonUsages.triggerButton, out bool value) && value;
            if (pressed && !triggerWasPressed && hovered != null) hovered.Trigger(hitUi ? hit.point : transform.position + transform.forward * maxDistance);
            triggerWasPressed = pressed;
        }
    }
}
