using UnityEngine;

namespace Rally.Car
{
    /// <summary>All handling parameters of a car in one editable asset.</summary>
    [CreateAssetMenu(menuName = "Rally/Car Tuning", fileName = "CarTuning")]
    public class CarTuning : ScriptableObject
    {
        [Header("Body")]
        public float mass = 1250f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.12f, 0.08f);
        public float aeroDrag = 0.42f;
        public float downforce = 1.6f;

        [Header("Engine")]
        public float idleRpm = 900f;
        public float maxRpm = 7600f;
        public float peakTorque = 480f;
        [Tooltip("Normalised torque (0..1) over normalised rpm (0..1).")]
        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(0f, 0.55f), new Keyframe(0.35f, 0.88f), new Keyframe(0.62f, 1f),
            new Keyframe(0.85f, 0.94f), new Keyframe(1f, 0.72f));
        public float engineInertia = 0.35f;
        public float engineBraking = 90f;

        [Header("Gearbox")]
        public float[] gearRatios = { 3.3f, 2.25f, 1.7f, 1.36f, 1.12f, 0.94f };
        public float reverseRatio = 3.2f;
        public float finalDrive = 4.6f;
        public float shiftUpRpm = 7100f;
        public float shiftDownRpm = 3600f;
        public float shiftTime = 0.18f;
        [Range(0f, 1f)] public float drivetrainEfficiency = 0.88f;
        [Range(0f, 1f), Tooltip("Share of torque sent to the rear axle (AWD).")]
        public float rearTorqueBias = 0.58f;
        public float maxReverseSpeedKph = 35f;

        [Header("Brakes")]
        public float brakeTorque = 3400f;
        [Range(0f, 1f)] public float frontBrakeBias = 0.62f;
        public float handbrakeTorque = 5200f;

        [Header("Steering")]
        public float maxSteerAngle = 34f;
        public float highSpeedSteerAngle = 11f;
        public float steerAngleSpeedKph = 150f;
        [Tooltip("Degrees per second the wheels can turn.")]
        public float steerSpeed = 190f;
        [Tooltip("Degrees per second the wheels return to centre.")]
        public float steerReturnSpeed = 260f;

        [Header("Suspension")]
        public float wheelRadius = 0.34f;
        public float wheelMass = 22f;
        public float suspensionDistance = 0.24f;
        public float spring = 38000f;
        public float damper = 4200f;
        [Range(0f, 1f)] public float targetPosition = 0.45f;
        public float antiRollFront = 9000f;
        public float antiRollRear = 6500f;

        [Header("Tyres")]
        public float forwardStiffness = 1.35f;
        public float sidewaysStiffness = 1.55f;
        [Tooltip("Rear lateral grip multiplier while the handbrake is pulled.")]
        [Range(0f, 1f)] public float handbrakeRearGrip = 0.42f;
        [Tooltip("Rear lateral grip multiplier while power-oversteering (throttle + steering).")]
        [Range(0.5f, 1f)] public float powerOversteerGrip = 0.86f;

        [Header("Assists (arcade feel)")]
        [Tooltip("Torque that damps unwanted yaw. Lower = easier to slide.")]
        public float yawStability = 1.6f;
        [Tooltip("Extra yaw torque from steering input while sliding, to help rotate and catch drifts.")]
        public float driftSteerAssist = 0.9f;
        public float maxYawRate = 2.4f;
        [Tooltip("Keeps the car level in the air for cleaner landings.")]
        public float airStabilization = 2.5f;
        [Range(0f, 1f)] public float tractionControl = 0.35f;
        public float maxSpeedKph = 185f;
    }
}
