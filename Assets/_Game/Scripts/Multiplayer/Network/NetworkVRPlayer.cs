using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class NetworkVRPlayer : NetworkBehaviour
    {
        [Header("Ownership split")]
        [SerializeField] private GameObject localPlayerRoot;
        [SerializeField] private GameObject remoteVisualRoot;
        [SerializeField] private Transform localHead;
        [SerializeField] private Transform localLeftHand;
        [SerializeField] private Transform localRightHand;
        [SerializeField] private Transform remoteHead;
        [SerializeField] private Transform remoteLeftHand;
        [SerializeField] private Transform remoteRightHand;
        [SerializeField] private Renderer[] remoteRenderers;
        [SerializeField] private NetworkPlayerIdentity identity;
        [SerializeField, Range(5f, 30f)] private float interpolationSpeed = 18f;

        private readonly NetworkVariable<VRPoseState> pose = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private float nextSendTime;
        private MaterialPropertyBlock propertyBlock;

        public NetworkPlayerIdentity Identity => identity;
        public bool IsRoomHost => OwnerClientId == NetworkManager.ServerClientId;

        public void Configure(GameObject localRoot, GameObject remoteRoot, Transform head, Transform left, Transform right, Transform remoteHeadTransform, Transform remoteLeftTransform, Transform remoteRightTransform, Renderer[] colorRenderers, NetworkPlayerIdentity playerIdentity)
        {
            localPlayerRoot = localRoot;
            remoteVisualRoot = remoteRoot;
            localHead = head;
            localLeftHand = left;
            localRightHand = right;
            remoteHead = remoteHeadTransform;
            remoteLeftHand = remoteLeftTransform;
            remoteRightHand = remoteRightTransform;
            remoteRenderers = colorRenderers;
            identity = playerIdentity;
        }

        public override void OnNetworkSpawn()
        {
            localPlayerRoot.SetActive(IsOwner);
            remoteVisualRoot.SetActive(!IsOwner);
            if (IsOwner)
            {
                PlaceAtLobbySpawn();
                nextSendTime = Time.unscaledTime;
            }
            RefreshIdentityVisuals();
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                if (Time.unscaledTime >= nextSendTime)
                {
                    nextSendTime = Time.unscaledTime + 1f / MultiplayerConstants.PoseSendRate;
                    pose.Value = CapturePose();
                }
                return;
            }
            ApplyRemotePose(pose.Value);
        }

        public void RefreshIdentityVisuals()
        {
            if (identity == null || remoteRenderers == null || GameBootstrap.Instance == null) return;
            Color color = GameBootstrap.Instance.Profile.GetColor(identity.ColorIndex);
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_BaseColor", color);
            foreach (Renderer target in remoteRenderers) if (target != null) target.SetPropertyBlock(propertyBlock);
        }

        private VRPoseState CapturePose()
        {
            Transform root = localPlayerRoot.transform;
            return new VRPoseState
            {
                RootPosition = root.position,
                RootYaw = root.eulerAngles.y,
                HeadPosition = root.InverseTransformPoint(localHead.position),
                HeadRotation = Quaternion.Inverse(root.rotation) * localHead.rotation,
                LeftHandPosition = root.InverseTransformPoint(localLeftHand.position),
                LeftHandRotation = Quaternion.Inverse(root.rotation) * localLeftHand.rotation,
                RightHandPosition = root.InverseTransformPoint(localRightHand.position),
                RightHandRotation = Quaternion.Inverse(root.rotation) * localRightHand.rotation
            };
        }

        private void ApplyRemotePose(VRPoseState state)
        {
            float blend = 1f - Mathf.Exp(-interpolationSpeed * Time.unscaledDeltaTime);
            Transform root = remoteVisualRoot.transform;
            root.position = Vector3.Lerp(root.position, state.RootPosition, blend);
            root.rotation = Quaternion.Slerp(root.rotation, Quaternion.Euler(0f, state.RootYaw, 0f), blend);
            LerpLocal(remoteHead, state.HeadPosition, state.HeadRotation, blend);
            LerpLocal(remoteLeftHand, state.LeftHandPosition, state.LeftHandRotation, blend);
            LerpLocal(remoteRightHand, state.RightHandPosition, state.RightHandRotation, blend);
        }

        private static void LerpLocal(Transform target, Vector3 position, Quaternion rotation, float blend)
        {
            if (target == null) return;
            target.localPosition = Vector3.Lerp(target.localPosition, position, blend);
            target.localRotation = Quaternion.Slerp(target.localRotation, rotation, blend);
        }

        private void PlaceAtLobbySpawn()
        {
            int index = (int)(OwnerClientId % MultiplayerConstants.MaxPlayers);
            Vector3[] points =
            {
                new Vector3(-1.8f, 0.05f, -1.2f),
                new Vector3(1.8f, 0.05f, -1.2f),
                new Vector3(-1.8f, 0.05f, -3.2f),
                new Vector3(1.8f, 0.05f, -3.2f)
            };
            localPlayerRoot.transform.SetPositionAndRotation(points[index], Quaternion.identity);
        }
    }
}
