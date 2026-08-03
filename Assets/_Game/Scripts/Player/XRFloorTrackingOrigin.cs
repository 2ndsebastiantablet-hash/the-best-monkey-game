using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame
{
    /// <summary>
    /// Requests a floor-level OpenXR tracking origin. Vertical calibration is owned
    /// by VRFloorHeightCalibration so tracked camera poses are never overwritten here.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class XRFloorTrackingOrigin : MonoBehaviour
    {
        [SerializeField] private Transform trackingSpace;
        private readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
        private float nextConfigurationAttempt;

        public void Configure(Transform space)
        {
            trackingSpace = space;
        }

        private void OnEnable()
        {
            TryConfigureFloorOrigin();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextConfigurationAttempt)
            {
                TryConfigureFloorOrigin();
                nextConfigurationAttempt = Time.unscaledTime + 1f;
            }
        }

        private void TryConfigureFloorOrigin()
        {
            inputSubsystems.Clear();
            SubsystemManager.GetSubsystems(inputSubsystems);
            foreach (XRInputSubsystem subsystem in inputSubsystems)
            {
                if (subsystem == null || !subsystem.running)
                {
                    continue;
                }

                TrackingOriginModeFlags supported = subsystem.GetSupportedTrackingOriginModes();
                if ((supported & TrackingOriginModeFlags.Floor) != 0 &&
                    subsystem.GetTrackingOriginMode() != TrackingOriginModeFlags.Floor)
                {
                    subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                }
            }
        }
    }
}
