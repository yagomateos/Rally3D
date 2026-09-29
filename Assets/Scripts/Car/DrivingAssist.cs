using Rally.Systems;
using UnityEngine;

namespace Rally.Car
{
    /// <summary>
    /// Driving assist (main menu option, on by default on phones where tilt and thumbs are imprecise). It blends the
    /// player's steering with a steer that follows the road, and brakes for the player when arriving too fast at a
    /// bend, using the same pure-pursuit idea as the rivals. The player still drives: their input keeps its weight
    /// and can always pull the car off line.
    /// </summary>
    public static class DrivingAssist
    {
        public enum Level { No = 0, Media = 1, Alta = 2 }
        public static readonly string[] LevelNames = { "NO", "MEDIA", "ALTA" };

        // Per level: share of the road-following steer, and how firmly it brakes for bends.
        private static readonly float[] SteerShare = { 0f, 0.45f, 0.7f };
        private static readonly float[] BrakeStrength = { 0f, 0.7f, 1f };

        private const string Key = "Rally.DrivingAssist";
        private static int cache = -1;

        public static Level Setting
        {
            get
            {
                if (cache < 0) cache = PlayerPrefs.GetInt(Key, Application.isMobilePlatform ? (int)Level.Media : (int)Level.No);
                return (Level)Mathf.Clamp(cache, 0, 2);
            }
            set
            {
                cache = (int)value;
                PlayerPrefs.SetInt(Key, cache);
                PlayerPrefs.Save();
            }
        }

        private const float LookAheadBase = 7f;
        private const float LookAheadPerMs = 0.5f;
        private const float BendGripG = 0.95f;        // lateral grip the assist dares to use, in g (× surface grip)
        private const float Deceleration = 7.5f;      // m/s² it plans to brake at
        private const float BrakeWindow = 120f;       // metres of road ahead it checks

        /// <summary>Returns <paramref name="input"/> with the assist applied (unchanged when the assist is off).</summary>
        public static CarInput Apply(CarInput input, CarController car, RaceParticipant participant)
        {
            var level = Setting;
            if (level == Level.No || car == null || participant == null || participant.Path == null || car.Body == null) return input;
            if (!car.ControlEnabled || participant.HasFinished) return input;

            var path = participant.Path;
            float speed = car.Body.linearVelocity.magnitude;
            float distance = participant.Distance;
            float half = path.WidthAt(distance) * 0.5f;
            // Far off the road it doesn't try to drag the car back across country.
            if (Mathf.Abs(participant.LateralOffset) > half + 6f) return input;

            // Steer: pure pursuit to a point on the centre line ahead.
            float target = distance + LookAheadBase + speed * LookAheadPerMs;
            Vector3 local = car.transform.InverseTransformPoint(path.PositionAt(target));
            float angle = Mathf.Atan2(local.x, Mathf.Max(0.1f, local.z)) * Mathf.Rad2Deg;
            float roadSteer = Mathf.Clamp(angle / 22f, -1f, 1f);
            float share = SteerShare[(int)level];
            input.steer = Mathf.Clamp(Mathf.Lerp(input.steer, roadSteer, share) + input.steer * share * 0.35f, -1f, 1f);

            // Brake: the safe speed now is the lowest "corner speed + braking distance" over the road ahead.
            float grip = car.Surfaces != null ? car.Surfaces.Get(path.SurfaceAt(distance)).grip : 1f;
            float lateralAccel = BendGripG * 9.81f * grip;
            float safe = float.MaxValue;
            for (float d = 5f; d <= BrakeWindow; d += 5f)
            {
                float k = Mathf.Abs(path.CurvatureAt(distance + d, 8f));
                if (k < 0.002f) continue;
                float corner = Mathf.Sqrt(lateralAccel / k);
                safe = Mathf.Min(safe, Mathf.Sqrt(corner * corner + 2f * Deceleration * d));
            }
            float excess = speed - safe;
            if (excess > 0f)
            {
                float strength = BrakeStrength[(int)level];
                input.throttle *= Mathf.Clamp01(1f - excess * 0.5f * strength);
                input.brake = Mathf.Max(input.brake, Mathf.Clamp01(excess * 0.15f) * strength);
            }
            return input;
        }
    }
}
