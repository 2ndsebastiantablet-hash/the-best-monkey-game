using System;
using System.Text;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class PlayerProfileService : MonoBehaviour
    {
        private const string ProfileKey = "tbmg.player-profile.v1";
        public const int MaxDisplayNameLength = 16;

        private static readonly Color[] Palette =
        {
            new Color(0.18f, 0.72f, 1f),
            new Color(1f, 0.35f, 0.28f),
            new Color(0.35f, 0.9f, 0.42f),
            new Color(1f, 0.78f, 0.18f),
            new Color(0.7f, 0.38f, 1f),
            new Color(1f, 0.45f, 0.78f)
        };

        public event Action Changed;
        public LocalPlayerProfile Current { get; private set; }
        public int PaletteCount => Palette.Length;

        public void Initialize()
        {
            if (Current != null) return;
            Current = Load();
            ApplyAudio();
        }

        public Color GetColor(int index)
        {
            return Palette[Mathf.Clamp(index, 0, Palette.Length - 1)];
        }

        public void Save(LocalPlayerProfile profile)
        {
            if (profile == null) profile = new LocalPlayerProfile();
            profile.DisplayName = SanitizeDisplayName(profile.DisplayName);
            profile.ColorIndex = Mathf.Clamp(profile.ColorIndex, 0, Palette.Length - 1);
            profile.Turning = profile.Turning == TurningMode.Smooth ? TurningMode.Smooth : TurningMode.Snap;
            profile.SnapTurnAngle = Mathf.Clamp(Mathf.Round(profile.SnapTurnAngle / 5f) * 5f, 15f, 90f);
            profile.SmoothTurnSpeed = Mathf.Clamp(profile.SmoothTurnSpeed, 30f, 180f);
            profile.MasterVolume = Mathf.Clamp01(profile.MasterVolume);
            profile.SoundEffectVolume = Mathf.Clamp01(profile.SoundEffectVolume);
            Current = profile.Clone();
            PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
            ApplyAudio();
            Changed?.Invoke();
        }

        public static string SanitizeDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Monkey";
            var builder = new StringBuilder(MaxDisplayNameLength);
            foreach (char character in value.Trim())
            {
                if (builder.Length >= MaxDisplayNameLength) break;
                if (char.IsControl(character) || character == '<' || character == '>') continue;
                builder.Append(character);
            }
            string result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "Monkey" : result;
        }

        private static LocalPlayerProfile Load()
        {
            if (!PlayerPrefs.HasKey(ProfileKey)) return new LocalPlayerProfile();
            try
            {
                var profile = JsonUtility.FromJson<LocalPlayerProfile>(PlayerPrefs.GetString(ProfileKey));
                if (profile == null) return new LocalPlayerProfile();
                profile.DisplayName = SanitizeDisplayName(profile.DisplayName);
                profile.ColorIndex = Mathf.Clamp(profile.ColorIndex, 0, Palette.Length - 1);
                profile.Turning = profile.Turning == TurningMode.Smooth ? TurningMode.Smooth : TurningMode.Snap;
                profile.SnapTurnAngle = Mathf.Clamp(profile.SnapTurnAngle, 15f, 90f);
                profile.SmoothTurnSpeed = Mathf.Clamp(profile.SmoothTurnSpeed, 30f, 180f);
                profile.MasterVolume = Mathf.Clamp01(profile.MasterVolume);
                profile.SoundEffectVolume = Mathf.Clamp01(profile.SoundEffectVolume);
                return profile;
            }
            catch
            {
                return new LocalPlayerProfile();
            }
        }

        private void ApplyAudio()
        {
            if (Current != null) AudioListener.volume = Current.MasterVolume;
        }
    }
}
