using Rally.VFX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rally.Systems
{
    /// <summary>
    /// Lighter settings for phones, applied at start by <see cref="GameBootstrap"/>: the costliest post effects off
    /// (motion blur, film grain, chromatic aberration, lens distortion), cheaper bloom and anti-aliasing, a shorter
    /// draw distance (the fog already hides it) and fewer weather particles. Desktop is untouched.
    /// </summary>
    public static class MobilePerformance
    {
        public static bool Active => Application.isMobilePlatform;

        public static void Apply()
        {
            if (!Active) return;

            // Never simulate more than 50 ms of physics per frame on a slow phone (avoids the "spiral of death").
            Time.maximumDeltaTime = 0.05f;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Min(cam.farClipPlane, 900f);
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }

            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                var profile = volume.profile; // runtime copy; the asset on disk is not modified
                if (profile.TryGet(out MotionBlur blur)) blur.active = false;
                if (profile.TryGet(out FilmGrain grain)) grain.active = false;
                if (profile.TryGet(out ChromaticAberration chroma)) chroma.active = false;
                if (profile.TryGet(out LensDistortion lens)) lens.active = false;
                if (profile.TryGet(out Bloom bloom))
                {
                    bloom.highQualityFiltering.overrideState = true;
                    bloom.highQualityFiltering.value = false;
                    bloom.maxIterations.overrideState = true;
                    bloom.maxIterations.value = Mathf.Min(bloom.maxIterations.value, 4);
                }
            }

            SpeedPostProcess.LowSpec = true;
            WeatherEffects.Density = 0.5f;
        }
    }
}
