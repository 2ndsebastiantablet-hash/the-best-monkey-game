using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TheBestMonkeyGame.Monsters
{
    public sealed class MonsterSpawnPoint : MonoBehaviour
    {
        private static readonly List<MonsterSpawnPoint> Points = new List<MonsterSpawnPoint>();

        public static int Count => Points.Count;

        private void OnEnable()
        {
            if (!Points.Contains(this)) Points.Add(this);
        }

        private void OnDisable() => Points.Remove(this);

        public static Transform FindSafePoint(Transform playerHead, float minimumDistance, LayerMask obstructionMask)
        {
            return FindSafePoint(playerHead != null ? new[] { playerHead } : Array.Empty<Transform>(), minimumDistance, obstructionMask);
        }

        public static Transform FindSafePoint(IReadOnlyList<Transform> playerHeads, float minimumDistance, LayerMask obstructionMask)
        {
            if (Points.Count == 0) return null;
            int start = UnityEngine.Random.Range(0, Points.Count);
            for (int offset = 0; offset < Points.Count; offset++)
            {
                MonsterSpawnPoint point = Points[(start + offset) % Points.Count];
                if (point == null || !point.isActiveAndEnabled) continue;
                bool unsafeForPlayer = false;
                for (int index = 0; index < playerHeads.Count; index++)
                {
                    Transform playerHead = playerHeads[index];
                    if (playerHead == null) continue;
                    if (Vector3.Distance(point.transform.position, playerHead.position) < minimumDistance)
                    {
                        unsafeForPlayer = true;
                        break;
                    }
                    Vector3 delta = point.transform.position + Vector3.up - playerHead.position;
                    if (!Physics.Raycast(playerHead.position, delta.normalized, delta.magnitude, obstructionMask, QueryTriggerInteraction.Ignore))
                    {
                        unsafeForPlayer = true;
                        break;
                    }
                }
                if (unsafeForPlayer) continue;
                if (!NavMesh.SamplePosition(point.transform.position, out _, 1f, NavMesh.AllAreas)) continue;
                return point.transform;
            }
            return null;
        }
    }
}
