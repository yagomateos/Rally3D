using Rally.AI;
using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Makes the player's hits count: a rival the player runs into gets a push along the hit (and a spin when hit
    /// off-centre) and is briefly stunned (<see cref="AIDriver.Stun"/>). Without it, the AI's steering and the
    /// stability assist straighten a knocked rival at once, while the player — who has no such help — is the only
    /// one who ends up off the road. Added to the player's car by the race manager.
    /// </summary>
    public class PlayerContactPush : MonoBehaviour
    {
        [Tooltip("Impact speed (m/s, along the contact) below which a touch is just a touch.")]
        [SerializeField] private float threshold = 2f;
        [Tooltip("Extra push given to the rival, per m/s of impact.")]
        [SerializeField] private float pushPerMs = 0.35f;
        [SerializeField] private float maxPush = 4.5f;
        [Tooltip("Spin given to the rival when hit off-centre (rad/s per m/s of impact).")]
        [SerializeField] private float spinPerMs = 0.18f;
        [SerializeField] private float maxSpin = 2.2f;

        [Header("Continuous push (contact, even at low speed)")]
        [Tooltip("Force added to a rival while the player's car is pressing on it (newtons, at full throttle).")]
        public float pushForce = 5500f;
        [Tooltip("Share of the push force applied even without throttle (e.g. rolling into a car).")]
        [Range(0f, 1f)] public float pushForceNoThrottle = 0.35f;
        [Tooltip("No extra force once the rival already moves away faster than this (m/s): no catapulting.")]
        public float maxSeparationSpeed = 2.5f;

        private Rigidbody body;
        private CarController car;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            car = GetComponent<CarController>();
        }

        /// <summary>
        /// Contact that lasts: while the player presses on a rival (typically from behind), help the push through
        /// and make the AI yield instead of braking and steering against it.
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            var other = collision.rigidbody;
            if (other == null || other == body || collision.contactCount == 0) return;
            var rival = other.GetComponent<AIDriver>();
            if (rival == null || !rival.enabled) return;

            Vector3 away = Vector3.ProjectOnPlane(other.worldCenterOfMass - body.worldCenterOfMass, Vector3.up).normalized;
            float closing = Vector3.Dot(body.linearVelocity - other.linearVelocity, away); // > 0: pressing into it
            if (closing < -maxSeparationSpeed) return;
            float pressingForward = Vector3.Dot(body.transform.forward, away);              // the player is facing it
            if (pressingForward < 0.3f) return;

            rival.Yield();
            float throttle = car != null ? car.EffectiveThrottle : 0f;
            float share = Mathf.Lerp(pushForceNoThrottle, 1f, throttle) * pressingForward;
            other.AddForce(away * pushForce * share, ForceMode.Force);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var other = collision.rigidbody;
            if (other == null || other == body || collision.contactCount == 0) return;
            var rival = other.GetComponent<AIDriver>();
            if (rival == null || !rival.enabled) return;

            var contact = collision.GetContact(0);
            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (impact < threshold) return;

            // Push the rival away from the player, flat on the ground.
            Vector3 away = Vector3.ProjectOnPlane(other.worldCenterOfMass - body.worldCenterOfMass, Vector3.up).normalized;
            float push = Mathf.Min(maxPush, (impact - threshold) * pushPerMs);
            other.AddForce(away * push, ForceMode.VelocityChange);

            // Off-centre hits turn the rival round: which way depends on which end of it was struck.
            Vector3 lever = contact.point - other.worldCenterOfMass;
            float side = Mathf.Sign(Vector3.Dot(Vector3.Cross(lever, -away), Vector3.up));
            float offCentre = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(lever, other.transform.forward)) / 1.5f);
            other.AddTorque(Vector3.up * side * Mathf.Min(maxSpin, impact * spinPerMs) * offCentre, ForceMode.VelocityChange);

            rival.Stun(Mathf.Clamp(impact * 0.12f, 0.4f, 1.4f));
        }
    }
}
