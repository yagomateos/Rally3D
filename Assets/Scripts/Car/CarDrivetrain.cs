using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Engine + automatic sequential gearbox. Plain class driven by <see cref="CarController"/>.
    /// Gear: -1 = reverse, 0 = neutral, 1..n = forward gears.
    /// </summary>
    public class CarDrivetrain
    {
        private const float ClutchEngageRpmMargin = 2200f;
        private const float ShiftCooldown = 0.55f;

        private readonly CarTuning tuning;
        private float shiftTimer;
        private float cooldown;
        private int pendingGear;

        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; }
        public bool IsShifting => shiftTimer > 0f;
        public bool IsLimiting { get; private set; }
        public float NormalizedRpm => Mathf.InverseLerp(tuning.idleRpm, tuning.maxRpm, Rpm);
        public int ForwardGearCount => tuning.gearRatios.Length;

        public CarDrivetrain(CarTuning tuning)
        {
            this.tuning = tuning;
            Rpm = tuning.idleRpm;
        }

        public void Reset()
        {
            Gear = 1;
            shiftTimer = 0f;
            cooldown = 0f;
            Rpm = tuning.idleRpm;
        }

        public float CurrentRatio
        {
            get
            {
                if (Gear == 0) return 0f;
                if (Gear < 0) return -tuning.reverseRatio * tuning.finalDrive;
                return tuning.gearRatios[Gear - 1] * tuning.finalDrive;
            }
        }

        /// <summary>Advance the drivetrain and return the total drive torque at the wheels.</summary>
        /// <param name="groundSpeed">Car speed over the ground (m/s); gear changes use it so wheelspin never triggers upshifts.</param>
        public float Step(float dt, float drivenWheelRpm, float groundSpeed, float throttle, bool wantReverse, bool grounded)
        {
            float groundRpm = Mathf.Abs(groundSpeed) / (2f * Mathf.PI * tuning.wheelRadius) * 60f * Mathf.Abs(CurrentRatio);
            HandleGearSelection(dt, wantReverse, grounded, groundRpm);

            float ratio = Mathf.Abs(CurrentRatio);
            float wheelDrivenRpm = Mathf.Abs(drivenWheelRpm) * ratio;

            // Slipping clutch at low speed keeps the engine alive and gives a lively launch.
            float clutchRpm = tuning.idleRpm + throttle * ClutchEngageRpmMargin;
            float targetRpm = IsShifting || Gear == 0
                ? tuning.idleRpm + throttle * (tuning.maxRpm - tuning.idleRpm) * 0.75f
                : Mathf.Max(wheelDrivenRpm, clutchRpm);

            Rpm = Mathf.Lerp(Rpm, Mathf.Min(targetRpm, tuning.maxRpm + 150f), 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, tuning.engineInertia * 0.1f)));

            IsLimiting = Rpm >= tuning.maxRpm;
            if (IsShifting || Gear == 0) return 0f;

            float torque = tuning.torqueCurve.Evaluate(NormalizedRpm) * tuning.peakTorque * throttle;
            if (IsLimiting) torque = 0f;

            return torque * CurrentRatio * tuning.drivetrainEfficiency;
        }

        private void HandleGearSelection(float dt, bool wantReverse, bool grounded, float groundRpm)
        {
            if (cooldown > 0f) cooldown -= dt;

            if (shiftTimer > 0f)
            {
                shiftTimer -= dt;
                if (shiftTimer <= 0f) Gear = pendingGear;
                return;
            }

            if (wantReverse && Gear != -1) { BeginShift(-1); return; }
            if (!wantReverse && Gear == -1) { BeginShift(1); return; }
            if (Gear < 1 || cooldown > 0f) return;

            // Do not upshift in the air: the free-spinning wheels would give false rpm readings.
            if (groundRpm > tuning.shiftUpRpm && Gear < tuning.gearRatios.Length && grounded)
                BeginShift(Gear + 1);
            else if (groundRpm < tuning.shiftDownRpm && Gear > 1)
                BeginShift(Gear - 1);
        }

        private void BeginShift(int target)
        {
            pendingGear = target;
            shiftTimer = tuning.shiftTime;
            cooldown = ShiftCooldown;
        }
    }
}
