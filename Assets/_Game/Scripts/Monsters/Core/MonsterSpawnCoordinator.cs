using System.Linq;
using UnityEngine;

namespace TheBestMonkeyGame.Monsters
{
    /// <summary>Places each monster in a distinct, hidden distant region at startup.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MonsterSpawnCoordinator : MonoBehaviour
    {
        [SerializeField] private TiptoeBrain tiptoe;
        [SerializeField] private StatueBrain statue;
        [SerializeField] private Transform playerHead;

        public void Configure(TiptoeBrain tiptoeBrain, StatueBrain statueBrain, Transform head)
        {
            tiptoe = tiptoeBrain;
            statue = statueBrain;
            playerHead = head;
        }

        private void Start()
        {
            if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;
            MonsterSpawnPoint[] points = MonsterSpawnPoint.ActivePoints
                .Where(point => point != null)
                .OrderByDescending(point => Vector3.Distance(point.transform.position, playerHead.position))
                .ToArray();
            if (points.Length < 2) return;

            MonsterSpawnPoint statuePoint = points.FirstOrDefault(point =>
                point.IsSafeFor(playerHead, statue.MinimumSpawnDistance, statue.Perception.ObstructionMask, null, statue.transform));
            MonsterSpawnPoint tiptoePoint = points
                .Where(point => point != statuePoint && point.Region != statuePoint?.Region)
                .OrderByDescending(point => statuePoint == null ? 0f : Vector3.Distance(point.transform.position, statuePoint.transform.position))
                .FirstOrDefault(point =>
                    point.IsSafeFor(playerHead, tiptoe.MinimumSpawnDistance, tiptoe.Perception.ObstructionMask, null, tiptoe.transform));

            if (statuePoint != null) statue.PlaceAtStartup(statuePoint);
            if (tiptoePoint != null) tiptoe.PlaceAtStartup(tiptoePoint);
        }
    }
}
