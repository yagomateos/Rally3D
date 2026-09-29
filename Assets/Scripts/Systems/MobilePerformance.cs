using Rally.VFX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rally.Systems
{
    /// <summary>
    /// Lighter settings for phones, applied at start by <see cref="GameBootstrap"/>: the costliest post effects off
    /// (motion blur, film grain, chromatic aberration, lens distortion), cheaper bloom and anti-aliasing, a shorter
    /// draw distance (the fog already hides it) and fewer weather particles. On the web it also sets the image
    /// quality for every device (see <see cref="WebImageQuality"/>).
    /// </summary>
    public static class MobilePerformance
    {
        public static bool Active => Application.isMobilePlatform;

        public static void Apply()
        {
            WebImageQuality();
            // Far enough for the sea and the horizon ring to reach the horizon (a shorter far plane left a band of
            // sky under the horizon). Beyond ~600 m there is only terrain, the horizon ring, sea and fog.
            var mainCam = Camera.main;
            if (mainCam != null) mainCam.farClipPlane = Active ? 3000f : 5000f;
            if (!Active) return;

            // Never simulate more than 50 ms of physics per frame on a slow phone (avoids the "spiral of death").
            Time.maximumDeltaTime = 0.05f;

            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.antialiasing = AntialiasingMode.None; // MSAA below does the edges (cheap on phone GPUs)
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

        /// <summary>
        /// The web uses the light render settings on every device, and they rendered at 80 % resolution without
        /// multisampling: edges of cars, fences and tape crawled and flickered as the menu camera turned. 4× MSAA
        /// (cheap on the tile-based GPUs of phones) and full resolution on desktop. Only in web builds, so the
        /// Editor never writes these values into the asset.
        /// </summary>
        private static void WebImageQuality()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            urp.msaaSampleCount = 4;
            urp.renderScale = Active ? 0.9f : 1f;
#endif
        }
    }
}
