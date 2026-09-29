using Rally.Car;
using Rally.Systems;
using UnityEngine;

namespace Rally.VFX
{
    /// <summary>
    /// Surface reactive wheel effects: billowing dust clouds, close-to-wheel puffs and flying
    /// gravel / mud. Emission scales with speed, wheel slip and throttle.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarDustEffects : MonoBehaviour
    {
        [SerializeField] private Material dustMaterial;
        [SerializeField] private Material debrisMaterial;

        [Header("Dust cloud")]
        [SerializeField] private float cloudRate = 22f;
        [SerializeField] private float cloudSlipRate = 45f;
        [SerializeField] private Vector2 cloudSize = new Vector2(2.2f, 4.2f);
        [SerializeField] private Vector2 cloudLifetime = new Vector2(2.8f, 5f);

        [Header("Wheel puffs")]
        [SerializeField] private float puffRate = 22f;
        [SerializeField] private Vector2 puffSize = new Vector2(0.45f, 1.1f);

        [Header("Debris")]
        [SerializeField] private float debrisRate = 55f;

        [Header("Shared")]
        [SerializeField] private float effectSpeedRange = 30f;
        [SerializeField] private float rearWheelBoost = 1.35f;
        [SerializeField, Range(0f, 1.5f)] private float lightingTint = 1.05f;

        [Header("Visibility")]
        [Tooltip("Dust cloud opacity multiplier. Lower = the road ahead stays visible behind other cars.")]
        [SerializeField, Range(0f, 1f)] private float cloudOpacity = 0.55f;
        [Tooltip("Dust cloud lifetime multiplier. Lower = clouds clear sooner instead of walling off the stage.")]
        [SerializeField, Range(0.2f, 1f)] private float cloudLifetimeScale = 0.55f;
        [Tooltip("Cloud and puff emission of rival cars relative to the player's (their dust is always in front of the camera).")]
        [SerializeField, Range(0f, 1f)] private float rivalDustScale = 0.4f;

        private CarController car;
        private float emissionScale = 1f;

        /// <summary>Soft particle material, reused for engine smoke by <see cref="CarDamage"/>.</summary>
        public Material DustMaterial => dustMaterial;
        private ParticleSystem cloud;
        private ParticleSystem puffs;
        private ParticleSystem debris;
        private float[] cloudAccum;
        private float[] puffAccum;
        private float[] debrisAccum;

        private void Awake()
        {
            car = GetComponent<CarController>();
            emissionScale = GetComponent<PlayerCarInput>() != null ? 1f : rivalDustScale;
            var root = new GameObject("DustFX").transform;
            root.SetParent(transform, false);

            cloud = ParticleFactory.Create(root, new ParticleFactory.Settings
            {
                name = "DustCloud", material = dustMaterial, maxParticles = 700, gravity = -0.015f,
                drag = 1.1f, growOverLife = true, noiseStrength = 0.6f, fadeIn = 0.12f,
                renderMode = ParticleSystemRenderMode.Billboard
            });
            puffs = ParticleFactory.Create(root, new ParticleFactory.Settings
            {
                name = "WheelPuffs", material = dustMaterial, maxParticles = 300, gravity = 0.05f,
                drag = 2.5f, growOverLife = true, noiseStrength = 0.25f, fadeIn = 0.05f,
                renderMode = ParticleSystemRenderMode.Billboard
            });
            debris = ParticleFactory.Create(root, new ParticleFactory.Settings
            {
                name = "Debris", material = debrisMaterial, maxParticles = 400, gravity = 1f,
                drag = 0.2f, growOverLife = false, fadeIn = 0.01f,
                renderMode = ParticleSystemRenderMode.Stretch, stretch = 0.03f
            });

            int n = car.Wheels.Length;
            cloudAccum = new float[n];
            puffAccum = new float[n];
            debrisAccum = new float[n];
            car.Landed += OnLanded;
        }

        private void OnDestroy()
        {
            if (car != null) car.Landed -= OnLanded;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 carVelocity = car.Body.linearVelocity;
            float speed = carVelocity.magnitude;
            float speed01 = Mathf.Clamp01((speed - 2f) / effectSpeedRange);
            float throttle = car.EffectiveThrottle;

            for (int i = 0; i < car.Wheels.Length; i++)
            {
                CarWheel wheel = car.Wheels[i];
                if (!wheel.IsGrounded) continue;

                SurfaceProperties surface = wheel.SurfaceProps;
                float slip = Mathf.Clamp01(Mathf.Abs(wheel.SidewaysSlip) * 2.2f + Mathf.Max(0f, Mathf.Abs(wheel.ForwardSlip) - 0.15f) * 1.8f);
                float boost = wheel.IsFront ? 1f : rearWheelBoost;
                float activity = speed01 * (0.6f + throttle * 0.4f) + slip * (0.4f + speed01);

                float cloudCount = emissionScale * surface.dustAmount * boost * (cloudRate * speed01 + cloudSlipRate * slip * Mathf.Max(0.3f, speed01)) * dt;
                float puffCount = emissionScale * surface.dustAmount * boost * puffRate * activity * dt;
                float debrisCount = surface.debrisAmount * boost * debrisRate * (slip + throttle * speed01 * 0.4f) * dt;

                Vector3 contact = wheel.ContactPoint;
                for (int k = Take(ref cloudAccum[i], cloudCount); k > 0; k--)
                    cloud.Emit(CloudParams(contact, carVelocity, surface, speed01), 1);
                for (int k = Take(ref puffAccum[i], puffCount); k > 0; k--)
                    puffs.Emit(PuffParams(contact, carVelocity, surface), 1);
                for (int k = Take(ref debrisAccum[i], debrisCount); k > 0; k--)
                    debris.Emit(DebrisParams(contact, carVelocity, surface, wheel), 1);
            }
        }

        /// <summary>Accumulates fractional emission and returns the whole particles to spawn now.</summary>
        private static int Take(ref float accumulator, float amount)
        {
            accumulator += amount;
            int whole = Mathf.FloorToInt(accumulator);
            accumulator -= whole;
            return whole;
        }

        private Color Tint(Color c, float alpha)
        {
            Color t = c * lightingTint;
            t.a = alpha;
            return t;
        }

        private ParticleSystem.EmitParams CloudParams(Vector3 contact, Vector3 carVelocity, SurfaceProperties surface, float speed01)
        {
            return new ParticleSystem.EmitParams
            {
                position = contact + Random.insideUnitSphere * 0.4f + Vector3.up * 0.35f - carVelocity.normalized * 0.6f,
                velocity = carVelocity * 0.18f + Random.insideUnitSphere * 1.2f + Vector3.up * Random.Range(0.3f, 1.3f),
                startSize = Random.Range(cloudSize.x, cloudSize.y) * (0.8f + speed01 * 0.5f) * surface.dustSize,
                startLifetime = Random.Range(cloudLifetime.x, cloudLifetime.y) * cloudLifetimeScale,
                startColor = Tint(surface.dustColor, Random.Range(0.3f, 0.5f) * cloudOpacity),
                rotation = Random.Range(0f, 360f)
            };
        }

        private ParticleSystem.EmitParams PuffParams(Vector3 contact, Vector3 carVelocity, SurfaceProperties surface)
        {
            return new ParticleSystem.EmitParams
            {
                position = contact + Random.insideUnitSphere * 0.2f + Vector3.up * 0.15f,
                velocity = carVelocity * 0.35f + Random.insideUnitSphere * 1.5f + Vector3.up * 0.6f,
                startSize = Random.Range(puffSize.x, puffSize.y) * surface.dustSize,
                startLifetime = Random.Range(0.45f, 0.9f),
                startColor = Tint(surface.dustColor, Random.Range(0.45f, 0.7f)),
                rotation = Random.Range(0f, 360f)
            };
        }

        private ParticleSystem.EmitParams DebrisParams(Vector3 contact, Vector3 carVelocity, SurfaceProperties surface, CarWheel wheel)
        {
            Vector3 back = -transform.forward * Random.Range(2f, 6f);
            Vector3 side = transform.right * (wheel.IsLeft ? -1f : 1f) * Random.Range(0f, 1.5f);
            return new ParticleSystem.EmitParams
            {
                position = contact + Vector3.up * 0.1f,
                velocity = carVelocity * 0.55f + back + side + Vector3.up * Random.Range(1.5f, 4.5f),
                startSize = Random.Range(0.035f, 0.09f),
                startLifetime = Random.Range(0.5f, 1.1f),
                startColor = Tint(surface.debrisColor, 1f)
            };
        }

        private void OnLanded(float impactSpeed)
        {
            float strength = Mathf.Clamp01(impactSpeed / 6f);
            foreach (var wheel in car.Wheels)
            {
                if (!wheel.IsGrounded) continue;
                int count = Mathf.RoundToInt(6 * strength * wheel.SurfaceProps.dustAmount);
                for (int k = 0; k < count; k++)
                    cloud.Emit(CloudParams(wheel.ContactPoint, car.Body.linearVelocity * 0.5f, wheel.SurfaceProps, 0.8f), 1);
            }
        }
    }
}
