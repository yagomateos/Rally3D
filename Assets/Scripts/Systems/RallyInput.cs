using UnityEngine;
using UnityEngine.InputSystem;

namespace Rally.Systems
{
    /// <summary>
    /// Central input map (keyboard + gamepad) built in code so it needs no extra assets.
    /// Driving axes are read by <see cref="Car.PlayerCarInput"/>, menu actions by UI and game flow.
    /// </summary>
    public class RallyInput : MonoBehaviour
    {
        public static RallyInput Instance { get; private set; }

        public InputAction Throttle { get; private set; }
        public InputAction Brake { get; private set; }
        public InputAction Steer { get; private set; }
        public InputAction Handbrake { get; private set; }
        public InputAction ResetCar { get; private set; }
        public InputAction Pause { get; private set; }
        public InputAction Confirm { get; private set; }
        public InputAction CycleCamera { get; private set; }
        public InputAction RestartStage { get; private set; }
        public InputAction LookBack { get; private set; }

        private InputActionMap map;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildMap();
            map.Enable();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            map?.Disable();
            map?.Dispose();
            Instance = null;
        }

        private void BuildMap()
        {
            map = new InputActionMap("Rally");

            Throttle = map.AddAction("Throttle", InputActionType.Value);
            Throttle.AddBinding("<Keyboard>/w");
            Throttle.AddBinding("<Keyboard>/upArrow");
            Throttle.AddBinding("<Gamepad>/rightTrigger");

            Brake = map.AddAction("Brake", InputActionType.Value);
            Brake.AddBinding("<Keyboard>/s");
            Brake.AddBinding("<Keyboard>/downArrow");
            Brake.AddBinding("<Gamepad>/leftTrigger");

            Steer = map.AddAction("Steer", InputActionType.Value);
            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            Steer.AddBinding("<Gamepad>/leftStick/x").WithProcessor("axisDeadzone(min=0.08,max=0.95)");

            Handbrake = map.AddAction("Handbrake", InputActionType.Button);
            Handbrake.AddBinding("<Keyboard>/space");
            Handbrake.AddBinding("<Gamepad>/buttonEast");
            Handbrake.AddBinding("<Gamepad>/rightShoulder");

            ResetCar = map.AddAction("Reset", InputActionType.Button);
            ResetCar.AddBinding("<Keyboard>/r");
            ResetCar.AddBinding("<Gamepad>/buttonNorth");

            Pause = map.AddAction("Pause", InputActionType.Button);
            Pause.AddBinding("<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");
#if UNITY_WEBGL && !UNITY_EDITOR
            // In browser fullscreen Esc is swallowed to leave fullscreen and never reaches the game.
            Pause.AddBinding("<Keyboard>/p");
#endif

            Confirm = map.AddAction("Confirm", InputActionType.Button);
            Confirm.AddBinding("<Keyboard>/enter");
            Confirm.AddBinding("<Keyboard>/numpadEnter");
            Confirm.AddBinding("<Gamepad>/buttonSouth");

            CycleCamera = map.AddAction("Camera", InputActionType.Button);
            CycleCamera.AddBinding("<Keyboard>/c");
            CycleCamera.AddBinding("<Gamepad>/select");

            RestartStage = map.AddAction("Restart", InputActionType.Button);
            RestartStage.AddBinding("<Keyboard>/backspace");
            RestartStage.AddBinding("<Gamepad>/buttonWest");

            // Hold to look behind. Down arrow is already brake / reverse, so Q; on a pad, press the right stick.
            LookBack = map.AddAction("LookBack", InputActionType.Button);
            LookBack.AddBinding("<Keyboard>/q");
            LookBack.AddBinding("<Gamepad>/rightStickPress");
        }
    }
}
