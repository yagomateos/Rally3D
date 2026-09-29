using Rally.Car;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rally.VFX
{
    /// <summary>
    /// Speed-driven post-processing: vignette and motion blur grow with speed, and a slight negative
    /// lens distortion kicks in above a threshold. Works on a runtime copy of the volume profile,
    /// so the StagePostProcess asset on disk is never modified.
    /// </summary>
    public class SpeedPostProcess : MonoBehaviour
    {
        private const float MsToKph = 3.6f;

        /// <summary>Phones: keep only the speed vignette (motion blur and lens distortion are too costly).</summary>
        public static bool LowSpec { get; set; }

        [SerializeField] private Volume volume;
        [Tooltip("Car rigidbody. If empty, the player car inside the 'Cars' object is used.")]
        [SerializeField] private Rigidbody carBody;
        [SerializeField] private string carsRootName = "Cars";

        [Header("Speed range (km/h)")]
        [SerializeField] private float maxEffectKph = 180f;
        [SerializeField] private float distortionStartKph = 100f;

        [Header("Targets (x = stopped, y = max speed)")]
        [SerializeField] private Vector2 vignetteIntensity = new Vector2(0.2f, 0.38f);
        [SerializeField] private Vector2 motionBlurIntensity = new Vector2(0.05f, 0.3f);
        [SerializeField, Range(-1f, 0f)] private float maxLensDistortion = -0.25f;

        [Tooltip("How quickly effects follow the speed (higher = snappier).")]
        [SerializeField] private float smoothing = 3f;

        private VolumeProfile runtimeProfile;
        private Vignette vignette;
        private MotionBlur motionBlur;
        private LensDistortion lensDistortion;

        private void Start()
        {
            if (volume == null) volume = GetComponent<Volume>();
            if (carBody == null) carBody = FindPlayerBody();
            if (volume == null || carBody == null)
            {
                Debug.LogWarning($"{nameof(SpeedPostProcess)}: missing Volume or car Rigidbody, disabling.", this);
                enabled = false;
                return;
            }

            // Accessing .profile clones the shared asset; all changes stay in memory.
            runtimeProfile = volume.profile;

            if (!runtimeProfile.TryGet(out vignette)) vignette = runtimeProfile.Add<Vignette>();
            if (!runtimeProfile.TryGet(out motionBlur)) motionBlur = runtimeProfile.Add<MotionBlur>();
            if (!runtimeProfile.TryGet(out lensDistortion)) lensDistortion = runtimeProfile.Add<LensDistortion>();

            vignette.active = true;
            motionBlur.active = lensDistortion.active = !LowSpec;
            vignette.intensity.overrideState = true;
            motionBlur.intensity.overrideState = true;
            lensDistortion.intensity.overrideState = true;
        }

        private Rigidbody FindPlayerBody()
        {
            var root = GameObject.Find(carsRootName);
            if (root == null) return null;
            var player = root.GetComponentInChildren<PlayerCarInput>();
            return player != null ? player.GetComponent<Rigidbody>() : root.GetComponentInChildren<Rigidbody>();
        }

        private void Update()
        {
            float kph = carBody.linearVelocity.magnitude * MsToKph;
            float speed01 = Mathf.Clamp01(kph / maxEffectKph);
            float distortion01 = Mathf.InverseLerp(distortionStartKph, maxEffectKph, kph);

            // Frame-rate independent smoothing factor for Mathf.Lerp.
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);

            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value,
                Mathf.Lerp(vignetteIntensity.x, vignetteIntensity.y, speed01), t);
            motionBlur.intensity.value = Mathf.Lerp(motionBlur.intensity.value,
                Mathf.Lerp(motionBlurIntensity.x, motionBlurIntensity.y, speed01), t);
            lensDistortion.intensity.value = Mathf.Lerp(lensDistortion.intensity.value,
                maxLensDistortion * distortion01, t);
        }

        private void OnDestroy()
        {
            // The cloned profile is not owned by any asset, so release it explicitly.
            if (runtimeProfile != null) Destroy(runtimeProfile);
        }
    }
}
