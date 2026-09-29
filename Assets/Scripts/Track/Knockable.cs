using Rally.Audio;
using Rally.Car;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// Light roadside prop (marker pole, tape post, fence) that a car knocks flying instead of stopping dead against.
    /// Until hit its collider is a trigger, so a car touching it only sets it off: it becomes a small physics body
    /// thrown along with the car, ignoring that car so it doesn't snag on it, and keeps colliding with the ground.
    /// </summary>
    public class Knockable : MonoBehaviour
    {
        [SerializeField] private float mass = 8f;
        [SerializeField, Range(0f, 1.5f)] private float carryFactor = 0.7f;
        [SerializeField] private float popUp = 3f;
        [SerializeField] private float hitVolume = 0.45f;

        public bool Knocked { get; private set; }

        private void Awake()
        {
            foreach (var c in GetComponents<Collider>()) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Knocked) return;
            var body = other.attachedRigidbody;
            if (body == null || body.GetComponent<CarController>() == null) return;
            Knock(body);
        }

        public void Knock(Rigidbody car)
        {
            Knocked = true;
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var c in GetComponents<Collider>())
            {
                c.isTrigger = false;
                foreach (var carCollider in car.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(c, carCollider);
            }
            Vector3 v = car.linearVelocity;
            rb.linearVelocity = v * carryFactor + Vector3.up * (popUp + v.magnitude * 0.08f) + Random.insideUnitSphere * 1.5f;
            rb.angularVelocity = Random.onUnitSphere * (4f + v.magnitude * 0.3f);
            AudioSource.PlayClipAtPoint(ProceduralAudio.Impact(), transform.position, hitVolume * Mathf.Clamp01(v.magnitude / 20f + 0.3f));
        }
    }
}
