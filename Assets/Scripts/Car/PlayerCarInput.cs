using Rally.Systems;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rally.Car
{
    /// <summary>
    /// Feeds keyboard / gamepad input to the car, with smoothing for digital keys.
    /// On phones it adds tilt steering (accelerometer) and on-screen pedals (<see cref="AcelerarTactil"/>,
    /// <see cref="FrenarTactil"/>); keyboard and gamepad keep working and take priority.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class PlayerCarInput : MonoBehaviour, ICarInputSource
    {
        [Tooltip("How fast keyboard steering ramps towards full lock (per second).")]
        [SerializeField] private float keyboardSteerRate = 4.5f;
        [Tooltip("How fast keyboard steering returns to centre (per second).")]
        [SerializeField] private float keyboardReturnRate = 7f;
        [SerializeField] private float pedalRate = 6f;
        [Tooltip("How fast the on-screen LEFT / RIGHT buttons ramp the steering (per second).")]
        [SerializeField] private float touchSteerRate = 9f;

        [Header("Tilt steering (mobile)")]
        [Tooltip("Steer by tilting the phone. Ignored on devices without an accelerometer.")]
        [SerializeField] private bool tiltSteering = true;
        [Tooltip("Degrees of left / right tilt for full lock at sensitivity 1 (MEDIA). Lower = more sensitive.")]
        [SerializeField, Range(10f, 45f)] private float tiltFullLockDegrees = 22f;
        [Tooltip("Player sensitivity multiplier (the pause menu cycles 0.75 / 1 / 1.3 / 1.65). Higher = less tilt needed.")]
        [Range(0.5f, 2f)] public float tiltSensitivity = 1f;
        [Tooltip("Degrees around the centre that are ignored, so holding the phone roughly level drives straight.")]
        [SerializeField, Range(0f, 8f)] private float tiltDeadzoneDegrees = 2f;
        [Tooltip("Response curve: 1 = linear like a steering wheel; slightly above 1 adds precision near the centre.")]
        [SerializeField, Range(1f, 2f)] private float tiltExponent = 1.15f;
        [Tooltip("One Euro filter: cutoff (Hz) while the phone is held still. Lower = less jitter, more lag.")]
        [SerializeField] private float tiltMinCutoff = 3f;
        [Tooltip("One Euro filter: how much the cutoff rises with tilt speed (per °/s). Higher = less lag on quick turns.")]
        [SerializeField] private float tiltBeta = 0.04f;
        [Tooltip("Tick if tilting right steers left on your phone / browser.")]
        [SerializeField] private bool invertTilt;

        [Header("Touch steering assist")]
        [Tooltip("The car narrows its steering lock with speed (34° -> 11° at 150 km/h), which is fine on keys " +
                 "but leaves tilt / joystick / touch without enough turn at speed. Multiplies their steering at " +
                 "top speed (never beyond the car's full lock). 1 = off.")]
        [SerializeField, Range(1f, 2f)] private float touchHighSpeedAssist = 1.6f;

        /// <summary>
        /// Phones only (main menu option): the car accelerates by itself unless braking, as many mobile racing
        /// games offer, so tilt players only steer and brake.
        /// </summary>
        public static bool AcelerarSolo
        {
            get
            {
                // On by default on phones: steering by tilt with a thumb held on a pedal was too much at once.
                if (autoThrottleCache < 0) autoThrottleCache = PlayerPrefs.GetInt("Rally.AutoThrottle", Application.isMobilePlatform ? 1 : 0);
                return autoThrottleCache == 1;
            }
            set
            {
                autoThrottleCache = value ? 1 : 0; // takes effect at once, also when changed in the menu
                PlayerPrefs.SetInt("Rally.AutoThrottle", autoThrottleCache);
                PlayerPrefs.Save();
            }
        }
        private static int autoThrottleCache = -1;

        /// <summary>Tilt sensitivity presets offered in the pause menu, with their Spanish labels.</summary>
        public static readonly float[] SensitivityLevels = { 0.75f, 1f, 1.3f, 1.65f };
        public static readonly string[] SensitivityNames = { "BAJA", "MEDIA", "ALTA", "MUY ALTA" };
        private const string SensitivityKey = "Rally.TiltSensitivity";

        private float steer;
        private float throttle;
        private float brake;
        private bool touchThrottle;
        private bool touchBrake;
        private bool touchHandbrake;
        private float touchSteer;
        private float joystickSteer;
        private float tiltOffsetDeg;
        private float lastRollDeg;
        private float filteredRoll, filteredRollSpeed;
        private bool filterPrimed;

        /// <summary>True once the phone's tilt sensor is delivering data (the touch UI uses it to decide
        /// whether it needs on-screen steering buttons as a fallback).</summary>
        public bool TiltActive { get; private set; }

        /// <summary>Tilt steering on/off at runtime (the on-screen gamepad mode turns it off).</summary>
        public bool InclinacionActivada { get; set; } = true;

        /// <summary>On-screen accelerator: acts like holding W / Up while <paramref name="presionado"/> is true.</summary>
        public void AcelerarTactil(bool presionado) => touchThrottle = presionado;

        /// <summary>On-screen brake / reverse: acts like holding S / Down while <paramref name="presionado"/> is true.</summary>
        public void FrenarTactil(bool presionado) => touchBrake = presionado;

        /// <summary>On-screen steering, for phones without a usable tilt sensor: -1 = left, 0 = none, 1 = right.</summary>
        public void GirarTactil(float direccion) => touchSteer = Mathf.Clamp(direccion, -1f, 1f);

        /// <summary>On-screen joystick: analog steering, -1 = full left, 1 = full right.</summary>
        public void GirarJoystick(float valor) => joystickSteer = Mathf.Clamp(valor, -1f, 1f);

        /// <summary>On-screen handbrake: acts like holding Space while <paramref name="presionado"/> is true.</summary>
        public void FrenoDeManoTactil(bool presionado) => touchHandbrake = presionado;

        /// <summary>Takes the way the phone is held right now as "straight ahead" (at GO and when leaving the pause).</summary>
        public void CalibrarInclinacion() => tiltOffsetDeg = TiltActive ? Mathf.Clamp(lastRollDeg, -20f, 20f) : 0f;

        /// <summary>Index into <see cref="SensitivityLevels"/> currently in use.</summary>
        public int NivelSensibilidad
        {
            get
            {
                int best = 1;
                for (int i = 0; i < SensitivityLevels.Length; i++)
                    if (Mathf.Abs(SensitivityLevels[i] - tiltSensitivity) < Mathf.Abs(SensitivityLevels[best] - tiltSensitivity)) best = i;
                return best;
            }
            set
            {
                int i = (value % SensitivityLevels.Length + SensitivityLevels.Length) % SensitivityLevels.Length;
                tiltSensitivity = SensitivityLevels[i];
                PlayerPrefs.SetFloat(SensitivityKey, tiltSensitivity);
                PlayerPrefs.Save();
            }
        }

        private CarController car;
        private bool touchSteering; // this frame's steering came from tilt, joystick or on-screen buttons

        private void Awake()
        {
            car = GetComponent<CarController>();
            car.InputSource = this;
            tiltSensitivity = PlayerPrefs.GetFloat(SensitivityKey, tiltSensitivity);
        }

        private void OnDisable()
        {
            // A button released while the car was disabled must not stay stuck on.
            touchThrottle = touchBrake = touchHandbrake = false;
            touchSteer = joystickSteer = 0f;
        }

        private void Update()
        {
            var input = RallyInput.Instance;
            if (input == null) return;

            float dt = Time.deltaTime;
            float targetSteer = Mathf.Clamp(input.Steer.ReadValue<float>() + touchSteer, -1f, 1f);
            bool analog = input.Steer.activeControl != null && input.Steer.activeControl.device is UnityEngine.InputSystem.Gamepad;
            bool hasTilt = ReadTilt(dt, out float tilt);
            touchSteering = false;

            if (analog)
            {
                // Slight response curve: precise around centre, full lock at the end of travel.
                steer = Mathf.Sign(targetSteer) * Mathf.Pow(Mathf.Abs(targetSteer), 1.35f);
            }
            else if (Mathf.Abs(joystickSteer) > 0.01f)
            {
                // On-screen joystick: analog, same response curve as a real stick.
                steer = Mathf.Sign(joystickSteer) * Mathf.Pow(Mathf.Abs(joystickSteer), 1.35f);
                touchSteering = true;
            }
            else if (hasTilt && Mathf.Abs(targetSteer) < 0.01f)
            {
                // No key held: the phone's tilt steers (already filtered in ReadTilt).
                steer = tilt;
                touchSteering = true;
            }
            else
            {
                float rate = Mathf.Abs(targetSteer) < Mathf.Abs(steer) || Mathf.Sign(targetSteer) != Mathf.Sign(steer)
                    ? keyboardReturnRate : keyboardSteerRate;
                if (touchSteer != 0f)
                {
                    rate = Mathf.Max(rate, touchSteerRate); // thumbs need a quicker response than keys
                    touchSteering = true;
                }
                steer = Mathf.MoveTowards(steer, targetSteer, rate * dt);
            }

            // Touch pedals behave exactly like a held key.
            float throttleTarget = Mathf.Max(Mathf.Clamp01(input.Throttle.ReadValue<float>()), touchThrottle ? 1f : 0f);
            float brakeTarget = Mathf.Max(Mathf.Clamp01(input.Brake.ReadValue<float>()), touchBrake ? 1f : 0f);
            if (Application.isMobilePlatform && AcelerarSolo && brakeTarget < 0.1f) throttleTarget = 1f;
            throttle = Mathf.MoveTowards(throttle, throttleTarget, pedalRate * dt);
            brake = Mathf.MoveTowards(brake, brakeTarget, pedalRate * 1.5f * dt);
        }

        /// <summary>
        /// Phone roll (left / right tilt) mapped to -1..1. False when there is no accelerometer or no data yet
        /// (desktop browsers), so keyboard steering is untouched there.
        /// Pipeline: roll angle in degrees -> One Euro filter (removes hand tremor without the lag of a fixed
        /// low-pass) -> minus the calibrated centre -> dead zone -> linear-ish curve -> full lock at N degrees.
        /// </summary>
        private bool ReadTilt(float dt, out float value)
        {
            value = 0f;
            if (!tiltSteering || !InclinacionActivada)
            {
                filterPrimed = false;
                return false;
            }
            if (!ReadGravity(out Vector3 a, out bool screenAxes)) return false;

            float g = a.magnitude;
            if (g < 0.1f) return false; // device exists but reports nothing (e.g. a laptop)
            a /= g; // unit-agnostic: works whether the platform reports g or m/s²
            TiltActive = true;

            // Screen-oriented readings: X is always "screen right" (portrait or landscape). Device-oriented
            // readings in landscape carry the lateral tilt on Y, signed by which side of the phone is down.
            float lateral = screenAxes || Screen.width <= Screen.height ? a.x : a.y * Mathf.Sign(a.x);
            if (invertTilt) lateral = -lateral;
            float roll = Mathf.Asin(Mathf.Clamp(lateral, -1f, 1f)) * Mathf.Rad2Deg;

            roll = OneEuro(roll, dt);
            lastRollDeg = roll;

            float fullLock = tiltFullLockDegrees / Mathf.Max(0.1f, tiltSensitivity);
            float centred = roll - tiltOffsetDeg;
            float magnitude = Mathf.Clamp01((Mathf.Abs(centred) - tiltDeadzoneDegrees) / Mathf.Max(1f, fullLock - tiltDeadzoneDegrees));
            value = Mathf.Sign(centred) * Mathf.Pow(magnitude, tiltExponent);
            return true;
        }

        /// <summary>One Euro filter (Casiez et al., CHI 2012): adaptive low-pass whose cutoff rises with speed.</summary>
        private float OneEuro(float x, float dt)
        {
            if (!filterPrimed || dt <= 0f)
            {
                filterPrimed = true;
                filteredRoll = x;
                filteredRollSpeed = 0f;
                return x;
            }
            float speed = (x - filteredRoll) / dt;
            filteredRollSpeed = Mathf.Lerp(filteredRollSpeed, speed, Alpha(1f, dt)); // speed estimate at 1 Hz
            float cutoff = tiltMinCutoff + tiltBeta * Mathf.Abs(filteredRollSpeed);
            filteredRoll = Mathf.Lerp(filteredRoll, x, Alpha(cutoff, dt));
            return filteredRoll;
        }

        private static float Alpha(float cutoffHz, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoffHz);
            return 1f / (1f + tau / dt);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Assets/Plugins/WebGL/RallyMotion.jslib: reads `devicemotion` directly, because Unity's own web
        // accelerometer never starts on Android Chrome unless the permissions API says "granted".
        [DllImport("__Internal")] private static extern void RallyMotion_Start();
        [DllImport("__Internal")] private static extern float RallyMotion_X();
        [DllImport("__Internal")] private static extern float RallyMotion_Y();
        [DllImport("__Internal")] private static extern float RallyMotion_Z();
        [DllImport("__Internal")] private static extern int RallyMotion_Samples();
        [DllImport("__Internal")] private static extern int RallyMotion_Source();
        [DllImport("__Internal")] private static extern int RallyMotion_Flags();
        [DllImport("__Internal")] private static extern string RallyMotion_Detail();
        private static bool motionStarted;

        /// <summary>One-line tilt sensor diagnosis for the pause menu on phones (Spanish UI).</summary>
        public static string SensorStatus
        {
            get
            {
                if (!motionStarted) return "SENSOR: SIN INICIAR";
                int flags = RallyMotion_Flags(), samples = RallyMotion_Samples(), source = RallyMotion_Source();
                string[] sources = { "?", "MOVIMIENTO", "SENSOR", "ORIENTACIÓN" };
                string head = samples > 0
                    ? $"SENSOR: OK ({sources[Mathf.Clamp(source, 0, 3)]})  X {RallyMotion_X():0.00}  Y {RallyMotion_Y():0.00}"
                    : "SENSOR: SIN DATOS";
                string detail = RallyMotion_Detail();
                return string.IsNullOrEmpty(detail) ? $"{head}   ·   CÓDIGO {flags}" : $"{head}   ·   CÓDIGO {flags}   ·   {detail}";
            }
        }

        private static bool ReadGravity(out Vector3 a, out bool screenAxes)
        {
            screenAxes = true; // the plugin already rotates to the screen orientation
            a = Vector3.zero;
            if (!motionStarted) { motionStarted = true; RallyMotion_Start(); }
            if (RallyMotion_Samples() == 0) return false;
            a = new Vector3(RallyMotion_X(), RallyMotion_Y(), RallyMotion_Z());
            return true;
        }
#else
        public static string SensorStatus
        {
            get
            {
                var accel = Accelerometer.current;
                return accel == null ? "SENSOR: NO HAY" : $"SENSOR: {(accel.enabled ? "OK" : "APAGADO")}  {accel.acceleration.ReadValue():0.00}";
            }
        }

        private static bool ReadGravity(out Vector3 a, out bool screenAxes)
        {
            a = Vector3.zero;
            screenAxes = InputSystem.settings.compensateForScreenOrientation;
            var accel = Accelerometer.current;
            if (accel == null) return false;
            if (!accel.enabled)
            {
                InputSystem.EnableDevice(accel); // sensors start disabled in the Input System
                return false;
            }
            a = accel.acceleration.ReadValue();
            return true;
        }
#endif

        /// <summary>
        /// Extra steering for touch controls at speed, mirroring CarController's speed-sensitive lock so the
        /// resulting wheel angle never exceeds the car's low-speed maximum.
        /// </summary>
        private float HighSpeedAssist()
        {
            var tuning = car != null ? car.Tuning : null;
            if (tuning == null || touchHighSpeedAssist <= 1f) return 1f;
            float speedT = Mathf.Clamp01(Mathf.Abs(car.ForwardSpeed) * 3.6f / tuning.steerAngleSpeedKph);
            float smooth = speedT * speedT * (3f - 2f * speedT);
            float lockNow = Mathf.Lerp(tuning.maxSteerAngle, tuning.highSpeedSteerAngle, smooth);
            float wanted = Mathf.Lerp(1f, touchHighSpeedAssist, smooth);
            return Mathf.Min(wanted, tuning.maxSteerAngle / Mathf.Max(1f, lockNow));
        }

        public CarInput ReadInput()
        {
            var input = RallyInput.Instance;
            var result = new CarInput
            {
                steer = touchSteering ? steer * HighSpeedAssist() : steer,
                throttle = throttle,
                brake = brake,
                handbrake = touchHandbrake || (input != null && input.Handbrake.IsPressed())
            };
            if (participant == null) participant = GetComponent<Rally.Systems.RaceParticipant>();
            return DrivingAssist.Apply(result, car, participant);
        }

        private Rally.Systems.RaceParticipant participant;
    }
}
