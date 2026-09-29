using Rally.Systems;
using Rally.VFX;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// Trigger volume over a pine forest stretch. While the player car is inside, the
    /// <see cref="AtmosphereController"/> thickens the fog and darkens the ambient light.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FogZone : MonoBehaviour
    {
        [Tooltip("Fog density multiplier applied while inside.")]
        [SerializeField, Min(1f)] private float fogDensityMultiplier = 2.2f;
        [Tooltip("Fraction the ambient light is darkened while inside (0.2 = 20% darker).")]
        [SerializeField, Range(0f, 1f)] private float ambientDarkening = 0.2f;

        public float FogDensityMultiplier => fogDensityMultiplier;
        public float AmbientDarkening => ambientDarkening;

        // The car has several colliders: count them so enter/exit fire once per car.
        private int playerColliders;

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private static bool IsPlayer(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null) return false;
            var participant = body.GetComponent<RaceParticipant>();
            return participant != null && participant.IsPlayer;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (++playerColliders == 1 && AtmosphereController.Instance != null)
                AtmosphereController.Instance.EnterZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other) || playerColliders == 0) return;
            if (--playerColliders == 0 && AtmosphereController.Instance != null)
                AtmosphereController.Instance.ExitZone(this);
        }

        private void OnDisable()
        {
            if (playerColliders > 0 && AtmosphereController.Instance != null)
                AtmosphereController.Instance.ExitZone(this);
            playerColliders = 0;
        }
    }
}
