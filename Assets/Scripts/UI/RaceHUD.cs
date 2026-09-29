using System.Collections.Generic;
using Rally.Systems;
using Rally.Track;
using UnityEngine;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>
    /// In-race HUD: stage badge, timer, position, checkpoints, stage progress bar with all cars,
    /// speedometer / gear / rev bar and centre messages.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        private const int RevSegments = 24;
#if UNITY_WEBGL && !UNITY_EDITOR
        private const string PauseHint = "ESC / P  PAUSA";
#else
        private const string PauseHint = "ESC  PAUSA";
#endif

        [SerializeField] private float messageDuration = 2.2f;

        private RaceManager race;
        private Canvas canvas;
        private RectTransform root;

        private Text timeText, bestText, positionText, checkpointText, progressText;
        private Text speedText, gearText, messageText, subMessageText, warningText, standingsText;
        private Image[] revSegments;
        private RectTransform progressFill;
        private RectTransform progressTrack;
        private readonly Dictionary<RaceParticipant, RectTransform> carMarkers = new Dictionary<RaceParticipant, RectTransform>();
        private CanvasGroup hudGroup;

        private float messageTimer;
        private float displayedSpeed;

        public Canvas Canvas => canvas;

        private void Start()
        {
            race = RaceManager.Instance;
            BuildCanvas();
            BuildStageBadge();
            BuildTimer();
            BuildPositionPanel();
            BuildProgressBar();
            BuildSpeedometer();
            BuildMessages();
            BuildHints();
            new GameObject("TouchControls").AddComponent<TouchControls>(); // hidden unless on a phone / touched

            race.CountdownTick += t => ShowMessage(t.ToString(), "", 0.9f);
            race.Go += () => ShowMessage("¡YA!", "", 1.1f);
            race.CheckpointPassed += OnCheckpoint;
            race.PlayerResultsReady += () => hudGroup.alpha = 0.35f;
        }

        // ------------------------------------------------------------------ building

        private void BuildCanvas()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = UIFactory.Stretch("HUD", transform);
            hudGroup = root.gameObject.AddComponent<CanvasGroup>();
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
        }

        private void BuildStageBadge()
        {
            var anchor = new Vector2(0f, 1f);
            var tag = UIFactory.Panel("StageTag", root, anchor, new Vector2(48f, -44f), new Vector2(150f, 40f), UIFactory.Accent);
            UIFactory.Label("Text", tag.transform, race.Stage.stageNumber, 26, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.08f));

            // Bounded box + best fit: on 4:3 / 3:2 screens (and with the web fallback font, which is wider)
            // a long stage name would otherwise run into the progress bar.
            var name = UIFactory.Label("StageName", root, anchor, new Vector2(212f, -44f), new Vector2(330f, 40f),
                race.Stage.stageName, 26, TextAnchor.MiddleLeft, UIFactory.TextMain);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 26;
            UIFactory.AddShadow(name);
        }

        private void BuildTimer()
        {
            var anchor = new Vector2(0f, 1f);
            var panel = UIFactory.Panel("TimerPanel", root, anchor, new Vector2(48f, -96f), new Vector2(330f, 108f), UIFactory.PanelDark);
            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 108f), UIFactory.Accent);

            UIFactory.Label("TimeLabel", panel.transform, new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(200f, 24f),
                "TIEMPO", 18, TextAnchor.UpperLeft, UIFactory.TextDim);

            timeText = UIFactory.Label("Time", panel.transform, new Vector2(0f, 1f), new Vector2(22f, -36f), new Vector2(300f, 50f),
                "00:00.000", 48, TextAnchor.UpperLeft, UIFactory.TextMain);

            bestText = UIFactory.Label("Best", root, anchor, new Vector2(48f, -214f), new Vector2(330f, 26f),
                "MEJOR  " + RaceManager.FormatTime(race.BestTime), 20, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.AddShadow(bestText, 1.5f);
        }

        private void BuildPositionPanel()
        {
            var c = UIFactory.Center;
            var panel = UIFactory.Panel("PositionPanel", root, new Vector2(1f, 1f), new Vector2(-48f, -44f), new Vector2(330f, 170f), UIFactory.PanelDark);

            UIFactory.Label("PosLabel", panel.transform, new Vector2(0.27f, 1f), new Vector2(0f, -24f), new Vector2(160f, 24f),
                "POSICIÓN", 18, TextAnchor.MiddleCenter, UIFactory.TextDim, FontStyle.Bold, c);
            positionText = UIFactory.Label("Position", panel.transform, new Vector2(0.27f, 1f), new Vector2(0f, -78f), new Vector2(160f, 76f),
                "1/3", 64, TextAnchor.MiddleCenter, UIFactory.TextMain, FontStyle.Bold, c);

            UIFactory.Label("CpLabel", panel.transform, new Vector2(0.73f, 1f), new Vector2(0f, -24f), new Vector2(160f, 24f),
                "CONTROL", 18, TextAnchor.MiddleCenter, UIFactory.TextDim, FontStyle.Bold, c);
            checkpointText = UIFactory.Label("Checkpoint", panel.transform, new Vector2(0.73f, 1f), new Vector2(0f, -78f), new Vector2(160f, 76f),
                "0/0", 50, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold, c);

            UIFactory.Panel("Divider", panel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(290f, 2f),
                new Color(1f, 1f, 1f, 0.12f), c);
            standingsText = UIFactory.Label("Standings", panel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(320f, 30f),
                "", 17, TextAnchor.MiddleCenter, UIFactory.TextDim, FontStyle.Normal, c);
        }

        private void BuildProgressBar()
        {
            var anchor = new Vector2(0.5f, 1f);
            var track = UIFactory.Panel("ProgressTrack", root, anchor, new Vector2(0f, -62f), new Vector2(640f, 8f), new Color(1f, 1f, 1f, 0.22f));
            progressTrack = track.rectTransform;

            var fill = UIFactory.Panel("Fill", track.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f), UIFactory.Accent);
            progressFill = fill.rectTransform;

            // Checkpoint ticks.
            var path = race.Path;
            foreach (var cp in race.Checkpoints)
            {
                float t = Mathf.Clamp01((cp.DistanceAlongTrack - path.StartDistance) / path.StageLength);
                var tick = UIFactory.Panel("Tick", track.transform, new Vector2(t, 0.5f), Vector2.zero,
                    cp.IsFinish ? new Vector2(4f, 26f) : new Vector2(2f, 16f), cp.IsFinish ? UIFactory.TextMain : new Color(1f, 1f, 1f, 0.55f));
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            // Pivots on the outer side so the labels sit just outside the bar ends instead of over them.
            UIFactory.Label("Start", track.transform, new Vector2(0f, 0.5f), new Vector2(-14f, 0f), new Vector2(90f, 24f),
                "SALIDA", 16, TextAnchor.MiddleRight, UIFactory.TextDim, FontStyle.Bold, new Vector2(1f, 0.5f));
            UIFactory.Label("Finish", track.transform, new Vector2(1f, 0.5f), new Vector2(18f, 0f), new Vector2(90f, 24f),
                "META", 16, TextAnchor.MiddleLeft, UIFactory.TextDim, FontStyle.Bold, new Vector2(0f, 0.5f));

            progressText = UIFactory.Label("ProgressText", root, anchor, new Vector2(0f, -94f), new Vector2(400f, 26f),
                "", 18, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.AddShadow(progressText, 1.5f);

            foreach (var p in race.Participants)
            {
                float size = p.IsPlayer ? 20f : 14f;
                var marker = UIFactory.Panel(p.DisplayName, track.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size, size), p.Color);
                marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                UIFactory.AddShadow(marker, 1f, 0.8f);
                carMarkers[p] = marker.rectTransform;
                if (p.IsPlayer) marker.transform.SetAsLastSibling();
            }
        }

        private void BuildSpeedometer()
        {
            var c = UIFactory.Center;
            var panel = UIFactory.Panel("Speedo", root, new Vector2(1f, 0f), new Vector2(-48f, 48f), new Vector2(380f, 176f), UIFactory.PanelDark);

            speedText = UIFactory.Label("Speed", panel.transform, new Vector2(0f, 0f), new Vector2(140f, 86f), new Vector2(240f, 100f),
                "0", 100, TextAnchor.MiddleRight, UIFactory.TextMain, FontStyle.Bold, c);
            UIFactory.Label("Unit", panel.transform, new Vector2(0f, 0f), new Vector2(212f, 24f), new Vector2(96f, 26f),
                "KM/H", 22, TextAnchor.MiddleRight, UIFactory.TextDim, FontStyle.Bold, c);

            var gearBox = UIFactory.Panel("GearBox", panel.transform, new Vector2(1f, 0f), new Vector2(-62f, 76f), new Vector2(92f, 112f),
                UIFactory.PanelLight, c);
            UIFactory.Label("GearLabel", gearBox.transform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(90f, 20f),
                "MARCHA", 16, TextAnchor.MiddleCenter, UIFactory.TextDim, FontStyle.Bold, c);
            gearText = UIFactory.Label("Gear", gearBox.transform, c, new Vector2(0f, -8f), new Vector2(90f, 80f),
                "N", 68, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold, c);

            revSegments = new Image[RevSegments];
            float width = 332f, gap = 3f;
            float segW = (width - gap * (RevSegments - 1)) / RevSegments;
            for (int i = 0; i < RevSegments; i++)
            {
                var seg = UIFactory.Panel("Rev" + i, panel.transform, new Vector2(0f, 0f), new Vector2(24f + i * (segW + gap), 146f),
                    new Vector2(segW, 12f + i * 0.5f), new Color(1f, 1f, 1f, 0.12f));
                revSegments[i] = seg;
            }
        }

        private void BuildMessages()
        {
            var anchor = new Vector2(0.5f, 0.5f);
            messageText = UIFactory.Label("Message", root, anchor, new Vector2(0f, 170f), new Vector2(1200f, 130f),
                "", 110, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.AddShadow(messageText, 3f, 0.6f);
            subMessageText = UIFactory.Label("SubMessage", root, anchor, new Vector2(0f, 92f), new Vector2(1200f, 50f),
                "", 34, TextAnchor.MiddleCenter, UIFactory.Accent);
            UIFactory.AddShadow(subMessageText, 2f, 0.6f);
            warningText = UIFactory.Label("Warning", root, anchor, new Vector2(0f, -150f), new Vector2(1200f, 60f),
                "", 44, TextAnchor.MiddleCenter, new Color(1f, 0.28f, 0.2f));
            UIFactory.AddShadow(warningText, 2f, 0.7f);
        }

        private void BuildHints()
        {
            var hint = UIFactory.Label("Hints", root, new Vector2(0f, 0f), new Vector2(48f, 40f), new Vector2(900f, 26f),
                "R  REINICIAR     C  CÁMARA     " + PauseHint + "     RETROCESO  REPETIR TRAMO", 18, TextAnchor.MiddleLeft,
                new Color(1f, 1f, 1f, 0.55f), FontStyle.Normal);
            UIFactory.AddShadow(hint, 1f);
            if (Application.isMobilePlatform) hint.gameObject.SetActive(false); // keyboard hints; phones use the touch buttons
        }

        // ------------------------------------------------------------------ runtime

        private void OnCheckpoint(RaceParticipant participant, Checkpoint checkpoint, float time)
        {
            if (!participant.IsPlayer || checkpoint.IsFinish) return;
            ShowMessage("", $"CONTROL {checkpoint.Index + 1}   {RaceManager.FormatTime(time)}", messageDuration);
        }

        private void ShowMessage(string main, string sub, float duration)
        {
            messageText.text = main;
            subMessageText.text = sub;
            messageTimer = duration;
        }

        private void Update()
        {
            if (race == null || race.Player == null) return;
            var player = race.Player;
            var car = player.Car;

            // Timer
            float shownTime = player.HasFinished ? player.FinishTime
                : race.CurrentState == RaceManager.State.Racing ? race.StageTime : 0f;
            timeText.text = shownTime > 0f ? RaceManager.FormatTime(shownTime) : "00:00.000";
            bestText.text = "MEJOR  " + RaceManager.FormatTime(race.BestTime);

            // Position & checkpoints
            positionText.text = $"{race.GetPosition(player)}/{race.Participants.Count}";
            checkpointText.text = $"{player.NextCheckpoint}/{player.CheckpointCount}";
            UpdateStandings();

            // Progress
            progressFill.sizeDelta = new Vector2(progressTrack.rect.width * player.Progress01, progressFill.sizeDelta.y);
            float stageKm = race.Path.StageLength / 1000f;
            progressText.text = $"{player.Progress01 * stageKm:0.00} / {stageKm:0.00} KM";
            foreach (var pair in carMarkers)
            {
                pair.Value.anchorMin = pair.Value.anchorMax = new Vector2(pair.Key.Progress01, 0.5f);
                pair.Value.anchoredPosition = Vector2.zero;
            }

            // Speedometer
            displayedSpeed = Mathf.Lerp(displayedSpeed, car.SpeedKph, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            speedText.text = Mathf.RoundToInt(displayedSpeed).ToString();
            var drivetrain = car.Drivetrain;
            if (drivetrain != null)
            {
                gearText.text = drivetrain.Gear < 0 ? "R" : drivetrain.Gear == 0 ? "N" : drivetrain.Gear.ToString();
                UpdateRevBar(drivetrain.NormalizedRpm);
            }

            // Messages
            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(messageTimer * 3f);
                messageText.color = new Color(1f, 1f, 1f, a);
                subMessageText.color = new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b, a);
            }
            else
            {
                messageText.text = "";
                subMessageText.text = "";
            }

            UpdateWarnings(player);
        }

        private void UpdateStandings()
        {
            var standings = race.GetStandings();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < standings.Count; i++)
            {
                var p = standings[i];
                string colorHex = ColorUtility.ToHtmlStringRGB(p.IsPlayer ? UIFactory.Accent : UIFactory.TextDim);
                if (i > 0) sb.Append("   ");
                sb.Append($"<color=#{colorHex}>{i + 1}. {p.DisplayName}</color>");
            }
            standingsText.supportRichText = true;
            standingsText.text = sb.ToString();
        }

        private void UpdateRevBar(float rpm01)
        {
            int lit = Mathf.RoundToInt(rpm01 * RevSegments);
            bool flash = rpm01 > 0.93f && Mathf.Repeat(Time.unscaledTime * 12f, 1f) > 0.5f;
            for (int i = 0; i < RevSegments; i++)
            {
                float t = (float)i / (RevSegments - 1);
                Color on = t < 0.6f ? UIFactory.TextMain : t < 0.85f ? UIFactory.Accent : new Color(1f, 0.15f, 0.12f);
                if (flash) on = new Color(0.3f, 0.6f, 1f);
                revSegments[i].color = i < lit ? on : new Color(1f, 1f, 1f, 0.12f);
            }
        }

        private void UpdateWarnings(RaceParticipant player)
        {
            string warning = "";
            if (race.CurrentState == RaceManager.State.Racing && !player.HasFinished)
            {
                string reset = Application.isMobilePlatform ? "PULSA REINICIAR" : "PULSA R";
                if (player.IsFlipped) warning = "COCHE VOLCADO  —  " + reset;
                else if (player.MissedCheckpoint) warning = "CONTROL SALTADO  —  " + reset;
                else if (player.IsWrongWay) warning = "SENTIDO CONTRARIO";
                else if (player.IsOffTrack) warning = "VUELVE AL TRAMO  —  " + reset;
            }
            bool blink = Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) > 0.3f;
            warningText.text = blink ? warning : "";
        }
    }
}
