using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame.Multiplayer
{
    [DefaultExecutionOrder(400)]
    public sealed class DevelopmentPoseSimulator : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        public void Configure(Transform trackedHead, Transform trackedLeftHand, Transform trackedRightHand)
        {
            head = trackedHead;
            leftHand = trackedLeftHand;
            rightHand = trackedRightHand;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid || head == null || leftHand == null || rightHand == null) return;
            float phase = Time.unscaledTime;
            head.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(phase * 1.3f) * 0.025f, 0f);
            head.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * 0.45f) * 18f, 0f);
            leftHand.localPosition = new Vector3(-0.32f, 0.55f + Mathf.Sin(phase * 1.8f) * 0.08f, 0.28f);
            rightHand.localPosition = new Vector3(0.32f, 0.55f + Mathf.Cos(phase * 1.8f) * 0.08f, 0.28f);
#else
            enabled = false;
#endif
        }
    }
}
