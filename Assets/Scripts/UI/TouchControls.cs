using Rally.Car;
using Rally.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>
    /// On-screen controls for phones, in two layouts the player can switch between (the choice is remembered):
    /// <list type="bullet">
    /// <item>BOTONES: steer by tilting the phone, ACELERAR / FRENAR pedals and REINICIAR. If the tilt sensor
    /// delivers nothing once the stage is running, IZQUIERDA / DERECHA buttons appear.</item>
    /// <item>MANDO: console-style pad — analog stick for steering and A (acelerar), B (frenar),
    /// X (freno de mano), Y (reiniciar). Tilt is off in this mode.</item>
    /// </list>
    /// PAUSA and the layout switch are always shown. Hidden on desktop; shown on mobile browsers or on the first touch.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        private const string ModeKey = "Rally.TouchLayout"; // 0 = BOTONES, 1 = MANDO

        private static readonly Color PadGreen = new Color(0.35f, 0.8f, 0.35f);
        private static readonly Color PadRed = new Color(0.95f, 0.3f, 0.25f);
        private static readonly Color PadBlue = new Color(0.3f, 0.55f, 1f);
        private static readonly Color PadYellow = new Color(1f, 0.82f, 0.2f);

        private RaceManager race;
        private PlayerCarInput playerInput;
        private CanvasGroup group;
        private GameObject buttonsLayout, padLayout;
        private TouchPedal throttle, brake, left, right, reset;
        private TouchPedal padA, padB, padX, padY, lookBack;
        private TouchJoystick stick;
        private Text modeLabel;
        private bool visible;
        private bool steerButtons;
        private bool padMode;
        private RaceManager.State lastState;

        private void Start()
        {
            race = RaceManager.Instance;
            if (race == null) { enabled = false; return; }

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15; // above the HUD (10), below the menus (20)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var root = UIFactory.Stretch("Touch", transform);
            group = root.gameObject.AddComponent<CanvasGroup>();

            BuildButtonsLayout(root);
            BuildPadLayout(root);

            var pause = Pedal("Pause", root, new Vector2(1f, 1f), new Vector2(-48f, -236f), new Vector2(200f, 80f), "PAUSA", 26);
            pause.Changed = pressed => { if (pressed) Pause(); };
            var mode = Pedal("Mode", root, new Vector2(1f, 1f), new Vector2(-48f, -330f), new Vector2(200f, 70f), "", 24);
            // Hold to look behind (both layouts), left of the screen under the timer panels.
            lookBack = Pedal("LookBack", root, new Vector2(0f, 1f), new Vector2(48f, -300f), new Vector2(200f, 70f), "ATRÁS", 24);
            lookBack.Changed = pressed => Rally.CameraSystem.RallyCamera.TouchLookBack = pressed;
            // Next camera view (the C key on a keyboard), so phones can reach the CABINA view too.
            var camera = Pedal("Camera", root, new Vector2(0f, 1f), new Vector2(48f, -386f), new Vector2(200f, 70f), "CÁMARA", 24);
            camera.Changed = pressed =>
            {
                if (!pressed) return;
                var rallyCamera = FindAnyObjectByType<Rally.CameraSystem.RallyCamera>();
                if (rallyCamera != null) rallyCamera.NextMode();
            };
            modeLabel = mode.GetComponentInChildren<Text>();
            mode.Changed = pressed => { if (pressed) SetPadMode(!padMode); };

            lastState = race.CurrentState;
            // Leaving the pause: the phone may be held differently now, so re-centre the tilt.
            race.PauseChanged += paused => { if (!paused && race.CurrentState == RaceManager.State.Racing) PlayerInput?.CalibrarInclinacion(); };
            SetPadMode(PlayerPrefs.GetInt(ModeKey, 0) == 1);
            SetVisible(Application.isMobilePlatform);
        }

        // ------------------------------------------------------------------ layouts

        private void BuildButtonsLayout(RectTransform root)
        {
            buttonsLayout = UIFactory.Stretch("Buttons", root).gameObject;
            var t = buttonsLayout.transform;

            // Throttle above the speedometer (bottom right), brake bottom left: one thumb each.
            throttle = Pedal("Throttle", t, new Vector2(1f, 0f), new Vector2(-70f, 250f), new Vector2(300f, 300f), "ACELERAR");
            throttle.Changed = pressed => PlayerInput?.AcelerarTactil(pressed);
            brake = Pedal("Brake", t, new Vector2(0f, 0f), new Vector2(70f, 90f), new Vector2(300f, 300f), "FRENAR");
            brake.Changed = pressed => PlayerInput?.FrenarTactil(pressed);

            // Fallback steering, only shown if the phone's tilt sensor gives no data (see Update).
            left = Pedal("Left", t, new Vector2(0f, 0f), new Vector2(70f, 90f), new Vector2(230f, 230f), "IZQUIERDA", 30);
            right = Pedal("Right", t, new Vector2(0f, 0f), new Vector2(330f, 90f), new Vector2(230f, 230f), "DERECHA", 30);
            left.Changed = right.Changed = _ => PlayerInput?.GirarTactil((right.IsPressed ? 1f : 0f) - (left.IsPressed ? 1f : 0f));
            left.gameObject.SetActive(false);
            right.gameObject.SetActive(false);

            reset = Pedal("Reset", t, new Vector2(0f, 0f), new Vector2(70f, 420f), new Vector2(200f, 90f), "REINICIAR", 26);
            reset.Changed = pressed => { if (pressed) ResetCar(); };
        }

        private void BuildPadLayout(RectTransform root)
        {
            padLayout = UIFactory.Stretch("Pad", root).gameObject;
            var t = padLayout.transform;

            // Analog stick, bottom left.
            var stickBase = UIFactory.Panel("Stick", t, new Vector2(0f, 0f), new Vector2(90f, 80f), new Vector2(320f, 320f),
                new Color(0.04f, 0.05f, 0.06f, 0.35f));
            stickBase.sprite = UIFactory.Circle;
            stickBase.raycastTarget = true;
            var ring = UIFactory.Panel("Ring", stickBase.transform, UIFactory.Center, Vector2.zero, new Vector2(300f, 300f),
                new Color(1f, 1f, 1f, 0.12f), UIFactory.Center);
            ring.sprite = UIFactory.Circle;
            var knob = UIFactory.Panel("Knob", stickBase.transform, UIFactory.Center, Vector2.zero, new Vector2(140f, 140f),
                new Color(1f, 1f, 1f, 0.55f), UIFactory.Center);
            knob.sprite = UIFactory.Circle;
            stick = stickBase.gameObject.AddComponent<TouchJoystick>();
            stick.Knob = knob.rectTransform;
            stick.Changed = value => PlayerInput?.GirarJoystick(value);

            // Console face buttons, bottom right (diamond above the speedometer).
            var c = new Vector2(-320f, 400f);
            padA = Face("A", t, c + new Vector2(0f, -110f), "ACELERAR", PadGreen);
            padA.Changed = pressed => PlayerInput?.AcelerarTactil(pressed);
            padB = Face("B", t, c + new Vector2(110f, 0f), "FRENAR", PadRed);
            padB.Changed = pressed => PlayerInput?.FrenarTactil(pressed);
            padX = Face("X", t, c + new Vector2(-110f, 0f), "FRENO MANO", PadBlue);
            padX.Changed = pressed => PlayerInput?.FrenoDeManoTactil(pressed);
            padY = Face("Y", t, c + new Vector2(0f, 110f), "REINICIAR", PadYellow);
            padY.Changed = pressed => { if (pressed) ResetCar(); };
        }

        private static TouchPedal Pedal(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            string label, int fontSize = 34)
        {
            var bg = UIFactory.Panel(name, parent, anchor, position, size, Color.white);
            bg.raycastTarget = true;
            UIFactory.Panel("Edge", bg.transform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size.x, 4f), UIFactory.Accent);
            var text = UIFactory.Label("Label", bg.transform, label, fontSize, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.AddShadow(text, 1.5f);
            return bg.gameObject.AddComponent<TouchPedal>();
        }

        /// <summary>Round console-style button: coloured letter with a small caption; lights up in its colour when held.</summary>
        private static TouchPedal Face(string letter, Transform parent, Vector2 position, string caption, Color color)
        {
            var bg = UIFactory.Panel("Pad" + letter, parent, new Vector2(1f, 0f), position, new Vector2(130f, 130f), Color.white, UIFactory.Center);
            bg.sprite = UIFactory.Circle;
            bg.raycastTarget = true;
            var rim = UIFactory.Panel("Rim", bg.transform, UIFactory.Center, Vector2.zero, new Vector2(130f, 130f),
                new Color(color.r, color.g, color.b, 0.35f), UIFactory.Center);
            rim.sprite = UIFactory.Circle;
            rim.transform.SetAsFirstSibling();
            var l = UIFactory.Label("Letter", bg.transform, UIFactory.Center, new Vector2(0f, 12f), new Vector2(130f, 64f),
                letter, 54, TextAnchor.MiddleCenter, color, FontStyle.Bold, UIFactory.Center);
            UIFactory.AddShadow(l, 2f);
            UIFactory.Label("Caption", bg.transform, UIFactory.Center, new Vector2(0f, -36f), new Vector2(130f, 22f),
                caption, 15, TextAnchor.MiddleCenter, UIFactory.TextMain, FontStyle.Bold, UIFactory.Center);
            var pedal = bg.gameObject.AddComponent<TouchPedal>();
            pedal.PressedColor = color;
            return pedal;
        }

        // Resolved on first use: the race registers its player in its own Start.
        private PlayerCarInput PlayerInput
        {
            get
            {
                if (playerInput == null && race.Player != null) playerInput = race.Player.GetComponent<PlayerCarInput>();
                return playerInput;
            }
        }

        // ------------------------------------------------------------------ runtime

        private void Update()
        {
            ApplyVisibility();
            // Touch laptops / tablets that do not report as mobile: show the controls on the first touch.
            if (!visible)
            {
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.isPressed) SetVisible(true);
            }

            // At GO, the way the phone is held becomes "straight ahead" for tilt steering.
            if (race.CurrentState != lastState)
            {
                if (race.CurrentState == RaceManager.State.Racing) PlayerInput?.CalibrarInclinacion();
                lastState = race.CurrentState;
            }

            // No tilt data a couple of seconds into the stage (sensor blocked or missing): steer with buttons.
            if (visible && !padMode && !steerButtons && race.CurrentState == RaceManager.State.Racing && race.StageTime > 2f
                && PlayerInput != null && !PlayerInput.TiltActive)
                ShowSteerButtons();
        }

        private void SetPadMode(bool pad)
        {
            ReleaseAll();
            padMode = pad;
            buttonsLayout.SetActive(!pad);
            padLayout.SetActive(pad);
            modeLabel.text = pad ? "BOTONES" : "MANDO"; // the layout this button switches to
            if (PlayerInput != null) PlayerInput.InclinacionActivada = !pad;
            else StartCoroutine(ApplyTiltWhenReady());
            PlayerPrefs.SetInt(ModeKey, pad ? 1 : 0);
            PlayerPrefs.Save();
        }

        private System.Collections.IEnumerator ApplyTiltWhenReady()
        {
            while (PlayerInput == null) yield return null;
            PlayerInput.InclinacionActivada = !padMode;
        }

        private void ShowSteerButtons()
        {
            steerButtons = true;
            left.gameObject.SetActive(true);
            right.gameObject.SetActive(true);
            // Left thumb steers; brake moves next to the throttle for the right thumb.
            var rt = (RectTransform)brake.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-400f, 250f);
            rt.sizeDelta = new Vector2(230f, 230f);
            var edge = (RectTransform)rt.Find("Edge");
            edge.sizeDelta = new Vector2(230f, 4f);
        }

        private void SetVisible(bool show)
        {
            visible = show;
            ApplyVisibility();
        }

        // Shown on phones, except under the main menu.
        private void ApplyVisibility()
        {
            bool show = visible && race.CurrentState != RaceManager.State.Menu;
            if (group.blocksRaycasts == show && group.alpha == (show ? 1f : 0f)) return;
            group.alpha = show ? 1f : 0f;
            group.interactable = show;
            group.blocksRaycasts = show;
        }

        private void ReleaseAll()
        {
            foreach (var p in new[] { throttle, brake, left, right, padA, padB, padX, padY, lookBack })
                if (p != null) p.Release();
            if (stick != null) stick.Release();
        }

        private void ResetCar()
        {
            race.PlayerReset(); // same rules and +5 s penalty as the keyboard / gamepad reset
        }

        private void Pause()
        {
            if (!race.BeforeStart && !race.ResultsShown && !race.IsPaused)
            {
                ReleaseAll();
                race.SetPaused(true);
            }
        }
    }
}
