using System;
using System.Collections.Generic;
using Rally.Systems;
using UnityEngine;

namespace Rally.Track
{
    public enum SegmentKind { Straight, Turn }

    public enum RoadsideStyle
    {
        Forest,  // dense trees close to the road
        Open,    // meadow, sparse trees
        Village  // tarmac with guard rails
    }

    [Serializable]
    public struct TrackSegment
    {
        public SegmentKind kind;
        [Tooltip("Straight: length in metres. Turn: angle in degrees (+ right, - left).")]
        public float amount;
        [Tooltip("Turn radius in metres (turns only).")]
        public float radius;
        public float width;
        public SurfaceType surface;
        public RoadsideStyle roadside;
        [Tooltip("0 = none. Height in metres of a jump crest placed in the middle of the segment.")]
        public float jumpHeight;

        public static TrackSegment Straight(float length, float width, SurfaceType surface, RoadsideStyle side, float jump = 0f) =>
            new TrackSegment { kind = SegmentKind.Straight, amount = length, width = width, surface = surface, roadside = side, jumpHeight = jump };

        public static TrackSegment Turn(float angle, float radius, float width, SurfaceType surface, RoadsideStyle side) =>
            new TrackSegment { kind = SegmentKind.Turn, amount = angle, radius = radius, width = width, surface = surface, roadside = side };
    }

    /// <summary>Authoring data for a rally stage: route, surfaces and terrain look.</summary>
    [CreateAssetMenu(menuName = "Rally/Stage Definition", fileName = "StageDefinition")]
    public class StageDefinition : ScriptableObject
    {
        public string stageNumber = "TRAMO 01";
        public string stageName = "PINAR DE VALDENIEBLA";
        public int seed = 1207;

        [Header("Look & rules")]
        [Tooltip("Line under the stage name on the title card and in the stage list.")]
        public string description = "GRAVA · TIERRA · BARRO · ASFALTO   ·   NUBLADO, MOJADO";
        [Tooltip("0 = pine forest after rain. 1 = snow. 2 = desert. 3 = coast at sunset (the builder makes each theme's own textures and materials).")]
        public int theme;

        public const int ForestTheme = 0, SnowTheme = 1, DesertTheme = 2, CoastTheme = 3, NightTheme = 4;

        [Tooltip("Top speed of every car on this stage (the player's limiter and gearing, the rivals' speed plan). " +
                 "Above 1 on the fast tarmac stage as a challenge.")]
        [Range(1f, 1.3f)] public float topSpeedScale = 1f;
        [Tooltip("Grip / dust per surface for this stage. Empty = the table the cars were built with.")]
        public SurfaceDatabase surfaces;

        [Header("Route")]
        public Vector2 startPosition = new Vector2(180f, 140f);
        public float startHeading;
        public float sampleSpacing = 2f;
        public float startLineDistance = 40f;
        public float finishLineOffset = 130f;
        public int checkpointCount = 10;
        public List<TrackSegment> segments = new List<TrackSegment>();

        [Header("Terrain")]
        public float terrainMargin = 260f;
        public float terrainMaxHeight = 220f;
        public float hillAmplitude = 60f;
        public float hillScale = 380f;
        public float roadSmoothingDistance = 55f;
        [Range(0.02f, 0.2f)] public float maxRoadGradient = 0.115f;

        /// <summary>
        /// Stage 2: the snow stage. The route is stage 1 mirrored (every corner turns the other way), a bit
        /// narrower and in taller mountains. Surfaces mean: Dirt = packed snow, Gravel = snow over gravel,
        /// Mud = slush, Asphalt = ice, Grass (off the road) = deep snow; see the stage's snow SurfaceDatabase.
        /// </summary>
        public void ResetToSnowStage()
        {
            ResetToDefaultStage();
            stageNumber = "TRAMO 02";
            stageName = "PUERTO DE PEÑA BLANCA";
            description = "NIEVE · HIELO · NIEVE BLANDA   ·   NEVANDO";
            theme = SnowTheme;
            seed = 3311;
            startHeading = 35f;
            hillAmplitude = 85f;
            terrainMaxHeight = 260f;
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                if (s.kind == SegmentKind.Turn) s.amount = -s.amount;
                s.width = Mathf.Max(6.5f, s.width - 0.5f);
                segments[i] = s;
            }
        }

