using Rally.Car;
using Rally.Systems;
using UnityEngine;

namespace Rally.Audio
{
    /// <summary>
    /// Engine (rpm/load crossfade), turbo, tyres (surface aware), wind, impacts and landings.
    /// Any clip left empty is replaced by a synthesised placeholder.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarAudio : MonoBehaviour
    {
        [Header("Clips (optional — procedural fallback)")]
        [SerializeField] private AudioClip engineOnLoad;
        [SerializeField] private AudioClip engineOffLoad;
        [SerializeField] private AudioClip turbo;
        [SerializeField] private AudioClip gravelSlide;
        [SerializeField] private AudioClip tarmacSqueal;
        [SerializeField] private AudioClip rolling;
        [SerializeField] private AudioClip wind;
        [SerializeField] private AudioClip impact;
        [SerializeField] private AudioClip landing;

        [Header("Mix")]
        [SerializeField] private float engineVolume = 0.55f;
        [SerializeField] private float tyreVolume = 0.5f;
        [SerializeField] private float windVolume = 0.25f;
        [SerializeField] private float effectsVolume = 0.8f;
        [SerializeField] private float clipBaseRpm = ProceduralAudio.EngineBaseRpm;
        [SerializeField] private bool isPlayer = true;

        private CarController car;
        private AudioSource engineOn, engineOff, turboSource, slide, squeal, roll, windSource, oneShots;
        private float throttleSmoothed;

        public void SetIsPlayer(bool player) => isPlayer = player;

        private void Awake()
        {
            car = GetComponent<CarController>();

            engineOn = CreateLoop("EngineOnLoad", engineOnLoad != null ? engineOnLoad : ProceduralAudio.Engine(true));
            engineOff = CreateLoop("EngineOffLoad", engineOffLoad != null ? engineOffLoad : ProceduralAudio.Engine(false));
            turboSource = CreateLoop("Turbo", turbo != null ? turbo : ProceduralAudio.Turbo());
            slide = CreateLoop("GravelSlide", gravelSlide != null ? gravelSlide : ProceduralAudio.GravelSlide());
            squeal = CreateLoop("Squeal", tarmacSqueal != null ? tarmacSqueal : ProceduralAudio.Squeal());
            roll = CreateLoop("Rolling", rolling != null ? rolling : ProceduralAudio.Rolling());
            windSource = CreateLoop("Wind", wind != null ? wind : ProceduralAudio.Wind());
            oneShots = CreateSource("OneShots");

            if (impact == null) impact = ProceduralAudio.Impact();
            if (landing == null) landing = ProceduralAudio.Landing();
            car.Landed += OnLanded;
        }

        private void OnDestroy()
        {
            if (car != null) car.Landed -= OnLanded;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = isPlayer ? 0.65f : 1f;
            source.minDistance = isPlayer ? 10f : 6f;
            source.maxDistance = 260f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = isPlayer ? 0f : 0.4f;
            return source;
        }

        private AudioSource CreateLoop(string sourceName, AudioClip clip)
        {
            var source = CreateSource(sourceName);
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            // On the web a start offset makes Unity queue a second Play while the browser still has
            // audio locked; when it unlocks, the stale entry stops the channel and the loop stays silent.
            source.time = Random.Range(0f, clip.length * 0.9f);
#endif
            source.Play();
            return source;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var drivetrain = car.Drivetrain;
            if (drivetrain == null) return;

            // --- Engine
            float rpm = drivetrain.Rpm;
            if (drivetrain.IsLimiting) rpm -= Mathf.PingPong(Time.time * 4000f, 350f);
            float pitch = Mathf.Clamp(rpm / clipBaseRpm, 0.25f, 2.9f);
            throttleSmoothed = Mathf.Lerp(throttleSmoothed, car.EffectiveThrottle, 1f - Mathf.Exp(-10f * dt));
            float rpm01 = drivetrain.NormalizedRpm;

            engineOn.pitch = pitch;
            engineOff.pitch = pitch;
            engineOn.volume = engineVolume * throttleSmoothed * (0.55f + rpm01 * 0.45f);
            engineOff.volume = engineVolume * (1f - throttleSmoothed * 0.8f) * (0.35f + rpm01 * 0.3f);

            turboSource.pitch = 0.6f + rpm01 * 0.9f;
            turboSource.volume = engineVolume * 0.18f * throttleSmoothed * rpm01 * rpm01;

            // --- Tyres
            float speed = car.Body.linearVelocity.magnitude;
            float slipLoose = 0f, slipTarmac = 0f, rollAmount = 0f;
            int grounded = 0;
            foreach (var w in car.Wheels)
            {
                if (!w.IsGrounded) continue;
                grounded++;
                float slip = Mathf.Clamp01(Mathf.Abs(w.SidewaysSlip) * 1.8f + Mathf.Max(0f, Mathf.Abs(w.ForwardSlip) - 0.3f));
                var s = w.SurfaceProps;
                if (s.type == SurfaceType.Asphalt) slipTarmac += slip * s.skidNoise;
                else slipLoose += slip * s.skidNoise;
                rollAmount += s.rollingNoise;
            }
            if (grounded > 0)
            {
                slipLoose /= grounded;
                slipTarmac /= grounded;
                rollAmount /= grounded;
            }
            float speed01 = Mathf.Clamp01(speed / 35f);

            Blend(slide, tyreVolume * Mathf.Clamp01(slipLoose * 1.4f) * Mathf.Clamp01(speed / 6f), 0.8f + speed01 * 0.4f, dt);
            Blend(squeal, tyreVolume * 0.6f * Mathf.Clamp01(slipTarmac * 1.5f - 0.2f) * Mathf.Clamp01(speed / 8f), 0.9f + speed01 * 0.2f, dt);
            Blend(roll, tyreVolume * 0.7f * rollAmount * speed01, 0.7f + speed01 * 0.6f, dt);
            Blend(windSource, windVolume * speed01 * speed01, 0.8f + speed01 * 0.5f, dt);
        }

        private static void Blend(AudioSource source, float volume, float pitch, float dt)
        {
            source.volume = Mathf.Lerp(source.volume, volume, 1f - Mathf.Exp(-12f * dt));
            source.pitch = pitch;
        }

        private void OnLanded(float impactSpeed)
        {
            float v = Mathf.Clamp01(impactSpeed / 7f);
            oneShots.pitch = Random.Range(0.9f, 1.1f);
            oneShots.PlayOneShot(landing, effectsVolume * (0.35f + v * 0.65f));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.isTrigger) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 2.5f) return;
            oneShots.pitch = Random.Range(0.85f, 1.15f);
            oneShots.PlayOneShot(impact, effectsVolume * Mathf.Clamp01(speed / 18f));
        }
    }
}
