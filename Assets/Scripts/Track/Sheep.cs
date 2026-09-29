using Rally.Audio;
using Rally.Car;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// A sheep crossing the road (coastal stage). Walks straight across at a steady pace with a little leg swing
    /// and body bob, bleats every few seconds (3D sound, only heard when close) and, if a car hits it, screams and
    /// is knocked away. It is a light physics body (70 kg), so the car loses little speed and takes little damage.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Sheep : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform[] legs = new Transform[0];

        [Header("Walking")]
        public float walkSpeed = 1.1f;
        [SerializeField] private float stepRate = 7f;
        [SerializeField] private float legSwing = 28f;

        [Header("Voice")]
        [SerializeField] private Vector2 bleatInterval = new Vector2(1.8f, 3.8f);
        [SerializeField] private float bleatVolume = 1f;
        [SerializeField] private float screamVolume = 1f;
        [Tooltip("Within this distance the bleat is at full volume; it fades out towards hearingDistance.")]
        [SerializeField] private float closeDistance = 6f;
        [SerializeField] private float hearingDistance = 90f;

        [Header("Hit")]
        [SerializeField] private float minHitSpeed = 2.5f;
        [SerializeField] private float removeAfterHit = 8f;

        private Rigidbody rb;
        private AudioSource voice;
        private Vector3 walkDirection;
        private float walkTimeLeft, nextBleat, stepPhase, stuckTime;
        private Vector3 bodyRest;

        public bool WasHit { get; private set; }
        public bool IsWalking => !WasHit && walkTimeLeft > 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 70f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // cars arrive very fast
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            // It is moved by velocity, so no friction to drag it back; nothing to bounce either.
            var col = GetComponent<Collider>();
            if (col != null)
                col.material = new PhysicsMaterial("Sheep")
                {
                    dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
                };

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Logarithmic;
            voice.minDistance = closeDistance;
            voice.maxDistance = hearingDistance;
            voice.dopplerLevel = 0f;

            if (body != null) bodyRest = body.localPosition;
            nextBleat = Random.Range(0.2f, 1f);
        }

        /// <summary>Starts crossing: walks along <paramref name="direction"/> for <paramref name="seconds"/>, then leaves.</summary>
        public void Walk(Vector3 direction, float seconds)
        {
            walkDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            walkTimeLeft = seconds;
            transform.rotation = Quaternion.LookRotation(walkDirection);
        }

        private void FixedUpdate()
        {
            if (WasHit) return;
            if (walkTimeLeft <= 0f)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                return;
            }
            Vector3 v = walkDirection * walkSpeed;
            Vector3 current = rb.linearVelocity;
            // Held back by a kerb or a bump: a little hop up, like a sheep stepping over it.
            float along = Vector3.Dot(current, walkDirection);
            stuckTime = along < walkSpeed * 0.4f ? stuckTime + Time.fixedDeltaTime : 0f;
            float up = stuckTime > 0.25f ? Mathf.Max(current.y, 1.6f) : current.y;
            if (stuckTime > 0.25f) stuckTime = 0f;
            rb.linearVelocity = new Vector3(v.x, up, v.z);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (WasHit) return;

            if (walkTimeLeft > 0f)
            {
                walkTimeLeft -= dt;
                if (walkTimeLeft <= 0f) { Destroy(gameObject, 0.5f); return; } // across and off into the scrub
                stepPhase += dt * stepRate;
                for (int i = 0; i < legs.Length; i++)
                {
                    // Diagonal pairs move together, like a real walk.
                    float s = Mathf.Sin(stepPhase + (i == 0 || i == 3 ? 0f : Mathf.PI)) * legSwing;
                    legs[i].localRotation = Quaternion.Euler(s, 0f, 0f);
                }
                if (body != null) body.localPosition = bodyRest + Vector3.up * Mathf.Abs(Mathf.Sin(stepPhase)) * 0.03f;
            }

            nextBleat -= dt;
            if (nextBleat <= 0f)
            {
                voice.pitch = Random.Range(0.9f, 1.15f);
                voice.PlayOneShot(ProceduralAudio.Bleat(), bleatVolume);
                nextBleat = Random.Range(bleatInterval.x, bleatInterval.y);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (WasHit) return;
            var car = collision.rigidbody != null ? collision.rigidbody.GetComponent<CarController>() : null;
            if (car == null || collision.relativeVelocity.magnitude < minHitSpeed) return;
            Hit(collision.rigidbody.linearVelocity);
        }

        /// <summary>Knocked by a car moving at <paramref name="carVelocity"/>: tumble away and scream.</summary>
        public void Hit(Vector3 carVelocity)
        {
            WasHit = true;
            rb.constraints = RigidbodyConstraints.None;
            float speed = carVelocity.magnitude;
            rb.AddForce(carVelocity * 0.35f + Vector3.up * (2.5f + speed * 0.12f), ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * (4f + speed * 0.2f), ForceMode.VelocityChange);

            voice.Stop();
            voice.pitch = Random.Range(0.95f, 1.08f);
            voice.minDistance = closeDistance * 2f; // the scream carries further
            voice.PlayOneShot(ProceduralAudio.SheepScream(), screamVolume);
            Destroy(gameObject, removeAfterHit);
        }
    }
}
