using Rally.Car;
using UnityEngine;

namespace Rally.VFX
{
    /// <summary>Feeds wheel slip into the shared <see cref="SkidMarks"/> mesh.</summary>
    [RequireComponent(typeof(CarController))]
    public class CarSkidMarks : MonoBehaviour
    {
        [SerializeField] private float slipThreshold = 0.25f;
        [SerializeField] private float fullSlip = 0.9f;
        [SerializeField] private float rollingMarkStrength = 0.18f;

        private CarController car;
        private int idBase;

        private void Awake()
        {
            car = GetComponent<CarController>();
            idBase = GetInstanceID() * 8;
        }

        private void FixedUpdate()
        {
            var marks = SkidMarks.Instance;
            if (marks == null) return;

            float speed = car.Body.linearVelocity.magnitude;
            for (int i = 0; i < car.Wheels.Length; i++)
            {
                CarWheel w = car.Wheels[i];
                float intensity = 0f;
                if (w.IsGrounded && speed > 1.5f)
                {
                    float slip = Mathf.Max(Mathf.Abs(w.SidewaysSlip), Mathf.Abs(w.ForwardSlip) * 0.8f);
                    float slide = Mathf.InverseLerp(slipThreshold, fullSlip, slip);
                    // Loose surfaces also get faint rolling ruts.
                    float rolling = w.SurfaceProps.type == Systems.SurfaceType.Asphalt ? 0f : rollingMarkStrength;
                    intensity = Mathf.Max(slide, rolling) * w.SurfaceProps.skidMarkOpacity;
                }
                Vector3 normal = w.IsGrounded ? w.Hit.normal : Vector3.up;
                marks.AddMark(idBase + i, w.ContactPoint, normal, transform.forward, intensity);
            }
        }
    }
}
