using Rally.Systems;
using Rally.Track;
using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Wraps a WheelCollider: surface detection, friction setup and the visual wheel pose.
    /// </summary>
    public class CarWheel : MonoBehaviour
    {
        [SerializeField] private WheelCollider wheelCollider;
        [SerializeField] private Transform visual;
        [SerializeField] private bool isFront;
        [SerializeField] private bool isLeft;
        [SerializeField] private SurfaceType offRoadSurface = SurfaceType.Grass;

        public WheelCollider Collider => wheelCollider;
        public bool IsFront => isFront;
        public bool IsLeft => isLeft;

        public bool IsGrounded { get; private set; }
        public WheelHit Hit => hit;
        public SurfaceType Surface { get; private set; }
        public SurfaceProperties SurfaceProps { get; private set; } = SurfaceProperties.Default;
        public float ForwardSlip { get; private set; }
        public float SidewaysSlip { get; private set; }
        /// <summary>0 = fully extended, 1 = fully compressed.</summary>
        public float Compression { get; private set; }
        public Vector3 ContactPoint { get; private set; }

        private WheelHit hit;
        private WheelFrictionCurve forwardCurve;
        private WheelFrictionCurve sidewaysCurve;

        public void Configure(WheelCollider collider, Transform visualTransform, bool front, bool left)
        {
            wheelCollider = collider;
            visual = visualTransform;
            isFront = front;
            isLeft = left;
        }

        public void Setup(CarTuning tuning)
        {
            wheelCollider.radius = tuning.wheelRadius;
            wheelCollider.mass = tuning.wheelMass;
            wheelCollider.suspensionDistance = tuning.suspensionDistance;
            wheelCollider.forceAppPointDistance = 0.12f;
            wheelCollider.wheelDampingRate = 0.35f;
            wheelCollider.suspensionSpring = new JointSpring
            {
                spring = tuning.spring,
                damper = tuning.damper,
                targetPosition = tuning.targetPosition
            };

            forwardCurve = new WheelFrictionCurve
            {
                extremumSlip = 0.42f, extremumValue = 1f,
                asymptoteSlip = 0.9f, asymptoteValue = 0.65f,
                stiffness = tuning.forwardStiffness
            };
            sidewaysCurve = new WheelFrictionCurve
            {
                extremumSlip = 0.24f, extremumValue = 1f,
                asymptoteSlip = 0.55f, asymptoteValue = 0.8f,
                stiffness = tuning.sidewaysStiffness
            };
            wheelCollider.forwardFriction = forwardCurve;
            wheelCollider.sidewaysFriction = sidewaysCurve;
        }

        public void SampleGround(SurfaceDatabase surfaces)
        {
            IsGrounded = wheelCollider.GetGroundHit(out hit);
            if (IsGrounded)
            {
                Surface = hit.collider is TerrainCollider && TerrainSurfaceMap.Instance != null
                    ? TerrainSurfaceMap.Instance.Sample(hit.point, offRoadSurface)
                    : SurfaceZone.Resolve(hit.collider, offRoadSurface);
                SurfaceProps = surfaces != null ? surfaces.Get(Surface) : SurfaceProperties.Default;
                ForwardSlip = hit.forwardSlip;
                SidewaysSlip = hit.sidewaysSlip;
                ContactPoint = hit.point;

                float travel = -wheelCollider.transform.InverseTransformPoint(hit.point).y - wheelCollider.radius;
                Compression = 1f - Mathf.Clamp01(travel / Mathf.Max(0.01f, wheelCollider.suspensionDistance));
            }
            else
            {
                ForwardSlip = 0f;
                SidewaysSlip = 0f;
                Compression = 0f;
            }
        }

        public void SetGrip(float forwardMultiplier, float sidewaysMultiplier, CarTuning tuning)
        {
            forwardCurve.stiffness = tuning.forwardStiffness * forwardMultiplier;
            sidewaysCurve.stiffness = tuning.sidewaysStiffness * sidewaysMultiplier;
            wheelCollider.forwardFriction = forwardCurve;
            wheelCollider.sidewaysFriction = sidewaysCurve;
        }

        private void LateUpdate()
        {
            if (visual == null || wheelCollider == null) return;
            wheelCollider.GetWorldPose(out var pos, out var rot);
            visual.SetPositionAndRotation(pos, rot);
        }
    }
}
