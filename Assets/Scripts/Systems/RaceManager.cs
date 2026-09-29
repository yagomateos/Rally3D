using System;
using System.Collections.Generic;
using Rally.AI;
using Rally.Car;
using Rally.Track;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rally.Systems
{
    /// <summary>
    /// Stage flow: main menu → title card → countdown → racing → finished. Owns the stage clock, positions,
    /// best time, pause, restart and the way back to the main menu.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public enum State { Menu, Intro, Countdown, Racing, Finished }

        /// <summary>
        /// Whether the next load of the stage opens on the main menu. "Repetir tramo" clears it (straight to the
        /// title card); "Salir al menú" sets it. Starts true, so launching the game shows the menu.
        /// </summary>
        public static bool OpenMenuOnLoad { get; set; } = true;

        /// <summary>
        /// Rivals on the stage (main menu option). Off = the real rally format: alone against the clock,
        /// and no bumping at the start.
        /// </summary>
        public static bool RivalsEnabled
        {
            get => PlayerPrefs.GetInt("Rally.Rivals", 1) == 1;
            set { PlayerPrefs.SetInt("Rally.Rivals", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Set by the main menu when it loads another stage: skip menu and title card, start the countdown.</summary>
        public static bool StartOnLoad { get; set; }

        /// <summary>Loads another stage and starts it straight away (car choice is applied on load).</summary>
        public static void LoadStageAndStart(string sceneName)
        {
            OpenMenuOnLoad = false;
            StartOnLoad = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(sceneName);
        }

        // Also with "Enter Play Mode" domain reload disabled, every play session starts on the menu.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OpenMenuOnLoad = true;
            StartOnLoad = false;
        }

        [SerializeField] private StageDefinition stage;
        [SerializeField] private TrackPath path;
        [SerializeField] private Checkpoint[] checkpoints = new Checkpoint[0];
        [SerializeField] private RaceParticipant[] participants = new RaceParticipant[0];
        [SerializeField] private float countdownSeconds = 3f;
        [SerializeField] private float resultsDelay = 1.5f;

        public static RaceManager Instance { get; private set; }

        public event Action<int> CountdownTick;
        public event Action Go;
        public event Action<RaceParticipant, Checkpoint, float> CheckpointPassed;
        public event Action<RaceParticipant> ParticipantFinished;
        public event Action PlayerResultsReady;
        public event Action<bool> PauseChanged;
        /// <summary>Seconds added to the player's time (a reset they asked for).</summary>
        public event Action<float> PenaltyAdded;

        /// <summary>Time added each time the player asks for a reset (automatic recovery from the roof is free).</summary>
        public const float ResetPenalty = 5f;

        public State CurrentState { get; private set; } = State.Intro;
        /// <summary>Main menu or title card: nothing to pause, restart or reset yet.</summary>
        public bool BeforeStart => CurrentState == State.Menu || CurrentState == State.Intro;
        public bool IsPaused { get; private set; }
        public float StageTime { get; private set; }
        public float BestTime { get; private set; }
        public bool NewBest { get; private set; }
        public bool ResultsShown { get; private set; }
        public StageDefinition Stage => stage;
        public TrackPath Path => path;
        public IReadOnlyList<Checkpoint> Checkpoints => checkpoints;
        public IReadOnlyList<RaceParticipant> Participants => participants;
        public RaceParticipant Player { get; private set; }
        public float CountdownRemaining { get; private set; }

        private string BestTimeKey => $"BestTime_{stage.stageNumber}_{stage.stageName}";
        private readonly List<RaceParticipant> standings = new List<RaceParticipant>();
        private string SplitsKey => BestTimeKey + "_Splits";
        private float[] bestSplits = new float[0];
        private readonly List<float> runSplits = new List<float>();
        private int lastTick;

        public void Configure(StageDefinition def, TrackPath trackPath, Checkpoint[] ordered, RaceParticipant[] cars)
        {
            stage = def;
            path = trackPath;
            checkpoints = ordered;
            participants = cars;
        }

        private void Awake()
        {
            Instance = this;
            CurrentState = OpenMenuOnLoad ? State.Menu : State.Intro;
            OpenMenuOnLoad = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            BestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
            bestSplits = LoadSplits();

            // Stage-specific grip (snow): every car and the AI's speed plan use the stage's table.
            AIDriver.StageSurfaces = stage != null ? stage.surfaces : null;
            if (stage != null && stage.surfaces != null)
                foreach (var p in participants)
                    p.GetComponent<CarController>().Surfaces = stage.surfaces;

            if (!RivalsEnabled)
            {
                // Against the clock: only the player's car stays (before the HUD builds its standings and map).
                var keep = new List<RaceParticipant>();
                foreach (var p in participants)
                {
                    if (p.IsPlayer) keep.Add(p);
                    else p.gameObject.SetActive(false);
                }
                participants = keep.ToArray();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            foreach (var p in participants)
            {
                p.Initialize(path, checkpoints, () => StageTime);
                p.CheckpointPassed += OnCheckpointPassed;
                p.Finished += OnParticipantFinished;
                p.Car.ControlEnabled = false;
                if (p.GetComponent<CarDamage>() == null) p.gameObject.AddComponent<CarDamage>();
                if (p.IsPlayer) Player = p;
            }
            standings.AddRange(participants);
            // The car picked in the main menu, also after "Repetir tramo" (which reloads the stage without the menu).
            CarCatalog.Apply(this, CarCatalog.Selected);

            if (StartOnLoad)
            {
                StartOnLoad = false;
                BeginCountdown();
            }
        }

        private void Update()
        {
            var input = RallyInput.Instance;
            if (input == null) return;

            if (input.Pause.WasPressedThisFrame() && !BeforeStart && !ResultsShown)
                SetPaused(!IsPaused);
            if (IsPaused) return;

            switch (CurrentState)
            {
                case State.Intro:
                    if (input.Confirm.WasPressedThisFrame() || input.Throttle.WasPressedThisFrame() || WebStartClicked())
                        BeginCountdown();
                    break;
                case State.Countdown:
                    UpdateCountdown();
                    break;
                case State.Racing:
                    StageTime += Time.deltaTime;
                    if (input.ResetCar.WasPressedThisFrame()) PlayerReset();
                    break;
                case State.Finished:
                    StageTime += Time.deltaTime;
                    break;
            }

            if (input.RestartStage.WasPressedThisFrame() && !BeforeStart) Restart();
            if (ResultsShown && input.Confirm.WasPressedThisFrame()) Restart();
        }

        /// <summary>
        /// On the web the first click both focuses the page (so it receives keys) and unlocks audio,
        /// so it also starts the stage.
        /// </summary>
        private static bool WebStartClicked()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (touch != null && touch.primaryTouch.press.wasPressedThisFrame); // phones: tap to start
#else
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Clicking outside the game on the itch.io page takes keyboard focus away mid-stage: pause.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !BeforeStart && !ResultsShown) SetPaused(true);
        }
#endif

        /// <summary>Leaves the title card and starts the 3-2-1 countdown.</summary>
        public void BeginCountdown()
        {
            CurrentState = State.Countdown;
            CountdownRemaining = countdownSeconds;
            lastTick = Mathf.CeilToInt(countdownSeconds) + 1;
        }

        private void UpdateCountdown()
        {
            CountdownRemaining -= Time.deltaTime;
            int tick = Mathf.CeilToInt(CountdownRemaining);
            if (tick != lastTick && tick > 0)
            {
                lastTick = tick;
                CountdownTick?.Invoke(tick);
            }
            if (CountdownRemaining > 0f) return;

            CurrentState = State.Racing;
            StageTime = 0f;
            foreach (var p in participants) p.Car.ControlEnabled = true;
            Go?.Invoke();
        }

        /// <summary>The player asked to be put back on the road: costs <see cref="ResetPenalty"/> seconds.</summary>
        public void PlayerReset()
        {
            if (Player == null || Player.HasFinished || CurrentState != State.Racing || IsPaused) return;
            if (!Player.ResetToTrack()) return;
            Player.AddPenalty(ResetPenalty);
            PenaltyAdded?.Invoke(ResetPenalty);
        }

        /// <summary>Player's running time, penalties included.</summary>
        public float PlayerTime => StageTime + (Player != null ? Player.Penalty : 0f);

        private void OnCheckpointPassed(RaceParticipant participant, Checkpoint checkpoint)
        {
            float time = participant.IsPlayer ? StageTime + participant.Penalty : StageTime;
            if (participant.IsPlayer) runSplits.Add(time);
            CheckpointPassed?.Invoke(participant, checkpoint, time);
        }

        /// <summary>Difference with the best run at this checkpoint (negative = faster). False if there is no best run yet.</summary>
        public bool TryGetSplitDelta(int checkpointIndex, float time, out float delta)
        {
            delta = 0f;
            if (checkpointIndex < 0 || checkpointIndex >= bestSplits.Length) return false;
            delta = time - bestSplits[checkpointIndex];
            return true;
        }

        private float[] LoadSplits()
        {
            string raw = PlayerPrefs.GetString(SplitsKey, "");
            if (string.IsNullOrEmpty(raw)) return new float[0];
            var parts = raw.Split(';');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result[i]);
            return result;
        }

        private void SaveSplits()
        {
            var parts = new string[runSplits.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = runSplits[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            PlayerPrefs.SetString(SplitsKey, string.Join(";", parts));
        }

        private void OnParticipantFinished(RaceParticipant participant)
        {
            ParticipantFinished?.Invoke(participant);
            if (!participant.IsPlayer) return;

            CurrentState = State.Finished;
            // Hand the car to the AI so it coasts safely after the line.
            var autopilot = participant.GetComponent<AIDriver>();
            if (autopilot != null)
            {
                autopilot.enabled = true;
                autopilot.SpeedCapKph = 35f;
                participant.Car.InputSource = autopilot;
            }

            if (BestTime <= 0f || participant.FinishTime < BestTime)
            {
                NewBest = true;
                BestTime = participant.FinishTime;
                PlayerPrefs.SetFloat(BestTimeKey, BestTime);
                SaveSplits();
                PlayerPrefs.Save();
            }
            Invoke(nameof(ShowResults), resultsDelay);
        }

        private void ShowResults()
        {
            ResultsShown = true;
            PlayerResultsReady?.Invoke();
        }

        /// <summary>Current race order (finished cars by time, then by distance covered).</summary>
        public IReadOnlyList<RaceParticipant> GetStandings()
        {
            standings.Sort((a, b) =>
            {
                if (a.HasFinished && b.HasFinished) return a.FinishTime.CompareTo(b.FinishTime);
                if (a.HasFinished) return -1;
                if (b.HasFinished) return 1;
                return b.Distance.CompareTo(a.Distance);
            });
            return standings;
        }

        public int GetPosition(RaceParticipant participant)
        {
            var order = GetStandings();
            for (int i = 0; i < order.Count; i++)
                if (order[i] == participant) return i + 1;
            return order.Count;
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            PauseChanged?.Invoke(paused);
        }

        public void Restart()
        {
            OpenMenuOnLoad = false;
            ReloadStage();
        }

        /// <summary>Back to the main menu (reloads the stage so every car, clock and effect starts clean).</summary>
        public void ExitToMenu()
        {
            OpenMenuOnLoad = true;
            ReloadStage();
        }

        private static void ReloadStage()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>False on the web, where the browser tab owns the app's lifetime.</summary>
        public static bool CanQuit =>
#if UNITY_WEBGL && !UNITY_EDITOR
            false;
#else
            true;
#endif

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            // Application.Quit() on the web only stops the player and leaves a frozen canvas.
#else
            Application.Quit();
#endif
        }

        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--.---";
            int totalMs = Mathf.RoundToInt(seconds * 1000f);
            return $"{totalMs / 60000:00}:{totalMs / 1000 % 60:00}.{totalMs % 1000:000}";
        }
    }
}
