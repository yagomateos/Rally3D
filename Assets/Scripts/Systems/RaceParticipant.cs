using System;
using Rally.Car;
using Rally.Track;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// Tracks one car's progress through the stage: checkpoints, distance, wrong way / off-track
    /// detection and resetting onto the road.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class RaceParticipant : MonoBehaviour
    {
        [SerializeField] private string displayName = "Driver";
        [SerializeField] private bool isPlayer;
        [SerializeField] private Color color = Color.white;

        [Header("Detection")]
        [SerializeField] private float wrongWayDelay = 1.2f;
        [SerializeField] private float offTrackMargin = 22f;
        [SerializeField] private float missedCheckpointMargin = 45f;
        [SerializeField] private float resetCooldown = 1.2f;
        [Tooltip("Lateral margin beyond the road edge that still counts as 'on the road' for resets.")]
        [SerializeField] private float onRoadMargin = 3f;
        [Tooltip("How many steps back along the road a reset may try to find a spot free of other cars.")]
        [SerializeField] private int freeSpotAttempts = 6;
        [SerializeField] private float freeSpotStep = 7f;
        [Tooltip("Seconds a car may lie on its roof or side before it is put back on the road.")]
        [SerializeField] private float flippedRecoveryDelay = 3f;

        public event Action<RaceParticipant, Checkpoint> CheckpointPassed;
        public event Action<RaceParticipant> Finished;
        public event Action<RaceParticipant> WasReset;

        public string DisplayName => displayName;
        public bool IsPlayer => isPlayer;
        public Color Color => color;
        public CarController Car { get; private set; }
        public TrackPath Path { get; private set; }
        public float Distance { get; private set; }
        public int NextCheckpoint { get; private set; }
        public int CheckpointCount => checkpoints != null ? checkpoints.Length : 0;
        public bool HasFinished { get; private set; }
        public float FinishTime { get; private set; }
        /// <summary>Time added for resets asked for by the player (included in <see cref="FinishTime"/>).</summary>
        public float Penalty { get; private set; }
        public void AddPenalty(float seconds) => Penalty += seconds;
        public bool IsWrongWay { get; private set; }
        public bool IsOffTrack { get; private set; }
        public bool MissedCheckpoint { get; private set; }
        public float LateralOffset { get; private set; }
        /// <summary>On its roof or side and (almost) stopped: the car cannot continue on its own.</summary>
        public bool IsFlipped { get; private set; }

        public float Progress01 => Path == null ? 0f
            : Mathf.Clamp01((Distance - Path.StartDistance) / Mathf.Max(1f, Path.StageLength));

        private Checkpoint[] checkpoints;
        private float wrongWayTimer;
        private float flippedTimer;
        private float lastResetTime = -10f;
        private float lastOnRoadDistance;

        // Half extents of a car with a safety margin, used to test reset spots.
        private static readonly Vector3 ResetFootprint = new Vector3(1.2f, 0.8f, 2.6f);
        private static readonly float[] LateralOptions = { 0f, 1f, -1f };
        private readonly Collider[] overlapBuffer = new Collider[16];
        private Func<float> clock;

        public void Configure(string name, bool player, Color displayColor)
        {
            displayName = name;
            isPlayer = player;
            color = displayColor;
        }

        public void Initialize(TrackPath path, Checkpoint[] orderedCheckpoints, Func<float> raceClock)
        {
            Car = GetComponent<CarController>();
            Path = path;
            checkpoints = orderedCheckpoints;
            clock = raceClock;
            Distance = path.ProjectGlobal(transform.position);
            lastOnRoadDistance = Distance;
            NextCheckpoint = 0;
            HasFinished = false;
        }

        private void Update()
        {
            if (Path == null) return;

            Vector3 pos = transform.position;
            Distance = Path.Project(pos, Distance, 40f);
            LateralOffset = Path.LateralOffset(pos, Distance);
            if (HasFinished) return;

            float halfWidth = Path.WidthAt(Distance) * 0.5f;
            IsOffTrack = Mathf.Abs(LateralOffset) > halfWidth + offTrackMargin;
            if (Mathf.Abs(LateralOffset) <= halfWidth + onRoadMargin) lastOnRoadDistance = Distance;

            Vector3 velocity = Car.Body.linearVelocity;
            float along = Vector3.Dot(velocity, Path.TangentAt(Distance));
            wrongWayTimer = along < -3f ? wrongWayTimer + Time.deltaTime : 0f;
            IsWrongWay = wrongWayTimer > wrongWayDelay;

            MissedCheckpoint = NextCheckpoint < checkpoints.Length &&
                               Distance > checkpoints[NextCheckpoint].DistanceAlongTrack + missedCheckpointMargin;

            UpdateFlipped();
        }

        private void UpdateFlipped()
        {
            IsFlipped = Car.transform.up.y < 0.3f && Car.SpeedKph < 8f;
            flippedTimer = IsFlipped ? flippedTimer + Time.deltaTime : 0f;
            if (flippedTimer > flippedRecoveryDelay && ResetToTrack()) flippedTimer = 0f;
        }

        public void NotifyCheckpoint(Checkpoint checkpoint)
        {
            if (HasFinished || checkpoints == null || NextCheckpoint >= checkpoints.Length) return;
            if (checkpoints[NextCheckpoint] != checkpoint) return;

            NextCheckpoint++;
            CheckpointPassed?.Invoke(this, checkpoint);

            if (checkpoint.IsFinish)
            {
                HasFinished = true;
                FinishTime = (clock != null ? clock() : 0f) + Penalty;
                IsWrongWay = false;
                Finished?.Invoke(this);
            }
        }

        /// <summary>
        /// Place the car back on the road where it last left it (so cutting across country gains nothing).
        /// After a missed checkpoint it returns to that checkpoint.
        /// </summary>
        public bool ResetToTrack()
        {
            if (Path == null || Time.time - lastResetTime < resetCooldown) return false;
            lastResetTime = Time.time;

            float d = Mathf.Min(Distance, lastOnRoadDistance) - 4f;
            if (MissedCheckpoint && NextCheckpoint < checkpoints.Length)
                d = checkpoints[NextCheckpoint].DistanceAlongTrack - 25f;
            d = Mathf.Clamp(d, Path.StartDistance - 20f, Path.Length - 5f);

            FindFreeSpot(ref d, out Vector3 position, out Quaternion rotation);

            Car.Body.position = position;
            Car.Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Car.ResetMotion();

            Distance = d;
            lastOnRoadDistance = d;
            wrongWayTimer = 0f;
            flippedTimer = 0f;
            IsWrongWay = false;
            IsFlipped = false;
            MissedCheckpoint = false;
            WasReset?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Walks back along the road (and tries both sides of the lane) until the car's footprint is free
        /// of other cars, so a reset never spawns inside a rival.
        /// </summary>
        private void FindFreeSpot(ref float distance, out Vector3 position, out Quaternion rotation)
        {
            int vehicles = LayerMask.GetMask("Vehicle");
            int groundMask = ~(vehicles | Physics.IgnoreRaycastLayer);
            Physics.SyncTransforms();

            position = Vector3.zero;
            rotation = Quaternion.identity;
            for (int attempt = 0; attempt <= freeSpotAttempts; attempt++)
            {
                float d = Mathf.Max(Path.StartDistance - 20f, distance - attempt * freeSpotStep);
                float halfWidth = Path.WidthAt(d) * 0.5f;
                foreach (float lateralFactor in LateralOptions)
                {
                    float lateral = lateralFactor * Mathf.Max(0f, halfWidth - 1.2f);
                    Vector3 tangent = Path.TangentAt(d);
                    rotation = Quaternion.LookRotation(new Vector3(tangent.x, 0f, tangent.z), Vector3.up);
                    position = Path.PositionAt(d) + Path.RightAt(d) * lateral + Vector3.up * 0.8f;
                    if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, groundMask, QueryTriggerInteraction.Ignore))
                        position.y = hit.point.y + 0.6f;

                    if (!IsOccupied(position, rotation, vehicles))
                    {
                        distance = d;
                        return;
                    }
                }
            }
            // Every candidate was blocked (very unlikely): fall back to the last one tried.
        }

        private bool IsOccupied(Vector3 position, Quaternion rotation, int vehicleMask)
        {
            int hits = Physics.OverlapBoxNonAlloc(position + rotation * Vector3.up * 0.4f, ResetFootprint, overlapBuffer,
                rotation, vehicleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
                if (overlapBuffer[i].attachedRigidbody != Car.Body) return true;
            return false;
        }
    }
}
