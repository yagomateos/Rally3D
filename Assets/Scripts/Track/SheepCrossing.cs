using System.Collections.Generic;
using Rally.Systems;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// Coastal stage: now and then a sheep wanders across the road ahead of the player. Crossing points are picked
    /// by the stage builder where the road is straight enough to see it coming; each run a random few of them
    /// are used. The sheep starts walking when the player is a few seconds away, so it is on the road when the
    /// player arrives but there is time to brake or steer round it.
    /// </summary>
    public class SheepCrossing : MonoBehaviour
    {
        [SerializeField] private Sheep sheepPrefab;
        [Tooltip("Distances along the track where a sheep may cross (set by the stage builder).")]
        [SerializeField] private float[] crossingPoints = new float[0];
        [SerializeField, Range(0f, 1f)] private float chancePerPoint = 0.3f; // ~2 sheep per run on average, sometimes none
        [Tooltip("The sheep sets off when the player is this many seconds from the crossing.")]
        [SerializeField] private float leadSeconds = 5f;
        [SerializeField] private float minLeadDistance = 70f;
        [SerializeField] private float startOffRoad = 4.5f;

        private readonly List<float> pending = new List<float>();
        private RaceManager race;

        public IReadOnlyList<float> CrossingPoints => crossingPoints;

        public void Configure(Sheep prefab, float[] points)
        {
            sheepPrefab = prefab;
            crossingPoints = points;
        }

        private void Start()
        {
            race = RaceManager.Instance;
            foreach (float d in crossingPoints)
                if (Random.value < chancePerPoint) pending.Add(d);
        }

        private void Update()
        {
            if (race == null || race.CurrentState != RaceManager.State.Racing || race.Player == null || pending.Count == 0) return;
            float playerDistance = race.Player.Distance;
            float speed = race.Player.Car != null ? Mathf.Max(8f, race.Player.Car.SpeedKph / 3.6f) : 8f;
            float trigger = Mathf.Max(minLeadDistance, speed * leadSeconds);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                float gap = pending[i] - playerDistance;
                if (gap < 0f) { pending.RemoveAt(i); continue; } // already passed it
                if (gap > trigger) continue;
                SpawnAt(pending[i], Random.value < 0.5f ? -1 : 1);
                pending.RemoveAt(i);
            }
        }

        /// <summary>Puts a sheep at the road edge on <paramref name="side"/> (-1 left, +1 right) and starts it walking across.</summary>
        public Sheep SpawnAt(float distance, int side)
        {
            if (sheepPrefab == null || race == null || race.Path == null) return null;
            var path = race.Path;
            float half = path.WidthAt(distance) * 0.5f;
            Vector3 right = path.RightAt(distance);
            Vector3 start = path.PositionAt(distance) + right * side * (half + startOffRoad);
            if (Physics.Raycast(start + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                start.y = hit.point.y;

            // Across the road, a little diagonal so it doesn't look ruled.
            Vector3 across = -right * side + path.TangentAt(distance) * Random.Range(-0.25f, 0.25f);
            var sheep = Instantiate(sheepPrefab, start + Vector3.up * 0.05f, Quaternion.LookRotation(across));
            sheep.Walk(across, (half + startOffRoad) * 2f / sheep.walkSpeed + 1f);
            return sheep;
        }
    }
}
