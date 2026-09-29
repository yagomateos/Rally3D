using Rally.CameraSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rally.Car
{
    /// <summary>Player-only feedback: camera impulses and gamepad rumble for impacts, landings and slides.</summary>
    [RequireComponent(typeof(CarController))]
    public class CarFeedback : MonoBehaviour
    {
        [SerializeField] private RallyCamera rallyCamera;
        [SerializeField] private float impactShake = 0.05f;
        [SerializeField] private float landingShake = 0.07f;
        [SerializeField] private float rumbleStrength = 0.6f;

        private CarController car;
        private float rumbleImpulse;

        public void SetCamera(RallyCamera cam) => rallyCamera = cam;

        private void Awake()
        {
            car = GetComponent<CarController>();
            car.Landed += OnLanded;
        }

        private void OnDestroy()
        {
            if (car != null) car.Landed -= OnLanded;
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        private void OnDisable() => Gamepad.current?.SetMotorSpeeds(0f, 0f);

        private void OnLanded(float impactSpeed)
        {
            float s = Mathf.Clamp01(impactSpeed / 7f);
            if (rallyCamera != null) rallyCamera.AddImpulse(landingShake * (0.3f + s));
            rumbleImpulse = Mathf.Max(rumbleImpulse, 0.4f + s * 0.6f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.isTrigger) return;
            float impact = collision.relativeVelocity.magnitude;
            if (impact < 3f) return;
            float s = Mathf.Clamp01(impact / 20f);
            if (rallyCamera != null) rallyCamera.AddImpulse(impactShake * (0.5f + s * 2f));
            rumbleImpulse = Mathf.Max(rumbleImpulse, s);
        }

        private void Update()
        {
            var pad = Gamepad.current;
            rumbleImpulse = Mathf.MoveTowards(rumbleImpulse, 0f, Time.unscaledDeltaTime * 3f);
            if (pad == null) return;
            if (Time.timeScale <= 0f)
            {
                pad.SetMotorSpeeds(0f, 0f);
                return;
            }

            float slip = 0f;
            foreach (var w in car.Wheels)
                if (w.IsGrounded) slip = Mathf.Max(slip, Mathf.Abs(w.SidewaysSlip));
            float road = car.GroundedWheels > 0 ? Mathf.Clamp01(car.SpeedKph / 150f) * 0.08f : 0f;
            float low = Mathf.Clamp01(rumbleImpulse + road) * rumbleStrength;
            float high = Mathf.Clamp01(slip * 0.5f + (car.Drivetrain.IsLimiting ? 0.2f : 0f)) * rumbleStrength * 0.5f;
            pad.SetMotorSpeeds(low, high);
        }
    }
}
