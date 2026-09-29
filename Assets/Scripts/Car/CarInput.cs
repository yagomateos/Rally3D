namespace Rally.Car
{
    /// <summary>Driver commands for one physics step. Values are normalised.</summary>
    public struct CarInput
    {
        /// <summary>-1 (left) .. 1 (right)</summary>
        public float steer;
        /// <summary>0..1</summary>
        public float throttle;
        /// <summary>0..1 — brakes, or reverses when the car is (almost) stopped.</summary>
        public float brake;
        public bool handbrake;

        public static CarInput None => default;
    }

    /// <summary>Anything that can drive a car: the player, the AI, a replay...</summary>
    public interface ICarInputSource
    {
        CarInput ReadInput();
    }
}
