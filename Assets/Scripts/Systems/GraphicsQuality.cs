using Rally.VFX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rally.Systems
{
    /// <summary>
    /// CALIDAD GRÁFICA option (BAJA / MEDIA / ALTA): resolution, edge smoothing, shadows, draw distances, weather
    /// particles and post effects. MEDIA by default on phones, ALTA on computers. Applied when a stage loads and at
    /// once when changed in the menu (weather particles take effect from the next stage load). The render asset is
    /// only changed in web / player builds, never in the Editor, so the project asset on disk stays as it is.
    /// </summary>
    public static class GraphicsQuality
    {
        public enum Level { Baja = 0, Media = 1, Alta = 2 }
        public static readonly string[] LevelNames = { "BAJA", "MEDIA", "ALTA" };

        private const string Key = "Rally.Quality";
        private static int cache = -1;

        public static Level Setting
        {
            get
            {
                if (cache < 0) cache = PlayerPrefs.GetInt(Key, Application.isMobilePlatform ? (int)Level.Media : (int)Level.Alta);
                return (Level)Mathf.Clamp(cache, 0, 2);
            }
            set
            {
                cache = (int)value;
                PlayerPrefs.SetInt(Key, cache);
                PlayerPrefs.Save();
                Apply();
            }
        }

#pragma warning disable 0414 // the render-asset values are only used outside the Editor
        //                                         BAJA    MEDIA   ALTA
        private static readonly float[] FarClip = { 1500f, 3000f, 5000f };
        private static readonly int[] Msaa = { 1, 2, 4 };
        private static readonly float[] RenderScale = { 0.75f, 0.9f, 1f };
        private static readonly float[] ShadowDistance = { 0f, 50f, 80f };
        private static readonly float[] Weather = { 0.3f, 0.5f, 1f };
        private static readonly float[] PixelError = { 8f, 5f, 3f };
        private static readonly float[] TreeDistance = { 350f, 450f, 560f };
        private static readonly float[] GrassDistance = { 50f, 90f, 120f };
#pragma warning restore 0414

        public static float FarClipDistance => FarClip[(int)Setting];

        public static void Apply()
        {
            int q = (int)Setting;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = FarClip[q];
                // 0.3 m (was 0.1): three times the depth precision, which the far plane of several km needs on the
                // web (surfaces close together flickered or hid each other). Nothing the cameras see is nearer.
                cam.nearClipPlane = 0.3f;
                var data = cam.GetUniversalAdditionalCameraData();
                // Post-process edge smoothing only where MSAA alone isn't enough and the GPU can afford it.
                if (data != null)
                    data.antialiasing = q == (int)Level.Alta && !Application.isMobilePlatform
                        ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                        : AntialiasingMode.None;
            }

            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                terrain.heightmapPixelError = PixelError[q];
                terrain.treeDistance = TreeDistance[q];
                terrain.detailObjectDistance = GrassDistance[q];
            }

            WeatherEffects.Density = Weather[q];
            SpeedPostProcess.LowSpec = q < (int)Level.Alta;

            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                var profile = volume.profile; // runtime copy; the asset on disk is not modified
                bool full = q == (int)Level.Alta;
                if (profile.TryGet(out MotionBlur blur)) blur.active = full;
                if (profile.TryGet(out FilmGrain grain)) grain.active = full;
                if (profile.TryGet(out ChromaticAberration chroma)) chroma.active = full;
                if (profile.TryGet(out LensDistortion lens)) lens.active = full;
                if (profile.TryGet(out Bloom bloom))
                {
                    bloom.active = q > (int)Level.Baja;
                    bloom.highQualityFiltering.overrideState = true;
                    bloom.highQualityFiltering.value = full;
                }
            }

#if !UNITY_EDITOR
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.msaaSampleCount = Msaa[q];
                urp.renderScale = RenderScale[q];
                urp.shadowDistance = ShadowDistance[q];
            }
#endif
        }
    }
}
