using GorillaLocomotion;
using UnityEngine;

namespace TheBestMonkeyGame
{
    public sealed class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float fallThreshold = -6f;

        private Rigidbody body;
        private Player locomotion;
        private Vector3 fallbackSpawn;
        private Quaternion fallbackRotation;

        public Transform SpawnPoint
        {
            get => spawnPoint;
            set => spawnPoint = value;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            locomotion = GetComponent<Player>();
            fallbackSpawn = transform.position;
            fallbackRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (transform.position.y < fallThreshold)
            {
                Respawn();
            }
        }

        public void Respawn()
        {
            Vector3 position = spawnPoint != null ? spawnPoint.position : fallbackSpawn;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : fallbackRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            locomotion.InitializeValues();
        }
    }

    public sealed class FallResetVolume : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
            if (respawn != null)
            {
                respawn.Respawn();
            }
        }
    }
}
