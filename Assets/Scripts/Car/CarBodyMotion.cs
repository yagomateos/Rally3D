using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Purely visual body movement on top of the physics: pitch under acceleration/braking,
    /// roll in corners, landing squash and small engine / road vibrations.
    /// </summary>
    public class CarBodyMotion : MonoBehaviour
    {
        [SerializeField] private CarController car;
        [SerializeField] private float pitchPerG = 3.2f;
        [SerializeField] private float rollPerG = 3.8f;
        [SerializeField] private float maxAngle = 5f;
        [SerializeField] private float stiffness = 90f;
        [SerializeField] private float damping = 11f;
        [SerializeField] private float landingSquash = 0.09f;
        [SerializeField] private float vibration = 0.004f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector2 angle;
        private Vector2 angleVelocity;
        private float heave;
        private float heaveVelocity;
        private Vector3 filteredAccel;

        public void SetCar(CarController controller) => car = controller;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            if (car != null) car.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (car != null) car.Landed -= OnLanded;
        }

        private void OnLanded(float impactSpeed) => heaveVelocity -= Mathf.Clamp(impactSpeed, 0f, 8f) * landingSquash * 6f;

        private void LateUpdate()
        {
            if (car == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            const float G = 9.81f;
            filteredAccel = Vector3.Lerp(filteredAccel, car.LocalAcceleration, 1f - Mathf.Exp(-8f * dt));
            bool grounded = car.GroundedWheels > 0;

            Vector2 target = grounded
                ? new Vector2(-filteredAccel.z / G * pitchPerG, filteredAccel.x / G * rollPerG)
                : Vector2.zero;
            target = Vector2.ClampMagnitude(target, maxAngle);

            // Critically damped-ish spring towards the target lean.
            Vector2 force = (target - angle) * stiffness - angleVelocity * damping;
            angleVelocity += force * dt;
            angle += angleVelocity * dt;

            float heaveForce = -heave * stiffness * 1.4f - heaveVelocity * damping;
            heaveVelocity += heaveForce * dt;
            heave += heaveVelocity * dt;
            heave = Mathf.Clamp(heave, -landingSquash * 1.5f, landingSquash);

            float speed01 = Mathf.Clamp01(car.SpeedKph / 140f);
            float bump = 0f;
            if (grounded && car.Wheels.Length > 0)
                bump = car.Wheels[0].SurfaceProps.bumpiness;
            float rpm01 = car.Drivetrain != null ? car.Drivetrain.NormalizedRpm : 0f;
            float t = Time.time;
            float shake = vibration * (rpm01 * 0.5f + speed01 * bump * 3f);
            Vector3 jitter = new Vector3(
                (Mathf.PerlinNoise(t * 31f, 0.3f) - 0.5f) * shake,
                (Mathf.PerlinNoise(0.7f, t * 37f) - 0.5f) * shake * 2f,
                0f);

            transform.localPosition = restPosition + Vector3.up * heave + jitter;
            transform.localRotation = restRotation * Quaternion.Euler(angle.x, 0f, angle.y);
        }
    }
}
