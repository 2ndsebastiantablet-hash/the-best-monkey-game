using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class NetworkPlayerIdentity : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString32Bytes> displayName = new(
            new FixedString32Bytes("Monkey"), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<byte> colorIndex = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public string DisplayName => displayName.Value.ToString();
        public int ColorIndex => colorIndex.Value;

        public override void OnNetworkSpawn()
        {
            displayName.OnValueChanged += OnNameChanged;
            colorIndex.OnValueChanged += OnColorChanged;
            if (IsOwner && GameBootstrap.Instance != null)
            {
                ApplyLocalProfile();
                GameBootstrap.Instance.Profile.Changed += ApplyLocalProfile;
            }
            NetworkVRPlayer owner = GetComponent<NetworkVRPlayer>();
            owner?.RefreshIdentityVisuals();
        }

        public override void OnNetworkDespawn()
        {
            displayName.OnValueChanged -= OnNameChanged;
            colorIndex.OnValueChanged -= OnColorChanged;
            if (IsOwner && GameBootstrap.Instance != null) GameBootstrap.Instance.Profile.Changed -= ApplyLocalProfile;
        }

        private void ApplyLocalProfile()
        {
            if (!IsOwner || GameBootstrap.Instance == null) return;
            LocalPlayerProfile profile = GameBootstrap.Instance.Profile.Current;
            displayName.Value = new FixedString32Bytes(PlayerProfileService.SanitizeDisplayName(profile.DisplayName));
            colorIndex.Value = (byte)Mathf.Clamp(profile.ColorIndex, 0, GameBootstrap.Instance.Profile.PaletteCount - 1);
            GetComponent<NetworkVRPlayer>()?.RefreshIdentityVisuals();
        }

        private void OnNameChanged(FixedString32Bytes previous, FixedString32Bytes current)
        {
            GetComponent<NetworkVRPlayer>()?.RefreshIdentityVisuals();
        }

        private void OnColorChanged(byte previous, byte current)
        {
            GetComponent<NetworkVRPlayer>()?.RefreshIdentityVisuals();
        }
    }
}
