using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Gives the car's body colliders a low-friction, non-bouncy surface, so two cars in contact slide along each
    /// other and pushes are transmitted, instead of the bodies gripping, climbing or catapulting each other
    /// (the colliders had Unity's default friction, 0.6). Added to every car by the race manager.
    /// </summary>
    public class CarBodyContact : MonoBehaviour
    {
        [Tooltip("Friction of the car body against other cars and the scenery (wheels are unaffected).")]
        [Range(0f, 1f)] public float bodyFriction = 0.15f;
        [Tooltip("Bounciness of the body; kept near 0 so a bump never launches a car.")]
        [Range(0f, 1f)] public float bodyBounce = 0.05f;

        private void Awake()
        {
            var material = new PhysicsMaterial("CarBody")
            {
                dynamicFriction = bodyFriction,
                staticFriction = bodyFriction,
                bounciness = bodyBounce,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            foreach (var box in GetComponents<BoxCollider>()) box.sharedMaterial = material;
        }
    }
}
