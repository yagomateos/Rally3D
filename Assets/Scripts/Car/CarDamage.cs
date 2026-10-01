using Rally.VFX;
using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Collision damage. Visual: the body mesh dents where it is hit and the engine smokes when badly damaged.
    /// Mechanical (optional): less engine power and a steering pull towards the damaged side. Damage is kept per
    /// zone (front, rear, left, right). Resetting to the road does not repair the car; restarting the stage does.
    /// Added at runtime to every car by the race manager.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarDamage : MonoBehaviour
    {
        public enum Mode { Completos = 0, SoloVisuales = 1, Desactivados = 2 }

        /// <summary>Damage setting chosen in the main menu (remembered between sessions).</summary>
        public static Mode Setting
        {
            get => (Mode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(ModeKey, (int)value); PlayerPrefs.Save(); }
        }
        public static readonly string[] ModeNames = { "COMPLETOS", "SOLO VISUALES", "DESACTIVADOS" };
        private const string ModeKey = "Rally.DamageMode";

        [Tooltip("Impact speed (m/s, along the contact normal) below which nothing happens.")]
        [SerializeField] private float impactThreshold = 4.5f;
        [Tooltip("Damage added per m/s of impact above the threshold.")]
        [SerializeField] private float damagePerMs = 0.035f;
        [Tooltip("Hits from below (hard landings) count this much.")]
        [SerializeField, Range(0f, 1f)] private float groundHitScale = 0.3f;
        [Tooltip("Engine power lost at full front damage.")]
        [SerializeField, Range(0f, 0.8f)] private float maxPowerLoss = 0.35f;
        [Tooltip("Steering pull at full damage on one side (fraction of full lock).")]
        [SerializeField, Range(0f, 0.3f)] private float maxSteerPull = 0.07f;
        [Header("Dents")]
        [SerializeField] private float dentRadius = 0.75f;
        [SerializeField] private float dentPerMs = 0.02f;
        [SerializeField] private float maxDent = 0.22f;
        [Tooltip("How uneven a dent is: each point of the body gives way a different amount (crumpled metal).")]
        [SerializeField, Range(0f, 1f)] private float crumple = 0.6f;

        private CarController car;
        private Rigidbody myBody;
        private float front, rear, left, right;
        private MeshFilter body;
        private Mesh bodyMesh;
        private Vector3[] original, vertices;
        private ParticleSystem smoke;
        private float smokeAccum;

        /// <summary>0 = intact, 1 = wrecked (for the HUD).</summary>
        public float Damage01
        {
            get
            {
                float worst = Mathf.Max(Mathf.Max(front, rear), Mathf.Max(left, right));
                return Mathf.Clamp01(worst * 0.6f + (front + rear + left + right) * 0.25f * 0.4f);
            }
        }

        private void Awake()
        {
            car = GetComponent<CarController>();
            myBody = GetComponent<Rigidbody>();
            var bodyTransform = transform.Find("Body");
            if (bodyTransform != null) body = bodyTransform.GetComponent<MeshFilter>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Setting == Mode.Desactivados || collision.collider.isTrigger || collision.contactCount == 0) return;
            var contact = collision.GetContact(0);
            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            // Something light that gets knocked away (a sheep) hurts far less than a wall or another car.
            var other = collision.rigidbody;
            if (other != null && !other.isKinematic && myBody != null)
                impact *= Mathf.Clamp01(other.mass / myBody.mass * 4f);
            if (impact < impactThreshold) return;

            float over = impact - impactThreshold;
            float scale = contact.normal.y > 0.7f ? groundHitScale : 1f; // landing on the ground, not a crash
            float amount = over * damagePerMs * scale;

            // Which side took the hit, in the car's own axes.
            Vector3 local = transform.InverseTransformPoint(contact.point);
            if (Mathf.Abs(local.z) > Mathf.Abs(local.x) * 1.6f)
            {
                if (local.z > 0f) front = Mathf.Clamp01(front + amount); else rear = Mathf.Clamp01(rear + amount);
            }
            else if (local.x < 0f) left = Mathf.Clamp01(left + amount);
            else right = Mathf.Clamp01(right + amount);

            if (scale >= 1f) Dent(contact.point, contact.normal, over);
            ApplyMechanical();
        }

        /// <summary>Damage per zone: front, rear, left, right (0..1). Carried from stage to stage in a championship.</summary>
        public float[] Zones => new[] { front, rear, left, right };

        /// <summary>
        /// Puts back damage from the previous stage: the same zones (and their mechanical effects) and a dent in the
        /// middle of each damaged side, as deep as a hit that would have caused it.
        /// </summary>
        public void Restore(float[] zones)
        {
            if (zones == null || zones.Length < 4 || Setting == Mode.Desactivados) return;
            front = Mathf.Clamp01(zones[0]); rear = Mathf.Clamp01(zones[1]);
            left = Mathf.Clamp01(zones[2]); right = Mathf.Clamp01(zones[3]);
            ApplyMechanical();
            if (body == null) return;
            Transform t = body.transform;
            void DentAt(float amount, Vector3 local, Vector3 inward)
            {
                if (amount > 0.01f) Dent(t.TransformPoint(local), t.TransformDirection(inward), amount / damagePerMs);
            }
            DentAt(front, new Vector3(0f, 0.6f, 2.1f), Vector3.back);
            DentAt(rear, new Vector3(0f, 0.6f, -2.0f), Vector3.forward);
            DentAt(left, new Vector3(-0.95f, 0.6f, 0f), Vector3.right);
            DentAt(right, new Vector3(0.95f, 0.6f, 0f), Vector3.left);
        }

        private void ApplyMechanical()
        {
            if (Setting != Mode.Completos) { car.DamagePowerScale = 1f; car.DamageSteerBias = 0f; return; }
            car.DamagePowerScale = 1f - maxPowerLoss * front;
            car.DamageSteerBias = (right - left) * maxSteerPull; // a bent left corner pulls left, and vice versa
        }

        /// <summary>Pushes the body's vertices near the hit inwards (the body mesh is copied per car first).</summary>
        private void Dent(Vector3 worldPoint, Vector3 worldNormal, float over)
        {
            if (body == null) return;
            if (bodyMesh == null || body.sharedMesh != bodyMesh) // first dent, or the body model was swapped
            {
                bodyMesh = body.mesh; // instance: other cars keep their own shape
                original = bodyMesh.vertices;
                vertices = bodyMesh.vertices;
            }

            Transform t = body.transform;
            Vector3 p = t.InverseTransformPoint(worldPoint);
            Vector3 push = t.InverseTransformDirection(worldNormal).normalized; // the normal points into this car
            float depth = Mathf.Min(maxDent, over * dentPerMs);
            float r2 = dentRadius * dentRadius;
            bool changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                float d2 = (vertices[i] - p).sqrMagnitude;
                if (d2 > r2) continue;
                float falloff = 1f - Mathf.Sqrt(d2) / dentRadius;
                // Vertices that share a position (flat-shaded faces) get the same amount, so the body stays closed.
                float give = 1f + crumple * (Crumple(original[i]) * 2f - 1f);
                Vector3 moved = vertices[i] + push * depth * falloff * falloff * give;
                Vector3 total = moved - original[i];
                if (total.magnitude > maxDent) moved = original[i] + total.normalized * maxDent;
                vertices[i] = moved;
                changed = true;
            }
            if (!changed) return;
            bodyMesh.vertices = vertices;
            bodyMesh.RecalculateNormals();
            bodyMesh.RecalculateBounds();
        }

        private static float Crumple(Vector3 p)
        {
            float h = Mathf.Sin(Vector3.Dot(p, new Vector3(127.1f, 311.7f, 74.7f))) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private void Update()
        {
            // Engine smoke once the front is badly damaged (thicker at low speed, where it hangs around).
            if (Setting == Mode.Desactivados || front < 0.45f) return;
            if (smoke == null) CreateSmoke();
            if (smoke == null) return;
            float rate = Mathf.Lerp(4f, 18f, Mathf.InverseLerp(0.45f, 1f, front)) * Time.deltaTime;
            smokeAccum += rate;
            Vector3 origin = transform.TransformPoint(new Vector3(0f, 0.9f, 1.4f));
            while (smokeAccum >= 1f)
            {
                smokeAccum -= 1f;
                smoke.Emit(new ParticleSystem.EmitParams
                {
                    position = origin + Random.insideUnitSphere * 0.2f,
                    velocity = car.Body.linearVelocity * 0.3f + Vector3.up * Random.Range(0.6f, 1.4f),
                    startSize = Random.Range(0.5f, 1f),
                    startLifetime = Random.Range(1f, 1.8f),
                    startColor = new Color(0.12f, 0.12f, 0.13f, Random.Range(0.35f, 0.55f)),
                    rotation = Random.Range(0f, 360f)
                }, 1);
            }
        }

        private void CreateSmoke()
        {
            var dust = GetComponent<CarDustEffects>();
            if (dust == null || dust.DustMaterial == null) return;
            smoke = ParticleFactory.Create(transform, new ParticleFactory.Settings
            {
                name = "EngineSmoke", material = dust.DustMaterial, maxParticles = 120, gravity = -0.05f,
                drag = 0.8f, growOverLife = true, noiseStrength = 0.3f, fadeIn = 0.1f,
                renderMode = ParticleSystemRenderMode.Billboard
            });
        }
    }
}
