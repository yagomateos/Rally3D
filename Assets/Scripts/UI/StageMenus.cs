using Rally.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>Title card before the start, pause menu and the stage results screen.</summary>
    public class StageMenus : MonoBehaviour
    {
        private RaceManager race;
        private RectTransform root;
        private CanvasGroup intro, pause, results;
        private Text sensorStatus, sensitivityLabel;
        private Text introPrompt, resultTime, resultBest, resultPosition, resultNewBest, resultStandings;
        private Button pauseDefault, resultsDefault;

#if UNITY_WEBGL && !UNITY_EDITOR
        private static string StartPrompt => Application.isMobilePlatform ? "TOCA LA PANTALLA PARA EMPEZAR" : "HAZ CLIC O PULSA  ENTER  /  A  PARA EMPEZAR";
        private const string PauseKeys = "ESC / P  PAUSA";
#else
        private const string StartPrompt = "PULSA  ENTER  /  A  PARA EMPEZAR";
        private const string PauseKeys = "ESC  PAUSA";
#endif

        private void Start()
        {
            race = RaceManager.Instance;
            EnsureEventSystem();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = UIFactory.Stretch("Menus", transform);

            new GameObject("MainMenu").AddComponent<MainMenu>(); // removes itself unless the stage opens on the menu
            BuildIntro();
            BuildPause();
            BuildResults();

            Show(intro, race.CurrentState == RaceManager.State.Intro); // hidden under the main menu
            // The title card has no buttons of its own: let taps reach the phone controls under it
            // (e.g. the MANDO / BOTONES layout switch). Starting the stage is read from input, not from UI.
            intro.blocksRaycasts = false;
            Show(pause, false);
            Show(results, false);

            race.PauseChanged += OnPauseChanged;
            race.PlayerResultsReady += OnResults;
        }

        private void OnDestroy()
        {
            if (race == null) return;
            race.PauseChanged -= OnPauseChanged;
            race.PlayerResultsReady -= OnResults;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static void Show(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private CanvasGroup Screen(string name, Color backdrop)
        {
            var rt = UIFactory.Stretch(name, root);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = backdrop;
            return rt.gameObject.AddComponent<CanvasGroup>();
        }

        // ------------------------------------------------------------------ intro

        private void BuildIntro()
        {
            intro = Screen("Intro", new Color(0f, 0f, 0f, 0.35f));
            var c = new Vector2(0.5f, 0.5f);
            var band = UIFactory.Panel("Band", intro.transform, c, new Vector2(0f, 40f), new Vector2(0f, 330f), UIFactory.PanelDark);
            // Full-width at any aspect ratio (a fixed width left gaps on ultra-wide screens).
            band.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            var line = UIFactory.Panel("Line", band.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f), UIFactory.Accent);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);

            var tag = UIFactory.Panel("Tag", intro.transform, c, new Vector2(0f, 165f), new Vector2(210f, 48f), UIFactory.Accent);
            UIFactory.Label("Text", tag.transform, race.Stage.stageNumber, 32, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.08f));

            var title = UIFactory.Label("Title", intro.transform, c, new Vector2(0f, 82f), new Vector2(1600f, 110f),
                race.Stage.stageName, 96, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.AddShadow(title, 3f);

            float km = race.Path.StageLength / 1000f;
            UIFactory.Label("Info", intro.transform, c, new Vector2(0f, 12f), new Vector2(1600f, 40f),
                $"{km:0.0} KM   ·   GRAVA · TIERRA · BARRO · ASFALTO   ·   NUBLADO, MOJADO", 28, TextAnchor.MiddleCenter, UIFactory.TextDim);
            UIFactory.Label("Best", intro.transform, c, new Vector2(0f, -36f), new Vector2(1600f, 36f),
                "MEJOR TIEMPO  " + RaceManager.FormatTime(race.BestTime), 26, TextAnchor.MiddleCenter, UIFactory.TextMain);

            introPrompt = UIFactory.Label("Prompt", intro.transform, c, new Vector2(0f, -210f), new Vector2(1600f, 50f),
                StartPrompt, 38, TextAnchor.MiddleCenter, UIFactory.Accent);
            UIFactory.AddShadow(introPrompt, 2f);

            // Two lines: the Spanish control list is too long for one line clear of the speedometer.
            UIFactory.Label("Controls", intro.transform, new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(1100f, 56f),
                Application.isMobilePlatform
                    ? "INCLINA EL MÓVIL PARA GIRAR     ACELERAR / FRENAR     REINICIAR     PAUSA\n" +
                      "BOTÓN MANDO: JOYSTICK PARA GIRAR  ·  A ACELERAR  ·  B FRENAR  ·  X FRENO DE MANO  ·  Y REINICIAR"
                    : "W / RT  ACELERAR     S / LT  FRENAR · MARCHA ATRÁS     A D / PALANCA  GIRAR     ESPACIO / B  FRENO DE MANO\n" +
                      "R / Y  REINICIAR     C  CÁMARA     " + PauseKeys,
                19, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f), FontStyle.Normal);
        }

        // ------------------------------------------------------------------ pause

        private void BuildPause()
        {
            pause = Screen("Pause", new Color(0.02f, 0.02f, 0.03f, 0.6f));
            var c = new Vector2(0.5f, 0.5f);
            UIFactory.Label("Title", pause.transform, c, new Vector2(0f, 190f), new Vector2(800f, 100f), "PAUSA", 84, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.Panel("Line", pause.transform, c, new Vector2(0f, 130f), new Vector2(120f, 4f), UIFactory.Accent);

            pauseDefault = UIFactory.Button("Resume", pause.transform, c, new Vector2(0f, 40f), new Vector2(420f, 70f), "CONTINUAR", () => race.SetPaused(false));
            UIFactory.Button("Restart", pause.transform, c, new Vector2(0f, -45f), new Vector2(420f, 70f), "REPETIR TRAMO", () => race.Restart());
            // Back to the main menu (car choice, options). Quitting the game lives in the main menu.
            UIFactory.Button("Menu", pause.transform, c, new Vector2(0f, -130f), new Vector2(420f, 70f), "SALIR AL MENÚ", () => race.ExitToMenu());

            if (Application.isMobilePlatform)
            {
                // Phones: tilt sensitivity (cycles BAJA / MEDIA / ALTA / MUY ALTA, remembered between sessions)
                // and a sensor diagnosis line, so a player can report why tilt steering does not work.
                var sensitivity = UIFactory.Button("Sensitivity", pause.transform, c, new Vector2(0f, -215f), new Vector2(520f, 70f), "",
                    () => CycleSensitivity());
                sensitivityLabel = sensitivity.GetComponentInChildren<Text>();
                sensorStatus = UIFactory.Label("Sensor", pause.transform, c, new Vector2(0f, -300f), new Vector2(1400f, 34f),
                    "", 24, TextAnchor.MiddleCenter, UIFactory.TextDim, FontStyle.Normal);
            }
        }

        private void OnPauseChanged(bool paused)
        {
            Show(pause, paused);
            if (paused && sensorStatus != null) sensorStatus.text = Rally.Car.PlayerCarInput.SensorStatus;
            if (paused) RefreshSensitivityLabel();
            if (paused) EventSystem.current?.SetSelectedGameObject(pauseDefault.gameObject);
        }

        private Rally.Car.PlayerCarInput PlayerInput => race.Player != null ? race.Player.GetComponent<Rally.Car.PlayerCarInput>() : null;

        private void CycleSensitivity()
        {
            var input = PlayerInput;
            if (input == null) return;
            input.NivelSensibilidad = input.NivelSensibilidad + 1;
            RefreshSensitivityLabel();
        }

        private void RefreshSensitivityLabel()
        {
            var input = PlayerInput;
            if (sensitivityLabel == null || input == null) return;
            sensitivityLabel.text = "SENSIBILIDAD INCLINACIÓN:  " + Rally.Car.PlayerCarInput.SensitivityNames[input.NivelSensibilidad];
        }

        // ------------------------------------------------------------------ results

        private void BuildResults()
        {
            results = Screen("Results", new Color(0.02f, 0.02f, 0.03f, 0.55f));
            var c = new Vector2(0.5f, 0.5f);
            var panel = UIFactory.Panel("Panel", results.transform, c, new Vector2(0f, 20f), new Vector2(900f, 640f), UIFactory.PanelDark);
            UIFactory.Panel("Top", panel.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 6f), UIFactory.Accent);

            UIFactory.Label("Stage", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(880f, 36f),
                $"{race.Stage.stageNumber}  ·  {race.Stage.stageName}", 26, TextAnchor.MiddleCenter, UIFactory.TextDim);
            var title = UIFactory.Label("Title", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(880f, 80f),
                "TRAMO COMPLETADO", 72, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.AddShadow(title, 2f);

            resultTime = UIFactory.Label("Time", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(880f, 64f),
                "", 54, TextAnchor.MiddleCenter, UIFactory.Accent);
            resultBest = UIFactory.Label("Best", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(880f, 40f),
                "", 34, TextAnchor.MiddleCenter, UIFactory.TextMain);
            resultNewBest = UIFactory.Label("NewBest", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -298f), new Vector2(880f, 30f),
                "", 24, TextAnchor.MiddleCenter, new Color(0.45f, 0.85f, 1f));
            resultPosition = UIFactory.Label("Position", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(880f, 40f),
                "", 30, TextAnchor.MiddleCenter, UIFactory.TextMain);
            resultStandings = UIFactory.Label("Standings", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(880f, 100f),
                "", 24, TextAnchor.MiddleCenter, UIFactory.TextDim, FontStyle.Normal);

            resultsDefault = UIFactory.Button("Restart", panel.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 60f), new Vector2(270f, 64f),
                "REPETIR", () => race.Restart());
            UIFactory.Button("Menu", panel.transform, new Vector2(0.5f, 0f), new Vector2(150f, 60f), new Vector2(270f, 64f),
                "MENÚ", () => race.ExitToMenu());
        }

        private void OnResults()
        {
            var player = race.Player;
            resultTime.text = "TIEMPO: " + RaceManager.FormatTime(player.FinishTime);
            resultBest.text = "MEJOR: " + RaceManager.FormatTime(race.BestTime);
            resultNewBest.text = race.NewBest ? "¡NUEVO RÉCORD PERSONAL!" : "";
            resultPosition.text = $"POSICIÓN  {race.GetPosition(player)} / {race.Participants.Count}";
            RefreshStandings();

            Show(results, true);
            EventSystem.current?.SetSelectedGameObject(resultsDefault.gameObject);
        }

        private void Update()
        {
            if (race == null) return;
            bool showIntro = race.CurrentState == RaceManager.State.Intro;
            if (intro.alpha > 0f && !showIntro)
            {
                intro.alpha = Mathf.MoveTowards(intro.alpha, 0f, Time.unscaledDeltaTime * 4f);
                intro.interactable = false;
                intro.blocksRaycasts = false;
            }
            if (showIntro)
                introPrompt.color = new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b,
                    0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f));

            // Standings on the results page keep updating as rivals finish.
            if (results.alpha > 0f && Time.frameCount % 30 == 0) RefreshStandings();
        }

        private void RefreshStandings()
        {
            var sb = new System.Text.StringBuilder();
            var standings = race.GetStandings();
            for (int i = 0; i < standings.Count; i++)
            {
                var p = standings[i];
                string time = p.HasFinished ? RaceManager.FormatTime(p.FinishTime) : $"{p.Progress01 * 100f:0}%";
                sb.AppendLine($"{i + 1}.  {p.DisplayName}    {time}");
            }
            resultStandings.text = sb.ToString();
        }
    }
}
