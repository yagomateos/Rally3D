using Rally.Car;
using Rally.Systems;
using Rally.Track;
using UnityEngine;

namespace Rally.AI
{
    /// <summary>
    /// Path-following rally AI: pure-pursuit steering, a curvature based speed profile,
    /// human-like imperfections and stuck recovery.
    /// </summary>
    [RequireComponent(typeof(CarController), typeof(RaceParticipant))]
    public class AIDriver : MonoBehaviour, ICarInputSource
    {
        [Header("Skill")]
        [Range(0.5f, 1.2f), Tooltip("Cornering grip the AI assumes (higher = faster in corners).")]
        [SerializeField] private float corneringSkill = 0.9f;
        [SerializeField] private float brakingDecel = 8.5f;
        [SerializeField] private float topSpeedKph = 165f;
        [SerializeField] private float jumpSpeedKph = 105f;

        [Header("Driving line")]
        [SerializeField] private float lookAheadBase = 7f;
        [SerializeField] private float lookAheadPerMs = 0.55f;
        [SerializeField] private float laneOffset;
        [SerializeField] private float cornerCutting = 0.45f;

        [Header("Imperfection")]
        [SerializeField, Range(0f, 1f)] private float steeringNoise = 0.08f;
        [SerializeField, Range(0f, 1f)] private float mistakeChance = 0.12f;

        [Header("Recovery")]
        [SerializeField] private float stuckSpeed = 1.5f;
        [SerializeField] private float stuckTime = 2.5f;
        [SerializeField] private int maxReverseAttempts = 2;

        /// <summary>Optional cap used for the post-finish cool-down lap.</summary>
        public float SpeedCapKph { get; set; } = float.MaxValue;

        private CarController car;
        private RaceParticipant participant;
        private TrackPath path;
        private float[] speedProfile;
        private float noiseSeed;
        private float stuckTimer;
        private int recoveryAttempts;
        private float reverseTimer;
        private float mistakeTimer;
        private float mistakeFactor = 1f;
        private float nextMistakeCheck;

        public void Configure(float skill, float power, float offset, float noise, float mistakes, float topSpeed)
        {
            corneringSkill = skill;
            laneOffset = offset;
            steeringNoise = noise;
            mistakeChance = mistakes;
            topSpeedKph = topSpeed;
            GetComponent<CarController>().PowerMultiplier = power;
        }

        private void Awake()
        {
            car = GetComponent<CarController>();
            participant = GetComponent<RaceParticipant>();
            noiseSeed = Random.value * 100f;
        }

        private void Start()
        {
            if (car.InputSource == null || !(car.InputSource is PlayerCarInput)) car.InputSource = this;
        }

        private void EnsureProfile()
        {
            if (speedProfile != null || participant.Path == null) return;
            path = participant.Path;
            speedProfile = BuildSpeedProfile(path);
        }

        private float[] BuildSpeedProfile(TrackPath trackPath)
        {
            int count = trackPath.Count;
            var profile = new float[count];
            float gravity = Physics.gravity.magnitude;
            float vmax = topSpeedKph / 3.6f;

            for (int i = 0; i < count; i++)
            {
                float d = i * trackPath.Spacing;
                float k = Mathf.Max(Mathf.Abs(trackPath.CurvatureAt(d, 8f)), Mathf.Abs(trackPath.CurvatureAt(d, 3f)) * 0.8f);
                float grip = corneringSkill * GripFor(trackPath.SurfaceAt(d));
                profile[i] = Mathf.Min(vmax, Mathf.Sqrt(grip * gravity / Mathf.Max(k, 0.0005f)));
            }

            // Jumps: approach crests at a controlled speed (detected by sharp elevation drop-offs).
            for (int i = 2; i < count - 6; i++)
            {
                float drop = trackPath.GetPoint(i).y - trackPath.GetPoint(i + 5).y;
                float rise = trackPath.GetPoint(i).y - trackPath.GetPoint(i - 2).y;
                if (drop > 1.2f && rise > 0.1f) profile[i] = Mathf.Min(profile[i], jumpSpeedKph / 3.6f);
            }

            // Backward pass: brake early enough for what is coming.
            for (int i = count - 2; i >= 0; i--)
            {
                float reachable = Mathf.Sqrt(profile[i + 1] * profile[i + 1] + 2f * brakingDecel * trackPath.Spacing);
                profile[i] = Mathf.Min(profile[i], reachable);
            }
            return profile;
        }

        /// <summary>
        /// Set by the race manager on stages with their own surface table (snow): the AI then reads the grip from
        /// it instead of the dirt-stage defaults below, so it slows down for snow and ice.
        /// </summary>
        public static SurfaceDatabase StageSurfaces { get; set; }

