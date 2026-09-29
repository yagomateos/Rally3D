using UnityEngine;

namespace Rally.CameraSystem
{
    /// <summary>
    /// Main menu camera: slowly orbits the player's car. On the main screens the car sits on the right (the menu
    /// is on the left); in the car selection ("showroom") it is centred and closer. Runs after the chase camera
    /// and simply overrides it; removed when the stage starts.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MenuCamera : MonoBehaviour
    {
        public Transform Target;
        public bool Showroom;

        [SerializeField] private float orbitSpeed = 9f;      // degrees per second
        [SerializeField] private float distance = 7.2f;
        [SerializeField] private float showroomDistance = 5.6f;
        [SerializeField] private float height = 1.9f;
        [SerializeField] private float sideOffset = 2.4f;     // shifts the car to the right of the screen
        [SerializeField] private float collisionRadius = 0.3f;

        private float angle = 150f;   // start looking at the car's front three-quarter
        private float blend;          // 0 = menu framing, 1 = showroom framing
        private int obstacleMask;

        private void Awake() => obstacleMask = ~LayerMask.GetMask("Vehicle");

        private void LateUpdate()
        {
            if (Target == null) return;
            float dt = Time.unscaledDeltaTime;
            angle += orbitSpeed * dt;
            blend = Mathf.MoveTowards(blend, Showroom ? 1f : 0f, dt * 2f);

            float dist = Mathf.Lerp(distance, showroomDistance, blend);
            Vector3 pivot = Target.position + Vector3.up * 0.7f;
            Quaternion orbit = Quaternion.Euler(0f, Target.eulerAngles.y + angle, 0f);
            // Showroom: a bit higher and aiming below the car, so it sits above the info panel at the bottom.
            float camHeight = Mathf.Lerp(height, height + 0.6f, blend);
            Vector3 wanted = pivot + orbit * new Vector3(0f, camHeight - 0.7f, -dist);

            // Keep the camera out of barriers, spectators and the hillside.
            Vector3 dir = wanted - pivot;
            if (Physics.SphereCast(pivot, collisionRadius, dir.normalized, out RaycastHit hit, dir.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
                wanted = pivot + dir.normalized * Mathf.Max(2.2f, hit.distance - 0.2f);

            transform.position = wanted;
            Vector3 right = Vector3.Cross(Vector3.up, (pivot - wanted).normalized);
            Vector3 look = pivot - right * sideOffset * (1f - blend) + Vector3.down * 1.3f * blend;
            transform.rotation = Quaternion.LookRotation(look - wanted, Vector3.up);
        }
    }
}
