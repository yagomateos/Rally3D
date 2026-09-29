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
