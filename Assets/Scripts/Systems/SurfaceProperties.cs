using System;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>Physical and cosmetic behaviour of a driving surface.</summary>
    [Serializable]
    public struct SurfaceProperties
    {
        public SurfaceType type;

        [Header("Physics")]
        [Tooltip("Multiplier applied to tyre friction stiffness.")]
        [Range(0.2f, 2f)] public float grip;
        [Tooltip("Resistance force per unit of speed (N per m/s) per wheel on this surface.")]
        [Range(0f, 40f)] public float rollingResistance;
        [Tooltip("How bumpy the surface feels (camera shake, body vibration).")]
        [Range(0f, 1f)] public float bumpiness;

        [Header("Effects")]
        [Range(0f, 2f)] public float dustAmount;
        public Color dustColor;
        [Tooltip("Size multiplier for dust clouds and wheel puffs on this surface.")]
        [Range(0.2f, 2f)] public float dustSize;
        [Tooltip("Amount of heavy debris (gravel stones / mud clods) thrown by wheels.")]
        [Range(0f, 2f)] public float debrisAmount;
        public Color debrisColor;
        [Range(0f, 1f)] public float rollingNoise;
        [Range(0f, 1f)] public float skidNoise;
        [Range(0f, 1f)] public float skidMarkOpacity;

        public static SurfaceProperties Default => new SurfaceProperties
        {
            type = SurfaceType.Dirt,
            grip = 1f,
            rollingResistance = 6f,
            bumpiness = 0.35f,
            dustAmount = 1f,
            dustColor = new Color(0.66f, 0.56f, 0.44f, 1f),
            dustSize = 1f,
            debrisAmount = 0.5f,
            debrisColor = new Color(0.3f, 0.24f, 0.18f, 1f),
            rollingNoise = 0.6f,
            skidNoise = 0.6f,
            skidMarkOpacity = 0.5f
        };
    }
}
