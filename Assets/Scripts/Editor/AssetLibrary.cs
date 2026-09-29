using System.Collections.Generic;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>Everything generated for the stage, handed from one builder step to the next.</summary>
    public class AssetLibrary
    {
        // Terrain
        public readonly List<TerrainLayer> terrainLayers = new List<TerrainLayer>();
        public Texture2D grassDetail;

        // Materials
        public Material roadDirt, roadGravel, roadMud, roadAsphalt;
        public Material bark, needles, leaves, birchBark, rock, wood, whitePaint, redReflector, metal, darkMetal;
        public Material chevron, checker, tape, hay, tyreRubber, plaster, roofTiles, windowDark, concrete;
        public Material puddle, skid, dust, debris, mote, drizzle, fogWisp;
        public Material[] clothing;
        public Material skin;

        // Prefabs
        public readonly List<GameObject> pines = new List<GameObject>();
        public readonly List<GameObject> broadleaf = new List<GameObject>();
        public readonly List<GameObject> bushes = new List<GameObject>();
        public readonly List<GameObject> rocks = new List<GameObject>();
        public readonly List<GameObject> spectators = new List<GameObject>();
        public readonly List<GameObject> houses = new List<GameObject>();
        public GameObject markerPole, chevronSign, chevronSignLeft, tapePost, hayBale, tyreStack, logPile, fenceSegment;

        // Coastal stage only
        public GameObject streetLight, curveSign, curveSignLeft;
        public Material lampGlow, curveSignFace;
    }
}
