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

        private Rigidbody body;

        private void Awake() => body = GetComponent<Rigidbody>();

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
