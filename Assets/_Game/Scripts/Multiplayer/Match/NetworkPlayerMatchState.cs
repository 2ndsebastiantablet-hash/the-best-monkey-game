using System.Collections;
using GorillaLocomotion;
using TheBestMonkeyGame.Monsters;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class NetworkPlayerMatchState : NetworkBehaviour
    {
        [SerializeField] private GameObject localPlayerRoot;
        [SerializeField] private Player locomotion;
        [SerializeField] private PlayerRespawn respawn;
        [SerializeField] private Rigidbody body;
        [SerializeField] private PlayerDeathController deathPresentation;

        private readonly NetworkVariable<bool> alive = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> respawning = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> spawnIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> protectionEndTime = new(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<PlayerRelocationCommand> relocation = new(default, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        private uint relocationSequence;
        private Coroutine relocationRoutine;

        public bool IsAlive => alive.Value;
        public bool IsRespawning => respawning.Value;
        public int SpawnIndex => spawnIndex.Value;
        public double ProtectionEndTime => protectionEndTime.Value;
        public bool IsProtected => NetworkManager != null && NetworkManager.IsListening && NetworkManager.ServerTime.Time < protectionEndTime.Value;

        public void Configure(GameObject localRoot, Player player, PlayerRespawn playerRespawn, Rigidbody playerBody, PlayerDeathController death)
        {
            localPlayerRoot = localRoot;
            locomotion = player;
            respawn = playerRespawn;
            body = playerBody;
            deathPresentation = death;
        }

        public override void OnNetworkSpawn()
        {
            relocation.OnValueChanged += OnRelocationChanged;
            if (IsOwner && relocation.Value.Sequence != 0) BeginLocalRelocation(relocation.Value);
        }

        public override void OnNetworkDespawn()
        {
            relocation.OnValueChanged -= OnRelocationChanged;
            if (relocationRoutine != null) StopCoroutine(relocationRoutine);
        }

        public bool ServerBeginDeath()
        {
            if (!IsServer || !alive.Value || respawning.Value || IsProtected) return false;
            alive.Value = false;
            respawning.Value = true;
            return true;
        }

        public void ServerAssignSpawn(int index, Vector3 position, Quaternion rotation, float protectionSeconds, bool fade)
        {
            if (!IsServer) return;
            spawnIndex.Value = index;
            protectionEndTime.Value = NetworkManager.ServerTime.Time + Mathf.Max(0f, protectionSeconds);
            relocation.Value = new PlayerRelocationCommand
            {
                Sequence = ++relocationSequence,
                Position = position,
                Yaw = rotation.eulerAngles.y,
                ProtectionEndTime = protectionEndTime.Value,
                Fade = fade
            };
        }

        public void ServerCompleteRespawn()
        {
            if (!IsServer) return;
            respawning.Value = false;
            alive.Value = true;
        }

        public void ServerRestoreForLobby(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            alive.Value = true;
            respawning.Value = false;
            protectionEndTime.Value = 0d;
            ServerAssignSpawn(spawnIndex.Value, position, rotation, 0f, false);
        }

        private void OnRelocationChanged(PlayerRelocationCommand previous, PlayerRelocationCommand current)
        {
            if (IsOwner && current.Sequence != previous.Sequence) BeginLocalRelocation(current);
        }

        private void BeginLocalRelocation(PlayerRelocationCommand command)
        {
            if (relocationRoutine != null) StopCoroutine(relocationRoutine);
            relocationRoutine = StartCoroutine(RelocateLocalOwner(command));
        }

        private IEnumerator RelocateLocalOwner(PlayerRelocationCommand command)
        {
            if (localPlayerRoot == null) yield break;
            if (locomotion != null) locomotion.disableMovement = true;
            ClearVelocity();
            if (command.Fade && deathPresentation != null) yield return deathPresentation.FadeToBlack(0.12f);

            localPlayerRoot.transform.SetPositionAndRotation(command.Position, Quaternion.Euler(0f, command.Yaw, 0f));
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            ClearVelocity();
            if (locomotion != null)
            {
                locomotion.ResetLocomotionState(true);
                locomotion.disableMovement = false;
            }
            if (command.Fade && deathPresentation != null) yield return deathPresentation.FadeFromBlack(0.12f);
            relocationRoutine = null;
            Debug.Log($"NETWORK_PLAYER_RELOCATED owner={OwnerClientId} spawn={spawnIndex.Value} protectionEnd={command.ProtectionEndTime:F2}");
        }

        private void ClearVelocity()
        {
            if (body == null) return;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