        private static float GripFor(SurfaceType surface)
        {
            if (StageSurfaces != null) return StageSurfaces.Get(surface).grip;
            switch (surface)
            {
                case SurfaceType.Asphalt: return 1.2f;
                case SurfaceType.Gravel: return 0.85f;
                case SurfaceType.Mud: return 0.65f;
                default: return 1f;
            }
        }

        public CarInput ReadInput()
        {
            EnsureProfile();
            if (path == null) return CarInput.None;

            float speed = car.Body.linearVelocity.magnitude;
            float distance = participant.Distance;

            UpdateMistakes();

            // --- Steering: pure pursuit towards a point ahead on the (slightly cut) racing line.
            float lookAhead = lookAheadBase + speed * lookAheadPerMs;
            float targetDistance = distance + lookAhead;
            float curvature = path.CurvatureAt(targetDistance, 10f);
            float halfWidth = path.WidthAt(targetDistance) * 0.5f;
            float apexOffset = Mathf.Clamp(curvature * 60f, -1f, 1f) * halfWidth * cornerCutting;
            float lateral = Mathf.Clamp(laneOffset + apexOffset, -halfWidth + 1.3f, halfWidth - 1.3f);

            Vector3 target = path.PositionAt(targetDistance) + path.RightAt(targetDistance) * lateral;
            Vector3 local = transform.InverseTransformPoint(target);
            float angle = Mathf.Atan2(local.x, Mathf.Max(0.1f, local.z)) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(angle / 22f, -1f, 1f);
            steer += (Mathf.PerlinNoise(Time.time * 0.7f, noiseSeed) - 0.5f) * 2f * steeringNoise;

            // Catch slides by steering into them.
            if (car.IsDrifting) steer += Mathf.Clamp(car.SlipAngle / 40f, -0.5f, 0.5f);
            steer = Mathf.Clamp(steer, -1f, 1f);

            // --- Speed control against the profile.
            int index = Mathf.Clamp(Mathf.RoundToInt((distance + 3f) / path.Spacing), 0, speedProfile.Length - 1);
            float targetSpeed = Mathf.Min(speedProfile[index] * mistakeFactor, SpeedCapKph / 3.6f);
            if (participant.HasFinished && distance > path.Length - 45f) targetSpeed = 0f; // park at the end of the stop zone
            float error = targetSpeed - speed;

            float throttle = Mathf.Clamp01(error * 0.35f + 0.25f);
            float brake = error < -1.5f ? Mathf.Clamp01(-error * 0.2f) : 0f;
            if (brake > 0f) throttle = 0f;
            if (Mathf.Abs(steer) > 0.85f && speed > 12f) throttle *= 0.6f;
            bool handbrake = false;
            float k = Mathf.Abs(path.CurvatureAt(distance + 6f, 5f));
            if (k > 0.04f && speed > 8f && speed < 22f) handbrake = Mathf.Abs(angle) > 28f;

            if (targetSpeed < 0.5f)
            {
                // Stop without engaging reverse (braking at standstill selects reverse).
                throttle = 0f;
                brake = speed > 1f ? 1f : 0f;
                handbrake = true;
            }

            var input = new CarInput { steer = steer, throttle = throttle, brake = brake, handbrake = handbrake };
            return ApplyRecovery(input, speed);
        }

        private void UpdateMistakes()
        {
            if (mistakeTimer > 0f)
            {
                mistakeTimer -= Time.fixedDeltaTime;
                if (mistakeTimer <= 0f) mistakeFactor = 1f;
                return;
            }
            if (Time.time < nextMistakeCheck) return;
            nextMistakeCheck = Time.time + 4f;
            if (Random.value > mistakeChance) return;

            // Either overcooks a corner or lifts too early.
            mistakeFactor = Random.value < 0.5f ? Random.Range(1.12f, 1.25f) : Random.Range(0.7f, 0.85f);
            mistakeTimer = Random.Range(1.5f, 3f);
        }

        private CarInput ApplyRecovery(CarInput input, float speed)
        {
            float dt = Time.fixedDeltaTime;
            if (!car.ControlEnabled || participant.HasFinished)
            {
                stuckTimer = 0f;
                return input;
            }

            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                return new CarInput { steer = -input.steer, throttle = 0f, brake = 1f };
            }

            if (speed > 8f) recoveryAttempts = 0;
            bool tryingToMove = input.throttle > 0.3f;
            stuckTimer = speed < stuckSpeed && tryingToMove ? stuckTimer + dt : Mathf.Max(0f, stuckTimer - dt * 0.5f);

            if (participant.IsOffTrack || participant.IsWrongWay)
            {
                participant.ResetToTrack();
                stuckTimer = 0f;
            }
            else if (stuckTimer > stuckTime)
            {
                stuckTimer = 0f;
                if (++recoveryAttempts > maxReverseAttempts)
                {
                    recoveryAttempts = 0;
                    participant.ResetToTrack();
                }
                else reverseTimer = 1.6f;
            }
            return input;
        }
    }
}
