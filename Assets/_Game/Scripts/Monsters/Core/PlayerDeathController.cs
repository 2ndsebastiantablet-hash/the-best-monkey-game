using System.Collections;
using GorillaLocomotion;
using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    [RequireComponent(typeof(Rigidbody), typeof(PlayerRespawn))]
    public sealed class PlayerDeathController : MonoBehaviour
    {
        [SerializeField] private Player locomotion;
        [SerializeField] private PlayerRespawn respawn;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Transform trackedHead;
        [SerializeField] private XRTrackedPose[] trackedPoses;
        [SerializeField] private Renderer[] handRenderers;
        [SerializeField] private Renderer fadeOverlay;
        [SerializeField] private JumpscareRoomController jumpscareRoom;

        private bool deathActive;
        private bool previousKinematic;
        private bool previousMovementDisabled;
        private Vector3 savedHeadLocalPosition;
        private Quaternion savedHeadLocalRotation;
        private MaterialPropertyBlock fadeProperties;

        public bool DeathActive => deathActive;

        public void Configure(
            Player player,
            PlayerRespawn playerRespawn,
            Rigidbody playerBody,
            Transform head,
            XRTrackedPose[] poses,
            Renderer[] hands,
            Renderer overlay)
        {
            locomotion = player;
            respawn = playerRespawn;
            body = playerBody;
            trackedHead = head;
            trackedPoses = poses;
            handRenderers = hands;
            fadeOverlay = overlay;
            fadeProperties = new MaterialPropertyBlock();
            SetFade(0f);
        }

        private void Start()
        {
            if (jumpscareRoom == null) jumpscareRoom = FindFirstObjectByType<JumpscareRoomController>();
        }

        public void BeginDeath(MonsterBrain killer)
        {
            if (deathActive || jumpscareRoom == null || killer == null) return;
            deathActive = true;
            killer.SuspendForJumpscare();
            StartCoroutine(jumpscareRoom.Run(killer, this));
        }

        public IEnumerator FadeToBlack(float duration)
        {
            fadeOverlay.enabled = true;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            SetFade(1f);
        }

        public IEnumerator FadeFromBlack(float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                SetFade(1f - Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            SetFade(0f);
            fadeOverlay.enabled = false;
        }

        public void MoveAndLockAt(Transform playerAnchor, Transform monsterAnchor)
        {
            previousMovementDisabled = locomotion.disableMovement;
            previousKinematic = body.isKinematic;
            locomotion.disableMovement = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;

            savedHeadLocalPosition = trackedHead.localPosition;
            savedHeadLocalRotation = trackedHead.localRotation;
            foreach (XRTrackedPose pose in trackedPoses) pose.enabled = false;

            Vector3 desiredForward = Vector3.ProjectOnPlane(monsterAnchor.position - playerAnchor.position, Vector3.up).normalized;
            transform.SetPositionAndRotation(playerAnchor.position, Quaternion.LookRotation(desiredForward, Vector3.up));
            trackedHead.localRotation = Quaternion.identity;
            foreach (Renderer hand in handRenderers) hand.enabled = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        public void RestoreAfterJumpscare()
        {
            trackedHead.localPosition = savedHeadLocalPosition;
            trackedHead.localRotation = savedHeadLocalRotation;
            foreach (XRTrackedPose pose in trackedPoses) pose.enabled = true;
            foreach (Renderer hand in handRenderers) hand.enabled = true;
            body.isKinematic = previousKinematic;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            locomotion.disableMovement = previousMovementDisabled;
            respawn.Respawn(3f);
            deathActive = false;
        }

        private void SetFade(float alpha)
        {
            if (fadeOverlay == null) return;
            if (fadeProperties == null) fadeProperties = new MaterialPropertyBlock();
            fadeOverlay.GetPropertyBlock(fadeProperties);
            Color color = Color.black;
            color.a = alpha;
            fadeProperties.SetColor("_Color", color);
            fadeProperties.SetColor("_BaseColor", color);
            fadeOverlay.SetPropertyBlock(fadeProperties);
        }
    }
}
