using System.Collections.Generic;
using Rally.Track;
using UnityEngine;

namespace Rally.VFX
{
    /// <summary>
    /// Blends fog density and ambient light between the scene's original values and the settings of
    /// the <see cref="FogZone"/>s the player is currently in. Lives on the Weather object.
    /// </summary>
    public class AtmosphereController : MonoBehaviour
    {
        [Tooltip("Seconds to (almost) complete a transition in or out of a zone.")]
        [SerializeField, Min(0.1f)] private float transitionTime = 2.5f;

        public static AtmosphereController Instance { get; private set; }

        private readonly List<FogZone> activeZones = new List<FogZone>();

        private float baseFogDensity;
        private Color baseSky, baseEquator, baseGround;
        private float fogMultiplier = 1f;
        private float darkening;

        private void Awake()
        {
            Instance = this;
            baseFogDensity = RenderSettings.fogDensity;
            baseSky = RenderSettings.ambientSkyColor; // same value as RenderSettings.ambientLight
            baseEquator = RenderSettings.ambientEquatorColor;
            baseGround = RenderSettings.ambientGroundColor;
        }

        private void OnDisable() => Apply(1f, 0f);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnterZone(FogZone zone)
        {
            if (!activeZones.Contains(zone)) activeZones.Add(zone);
        }

        public void ExitZone(FogZone zone) => activeZones.Remove(zone);

        private void Update()
        {
            // Overlapping zones: the strongest one wins, so moving between adjacent zones never flickers.
            float targetFog = 1f, targetDark = 0f;
            foreach (var zone in activeZones)
            {
                targetFog = Mathf.Max(targetFog, zone.FogDensityMultiplier);
                targetDark = Mathf.Max(targetDark, zone.AmbientDarkening);
            }

            // Exponential smoothing: ~95% of the way after transitionTime seconds.
            float t = 1f - Mathf.Exp(-3f / transitionTime * Time.deltaTime);
            fogMultiplier = Mathf.Lerp(fogMultiplier, targetFog, t);
            darkening = Mathf.Lerp(darkening, targetDark, t);
            Apply(fogMultiplier, darkening);
        }

        private void Apply(float fog, float dark)
        {
            RenderSettings.fogDensity = baseFogDensity * fog;
            float k = 1f - dark;
            // Trilight ambient: scale all three colours so the whole ambient term is darker.
            RenderSettings.ambientSkyColor = Scale(baseSky, k);
            RenderSettings.ambientEquatorColor = Scale(baseEquator, k);
            RenderSettings.ambientGroundColor = Scale(baseGround, k);
        }

        private static Color Scale(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
