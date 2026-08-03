using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class MonsterSpawnPoint : MonoBehaviour
    {
        private static readonly List<MonsterSpawnPoint> Points = new List<MonsterSpawnPoint>();

        [SerializeField] private string region = "Distant Map Region";

        public static int Count => Points.Count;
        public static IReadOnlyList<MonsterSpawnPoint> ActivePoints => Points;
        public string Region => region;

        public void Configure(string regionName)
        {
            region = regionName;
        }

        private void OnEnable()
        {
            if (!Points.Contains(this)) Points.Add(this);
        }

        private void OnDisable() => Points.Remove(this);

        public static MonsterSpawnPoint FindSafePoint(
            Transform playerHead,
            float minimumDistance,
            LayerMask obstructionMask,
            MonsterSpawnPoint excludedPoint = null,
            Transform resettingMonster = null)
        {
            if (Points.Count == 0) return null;

            MonsterBrain[] monsters = FindObjectsByType<MonsterBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return Points
                .Where(point => point != null && point.isActiveAndEnabled && point != excludedPoint)
                .Where(point => point.IsSafeFor(playerHead, minimumDistance, obstructionMask, monsters, resettingMonster))
                .OrderByDescending(point => playerHead == null ? 0f : Vector3.Distance(point.transform.position, playerHead.position))
                .FirstOrDefault();
        }

        public bool IsSafeFor(
            Transform playerHead,
            float minimumDistance,
            LayerMask obstructionMask,
            IReadOnlyList<MonsterBrain> monsters,
            Transform ignoredMonster = null)
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return false;
            if (Vector3.Distance(hit.position, transform.position) > 1.25f) return false;

            if (playerHead != null)
            {
                if (Vector3.Distance(transform.position, playerHead.position) < minimumDistance) return false;
                Vector3 target = transform.position + Vector3.up;
                Vector3 delta = target - playerHead.position;
                if (delta.sqrMagnitude > 0.01f &&
                    !Physics.Raycast(playerHead.position, delta.normalized, delta.magnitude, obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }
            }

            if (monsters != null)
            {
                foreach (MonsterBrain monster in monsters)
                {
                    if (monster == null || monster.transform == ignoredMonster) continue;
                    if (Vector3.Distance(transform.position, monster.transform.position) < 4f) return false;
                }
            }
            return true;
        }
    }
}
