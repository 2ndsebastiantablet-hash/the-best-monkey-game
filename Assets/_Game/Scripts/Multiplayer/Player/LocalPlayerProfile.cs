using System;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public enum TurningMode
    {
        Snap = 0,
        Smooth = 1
    }

    [Serializable]
    public sealed class LocalPlayerProfile
    {
        public string DisplayName = "Monkey";
        public int ColorIndex;
        public TurningMode Turning = TurningMode.Snap;
        public float SnapTurnAngle = 45f;
        public float SmoothTurnSpeed = 90f;
        public float MasterVolume = 1f;
        public float SoundEffectVolume = 1f;

        public LocalPlayerProfile Clone()
        {
            return (LocalPlayerProfile)MemberwiseClone();
        }
    }
}
