using System.Collections.Generic;
using System.IO;
using Rally.Audio;
using Rally.CameraSystem;
using Rally.Car;
using Rally.Systems;
using Rally.Track;
using Rally.Track.Generation;
using Rally.UI;
using Rally.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rally.EditorTools
{
    /// <summary>
    /// One-click generator for the whole playable stage: textures, materials, prefabs, terrain,
    /// road, dressing, cars, camera, lighting, post-processing, UI — saved as Assets/Scenes/Stage01.unity.
    /// The snow stage (Stage02.unity) is built with <see cref="StageTheme.Kind.Snow"/>: its own "_Snow" assets,
    /// winter colours, cold light, snowfall and a snow surface table; it reuses stage 1's car prefabs.
    /// </summary>
    public static class StageBuilder
    {
        public const string ScenePath = "Assets/Scenes/Stage01.unity";
        public const string SnowScenePath = "Assets/Scenes/Stage02.unity";
        public const string DesertScenePath = "Assets/Scenes/Stage03.unity";
        private const string DataFolder = "Assets/Settings/Rally";

        [MenuItem("Rally/Build Stage (full)")]
        public static void BuildFromMenu()
        {
            try { Build(); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("Rally/Build Snow Stage 02 (full)")]
        public static void BuildSnowFromMenu()
        {
            try { BuildSnow(); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("Rally/Build Desert Stage 03 (full)")]
        public static void BuildDesertFromMenu()
        {
            try { BuildDesert(); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        /// <summary>Entry point for -executeMethod in batch mode.</summary>
        public static void BuildFromCommandLine() => RunInBatch(Build);

        /// <summary>Entry point for -executeMethod in batch mode (snow stage).</summary>
        public static void BuildSnowFromCommandLine() => RunInBatch(BuildSnow);

        /// <summary>Entry point for -executeMethod in batch mode (desert stage).</summary>
        public static void BuildDesertFromCommandLine() => RunInBatch(BuildDesert);

        public static void BuildSnow() => Build("Stage02", SnowScenePath, StageTheme.Kind.Snow);
        public static void BuildDesert() => Build("Stage03", DesertScenePath, StageTheme.Kind.Desert);

        private static void RunInBatch(System.Action build)
        {
            try
            {
                build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build() => Build("Stage01", ScenePath, StageTheme.Kind.Forest);

        public static void Build(string stageAsset, string scenePath, StageTheme.Kind theme)
        {
            StageTheme.Current = theme;
            try { BuildStage(stageAsset, scenePath); }
            finally { StageTheme.Current = StageTheme.Kind.Forest; }
        }

        private static void BuildStage(string stageAsset, string scenePath)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool snow = StageTheme.Snow, desert = StageTheme.Desert;
            ProjectSetup.Apply();
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory("Assets/Scenes");

            var def = LoadOrCreate<StageDefinition>($"{DataFolder}/{stageAsset}.asset",
                d =>
                {
                    if (snow) d.ResetToSnowStage();
                    else if (desert) d.ResetToDesertStage();
                    else d.ResetToDefaultStage();
                });
            var tuning = LoadOrCreate<CarTuning>($"{DataFolder}/CarTuning.asset", null);
            var surfaces = LoadOrCreate<SurfaceDatabase>($"{DataFolder}/SurfaceDatabase.asset", s => s.SetSurfaces(SurfaceDatabase.CreateDefaults()));
            if (snow)
            {
                def.surfaces = LoadOrCreate<SurfaceDatabase>($"{DataFolder}/SurfaceDatabase_Snow.asset", s => s.SetSurfaces(SurfaceDatabase.CreateSnow()));
                EditorUtility.SetDirty(def);
            }
            else if (desert)
            {
                def.surfaces = LoadOrCreate<SurfaceDatabase>($"{DataFolder}/SurfaceDatabase_Desert.asset", s => s.SetSurfaces(SurfaceDatabase.CreateDesert()));
                EditorUtility.SetDirty(def);
            }

            // ---- Assets
            var lib = new AssetLibrary();
            Progress("Textures & materials", 0.05f);
            TerrainBuilder.CreateLayers(lib);
            CreateRoadMaterials(lib);
            PropFactory.CreateMaterials(lib);
            Progress("Vegetation & props", 0.12f);
            PropFactory.CreateVegetation(lib);
            PropFactory.CreateProps(lib);
            Progress("Cars", 0.18f);
            var liveries = Liveries();
            var carPrefabs = new List<GameObject>();
            if (snow || desert)
            {
                // Same cars as stage 1 (the stage swaps in its own grip table at runtime).
                foreach (var livery in liveries)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarFactory.PrefabFolder}/{livery.name}.prefab");
                    if (prefab == null) throw new System.InvalidOperationException($"Build stage 1 first: missing car prefab {livery.name}.");
                    carPrefabs.Add(prefab);
                }
            }
            else
            {
                CarFactory.CreateSharedMaterials();
                for (int i = 0; i < liveries.Length; i++)
                    carPrefabs.Add(CarFactory.BuildCarPrefab(liveries[i], i == 0, tuning, surfaces, lib.dust, lib.debris));
            }

            // ---- Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Progress("Route", 0.22f);
            var route = StageRoute.Build(def);
            var sculptor = new TerrainSculptor(def, route);
            sculptor.ComputeRoadHeights();

            Progress("Terrain", 0.3f);
            var terrain = TerrainBuilder.Build(def, route, sculptor, lib);

            var trackGo = new GameObject("Track");
            var path = trackGo.AddComponent<TrackPath>();
            var points = new Vector3[route.Count];
            for (int i = 0; i < route.Count; i++) points[i] = route.Point(i);
            path.SetData(points, route.Widths.ToArray(), route.Surfaces.ToArray(), route.Spacing,
                def.startLineDistance, route.Length - def.finishLineOffset);

            Progress("Road & dressing", 0.6f);
            var decorator = new TrackDecorator(def, route, sculptor, lib, terrain);
            decorator.BuildRoads();
            if (!desert) decorator.BuildPuddles();
            decorator.BuildGantry(path.StartDistance, "SALIDA");
            decorator.BuildGantry(path.FinishDistance, "META");
            decorator.BuildStageBoard(path.StartDistance - 30f);
            decorator.BuildGuardRails();
            decorator.BuildVillage();
            decorator.DressCorners();
            decorator.BuildJumpZones();
            decorator.PlaceMarkerPoles();
            decorator.ScatterRocksAndProps();
            var checkpointRoot = new GameObject("Checkpoints").transform;
            checkpointRoot.SetParent(trackGo.transform, false);
            var checkpoints = decorator.BuildCheckpoints(path, checkpointRoot);

            Progress("Trees", 0.75f);
            TerrainBuilder.PlantTrees(terrain, route, sculptor, lib, decorator.IsBlocked);
            DressingRandomizer.Apply();

            // ---- Gameplay
            Progress("Gameplay objects", 0.85f);
            var systems = new GameObject("Systems");
            systems.AddComponent<GameBootstrap>();
            systems.AddComponent<RallyInput>();

            var participants = SpawnCars(carPrefabs, path);
            var player = participants[0];

            var raceGo = new GameObject("RaceManager");
            raceGo.AddComponent<RaceManager>().Configure(def, path, checkpoints, participants.ToArray());

            var cam = CreateCamera(player.GetComponent<CarController>());
            foreach (var p in participants)
            {
                var feedback = p.GetComponent<CarFeedback>();
                if (feedback != null) CarFactory.SetSerialized(feedback, "rallyCamera", cam);
            }

            var volume = CreateLighting(lib);
            volume.gameObject.AddComponent<SpeedPostProcess>();

            var skid = new GameObject("SkidMarks");
            skid.AddComponent<MeshFilter>();
            var skidRenderer = skid.AddComponent<MeshRenderer>();
            skidRenderer.sharedMaterial = lib.skid;
            skidRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skid.AddComponent<SkidMarks>();

            var weather = new GameObject("Weather").AddComponent<WeatherEffects>();
            weather.Configure(cam.transform, lib.mote, lib.drizzle, lib.fogWisp, snow, desert);
            FogZoneBuilder.Build(def, path);
            FogZoneBuilder.EnsureController();

            new GameObject("StageAudio").AddComponent<StageAudio>();
            new GameObject("HUD").AddComponent<RaceHUD>();
            new GameObject("Menus").AddComponent<StageMenus>();

            // ---- Save
            Progress("Saving", 0.95f);
            EditorSceneManager.SaveScene(scene, scenePath);
            // Keep every stage in the build (stage 1 first, it opens with the main menu).
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(b => b.path == scenePath)) buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            buildScenes.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Rally] Stage built in {timer.Elapsed.TotalSeconds:0.0}s. Length {path.StageLength:0} m, {route.Jumps.Count} jumps, {checkpoints.Length} checkpoints.");
        }

        private static void Progress(string step, float t)
        {
            Debug.Log($"[Rally] {step}...");
            if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("Rally Stage Builder", step, t);
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static CarFactory.Livery[] Liveries() => new[]
        {
            new CarFactory.Livery { name = "PlayerCar", driver = "TÚ", paint = new Color(0.92f, 0.92f, 0.9f), accent = new Color(1f, 0.42f, 0.1f), stripe = new Color(0.05f, 0.12f, 0.32f), number = "7" },
            new CarFactory.Livery { name = "RivalCar_Azure", driver = "M. KORVEN", paint = new Color(0.06f, 0.18f, 0.5f), accent = new Color(0.98f, 0.8f, 0.1f), stripe = new Color(0.9f, 0.9f, 0.9f), number = "3" },
            new CarFactory.Livery { name = "RivalCar_Crimson", driver = "L. ARANDA", paint = new Color(0.55f, 0.04f, 0.05f), accent = new Color(0.95f, 0.95f, 0.95f), stripe = new Color(0.05f, 0.05f, 0.05f), number = "11" },
        };

        private static void CreateRoadMaterials(AssetLibrary lib)
        {
            var (dirt, dirtN) = ProceduralTextures.RoadDirt();
            var (gravel, gravelN) = ProceduralTextures.RoadGravel();
            var (mud, mudN) = ProceduralTextures.RoadMud();
            var (asphalt, asphaltN) = ProceduralTextures.RoadAsphalt();
            lib.roadDirt = MaterialFactory.Opaque("Road_Dirt", Color.white, dirt, dirtN, 0.7f, 0f, 0.8f, null, "Track");
            lib.roadGravel = MaterialFactory.Opaque("Road_Gravel", Color.white, gravel, gravelN, 0.7f, 0f, 1f, null, "Track");
            lib.roadMud = MaterialFactory.Opaque("Road_Mud", Color.white, mud, mudN, 0.72f, 0f, 1f, null, "Track");
            lib.roadAsphalt = MaterialFactory.Opaque("Road_Asphalt", Color.white, asphalt, asphaltN, 0.85f, 0f, 0.8f, null, "Track");
        }

        // ------------------------------------------------------------------ cars

        private static List<RaceParticipant> SpawnCars(List<GameObject> prefabs, TrackPath path)
        {
            var root = new GameObject("Cars").transform;
            var result = new List<RaceParticipant>();
            // Grid: two rivals on the front row, the player behind them.
            var slots = new[]
            {
                new Vector2(-10f, 0f),    // player (distance offset from the line, lateral)
                new Vector2(-4f, -2.3f),
                new Vector2(-4f, 2.3f)
            };
            float[] skills = { 0f, 0.84f, 0.78f };
            float[] power = { 1f, 1f, 0.97f };
            for (int i = 0; i < prefabs.Count; i++)
            {
                float d = path.StartDistance + slots[i].x;
                Vector3 tangent = path.TangentAt(d);
                Vector3 pos = path.PositionAt(d) + path.RightAt(d) * slots[i].y + Vector3.up * 0.1f;
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], root);
                car.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(tangent.x, 0f, tangent.z)));
                if (i > 0)
                {
                    var ai = car.GetComponent<AI.AIDriver>();
                    ai.Configure(skills[i], power[i], slots[i].y * 0.6f, 0.06f + i * 0.02f, 0.12f + i * 0.05f, 150f - i * 8f);
                }
                result.Add(car.GetComponent<RaceParticipant>());
            }
            return result;
        }

        // ------------------------------------------------------------------ camera & lighting

        private static RallyCamera CreateCamera(CarController target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2500f;
            cam.fieldOfView = 58f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            go.AddComponent<AudioListener>();

            var rally = go.AddComponent<RallyCamera>();
            var so = new SerializedObject(rally);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("obstacleMask").intValue = ~((1 << ProjectSetup.VehicleLayerIndex) | (1 << 2) | (1 << 5));
            so.ApplyModifiedPropertiesWithoutUndo();

            Vector3 back = target.transform.position - target.transform.forward * 6f + Vector3.up * 2f;
            go.transform.SetPositionAndRotation(back, Quaternion.LookRotation(target.transform.position - back));
            return rally;
        }

        private static Volume CreateLighting(AssetLibrary lib)
        {
            // Sun, soft through thin clouds.
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            bool snow = StageTheme.Snow, desert = StageTheme.Desert;
            sun.color = snow ? new Color(0.93f, 0.96f, 1f) : desert ? new Color(1f, 0.92f, 0.78f) : new Color(1f, 0.95f, 0.88f);
            sun.intensity = snow ? 1.7f : desert ? 2.7f : 2.1f; // desert: strong, high sun with hard shadows
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = snow ? 0.6f : desert ? 0.85f : 0.72f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            sunGo.transform.rotation = Quaternion.Euler(desert ? 55f : 36f, -38f, 0f);
            sunGo.AddComponent<UniversalAdditionalLightData>();

            // Sky, ambient, fog.
            var skyTex = desert ? ProceduralTextures.ClearSky() : ProceduralTextures.OvercastSky();
            var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = desert ? "Sky_Clear" : "Sky_Overcast" };
            sky.SetTexture("_MainTex", skyTex);
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetFloat("_Rotation", 0f);
            sky = MaterialFactory.Save(sky, "Environment");

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = snow ? new Color(0.84f, 0.88f, 0.96f) : desert ? new Color(0.72f, 0.8f, 0.92f) : new Color(0.78f, 0.82f, 0.88f);
            RenderSettings.ambientEquatorColor = snow ? new Color(0.76f, 0.8f, 0.87f) : desert ? new Color(0.84f, 0.74f, 0.6f) : new Color(0.62f, 0.64f, 0.6f);
            // Snow and sand bounce a lot of light back up.
            RenderSettings.ambientGroundColor = snow ? new Color(0.68f, 0.71f, 0.78f) : desert ? new Color(0.62f, 0.5f, 0.36f) : new Color(0.32f, 0.3f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            // Desert calima: warm haze that softens the distance but still shows the next corners (~300 m to half fog).
            RenderSettings.fogDensity = snow ? 0.0036f : desert ? 0.0023f : 0.0026f;
            RenderSettings.fogColor = snow ? new Color(0.86f, 0.89f, 0.94f) : desert ? new Color(0.87f, 0.79f, 0.66f) : new Color(0.68f, 0.71f, 0.74f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;

            // Sky reflections for wet surfaces and car paint.
            var probeGo = new GameObject("SkyReflectionProbe");
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(8000f, 4000f, 8000f);
            probe.resolution = 128;
            probe.cullingMask = 0;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.importance = 0;

            // Post-processing.
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(snow ? 0.1f : desert ? 0.05f : 0.35f); // snow and sand are bright already
            color.contrast.Override(desert ? 18f : 14f);
            color.saturation.Override(desert ? 8f : 2f);
            color.colorFilter.Override(desert ? new Color(1f, 0.96f, 0.9f) : new Color(0.98f, 0.99f, 1f));
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(snow ? -14f : desert ? 16f : -6f); // desert: warm tones
            if (desert) wb.tint.Override(3f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.95f, 1f, 1.05f, -0.02f));
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(desert ? 1f : 1.05f);
            bloom.intensity.Override(desert ? 0.35f : 0.28f);
            bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
            var chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);
            var blur = profile.Add<MotionBlur>(true);
            blur.intensity.Override(0.1f);
            blur.quality.Override(MotionBlurQuality.Medium);
            var grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.08f);
            grain.type.Override(FilmGrainLookup.Thin1);

            string profilePath = $"Assets/Settings/Rally/{StageTheme.Name("StagePostProcess")}.asset";
            AssetDatabase.DeleteAsset(profilePath);
            AssetDatabase.CreateAsset(profile, profilePath);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            AssetDatabase.SaveAssets();

            var volumeGo = new GameObject("PostProcessVolume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            return volume;
        }
    }
}
