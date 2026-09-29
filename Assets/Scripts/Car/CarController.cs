using System;
using Rally.Systems;
using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Arcade-realistic rally car built on WheelColliders: AWD drivetrain, speed sensitive steering,
    /// handbrake slides, surface dependent grip and a few stability assists that keep it fun.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        private const float MsToKph = 3.6f;
        private const float DriftSlipAngle = 9f;
        private const float LandingMinAirTime = 0.25f;

        [SerializeField] private CarTuning tuning;
        [SerializeField] private SurfaceDatabase surfaces;
        [SerializeField] private CarWheel[] wheels = new CarWheel[4];
        [SerializeField] private float wheelBase = 2.52f;
        [Tooltip("Engine power multiplier (used to vary AI cars).")]
        [SerializeField] private float powerMultiplier = 1f;

        public event Action<float> Landed;

        public CarTuning Tuning => tuning;
        public CarWheel[] Wheels => wheels;
        public Rigidbody Body { get; private set; }
        public CarDrivetrain Drivetrain { get; private set; }
        public ICarInputSource InputSource { get; set; }

        /// <summary>When false the car holds its brakes (countdown, pause, finish).</summary>
        public bool ControlEnabled { get; set; } = true;

        public CarInput CurrentInput { get; private set; }
        public float SpeedKph => Body != null ? Body.linearVelocity.magnitude * MsToKph : 0f;
        public float ForwardSpeed { get; private set; }
        public float SlipAngle { get; private set; }
        public bool IsDrifting => Mathf.Abs(SlipAngle) > DriftSlipAngle && GroundedWheels >= 2 && Body.linearVelocity.magnitude > 6f;
        public int GroundedWheels { get; private set; }
        public bool IsAirborne => GroundedWheels == 0;
        public float AirTime { get; private set; }
        public float SteerAngle { get; private set; }
        public bool IsReversing { get; private set; }
        public float EffectiveThrottle { get; private set; }
        public float EffectiveBrake { get; private set; }
        public SurfaceType DominantSurface { get; private set; } = SurfaceType.Dirt;
        public Vector3 LocalAcceleration { get; private set; }

        /// <summary>Engine power left after collision damage (set by <see cref="CarDamage"/>; separate from the AI's
        /// <see cref="PowerMultiplier"/>).</summary>
        public float DamagePowerScale { get; set; } = 1f;

        /// <summary>Steering pull from a bent corner, added to the steering input (set by <see cref="CarDamage"/>).</summary>
        public float DamageSteerBias { get; set; }

        public float PowerMultiplier
        {
            get => powerMultiplier;
            set => powerMultiplier = value;
        }

        private Vector3 lastVelocity;
        private readonly int[] surfaceVotes = new int[Enum.GetValues(typeof(SurfaceType)).Length];

        public void Configure(CarTuning carTuning, SurfaceDatabase surfaceDatabase, CarWheel[] carWheels, float carWheelBase)
        {
            tuning = carTuning;
            surfaces = surfaceDatabase;
            wheels = carWheels;
            wheelBase = carWheelBase;
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = tuning.mass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.3f;
            Body.centerOfMass += tuning.centerOfMassOffset;

            Drivetrain = new CarDrivetrain(tuning);
            foreach (var w in wheels) w.Setup(tuning);
            if (wheels.Length > 0) wheels[0].Collider.ConfigureVehicleSubsteps(6f, 14, 18);
        }

        /// <summary>Surface grip / dust table in use (a stage can bring its own, e.g. snow).</summary>
        public SurfaceDatabase Surfaces
        {
            get => surfaces;
            set => surfaces = value;
        }

        /// <summary>
        /// Swaps in a different tuning at runtime (car selection gives the player its own copy, so rivals and the
        /// asset on disk are untouched). Call while the car is stationary, before the start.
        /// </summary>
        public void ApplyTuning(CarTuning newTuning)
        {
            if (newTuning == null || Body == null) return;
            tuning = newTuning;
            Body.mass = tuning.mass;
            Body.ResetCenterOfMass();
            Body.centerOfMass += tuning.centerOfMassOffset;
            Drivetrain = new CarDrivetrain(tuning);
            foreach (var w in wheels) w.Setup(tuning);
        }

        /// <summary>Stops the car dead, used when resetting to the track.</summary>
        public void ResetMotion()
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            lastVelocity = Vector3.zero;
            SteerAngle = 0f;
            AirTime = 0f; // time spent lying on the roof must not count as a jump
            Drivetrain.Reset();
            foreach (var w in wheels)
            {
                w.Collider.motorTorque = 0f;
                w.Collider.brakeTorque = tuning.brakeTorque;
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            CarInput input = ControlEnabled && InputSource != null ? InputSource.ReadInput() : CarInput.None;
            if (!ControlEnabled) input.handbrake = true;
            CurrentInput = input;

            SampleWheels();

            Vector3 velocity = Body.linearVelocity;
            ForwardSpeed = Vector3.Dot(velocity, transform.forward);
            Vector3 planar = Vector3.ProjectOnPlane(velocity, transform.up);
            SlipAngle = planar.magnitude > 2f ? Vector3.SignedAngle(transform.forward, planar, transform.up) : 0f;
            if (Mathf.Abs(SlipAngle) > 90f) SlipAngle = 0f; // travelling backwards is not a slide

            ResolveDriveIntent(input, out float drive, out float brake);
            EffectiveThrottle = drive;
            EffectiveBrake = brake;

            ApplySteering(input.steer, dt);
            ApplyDrive(drive, brake, input.handbrake, dt);
            ApplyGrip(input);
            ApplyAntiRoll();
            ApplyAerodynamics(velocity);
            ApplyAssists(input);
            TrackAirTime(dt);

            LocalAcceleration = transform.InverseTransformDirection((velocity - lastVelocity) / dt);
            lastVelocity = velocity;
        }

        private void SampleWheels()
        {
            GroundedWheels = 0;
            Array.Clear(surfaceVotes, 0, surfaceVotes.Length);
            foreach (var w in wheels)
            {
                w.SampleGround(surfaces);
                if (!w.IsGrounded) continue;
                GroundedWheels++;
                surfaceVotes[(int)w.Surface]++;
            }

            if (GroundedWheels == 0) return;
            int best = 0;
            for (int i = 1; i < surfaceVotes.Length; i++)
                if (surfaceVotes[i] > surfaceVotes[best]) best = i;
            DominantSurface = (SurfaceType)best;
        }

        private void ResolveDriveIntent(CarInput input, out float drive, out float brake)
        {
            const float StopSpeed = 1.2f;

            if (!IsReversing && input.brake > 0.1f && input.throttle < 0.1f && ForwardSpeed < StopSpeed)
                IsReversing = true;
            else if (IsReversing && input.throttle > 0.1f && ForwardSpeed > -StopSpeed)
                IsReversing = false;

            if (IsReversing)
            {
                drive = input.brake;
                brake = input.throttle;
                if (-ForwardSpeed * MsToKph > tuning.maxReverseSpeedKph) drive = 0f;
            }
            else
            {
                drive = input.throttle;
                brake = input.brake;
                if (ForwardSpeed < -StopSpeed) brake = Mathf.Max(brake, input.throttle);
            }

            if (SpeedKph > tuning.maxSpeedKph) drive = 0f;
        }

        private void ApplySteering(float steerInput, float dt)
        {
            float speedT = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) * MsToKph / tuning.steerAngleSpeedKph);
            float maxAngle = Mathf.Lerp(tuning.maxSteerAngle, tuning.highSpeedSteerAngle, speedT * speedT * (3f - 2f * speedT));

            // Allow extra lock towards the slide so drifts can be caught (natural counter-steer).
            if (IsDrifting && Mathf.Sign(steerInput) == Mathf.Sign(SlipAngle))
                maxAngle = Mathf.Max(maxAngle, Mathf.Min(tuning.maxSteerAngle, Mathf.Abs(SlipAngle) + 6f));

            float target = Mathf.Clamp(steerInput + DamageSteerBias, -1.6f, 1.6f) * maxAngle;
            bool returning = Mathf.Abs(target) < Mathf.Abs(SteerAngle) || Mathf.Sign(target) != Mathf.Sign(SteerAngle);
            float rate = returning ? tuning.steerReturnSpeed : tuning.steerSpeed;
            SteerAngle = Mathf.MoveTowards(SteerAngle, target, rate * dt);

            foreach (var w in wheels)
                if (w.IsFront) w.Collider.steerAngle = SteerAngle;
        }

        private void ApplyDrive(float drive, float brake, bool handbrake, float dt)
        {
            float drivenRpm = 0f;
            foreach (var w in wheels) drivenRpm += w.Collider.rpm;
            drivenRpm /= wheels.Length;

            float wheelTorque = Drivetrain.Step(dt, drivenRpm, ForwardSpeed, drive, IsReversing, GroundedWheels > 0) * powerMultiplier * DamagePowerScale;

            float frontShare = (1f - tuning.rearTorqueBias) * 0.5f;
            float rearShare = tuning.rearTorqueBias * 0.5f;
            float engineBrake = drive < 0.05f && !Drivetrain.IsShifting ? tuning.engineBraking * Drivetrain.NormalizedRpm : 0f;

            foreach (var w in wheels)
            {
                float share = w.IsFront ? frontShare : rearShare;
                float motor = wheelTorque * share;

                if (handbrake && !w.IsFront) motor = 0f; // hydraulic handbrake disengages the rear axle
                if (w.IsGrounded && Mathf.Abs(w.ForwardSlip) > 0.45f)
                    motor *= 1f - tuning.tractionControl;

                float brakeTorque = brake * tuning.brakeTorque * (w.IsFront ? tuning.frontBrakeBias : 1f - tuning.frontBrakeBias) * 2f;
                brakeTorque += engineBrake;
                if (handbrake && !w.IsFront) brakeTorque += tuning.handbrakeTorque;

                w.Collider.motorTorque = motor;
                w.Collider.brakeTorque = brakeTorque;
            }
        }

        private void ApplyGrip(CarInput input)
        {
            float speedFactor = Mathf.Clamp01((Mathf.Abs(ForwardSpeed) - 4f) / 12f);
            float powerOversteer = Mathf.Lerp(1f, tuning.powerOversteerGrip, input.throttle * Mathf.Abs(input.steer) * speedFactor);

            foreach (var w in wheels)
            {
                float grip = w.IsGrounded ? w.SurfaceProps.grip : 1f;
                float side = grip;
                if (!w.IsFront)
                {
                    side *= powerOversteer;
                    if (input.handbrake && ControlEnabled) side *= tuning.handbrakeRearGrip;
                }
                w.SetGrip(grip, side, tuning);
            }
        }

        private void ApplyAntiRoll()
        {
            ApplyAntiRollPair(true, tuning.antiRollFront);
            ApplyAntiRollPair(false, tuning.antiRollRear);
        }

        private void ApplyAntiRollPair(bool front, float stiffness)
        {
            CarWheel left = null, right = null;
            foreach (var w in wheels)
            {
                if (w.IsFront != front) continue;
                if (w.IsLeft) left = w; else right = w;
            }
            if (left == null || right == null) return;

            float travelL = left.IsGrounded ? 1f - left.Compression : 1f;
            float travelR = right.IsGrounded ? 1f - right.Compression : 1f;
            float force = (travelL - travelR) * stiffness;

            if (left.IsGrounded)
                Body.AddForceAtPosition(left.Collider.transform.up * -force, left.Collider.transform.position);
            if (right.IsGrounded)
                Body.AddForceAtPosition(right.Collider.transform.up * force, right.Collider.transform.position);
        }

        private void ApplyAerodynamics(Vector3 velocity)
        {
            float speed = velocity.magnitude;
            Body.AddForce(-velocity * (speed * tuning.aeroDrag));
            if (GroundedWheels > 0)
                Body.AddForce(-transform.up * (tuning.downforce * speed * speed));

            Vector3 planar = Vector3.ProjectOnPlane(velocity, transform.up);
            foreach (var w in wheels)
            {
                if (!w.IsGrounded) continue;
                Body.AddForce(-planar * w.SurfaceProps.rollingResistance);
            }
        }

        private void ApplyAssists(CarInput input)
        {
            Vector3 up = transform.up;
            float yawRate = Vector3.Dot(Body.angularVelocity, up);

            if (GroundedWheels >= 2)
            {
                float speed = Mathf.Abs(ForwardSpeed);
                float desiredYaw = ForwardSpeed * Mathf.Tan(SteerAngle * Mathf.Deg2Rad) / wheelBase;
                desiredYaw = Mathf.Clamp(desiredYaw, -tuning.maxYawRate, tuning.maxYawRate);

                float stability = input.handbrake ? 0.15f : 1f;
                stability *= Mathf.Clamp01(speed / 8f);
                float correction = -(yawRate - desiredYaw) * tuning.yawStability * stability;

                if (IsDrifting)
                    correction += input.steer * tuning.driftSteerAssist * Mathf.Clamp01(speed / 15f);

                Body.AddTorque(up * correction, ForceMode.Acceleration);

                if (Mathf.Abs(yawRate) > tuning.maxYawRate)
                    Body.AddTorque(up * (-(yawRate - Mathf.Sign(yawRate) * tuning.maxYawRate) * 4f), ForceMode.Acceleration);
            }
            else if (GroundedWheels == 0)
            {
                // Keep the car flying level-ish so jumps land cleanly.
                Vector3 axis = Vector3.Cross(up, Vector3.up);
                Body.AddTorque(axis * tuning.airStabilization, ForceMode.Acceleration);
                Vector3 local = transform.InverseTransformDirection(Body.angularVelocity);
                local.x *= 0.97f;
                local.z *= 0.97f;
                Body.angularVelocity = transform.TransformDirection(local);
            }
        }

        private void TrackAirTime(float dt)
        {
            if (GroundedWheels == 0)
            {
                AirTime += dt;
                return;
            }

            if (AirTime > LandingMinAirTime)
            {
                float impact = Mathf.Max(0f, -lastVelocity.y);
                Landed?.Invoke(impact);
            }
            AirTime = 0f;
        }
    }
}