        /// <summary>
        /// Stage 3: desert dunes. Its own route: wide and fast between the dunes, two hairpins, an oasis village
        /// on old tarmac and three dune-crest jumps. Surfaces mean: Dirt = packed-sand piste, Gravel = rocky
        /// hamada, Mud = soft sand drifts, Asphalt = old tarmac, Grass (off the road) = loose sand; see the stage's
        /// desert SurfaceDatabase.
        /// </summary>
        public void ResetToDesertStage()
        {
            const SurfaceType D = SurfaceType.Dirt, G = SurfaceType.Gravel, M = SurfaceType.Mud, A = SurfaceType.Asphalt;
            const RoadsideStyle O = RoadsideStyle.Open, V = RoadsideStyle.Village;

            stageNumber = "TRAMO 03";
            stageName = "DUNAS DEL DESIERTO";
            description = "ARENA · PISTA DURA · ARENA BLANDA · ROCA   ·   DESPEJADO, CALIMA";
            theme = DesertTheme;
            seed = 5521;
            startPosition = new Vector2(180f, 140f);
            startHeading = -20f;
            hillAmplitude = 26f;
            hillScale = 320f;
            terrainMaxHeight = 200f;

            segments = new List<TrackSegment>
            {
                TrackSegment.Straight(120, 12, G, O),
                TrackSegment.Turn(25, 300, 11, D, O),          // flat-out right
                TrackSegment.Straight(150, 11, D, O),
                TrackSegment.Turn(-60, 140, 10, D, O),         // fast left
                TrackSegment.Straight(80, 10, D, O, 1.8f),     // dune crest 1
                TrackSegment.Turn(90, 75, 10, D, O),           // medium right
                TrackSegment.Straight(60, 10, M, O),           // soft sand
                TrackSegment.Turn(-45, 120, 10, M, O),
                TrackSegment.Straight(130, 10, D, O),
                TrackSegment.Turn(-150, 22, 11, D, O),         // hairpin left
                TrackSegment.Straight(90, 10, G, O),
                TrackSegment.Turn(70, 90, 10, G, O),           // rocky esses
                TrackSegment.Turn(-50, 110, 10, G, O),
                TrackSegment.Straight(70, 9, A, V),            // oasis village on old tarmac
                TrackSegment.Turn(55, 85, 9, A, V),
                TrackSegment.Straight(120, 9, A, V),
                TrackSegment.Turn(-40, 140, 9, A, V),
                TrackSegment.Straight(60, 10, D, O),
                TrackSegment.Turn(35, 220, 11, D, O),
                TrackSegment.Straight(170, 11, D, O, 2.0f),    // dune crest 2
                TrackSegment.Turn(80, 60, 10, D, O),           // tight right
                TrackSegment.Straight(60, 10, M, O),           // soft sand
                TrackSegment.Turn(-60, 90, 10, M, O),
                TrackSegment.Straight(90, 10, D, O),
                TrackSegment.Turn(140, 25, 11, D, O),          // hairpin right
                TrackSegment.Straight(80, 10, D, O),
                TrackSegment.Turn(-40, 150, 11, G, O),
                TrackSegment.Straight(100, 11, G, O, 1.6f),    // dune crest 3
                TrackSegment.Turn(30, 200, 12, G, O),
                TrackSegment.Straight(120, 12, G, O),          // finish straight
                TrackSegment.Straight(115, 12, G, O),          // stop zone after the flying finish
            };
        }

        /// <summary>
        /// Stage 4: coastal tarmac at sunset. All asphalt with high grip, long fast curves between the cliffs and
        /// the sea, a fishing village, one hairpin and no jumps. Higher top speed for every car (the challenge).
        /// Guard rails, street lights and curve warning signs are added by the builder for this theme.
        /// </summary>
        public void ResetToCoastStage()
        {
            const SurfaceType A = SurfaceType.Asphalt;
            const RoadsideStyle O = RoadsideStyle.Open, V = RoadsideStyle.Village;

            stageNumber = "TRAMO 04";
            stageName = "COSTERA DE ASFALTO";
            description = "ASFALTO · CURVAS RÁPIDAS · MÁS VELOCIDAD   ·   PUESTA DE SOL";
            theme = CoastTheme;
            topSpeedScale = 1.15f;
            seed = 7043;
            startPosition = new Vector2(180f, 140f);
            startHeading = 10f;
            hillAmplitude = 45f;
            hillScale = 420f;
            terrainMaxHeight = 220f;
            maxRoadGradient = 0.09f; // smooth, fast tarmac

            segments = new List<TrackSegment>
            {
                TrackSegment.Straight(120, 10, A, O),
                TrackSegment.Turn(-35, 320, 9.5f, A, O),        // flat-out left
                TrackSegment.Straight(130, 9.5f, A, O),
                TrackSegment.Turn(50, 260, 9.5f, A, O),         // long fast right
                TrackSegment.Straight(70, 9.5f, A, O),
                TrackSegment.Turn(-70, 180, 9.5f, A, O),        // sweeping esses
                TrackSegment.Turn(45, 220, 9.5f, A, O),
                TrackSegment.Straight(140, 9.5f, A, O),
                TrackSegment.Turn(-60, 150, 9, A, O),
                TrackSegment.Straight(60, 9, A, O),
                TrackSegment.Turn(-95, 110, 9, A, O),           // long medium left
                TrackSegment.Straight(100, 8.5f, A, V),         // fishing village
                TrackSegment.Turn(-40, 300, 8.5f, A, V),
                TrackSegment.Straight(110, 8.5f, A, V),
                TrackSegment.Turn(120, 60, 9, A, O),            // tightening right above the cliffs
                TrackSegment.Straight(80, 9, A, O),
                TrackSegment.Turn(55, 200, 9.5f, A, O),
                TrackSegment.Turn(-45, 240, 9.5f, A, O),
                TrackSegment.Straight(120, 9.5f, A, O),
                TrackSegment.Turn(70, 140, 9, A, O),
                TrackSegment.Straight(80, 9, A, O),
                TrackSegment.Turn(-165, 28, 10, A, O),          // hairpin left
                TrackSegment.Straight(100, 9.5f, A, O),
                TrackSegment.Turn(40, 260, 10, A, O),
                TrackSegment.Straight(120, 10, A, O),           // finish straight
                TrackSegment.Straight(115, 10, A, O),           // stop zone after the flying finish
            };
        }

