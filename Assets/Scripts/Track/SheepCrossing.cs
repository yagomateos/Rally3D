using System.Collections.Generic;
using Rally.Systems;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// Stages 1 and 4: now and then sheep are standing in the road ahead of the player, ambling slowly across and
    /// blocking the way. Crossing points are picked by the stage builder where the road is straight enough to see
    /// them coming; each run a random few of them are used (at least one). The flock appears on the road well before
    /// the player can see it, so it is already there, in the middle, when it comes into view.
    /// </summary>
    public class SheepCrossing : MonoBehaviour
    {
        [SerializeField] private Sheep sheepPrefab;
        [Tooltip("Distances along the track where a sheep may cross (set by the stage builder).")]
        [SerializeField] private float[] crossingPoints = new float[0];
        [SerializeField, Range(0f, 1f)] private float chancePerPoint = 0.3f; // ~2 flocks per run on average, at least one
        [Tooltip("The flock appears when the player is this many seconds from the crossing.")]
        [SerializeField] private float leadSeconds = 9f;
        [SerializeField] private float minLeadDistance = 160f;
        [Tooltip("Slow amble across the road, so the sheep stay in the way.")]
        [SerializeField] private float ambleSpeed = 0.3f;
        [SerializeField] private int maxFlock = 3;
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
            // Always at least one per run, so the sheep is part of the stage and not a rare surprise.
            if (pending.Count == 0 && crossingPoints.Length > 0) pending.Add(crossingPoints[Random.Range(0, crossingPoints.Length)]);
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
                if (gap < LastChanceGap) { pending.RemoveAt(i); continue; } // too close (or passed) to appear fairly
                if (gap > trigger) continue;
                // The flock is for the player: while a rival is between the player and the crossing it would reach
                // the sheep first and knock them away, so wait until it has gone by.
                if (RivalBetween(playerDistance, pending[i] + 25f)) continue;
                SpawnFlockOnRoad(pending[i]);
                pending.RemoveAt(i);
            }
        }

        /// <summary>
        /// Side of the road away from the sea (-1 left, +1 right). The sea side has a guard rail the sheep can't
        /// climb, so a sheep starting there never reached the road.
        /// </summary>
        public int InlandSide(float distance)
        {
            Vector3 right = race.Path.RightAt(distance);
            Vector2 sea = Generation.TerrainSculptor.SeaDirection;
            return right.x * sea.x + right.z * sea.y > 0f ? -1 : 1;
        }

        private const float LastChanceGap = 60f;

        private bool RivalBetween(float from, float to)
        {
            foreach (var p in race.Participants)
                if (p != null && !p.IsPlayer && p.gameObject.activeInHierarchy && p.Distance > from && p.Distance < to) return true;
            return false;
        }

        /// <summary>
        /// One to <see cref="maxFlock"/> sheep standing in the road around <paramref name="distance"/>, across the lane
        /// and a few metres apart, ambling slowly towards one side.
        /// </summary>
        public Sheep[] SpawnFlockOnRoad(float distance)
        {
            if (sheepPrefab == null || race == null || race.Path == null) return new Sheep[0];
            var path = race.Path;
            int count = Random.Range(1, maxFlock + 1);
            int side = Random.value < 0.5f ? -1 : 1;
            var flock = new Sheep[count];
            for (int i = 0; i < count; i++)
            {
                float d = distance + (i - (count - 1) * 0.5f) * Random.Range(2.5f, 4f);
                float half = path.WidthAt(d) * 0.5f;
                Vector3 right = path.RightAt(d);
                Vector3 pos = path.PositionAt(d) + right * Random.Range(-half * 0.5f, half * 0.5f);
                if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                    pos.y = hit.point.y;
                Vector3 across = right * side + path.TangentAt(d) * Random.Range(-0.4f, 0.4f);
                var sheep = Instantiate(sheepPrefab, pos + Vector3.up * 0.05f, Quaternion.LookRotation(across));
                sheep.Walk(across, 45f, ambleSpeed * Random.Range(0.8f, 1.2f));
                flock[i] = sheep;
            }
            return flock;
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
