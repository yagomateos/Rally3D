using Rally.Car;
using Rally.Systems;
using UnityEngine;

namespace Rally.CameraSystem
{
    /// <summary>
    /// Rally chase camera: smooth follow with velocity-biased yaw, speed dependent distance and FOV,
    /// lean into corners, surface shake, impulse shake (landings, impacts) and terrain avoidance.
    /// Also offers bumper and hood views.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RallyCamera : MonoBehaviour
    {
        public enum Mode { Chase, FarChase, Hood, Bumper }

        [SerializeField] private CarController target;

        [Header("Chase")]
        [SerializeField] private float distance = 5.6f;
        [SerializeField] private float height = 1.75f;
        [SerializeField] private float lookHeight = 0.95f;
        [SerializeField] private float lookAhead = 2.5f;
        [SerializeField] private float extraDistanceAtSpeed = 1.3f;
        [SerializeField] private float speedForEffects = 44f;
        [SerializeField] private float driftDistanceFactor = 0.9f;
        [SerializeField, Range(0f, 1f)] private float velocityYawBias = 0.45f;
        [SerializeField] private float yawSmoothTime = 0.22f;
        [SerializeField] private float heightSmoothTime = 0.28f;
        [SerializeField] private float positionSharpness = 11f;

        [Header("Lens")]
        [SerializeField] private float baseFov = 58f;
        [SerializeField] private float maxFov = 74f;
        [SerializeField] private float fovSharpness = 3f;

        [Header("Motion")]
        [SerializeField] private float maxRoll = 3.5f;
        [SerializeField] private float rollFromYawRate = 1.6f;
        [SerializeField] private float speedShake = 0.05f;
        [SerializeField] private float impulseDecay = 3.5f;

        [Header("Collision")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float collisionRadius = 0.35f;
        [SerializeField] private float minGroundClearance = 0.6f;

        public Mode CurrentMode { get; private set; } = Mode.Chase;
        public CarController Target
        {
            get => target;
            set { target = value; SnapToTarget(); }
        }

        private Camera cam;
        private float yaw;
        private float yawVelocity;
        private float smoothedHeight;
        private float heightVelocity;
        private float roll;
        private float impulse;
        private float currentDistance;
        private float shakeTime;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            currentDistance = distance;
        }

        private void Start() => SnapToTarget();

        public void AddImpulse(float strength) => impulse = Mathf.Min(1.5f, impulse + strength);

        public void SnapToTarget()
        {
            if (target == null) return;
            yaw = target.transform.eulerAngles.y;
            yawVelocity = 0f;
            smoothedHeight = target.transform.position.y;
            currentDistance = distance;
            Vector3 pivot = target.transform.position + Vector3.up * lookHeight;
            transform.position = pivot - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance + Vector3.up * height;
            transform.LookAt(pivot);
        }

        private void Update()
        {
            var input = RallyInput.Instance;
            if (input != null && input.CycleCamera.WasPressedThisFrame())
                CurrentMode = (Mode)(((int)CurrentMode + 1) % 4);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Rigidbody body = target.Body;
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            float speed = velocity.magnitude;
            float speed01 = Mathf.Clamp01(speed / speedForEffects);

            UpdateShake(dt, speed01);

            if (CurrentMode == Mode.Hood || CurrentMode == Mode.Bumper)
                UpdateMountedView(dt, speed01);
            else
                UpdateChase(dt, velocity, speed01);

            float targetFov = Mathf.Lerp(baseFov, maxFov, speed01 * speed01);
            if (CurrentMode != Mode.Chase && CurrentMode != Mode.FarChase) targetFov += 6f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSharpness * dt));
        }

        private void UpdateChase(float dt, Vector3 velocity, float speed01)
        {
            Transform t = target.transform;
            float far = CurrentMode == Mode.FarChase ? 1.45f : 1f;

            // Yaw follows the car's heading, biased towards the direction of travel when sliding.
            float heading = t.eulerAngles.y;
            Vector3 planarVel = new Vector3(velocity.x, 0f, velocity.z);
            if (planarVel.sqrMagnitude > 16f && target.ForwardSpeed > 0f)
            {
                float velHeading = Quaternion.LookRotation(planarVel).eulerAngles.y;
                heading = Mathf.LerpAngle(heading, velHeading, velocityYawBias);
            }
            yaw = Mathf.SmoothDampAngle(yaw, heading, ref yawVelocity, yawSmoothTime);

            smoothedHeight = Mathf.SmoothDamp(smoothedHeight, t.position.y, ref heightVelocity, heightSmoothTime);

            float targetDistance = (distance + extraDistanceAtSpeed * speed01) * far;
            if (target.IsDrifting) targetDistance *= driftDistanceFactor;
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, 1f - Mathf.Exp(-2f * dt));

            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 carPos = new Vector3(t.position.x, smoothedHeight, t.position.z);
            Vector3 pivot = carPos + Vector3.up * lookHeight;
            Vector3 desired = pivot - yawRot * Vector3.forward * currentDistance + Vector3.up * (height * far - lookHeight);

            desired = ResolveCollision(pivot, desired);

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSharpness * dt));
            transform.position = KeepAboveGround(transform.position);

            Vector3 lookPoint = pivot + yawRot * Vector3.forward * lookAhead;
            Quaternion look = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

            float yawRate = target.Body != null ? Vector3.Dot(target.Body.angularVelocity, Vector3.up) : 0f;
            float targetRoll = Mathf.Clamp(-yawRate * rollFromYawRate * speed01, -maxRoll, maxRoll);
            roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-3f * dt));

            transform.rotation = look * Quaternion.Euler(ShakeOffset(), 0f, roll) * Quaternion.Euler(0f, ShakeOffset(0.5f) * 0.6f, 0f);
        }

        private void UpdateMountedView(float dt, float speed01)
        {
            Transform t = target.transform;
            Vector3 offset = CurrentMode == Mode.Hood ? new Vector3(0f, 1.32f, 0.35f) : new Vector3(0f, 0.72f, 2.15f);
            transform.position = t.TransformPoint(offset);
            Quaternion baseRot = Quaternion.LookRotation(t.forward, Vector3.Lerp(t.up, Vector3.up, 0.5f));
            transform.rotation = baseRot * Quaternion.Euler(ShakeOffset() * 0.6f, ShakeOffset(0.5f) * 0.4f, 0f);
        }

        private void UpdateShake(float dt, float speed01)
        {
            impulse = Mathf.MoveTowards(impulse, 0f, impulseDecay * dt * Mathf.Max(0.3f, impulse));
            shakeTime += dt * (9f + speed01 * 14f);
        }

        private float ShakeOffset(float phase = 0f)
        {
            float bumpiness = 0.35f;
            if (target.Wheels.Length > 0 && target.Wheels[0].IsGrounded) bumpiness = target.Wheels[0].SurfaceProps.bumpiness;
            float speed01 = Mathf.Clamp01(target.SpeedKph / 3.6f / speedForEffects);
            float amplitude = speedShake * speed01 * (0.4f + bumpiness) * (target.IsAirborne ? 0.2f : 1f) + impulse * 1.6f;
            return (Mathf.PerlinNoise(shakeTime, phase * 10f + 3.1f) * 2f - 1f) * amplitude * 4f;
        }

        private Vector3 ResolveCollision(Vector3 pivot, Vector3 desired)
        {
            Vector3 dir = desired - pivot;
            float dist = dir.magnitude;
            if (dist < 0.01f) return desired;
            if (Physics.SphereCast(pivot, collisionRadius, dir / dist, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
                return pivot + dir / dist * Mathf.Max(1.2f, hit.distance - 0.1f);
            return desired;
        }

        private Vector3 KeepAboveGround(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 30f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                float minY = hit.point.y + minGroundClearance;
                if (position.y < minY) position.y = minY;
            }
            return position;
        }
    }
}
