using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame.Multiplayer
{
    [DefaultExecutionOrder(100)]
    public sealed class VRTurningController : MonoBehaviour
    {
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform headset;
        [SerializeField, Range(0.2f, 0.95f)] private float snapActivationThreshold = 0.7f;

        private InputDevice rightHand;
        private bool snapReady = true;

        public void Configure(Transform root, Transform trackedHead)
        {
            playerRoot = root;
            headset = trackedHead;
        }

        private void OnEnable()
        {
            rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            snapReady = true;
        }

        private void Update()
        {
            if (playerRoot == null || headset == null || GameBootstrap.Instance == null) return;
            if (!rightHand.isValid) rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return;

            LocalPlayerProfile profile = GameBootstrap.Instance.Profile.Current;
            if (profile.Turning == TurningMode.Smooth)
            {
                float degrees = stick.x * profile.SmoothTurnSpeed * Time.unscaledDeltaTime;
                if (Mathf.Abs(degrees) > 0.01f) RotateAroundHead(degrees);
                snapReady = Mathf.Abs(stick.x) < 0.25f;
                return;
            }

            if (Mathf.Abs(stick.x) < 0.25f)
            {
                snapReady = true;
                return;
            }
            if (!snapReady || Mathf.Abs(stick.x) < snapActivationThreshold) return;
            snapReady = false;
            RotateAroundHead(Mathf.Sign(stick.x) * profile.SnapTurnAngle);
        }

        private void RotateAroundHead(float degrees)
        {
            Vector3 pivot = headset.position;
            playerRoot.RotateAround(pivot, Vector3.up, degrees);
        }
    }
}
