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
    /// Stage flow: title card → countdown → racing → finished. Owns the stage clock, positions,
    /// best time, pause and restart.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public enum State { Intro, Countdown, Racing, Finished }

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

        public State CurrentState { get; private set; } = State.Intro;
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
            Time.timeScale = 1f;
            AudioListener.pause = false;
            BestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
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
                if (p.IsPlayer) Player = p;
            }
            standings.AddRange(participants);
        }

        private void Update()
        {
            var input = RallyInput.Instance;
            if (input == null) return;

            if (input.Pause.WasPressedThisFrame() && CurrentState != State.Intro && !ResultsShown)
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
                    if (Player != null && !Player.HasFinished && input.ResetCar.WasPressedThisFrame())
                        Player.ResetToTrack();
                    break;
                case State.Finished:
                    StageTime += Time.deltaTime;
                    break;
            }

            if (input.RestartStage.WasPressedThisFrame() && CurrentState != State.Intro) Restart();
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
            if (!hasFocus && CurrentState != State.Intro && !ResultsShown) SetPaused(true);
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

        private void OnCheckpointPassed(RaceParticipant participant, Checkpoint checkpoint)
        {
            CheckpointPassed?.Invoke(participant, checkpoint, StageTime);
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
