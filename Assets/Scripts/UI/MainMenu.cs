using Rally.CameraSystem;
using Rally.Car;
using Rally.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>
    /// Main menu shown over the live stage (camera orbiting the player's car): JUGAR → car selection → start,
    /// CONTROLES, OPCIONES (volume, tilt sensitivity on phones) and SALIR (not on the web, where quitting only
    /// freezes the canvas). Built in code like the HUD. Only active while the race is in <see cref="RaceManager.State.Menu"/>.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private const string VolumeKey = "Rally.Volume";
        private static readonly float[] Volumes = { 1f, 0.75f, 0.5f, 0.25f, 0f };

        private RaceManager race;
        private CanvasGroup mainScreen, stageScreen, carScreen, optionsScreen, controlsScreen, current;
        private Button firstStageButton;
        private int stageIndex;
        private Button playButton, startButton, volumeButton, controlsBack, optionsBack;
        private Text carName, carDescription, volumeLabel, sensitivityLabel, damageLabel, coDriverLabel, rivalsLabel, autoLabel, difficultyLabel, assistLabel, qualityLabel;
        private Image[][] statBars;
        private int carIndex;
        private MenuCamera menuCamera;

        /// <summary>Applies the saved master volume (also when the stage is reloaded without the menu).</summary>
        public static void ApplySavedVolume() => AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

        private void Start()
        {
            ApplySavedVolume();
            race = RaceManager.Instance;
            if (race == null || race.CurrentState != RaceManager.State.Menu) { Destroy(gameObject); return; }

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30; // above HUD, touch controls and in-race menus
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = UIFactory.Stretch("MainMenu", transform);

            BuildMain(root);
            BuildStageSelect(root);
            BuildCarSelect(root);
            BuildOptions(root);
            BuildControls(root);
            // Controller: up / down wrap round in each list of buttons.
            MenuNavigation.WrapColumn(mainScreen.transform);
            MenuNavigation.WrapColumn(stageScreen.transform);
            MenuNavigation.WrapColumn(optionsScreen.transform);

            var cam = Camera.main;
            if (cam != null)
            {
                menuCamera = cam.gameObject.AddComponent<MenuCamera>();
                menuCamera.Target = race.Player != null ? race.Player.transform : null;
            }

            carIndex = CarCatalog.Selected;
            stageIndex = StageCatalog.Current;
            Open(mainScreen, playButton);
        }

        // ------------------------------------------------------------------ screens

        private CanvasGroup Screen(string name, RectTransform root, bool leftPanel)
        {
            var rt = UIFactory.Stretch(name, root);
            if (leftPanel)
            {
                // Dark band on the left for the text; the car stays visible on the right.
                var band = UIFactory.Panel("Band", rt, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(820f, 0f), new Color(0.02f, 0.02f, 0.03f, 0.72f));
                band.rectTransform.anchorMin = new Vector2(0f, 0f);
                band.rectTransform.anchorMax = new Vector2(0f, 1f);
                band.rectTransform.pivot = new Vector2(0f, 0.5f);
                band.rectTransform.sizeDelta = new Vector2(820f, 0f);
                var edge = UIFactory.Panel("Edge", band.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(4f, 0f), UIFactory.Accent);
                edge.rectTransform.anchorMin = new Vector2(1f, 0f);
                edge.rectTransform.anchorMax = new Vector2(1f, 1f);
                edge.rectTransform.sizeDelta = new Vector2(4f, 0f);
            }
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            Hide(group);
            return group;
        }

        private static Text Title(Transform parent, string text, Vector2 position, int size = 72)
        {
            var t = UIFactory.Label("Title", parent, new Vector2(0f, 1f), position, new Vector2(760f, size + 20f), text, size, TextAnchor.MiddleLeft, UIFactory.TextMain);
            UIFactory.AddShadow(t, 3f);
            return t;
        }

        private static Button MenuButton(Transform parent, string name, float y, string label, UnityEngine.Events.UnityAction action, float width = 560f)
        {
            return UIFactory.Button(name, parent, new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(width, 84f), label, action);
        }

        private void BuildMain(RectTransform root)
        {
            mainScreen = Screen("Main", root, true);
            var t = mainScreen.transform;
            Title(t, "RALLY 3D", new Vector2(120f, -110f), 120);
            UIFactory.Panel("Line", t, new Vector2(0f, 1f), new Vector2(124f, -250f), new Vector2(160f, 6f), UIFactory.Accent);
            UIFactory.Label("Stage", t, new Vector2(0f, 1f), new Vector2(120f, -270f), new Vector2(700f, 40f),
                $"{race.Stage.stageNumber}  ·  {race.Stage.stageName}", 30, TextAnchor.MiddleLeft, UIFactory.TextDim);

            playButton = MenuButton(t, "Play", -380f, "JUGAR", () => Open(stageScreen, firstStageButton));
            MenuButton(t, "Controls", -484f, "CONTROLES", () => Open(controlsScreen, controlsBack));
            MenuButton(t, "Options", -588f, "OPCIONES", () => { RefreshOptions(); Open(optionsScreen, volumeButton); });
            // On the web this shows an exit screen (a page cannot close its own tab); elsewhere it quits.
            MenuButton(t, "Quit", -692f, "SALIR", () => race.Quit());

            UIFactory.Label("Best", t, new Vector2(0f, 0f), new Vector2(120f, 60f), new Vector2(700f, 36f),
                "MEJOR TIEMPO  " + RaceManager.FormatTime(race.BestTime), 26, TextAnchor.MiddleLeft, UIFactory.TextDim);
        }

        private void BuildStageSelect(RectTransform root)
        {
            stageScreen = Screen("Stages", root, true);
            var t = stageScreen.transform;
            Title(t, "ELIGE TRAMO", new Vector2(120f, -80f));
            // Compact cards so four stages plus DIFICULTAD and VOLVER fit a landscape phone (canvas ~966 high).
            const float cardHeight = 112f, cardStep = 120f;
            float y = -180f;
            for (int i = 0; i < StageCatalog.Stages.Length; i++)
            {
                int index = i;
                var stage = StageCatalog.Stages[i];
                var card = UIFactory.Button("Stage" + i, t, new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(620f, cardHeight), "", () =>
                {
                    stageIndex = index;
                    Open(carScreen, startButton);
                });
                var label = card.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.UpperLeft;
                label.text = "";
                UIFactory.Label("Number", card.transform, new Vector2(0f, 1f), new Vector2(24f, -8f), new Vector2(300f, 26f),
                    stage.number + (i == StageCatalog.Current ? "   ·   AQUÍ" : ""), 20, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f)); // readable on the orange highlight too
                // Best time on the same top row, right-aligned, to keep the card short.
                UIFactory.Label("Best", card.transform, new Vector2(0f, 1f), new Vector2(300f, -8f), new Vector2(296f, 26f),
                    "MEJOR  " + RaceManager.FormatTime(stage.BestTime), 19, TextAnchor.MiddleRight, UIFactory.TextMain, FontStyle.Normal);
                UIFactory.Label("Name", card.transform, new Vector2(0f, 1f), new Vector2(24f, -32f), new Vector2(580f, 40f),
                    stage.name, 31, TextAnchor.MiddleLeft, UIFactory.TextMain);
                UIFactory.Label("Info", card.transform, new Vector2(0f, 1f), new Vector2(24f, -74f), new Vector2(580f, 26f),
                    stage.description, 16, TextAnchor.MiddleLeft, UIFactory.TextDim, FontStyle.Normal);
                if (i == 0) firstStageButton = card;
                y -= cardStep;
            }
            // Difficulty right in the play flow as well as in OPCIONES.
            var difficulty = MenuButton(t, "Difficulty", y - 10f, "", () => { }, 620f);
            var difficultyText = difficulty.GetComponentInChildren<Text>();
            void RefreshDifficulty() => difficultyText.text = "DIFICULTAD:  " + Rally.AI.AIDriver.LevelNames[(int)Rally.AI.AIDriver.Difficulty];
            difficulty.onClick.AddListener(() =>
            {
                Rally.AI.AIDriver.Difficulty = (Rally.AI.AIDriver.Level)(((int)Rally.AI.AIDriver.Difficulty + 1) % 3);
                RefreshDifficulty();
            });
            RefreshDifficulty();
            MenuButton(t, "Back", y - 114f, "VOLVER", () => Open(mainScreen, playButton), 620f);
        }

        private void BuildCarSelect(RectTransform root)
        {
            carScreen = Screen("Cars", root, false);
            var t = carScreen.transform;
            var top = UIFactory.Label("Title", t, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1200f, 90f),
                "ELIGE TU COCHE", 72, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.AddShadow(top, 3f);

            // Info panel at the bottom; the car turns in the middle of the screen.
            var panel = UIFactory.Panel("Panel", t, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 290f), UIFactory.PanelDark);
            UIFactory.Panel("Top", panel.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1100f, 5f), UIFactory.Accent);
            carName = UIFactory.Label("Name", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1000f, 64f),
                "", 56, TextAnchor.MiddleCenter, UIFactory.TextMain);
            carDescription = UIFactory.Label("Description", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(1040f, 32f),
                "", 24, TextAnchor.MiddleCenter, UIFactory.TextDim, FontStyle.Normal);

            string[] stats = { "VELOCIDAD", "ACELERACIÓN", "AGARRE" };
            statBars = new Image[stats.Length][];
            for (int s = 0; s < stats.Length; s++)
            {
                float y = -140f - s * 42f;
                UIFactory.Label("Stat" + s, panel.transform, new Vector2(0.5f, 1f), new Vector2(-190f, y), new Vector2(300f, 32f),
                    stats[s], 24, TextAnchor.MiddleRight, UIFactory.TextMain, FontStyle.Bold, new Vector2(1f, 1f));
                statBars[s] = new Image[5];
                for (int i = 0; i < 5; i++)
                    statBars[s][i] = UIFactory.Panel("Bar", panel.transform, new Vector2(0.5f, 1f), new Vector2(-160f + i * 74f, y - 8f),
                        new Vector2(66f, 16f), Color.white, new Vector2(0f, 1f));
            }

            UIFactory.Button("Prev", t, new Vector2(0f, 0.5f), new Vector2(60f, 40f), new Vector2(130f, 130f), "<", () => ShowCar(carIndex - 1));
            UIFactory.Button("Next", t, new Vector2(1f, 0.5f), new Vector2(-60f, 40f), new Vector2(130f, 130f), ">", () => ShowCar(carIndex + 1));

            UIFactory.Button("Back", t, new Vector2(0.5f, 0f), new Vector2(-310f, 40f), new Vector2(380f, 84f), "VOLVER", () => Open(stageScreen, firstStageButton));
            startButton = UIFactory.Button("Start", t, new Vector2(0.5f, 0f), new Vector2(310f, 40f), new Vector2(380f, 84f), "EMPEZAR", StartStage);
        }

        private void BuildOptions(RectTransform root)
        {
            optionsScreen = Screen("Options", root, true);
            var t = optionsScreen.transform;
            Title(t, "OPCIONES", new Vector2(120f, -80f));
            // Compact rows: with the phone-only entries there are ten plus VOLVER, and a landscape phone is ~970 units tall.
            float rowHeight = Application.isMobilePlatform ? 54f : 64f, rowStep = Application.isMobilePlatform ? 64f : 76f;
            float y = -180f;
            Button Row(string name, UnityEngine.Events.UnityAction action)
            {
                var b = UIFactory.Button(name, t, new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(620f, rowHeight), "", action);
                y -= rowStep;
                return b;
            }
            volumeButton = Row("Volume", CycleVolume);
            volumeLabel = volumeButton.GetComponentInChildren<Text>();
            difficultyLabel = Row("Difficulty", () =>
            {
                Rally.AI.AIDriver.Difficulty = (Rally.AI.AIDriver.Level)(((int)Rally.AI.AIDriver.Difficulty + 1) % 3);
                RefreshOptions();
            }).GetComponentInChildren<Text>();
            damageLabel = Row("Damage", CycleDamage).GetComponentInChildren<Text>();
            coDriverLabel = Row("CoDriver", CycleCoDriver).GetComponentInChildren<Text>();
            rivalsLabel = Row("Rivals", () => { RaceManager.RivalsEnabled = !RaceManager.RivalsEnabled; RefreshOptions(); ApplyRivalsNow(); })
                .GetComponentInChildren<Text>();
            qualityLabel = Row("Quality", () =>
            {
                GraphicsQuality.Setting = (GraphicsQuality.Level)(((int)GraphicsQuality.Setting + 1) % 3);
                RefreshOptions();
            }).GetComponentInChildren<Text>();
            assistLabel = Row("Assist", () =>
            {
                DrivingAssist.Setting = (DrivingAssist.Level)(((int)DrivingAssist.Setting + 1) % 3);
                RefreshOptions();
            }).GetComponentInChildren<Text>();
            if (Application.isMobilePlatform)
            {
                sensitivityLabel = Row("Sensitivity", CycleSensitivity).GetComponentInChildren<Text>();
                autoLabel = Row("AutoThrottle", () => { PlayerCarInput.AcelerarSolo = !PlayerCarInput.AcelerarSolo; RefreshOptions(); })
                    .GetComponentInChildren<Text>();
            }
            y -= 16f;
            optionsBack = Row("Back", () => Open(mainScreen, playButton));
            optionsBack.GetComponentInChildren<Text>().text = "VOLVER";
        }

        private void BuildControls(RectTransform root)
        {
            controlsScreen = Screen("Controls", root, true);
            var t = controlsScreen.transform;
            Title(t, "CONTROLES", new Vector2(120f, -110f));
            if (Application.isMobilePlatform)
            {
                var body = UIFactory.Label("Body", t, new Vector2(0f, 1f), new Vector2(120f, -230f), new Vector2(680f, 520f),
                    "MODO BOTONES\n   INCLINA EL MÓVIL PARA GIRAR\n   ACELERAR · FRENAR · REINICIAR\n   MANTÉN «ATRÁS» PARA MIRAR ATRÁS\n\n" +
                    "MODO MANDO (BOTÓN BAJO PAUSA)\n   JOYSTICK: GIRAR\n   A ACELERAR · B FRENAR\n   X FRENO DE MANO · Y REINICIAR\n\n" +
                    "EN PAUSA: SENSIBILIDAD DE LA INCLINACIÓN",
                    26, TextAnchor.UpperLeft, UIFactory.TextMain, FontStyle.Normal);
                body.lineSpacing = 1.2f;
            }
            else
            {
                // Three real columns: the UI font is proportional, so aligning with spaces would not line up.
                string[,] rows =
                {
                    { "", "TECLADO", "MANDO" },
                    { "ACELERAR", "W", "RT" },
                    { "FRENAR · ATRÁS", "S", "LT" },
                    { "GIRAR", "A  D", "STICK" },
                    { "FRENO DE MANO", "ESPACIO", "B" },
                    { "REINICIAR", "R", "Y" },
                    { "CÁMARA", "C", "VIEW" },
                    { "MIRAR ALREDEDOR", "Q (ATRÁS)", "STICK DER." },
                    { "PAUSA", "ESC / P", "START" },
                    { "REPETIR TRAMO", "RETROCESO", "X" },
                };
                float[] columns = { 120f, 430f, 640f };
                for (int r = 0; r < rows.GetLength(0); r++)
                    for (int c = 0; c < 3; c++)
                        UIFactory.Label($"Cell{r}_{c}", t, new Vector2(0f, 1f), new Vector2(columns[c], -230f - r * 56f), new Vector2(320f, 44f),
                            rows[r, c], 26, TextAnchor.MiddleLeft, r == 0 ? UIFactory.Accent : (c == 0 ? UIFactory.TextMain : UIFactory.TextDim),
                            r == 0 || c == 0 ? FontStyle.Bold : FontStyle.Normal);
                // The web font has no arrow glyphs, so the cursor keys are named in words.
                UIFactory.Label("Arrows", t, new Vector2(0f, 1f), new Vector2(120f, -230f - rows.GetLength(0) * 56f), new Vector2(680f, 40f),
                    "TAMBIÉN CON LAS FLECHAS DEL CURSOR", 22, TextAnchor.MiddleLeft, UIFactory.TextDim, FontStyle.Normal);
            }
            controlsBack = MenuButton(t, "Back", -800f, "VOLVER", () => Open(mainScreen, playButton), 620f);
        }

        // ------------------------------------------------------------------ behaviour

        private void Open(CanvasGroup screen, Button select)
        {
            foreach (var g in new[] { mainScreen, stageScreen, carScreen, optionsScreen, controlsScreen }) Hide(g);
            current = screen;
            current.alpha = 1f;
            current.interactable = true;
            current.blocksRaycasts = true;
            if (screen == carScreen) ShowCar(carIndex);
            if (menuCamera != null) menuCamera.Showroom = screen == carScreen;
            EventSystem.current?.SetSelectedGameObject(select != null ? select.gameObject : null);
        }

        private static void Hide(CanvasGroup g)
        {
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
        }

        private void ShowCar(int index)
        {
            int n = CarCatalog.Cars.Length;
            carIndex = (index % n + n) % n;
            var entry = CarCatalog.Cars[carIndex];
            carName.text = entry.name;
            carDescription.text = entry.description;
            int[] values = { entry.speed, entry.acceleration, entry.grip };
            for (int s = 0; s < values.Length; s++)
                for (int i = 0; i < 5; i++)
                    statBars[s][i].color = i < values[s] ? UIFactory.Accent : new Color(1f, 1f, 1f, 0.15f);
            CarCatalog.Apply(race, carIndex); // the car in the showroom changes livery straight away
        }

        private void StartStage()
        {
            CarCatalog.Selected = carIndex;
            if (stageIndex != StageCatalog.Current)
            {
                // Another stage: load it and start straight away (it applies the chosen car when it loads).
                RaceManager.LoadStageAndStart(StageCatalog.Stages[stageIndex].scene);
                return;
            }
            CarCatalog.Apply(race, carIndex);
            if (menuCamera != null) Destroy(menuCamera);
            var chase = Camera.main != null ? Camera.main.GetComponent<RallyCamera>() : null;
            if (chase != null) chase.SnapToTarget();
            race.BeginCountdown();
            Destroy(gameObject);
        }

        private void CycleVolume()
        {
            float v = PlayerPrefs.GetFloat(VolumeKey, 1f);
            int i = System.Array.IndexOf(Volumes, v);
            v = Volumes[(i + 1) % Volumes.Length];
            PlayerPrefs.SetFloat(VolumeKey, v);
            PlayerPrefs.Save();
            AudioListener.volume = v;
            RefreshOptions();
        }

        // The rivals are removed when the stage loads; if the option changes on the menu, reload so it takes effect.
        private void ApplyRivalsNow()
        {
            RaceManager.OpenMenuOnLoad = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void CycleCoDriver()
        {
            CoDriver.Setting = (CoDriver.Mode)(((int)CoDriver.Setting + 1) % 3);
            RefreshOptions();
        }

        private void CycleDamage()
        {
            CarDamage.Setting = (CarDamage.Mode)(((int)CarDamage.Setting + 1) % 3);
            RefreshOptions();
        }

        private void CycleSensitivity()
        {
            var input = race.Player != null ? race.Player.GetComponent<PlayerCarInput>() : null;
            if (input == null) return;
            input.NivelSensibilidad = input.NivelSensibilidad + 1;
            RefreshOptions();
        }

        private void RefreshOptions()
        {
            volumeLabel.text = $"VOLUMEN:  {Mathf.RoundToInt(PlayerPrefs.GetFloat(VolumeKey, 1f) * 100f)} %";
            damageLabel.text = "DAÑOS:  " + CarDamage.ModeNames[(int)CarDamage.Setting];
            difficultyLabel.text = "DIFICULTAD:  " + Rally.AI.AIDriver.LevelNames[(int)Rally.AI.AIDriver.Difficulty];
            coDriverLabel.text = "COPILOTO:  " + CoDriver.ModeNames[(int)CoDriver.Setting];
            rivalsLabel.text = RaceManager.RivalsEnabled ? "RIVALES:  SÍ" : "RIVALES:  NO (CONTRA EL RELOJ)";
            if (autoLabel != null) autoLabel.text = "ACELERAR SOLO:  " + (PlayerCarInput.AcelerarSolo ? "SÍ" : "NO");
            assistLabel.text = "AYUDA DE CONDUCCIÓN:  " + DrivingAssist.LevelNames[(int)DrivingAssist.Setting];
            qualityLabel.text = "CALIDAD GRÁFICA:  " + GraphicsQuality.LevelNames[(int)GraphicsQuality.Setting];
            if (sensitivityLabel != null)
            {
                var input = race.Player != null ? race.Player.GetComponent<PlayerCarInput>() : null;
                if (input != null) sensitivityLabel.text = "SENSIBILIDAD INCLINACIÓN:  " + PlayerCarInput.SensitivityNames[input.NivelSensibilidad];
            }
        }

        private void Update()
        {
            // The race was started some other way (e.g. automated tests): get out of the way.
            if (race.CurrentState != RaceManager.State.Menu)
            {
                if (menuCamera != null) Destroy(menuCamera);
                Destroy(gameObject);
                return;
            }

            // Esc / Start / B go back one screen.
            var input = RallyInput.Instance;
            if (input == null || current == mainScreen) return;
            if (input.Pause.WasPressedThisFrame())
            {
                if (current == carScreen) Open(stageScreen, firstStageButton);
                else Open(mainScreen, playButton);
            }
        }
    }
}
