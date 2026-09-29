using System;
using Rally.Car;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// The three difficulty levels (FÁCIL, NORMAL, DIFÍCIL): how the rivals drive and how much help the player's car
    /// gets. Lives in Resources/DifficultyData.asset (edit it in the Inspector); if missing, the defaults below are
    /// used. DIFÍCIL is the game as originally tuned; NORMAL is meant to let an average player win after a couple
    /// of tries. The chosen level is saved with PlayerPrefs.
    /// </summary>
    [CreateAssetMenu(menuName = "Rally/Difficulty Data", fileName = "DifficultyData")]
    public class DifficultyData : ScriptableObject
    {
        [Serializable]
        public struct Level
        {
            public string name;

            [Header("Rivals (multipliers on each rival's own values)")]
            [Tooltip("Overall pace (target speed everywhere).")] public float pace;
            [Tooltip("Cornering grip the AI dares to use.")] public float corneringSkill;
            [Tooltip("Top speed.")] public float topSpeed;
            [Tooltip("Engine power.")] public float power;
            [Tooltip("Chance of making mistakes (running wide, lifting).")] public float mistakes;
            [Tooltip("Steering wobble.")] public float steeringNoise;
            [Tooltip("How much a rival far ahead of the player eases off (0-1).")] public float catchUp;

            [Header("Player's car assists")]
            [Tooltip("Tyre grip (forward and sideways stiffness).")] public float grip;
            [Tooltip("Rear grip kept under power in corners (higher = less oversteer; car default 0.86).")] public float powerOversteerGrip;
            [Tooltip("Brake strength.")] public float brakes;
            [Tooltip("Yaw stability assist.")] public float stability;
            [Tooltip("Traction control (0-1; car default 0.35).")] public float tractionControl;
        }

        public Level[] levels = Defaults();

        public static Level[] Defaults() => new[]
        {
            new Level
            {
                name = "FÁCIL", pace = 0.82f, corneringSkill = 0.88f, topSpeed = 0.92f, power = 0.94f, mistakes = 1.6f, steeringNoise = 1.3f, catchUp = 0.22f,
                grip = 1.10f, powerOversteerGrip = 0.93f, brakes = 1.15f, stability = 1.35f, tractionControl = 0.6f
            },
            new Level
            {
                name = "NORMAL", pace = 0.90f, corneringSkill = 0.93f, topSpeed = 0.96f, power = 0.97f, mistakes = 1.25f, steeringNoise = 1.1f, catchUp = 0.15f,
                grip = 1.05f, powerOversteerGrip = 0.90f, brakes = 1.08f, stability = 1.15f, tractionControl = 0.45f
            },
            new Level
            {
                name = "DIFÍCIL", pace = 1f, corneringSkill = 1f, topSpeed = 1f, power = 1f, mistakes = 1f, steeringNoise = 1f, catchUp = 0.06f,
                grip = 1f, powerOversteerGrip = 0.86f, brakes = 1f, stability = 1f, tractionControl = 0.35f
            },
        };

        // ------------------------------------------------------------------ selection

        private const string Key = "Rally.Difficulty";
        private static int selectedCache = -1;
        private static DifficultyData instance;

        /// <summary>The asset in Resources (or a default instance when it is missing).</summary>
        public static DifficultyData Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<DifficultyData>("DifficultyData");
                if (instance == null) instance = CreateInstance<DifficultyData>();
                return instance;
            }
        }

        /// <summary>Chosen level, 0..2. Default: FÁCIL on phones (harder to drive), NORMAL elsewhere.</summary>
        public static int Selected
        {
            get
            {
                if (selectedCache < 0) selectedCache = PlayerPrefs.GetInt(Key, Application.isMobilePlatform ? 0 : 1);
                return Mathf.Clamp(selectedCache, 0, 2);
            }
            set
            {
                selectedCache = Mathf.Clamp(value, 0, 2);
                PlayerPrefs.SetInt(Key, selectedCache);
                PlayerPrefs.Save();
            }
        }

        public static Level Current => Instance.levels[Mathf.Clamp(Selected, 0, Instance.levels.Length - 1)];

        /// <summary>Player's car assists for the current level, applied to the player's own tuning copy.</summary>
        public static void ApplyToPlayer(CarTuning tuning)
        {
            var l = Current;
            tuning.forwardStiffness *= l.grip;
            tuning.sidewaysStiffness *= l.grip;
            tuning.powerOversteerGrip = Mathf.Max(tuning.powerOversteerGrip, l.powerOversteerGrip);
            tuning.brakeTorque *= l.brakes;
            tuning.yawStability *= l.stability;
            tuning.tractionControl = Mathf.Max(tuning.tractionControl, l.tractionControl);
        }
    }
}
