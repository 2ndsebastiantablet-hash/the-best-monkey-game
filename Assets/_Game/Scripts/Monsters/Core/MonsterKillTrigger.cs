using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    [RequireComponent(typeof(Collider))]
    public sealed class MonsterKillTrigger : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;
        private Collider triggerCollider;

        public void Configure(MonsterBrain owner)
        {
            brain = owner;
            triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();
            if (brain == null) brain = GetComponentInParent<MonsterBrain>();
        }

        public void SetArmed(bool armed)
        {
            if (triggerCollider != null) triggerCollider.enabled = armed;
        }

        private void OnTriggerEnter(Collider other)
        {
            brain?.TryBeginKill(other);
        }
    }
}
