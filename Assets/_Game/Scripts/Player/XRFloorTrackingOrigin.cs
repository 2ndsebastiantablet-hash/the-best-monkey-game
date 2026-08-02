using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace TheBestMonkeyGame
{
    /// <summary>
    /// Requests a floor-level OpenXR tracking origin so a real room floor maps to
    /// the player root's Y plane. The offset is only for centimeter-scale calibration.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class XRFloorTrackingOrigin : MonoBehaviour
    {
        [SerializeField] private Transform trackingSpace;
        [SerializeField, Range(-0.1f, 0.1f)] private float playerFloorOffset;

        private readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
        private float nextConfigurationAttempt;

        public float PlayerFloorOffset
        {
            get => playerFloorOffset;
            set => playerFloorOffset = Mathf.Clamp(value, -0.1f, 0.1f);
        }

        public void Configure(Transform space, float floorOffset = 0f)
        {
            trackingSpace = space;
            PlayerFloorOffset = floorOffset;
            ApplyOffset();
        }

        private void OnEnable()
        {
            ApplyOffset();
            TryConfigureFloorOrigin();
        }

        private void Update()
        {
            ApplyOffset();
            if (Time.unscaledTime >= nextConfigurationAttempt)
            {
                TryConfigureFloorOrigin();
                nextConfigurationAttempt = Time.unscaledTime + 1f;
            }
        }

        private void ApplyOffset()
        {
            if (trackingSpace != null)
            {
                trackingSpace.localPosition = Vector3.up * playerFloorOffset;
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
