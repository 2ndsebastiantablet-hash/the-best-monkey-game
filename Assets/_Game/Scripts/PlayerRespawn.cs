using GorillaLocomotion;
using System.Collections;
using UnityEngine;

namespace TheBestMonkeyGame
{
    [DefaultExecutionOrder(200)]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float fallThreshold = -8f;

        private Rigidbody body;
        private Player locomotion;
        private Vector3 fallbackSpawn;
        private Quaternion fallbackRotation;
        private Coroutine resetRoutine;

        public Transform SpawnPoint
        {
            get => spawnPoint;
            set => spawnPoint = value;
        }

        public float FallThreshold
        {
            get => fallThreshold;
            set => fallThreshold = value;
        }

        public float SpawnProtectionRemaining { get; private set; }
        public bool IsSpawnProtected => SpawnProtectionRemaining > 0f;
        public bool IsResetting { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            locomotion = GetComponent<Player>();
            fallbackSpawn = transform.position;
            fallbackRotation = transform.rotation;
        }

        private void Start()
        {
            BeginPoseReinitialization();
        }

        private void FixedUpdate()
        {
            SpawnProtectionRemaining = Mathf.Max(0f, SpawnProtectionRemaining - Time.fixedDeltaTime);
            if (!IsResetting && transform.position.y < fallThreshold)
            {
                Respawn();
            }
        }

        public void Respawn(float spawnProtectionSeconds = 3f)
        {
            Vector3 position = spawnPoint != null ? spawnPoint.position : fallbackSpawn;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : fallbackRotation;
            locomotion.disableMovement = true;
            ClearBodyVelocity();
            transform.SetPositionAndRotation(position, rotation);
            SpawnProtectionRemaining = Mathf.Max(SpawnProtectionRemaining, spawnProtectionSeconds);
            Physics.SyncTransforms();
            BeginPoseReinitialization();
        }

        public void StabilizeAfterCalibration()
        {
            locomotion.disableMovement = true;
            ClearBodyVelocity();
            BeginPoseReinitialization();
        }

        private void BeginPoseReinitialization()
        {
            if (resetRoutine != null) StopCoroutine(resetRoutine);
            resetRoutine = StartCoroutine(ReinitializeAfterTrackedPose());
        }

        private IEnumerator ReinitializeAfterTrackedPose()
        {
            IsResetting = true;
            locomotion.disableMovement = true;
            ClearBodyVelocity();

            // Let OpenXR update the tracked camera/controllers at the new root pose.
            yield return null;
            yield return null;

            Physics.SyncTransforms();
            ClearBodyVelocity();
            locomotion.ResetLocomotionState(true);
            locomotion.disableMovement = false;
            IsResetting = false;
            resetRoutine = null;
        }

        private void ClearBodyVelocity()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    public sealed class FallResetVolume : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
            if (respawn != null) respawn.Respawn();
        }
    }
}