        /// <summary>
        /// Stage 5: stage 1's pine forest at night. Same route and terrain shape; moonlight, dark fog, a starry sky
        /// and real headlights on every car (added at load by the race manager for this theme).
        /// </summary>
        public void ResetToNightStage()
        {
            ResetToDefaultStage();
            stageNumber = "TRAMO 05";
            stageName = "PINAR DE NOCHE";
            description = "TIERRA · GRAVA · BARRO · ASFALTO   ·   NOCHE CERRADA, FAROS";
            theme = NightTheme;
        }

        /// <summary>This stage's rules on a car's tuning copy (never on the shared asset).</summary>
        public void ApplyToTuning(Rally.Car.CarTuning tuning)
        {
            if (topSpeedScale <= 1f) return;
            tuning.maxSpeedKph *= topSpeedScale;
            tuning.finalDrive /= topSpeedScale; // taller gearing so the engine can reach the higher limiter
        }

        public void ResetToDefaultStage()
        {
            const SurfaceType D = SurfaceType.Dirt, G = SurfaceType.Gravel, M = SurfaceType.Mud, A = SurfaceType.Asphalt;
            const RoadsideStyle F = RoadsideStyle.Forest, O = RoadsideStyle.Open, V = RoadsideStyle.Village;

            segments = new List<TrackSegment>
            {
                TrackSegment.Straight(110, 11, G, O),
                TrackSegment.Turn(30, 260, 9, D, O),          // fast right
                TrackSegment.Straight(70, 9, D, F),
                TrackSegment.Turn(-55, 160, 9, D, F),         // fast left
                TrackSegment.Straight(50, 9, D, F),
                TrackSegment.Turn(85, 60, 8.5f, D, F),        // medium right
                TrackSegment.Straight(90, 8.5f, D, O, 1.7f),  // jump 1
                TrackSegment.Turn(-40, 110, 8, D, F),         // esses
                TrackSegment.Turn(45, 100, 8, D, F),
                TrackSegment.Turn(-35, 120, 8, M, F),         // muddy bend
                TrackSegment.Straight(120, 8, D, F),
                TrackSegment.Turn(-175, 16, 9, D, O),         // hairpin left
                TrackSegment.Straight(60, 7.5f, D, F),
                TrackSegment.Turn(55, 70, 7, D, F),           // narrow forest
                TrackSegment.Turn(-60, 60, 7, D, F),
                TrackSegment.Straight(70, 7, D, F),
                TrackSegment.Turn(80, 45, 7, D, F),           // tight right
                TrackSegment.Straight(45, 7, M, F),           // mud
                TrackSegment.Turn(-30, 150, 8, D, O),
                TrackSegment.Straight(60, 8, A, V),           // tarmac village section
                TrackSegment.Turn(60, 90, 8, A, V),
                TrackSegment.Straight(110, 8, A, V),
                TrackSegment.Turn(-70, 80, 8, A, V),
                TrackSegment.Straight(60, 8, A, V),
                TrackSegment.Turn(35, 180, 10, G, O),         // open gravel plain
                TrackSegment.Straight(140, 10, G, O, 1.9f),   // jump 2
                TrackSegment.Turn(75, 60, 9.5f, G, O),
                TrackSegment.Straight(70, 9, D, F),
                TrackSegment.Turn(-165, 18, 9, D, O),         // hairpin left
                TrackSegment.Straight(70, 8, D, F),
                TrackSegment.Turn(50, 90, 8, D, F),
                TrackSegment.Turn(-45, 100, 8, D, F),
                TrackSegment.Straight(70, 8.5f, D, O, 1.5f),  // crest jump 3
                TrackSegment.Turn(40, 140, 9, G, O),
                TrackSegment.Straight(120, 11, G, O),         // finish straight
                TrackSegment.Straight(115, 11, G, O),         // stop zone after the flying finish
            };
        }
    }
}
