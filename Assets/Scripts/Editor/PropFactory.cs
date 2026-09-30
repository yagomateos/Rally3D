using System.IO;
using Rally.Procedural;
using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>Builds procedural meshes, materials and prefabs for vegetation and roadside props.</summary>
    public static class PropFactory
    {
        public const string MeshFolder = "Assets/Art/Meshes";
        public const string PrefabFolder = "Assets/Prefabs/Environment";

        // ------------------------------------------------------------------ helpers

        public static Mesh SaveMesh(Mesh mesh)
        {
            Directory.CreateDirectory(MeshFolder);
            mesh.name = StageTheme.Name(mesh.name);
            string path = $"{MeshFolder}/{mesh.name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static GameObject MeshObject(string name, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            return go;
        }

        public static GameObject SavePrefab(GameObject go, string folder = PrefabFolder)
        {
            Directory.CreateDirectory(folder);
            go.name = StageTheme.Name(go.name);
            string path = $"{folder}/{go.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject Build(string name, MeshBuilder mb, params Material[] materials) =>
            MeshObject(name, SaveMesh(mb.ToMesh("M_" + name)), materials);

        /// <summary>
        /// Tree prefab with a detailed and a simplified LOD as children of an LODGroup root. Terrain accepts
        /// LOD trees with any shader, and distant forest becomes much cheaper to render.
        /// </summary>
        private static GameObject LodTree(string name, MeshBuilder detailed, MeshBuilder simple, float lod1Height, float cullHeight,
            Material farMaterial, params Material[] materials)
        {
            var root = new GameObject(name);
            var lod0 = MeshObject("LOD0", SaveMesh(detailed.ToMesh($"M_{name}_LOD0")), materials);
            var lod1 = MeshObject("LOD1", SaveMesh(simple.ToMesh($"M_{name}_LOD1")), farMaterial);
            lod0.transform.SetParent(root.transform, false);
            lod1.transform.SetParent(root.transform, false);
            lod1.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(lod1Height, new Renderer[] { lod0.GetComponent<MeshRenderer>() }),
                new LOD(cullHeight, new Renderer[] { lod1.GetComponent<MeshRenderer>() })
            });
            group.RecalculateBounds();
            return root;
        }

        // ------------------------------------------------------------------ materials

        public static void CreateMaterials(AssetLibrary lib)
        {
            var (bark, barkN) = ProceduralTextures.Bark();
            var (needles, needlesN) = ProceduralTextures.Needles();
            var (leaves, leavesN) = ProceduralTextures.Leaves();
            var (birch, birchN) = ProceduralTextures.BirchBark();
            var (rock, rockN) = ProceduralTextures.Rock();
            var (wood, woodN) = ProceduralTextures.Wood();
            var (plaster, plasterN) = ProceduralTextures.Plaster();
            var (roof, roofN) = ProceduralTextures.RoofTiles();
            var (meadow, meadowN) = ProceduralTextures.Meadow();
            var (tyre, tyreN) = ProceduralTextures.Tyre();

            lib.bark = MaterialFactory.Opaque("Bark", Color.white, bark, barkN, 0.35f, 0f, 1f, null, "Nature");
            lib.needles = MaterialFactory.Opaque("PineNeedles", new Color(0.85f, 0.95f, 0.85f), needles, needlesN, 0.35f, 0f, 1f, null, "Nature");
            lib.leaves = MaterialFactory.Opaque("Leaves", Color.white, leaves, leavesN, 0.4f, 0f, 1f, null, "Nature");
            lib.birchBark = MaterialFactory.Opaque("BirchBark", Color.white, birch, birchN, 0.3f, 0f, 1f, null, "Nature");
            lib.rock = MaterialFactory.Opaque("Rock", Color.white, rock, rockN, 0.6f, 0f, 1f, new Vector2(0.5f, 0.5f), "Nature");
            lib.wood = MaterialFactory.Opaque("Wood", Color.white, wood, woodN, 0.3f, 0f, 1f, null, "Props");
            lib.hay = MaterialFactory.Opaque("Hay", new Color(1.3f, 1.2f, 0.9f), meadow, meadowN, 0.2f, 0f, 1f, new Vector2(0.5f, 0.5f), "Props");
            lib.tyreRubber = MaterialFactory.Opaque("TyreRubber", Color.white, tyre, tyreN, 0.35f, 0f, 1f, null, "Props");
            lib.plaster = MaterialFactory.Opaque("Plaster", Color.white, plaster, plasterN, 0.25f, 0f, 0.6f, new Vector2(0.25f, 0.25f), "Props");
            lib.roofTiles = MaterialFactory.Opaque("RoofTiles", Color.white, roof, roofN, 0.55f, 0f, 1f, new Vector2(0.25f, 0.25f), "Props");
            lib.windowDark = MaterialFactory.Opaque("WindowDark", new Color(0.05f, 0.06f, 0.07f), null, null, 0.92f, 0.2f, 1f, null, "Props");
            lib.concrete = MaterialFactory.Opaque("Concrete", new Color(0.55f, 0.55f, 0.53f), rock, rockN, 0.25f, 0f, 0.4f, new Vector2(0.3f, 0.3f), "Props");
            lib.whitePaint = MaterialFactory.Opaque("WhitePaint", new Color(0.9f, 0.9f, 0.88f), null, null, 0.5f, 0f, 1f, null, "Props");
            lib.redReflector = MaterialFactory.Emissive("RedReflector", new Color(0.8f, 0.05f, 0.04f), new Color(0.35f, 0.02f, 0.01f), "Props");
            lib.metal = MaterialFactory.Opaque("GalvanisedMetal", new Color(0.62f, 0.64f, 0.66f), null, null, 0.7f, 0.85f, 1f, null, "Props");
            lib.darkMetal = MaterialFactory.Opaque("DarkMetal", new Color(0.12f, 0.12f, 0.13f), null, null, 0.5f, 0.6f, 1f, null, "Props");
            lib.chevron = MaterialFactory.Opaque("Chevron", Color.white, ProceduralTextures.Chevron(), null, 0.4f, 0f, 1f, null, "Props");
            lib.checker = MaterialFactory.Opaque("Checker", Color.white, ProceduralTextures.Checker(), null, 0.3f, 0f, 1f, null, "Props");
            lib.tape = MaterialFactory.Cutout("Tape", Color.white, ProceduralTextures.TapeStripes(), 0.1f, true, "Props");
            lib.skin = MaterialFactory.Opaque("Skin", new Color(0.78f, 0.58f, 0.46f), null, null, 0.35f, 0f, 1f, null, "Props");
            lib.clothing = new[]
            {
                MaterialFactory.Opaque("Cloth_Red", new Color(0.62f, 0.08f, 0.06f), null, null, 0.2f, 0f, 1f, null, "Props"),
                MaterialFactory.Opaque("Cloth_Blue", new Color(0.08f, 0.2f, 0.48f), null, null, 0.2f, 0f, 1f, null, "Props"),
                MaterialFactory.Opaque("Cloth_Yellow", new Color(0.85f, 0.65f, 0.08f), null, null, 0.2f, 0f, 1f, null, "Props"),
                MaterialFactory.Opaque("Cloth_Green", new Color(0.12f, 0.3f, 0.14f), null, null, 0.2f, 0f, 1f, null, "Props"),
                MaterialFactory.Opaque("Cloth_Dark", new Color(0.08f, 0.08f, 0.09f), null, null, 0.25f, 0f, 1f, null, "Props"),
                MaterialFactory.Opaque("Cloth_Orange", new Color(0.9f, 0.38f, 0.05f), null, null, 0.2f, 0f, 1f, null, "Props"),
            };

            lib.puddle = MaterialFactory.Cutout("Puddle", new Color(0.55f, 0.56f, 0.57f), ProceduralTextures.Puddle(), 0.5f, false, "Track", 0.93f);
            lib.skid = MaterialFactory.Particle("SkidMarks", ProceduralTextures.SkidMark(), new Color(1f, 1f, 1f, 1f), false);
            var dustTex = ProceduralTextures.DustPuff();
            var dot = ProceduralTextures.SoftDot();
            lib.dust = MaterialFactory.Particle("Dust", dustTex, Color.white);
            lib.debris = MaterialFactory.Particle("Debris", dot, Color.white, false);
            lib.mote = MaterialFactory.Particle("MistMote", dot, new Color(1f, 1f, 1f, 0.6f), false);
            lib.drizzle = MaterialFactory.Particle("Drizzle", dot, new Color(0.8f, 0.85f, 0.9f, 0.5f), false);
            lib.fogWisp = MaterialFactory.Particle("FogWisp", dustTex, new Color(0.85f, 0.87f, 0.9f, 1f));
        }

        // ------------------------------------------------------------------ vegetation

        public static void CreateVegetation(AssetLibrary lib)
        {
            if (StageTheme.Desert)
            {
                // Cacti take the pines' place in the tree scatter.
                float[] cactusHeights = { 3.5f, 5f, 6.5f };
                for (int v = 0; v < cactusHeights.Length; v++)
                    lib.pines.Add(SavePrefab(Cactus($"Cactus_{v}", cactusHeights[v], v, lib)));
            }
            else
            {
                float[] pineHeights = { 11f, 15f, 19f };
                for (int v = 0; v < pineHeights.Length; v++)
                    lib.pines.Add(SavePrefab(Pine($"Pine_{v}", pineHeights[v], v, lib)));
            }

            lib.broadleaf.Add(SavePrefab(Broadleaf("Birch_0", 10f, 0, lib)));
            lib.broadleaf.Add(SavePrefab(Broadleaf("Birch_1", 13f, 1, lib)));
            lib.bushes.Add(SavePrefab(Bush("Bush_0", 1.1f, 0, lib)));
            lib.bushes.Add(SavePrefab(Bush("Bush_1", 1.6f, 1, lib)));

            for (int i = 0; i < 4; i++)
                lib.rocks.Add(SavePrefab(Rock($"Rock_{i}", i, lib)));
        }

        private static GameObject Pine(string name, float h, int seed, AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            var low = new MeshBuilder();
            float trunkR = h * 0.018f + 0.08f;
            mb.Frustum(0, Vector3.zero, trunkR, 0.03f, h * 0.9f, 7, false, true);
            low.Frustum(0, Vector3.zero, trunkR * 1.5f, 0.03f, h * 0.4f, 4, false, false);
            int layers = 6 + seed;
            for (int i = 0; i < layers; i++)
            {
                float t = (float)i / (layers - 1);
                float y = h * (0.2f + 0.68f * t);
                float r = h * 0.2f * (1f - t * 0.82f) + 0.35f;
                float lh = h * (0.26f - 0.1f * t);
                mb.Frustum(1, new Vector3(0f, y, 0f), r, 0f, lh, 9, true, false, 0.3f, seed * 13 + i);
            }
            low.Frustum(0, new Vector3(0f, h * 0.2f, 0f), h * 0.2f + 0.35f, 0f, h * 0.8f, 6, true, false);

            var go = LodTree(name, mb, low, 0.08f, 0.02f, lib.needles, lib.bark, lib.needles);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = trunkR * 1.2f;
            col.height = h * 0.6f;
            col.center = new Vector3(0f, h * 0.3f, 0f);
            return go;
        }

        /// <summary>Saguaro-style cactus: ribbed trunk with a rounded top and one or two upturned arms.</summary>
        private static GameObject Cactus(string name, float h, int seed, AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            var low = new MeshBuilder();
            float r = 0.22f + h * 0.03f;
            mb.Frustum(0, Vector3.zero, r, r * 0.85f, h, 10, false, false, 0.02f, seed);
            mb.Blob(0, new Vector3(0f, h, 0f), new Vector3(r * 0.85f, r * 0.7f, r * 0.85f), 1, 0.05f, seed + 1, false);
            low.Frustum(0, Vector3.zero, r * 1.2f, r, h, 5, false, true);

            var rng = new System.Random(seed + 70);
            Vector3[] sides = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            int arms = 1 + seed % 2 + (h > 6f ? 1 : 0);
            int first = rng.Next(4);
            for (int k = 0; k < arms; k++)
            {
                Vector3 dir = sides[(first + k * 2 + k / 2) % 4];
                float armR = r * 0.6f;
                float y = h * (0.38f + 0.14f * k + (float)rng.NextDouble() * 0.06f);
                float reach = r + armR * 2.4f;
                // Elbow: an ellipsoid stretched along the arm's direction, then the upright part.
                Vector3 radii = new Vector3(Mathf.Abs(dir.x) > 0f ? reach * 0.55f : armR, armR, Mathf.Abs(dir.z) > 0f ? reach * 0.55f : armR);
                mb.Blob(0, dir * (reach * 0.5f) + Vector3.up * y, radii, 1, 0.04f, seed * 5 + k, false);
                float up = h * (0.22f + (float)rng.NextDouble() * 0.12f);
                mb.Frustum(0, dir * reach + Vector3.up * y, armR, armR * 0.85f, up, 8, false, false, 0.02f, seed + k);
                mb.Blob(0, dir * reach + Vector3.up * (y + up), new Vector3(armR * 0.85f, armR * 0.7f, armR * 0.85f), 1, 0.05f, seed + k + 9, false);
            }

            var go = LodTree(name, mb, low, 0.06f, 0.015f, lib.bark, lib.bark);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = r * 1.1f;
            col.height = h;
            col.center = new Vector3(0f, h * 0.5f, 0f);
            return go;
        }

        private static GameObject Broadleaf(string name, float h, int seed, AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            var low = new MeshBuilder();
            float trunkR = 0.14f + seed * 0.03f;
            mb.Frustum(0, Vector3.zero, trunkR, trunkR * 0.5f, h * 0.8f, 7, false, true);
            low.Frustum(0, Vector3.zero, trunkR * 2f, trunkR, h * 0.6f, 4, false, false);
            var rng = new System.Random(seed + 5);
            int blobs = 5 + seed;
            for (int i = 0; i < blobs; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = (float)rng.NextDouble() * h * 0.13f;
                float y = h * (0.58f + (float)rng.NextDouble() * 0.3f);
                float r = h * (0.13f + (float)rng.NextDouble() * 0.08f);
                mb.Blob(1, new Vector3(Mathf.Cos(a) * d, y, Mathf.Sin(a) * d), new Vector3(r, r * 0.85f, r), 1, 0.3f, seed * 7 + i);
            }
            low.Blob(0, new Vector3(0f, h * 0.72f, 0f), new Vector3(h * 0.26f, h * 0.24f, h * 0.26f), 0, 0.2f, seed);

            var go = LodTree(name, mb, low, 0.08f, 0.02f, lib.leaves, lib.birchBark, lib.leaves);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = trunkR * 1.3f;
            col.height = h * 0.6f;
            col.center = new Vector3(0f, h * 0.3f, 0f);
            return go;
        }

        private static GameObject Bush(string name, float size, int seed, AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            var low = new MeshBuilder();
            var rng = new System.Random(seed + 50);
            for (int i = 0; i < 4; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = (float)rng.NextDouble() * size * 0.5f;
                float r = size * (0.45f + (float)rng.NextDouble() * 0.3f);
                mb.Blob(0, new Vector3(Mathf.Cos(a) * d, r * 0.6f, Mathf.Sin(a) * d), new Vector3(r, r * 0.8f, r), 1, 0.35f, seed * 11 + i);
            }
            low.Blob(0, new Vector3(0f, size * 0.45f, 0f), new Vector3(size, size * 0.7f, size), 0, 0.25f, seed);
            var go = LodTree(name, mb, low, 0.1f, 0.03f, lib.leaves, lib.leaves);
            // Solid: in the desert and on the coast these scrubs read as rocks, and driving through them looked wrong.
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = size * 0.6f;
            col.height = size * 1.4f;
            col.center = new Vector3(0f, size * 0.5f, 0f);
            return go;
        }

        private static GameObject Rock(string name, int seed, AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            float r = 1f + seed * 0.25f;
            mb.Blob(0, new Vector3(0f, r * 0.25f, 0f), new Vector3(r, r * (0.55f + seed * 0.05f), r * 0.8f), 2, 0.32f, seed * 17 + 3);
            var go = Build(name, mb, lib.rock);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            col.convex = true;
            return go;
        }

        // ------------------------------------------------------------------ roadside props

        public static void CreateProps(AssetLibrary lib)
        {
            lib.markerPole = SavePrefab(MarkerPole(lib));
            lib.chevronSign = SavePrefab(ChevronSign(lib, false));
            lib.chevronSignLeft = SavePrefab(ChevronSign(lib, true));
            lib.tapePost = SavePrefab(TapePost(lib));
            lib.hayBale = SavePrefab(HayBale(lib));
            lib.tyreStack = SavePrefab(TyreStack(lib));
            lib.logPile = SavePrefab(LogPile(lib));
            lib.fenceSegment = SavePrefab(Fence(lib));
            for (int i = 0; i < 3; i++) lib.houses.Add(SavePrefab(House(i, lib)));
            for (int i = 0; i < 6; i++) lib.spectators.Add(SavePrefab(Spectator(i, lib)));

            if (StageTheme.Coast)
            {
                // Glowing lamp heads, no real lights: dozens of point lights would be far too slow on the web.
                lib.lampGlow = MaterialFactory.Emissive("LampGlow", new Color(1f, 0.86f, 0.62f), new Color(2.6f, 1.9f, 1.05f), "Props");
                lib.curveSignFace = MaterialFactory.Cutout("CurveSign", Color.white, ProceduralTextures.CurveSign(), 0.5f, false, "Props", 0.3f);
                lib.streetLight = SavePrefab(StreetLight(lib));
                lib.curveSign = SavePrefab(CurveSign(lib, false));
                lib.curveSignLeft = SavePrefab(CurveSign(lib, true));
            }
            // Sheep in the road on stage 1 (forest) and stage 4 (coast).
            if (StageTheme.Coast || StageTheme.ForestLike) lib.sheep = SavePrefab(Sheep());
        }

        private static GameObject MarkerPole(AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            mb.Frustum(0, Vector3.zero, 0.05f, 0.045f, 1.15f, 8);
            mb.Box(1, new Vector3(0f, 1.0f, 0.05f), new Vector3(0.07f, 0.14f, 0.015f));
            mb.Box(1, new Vector3(0f, 1.0f, -0.05f), new Vector3(0.07f, 0.14f, 0.015f));
            var go = Build("MarkerPole", mb, lib.whitePaint, lib.redReflector);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.08f;
            col.height = 1.2f;
            col.center = new Vector3(0f, 0.6f, 0f);
            Knockable(go, 6f);
            return go;
        }

        /// <summary>Street light: pole, arm reaching towards the road (+z) and a glowing lamp head.</summary>
        private static GameObject StreetLight(AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            const float h = 7f;
            mb.Frustum(0, Vector3.zero, 0.1f, 0.06f, h, 8);
            mb.Box(0, new Vector3(0f, h - 0.1f, 0.8f), new Vector3(0.07f, 0.07f, 1.6f));
            mb.Box(0, new Vector3(0f, h - 0.1f, 1.55f), new Vector3(0.3f, 0.12f, 0.6f));
            mb.Box(1, new Vector3(0f, h - 0.19f, 1.55f), new Vector3(0.24f, 0.04f, 0.5f));
            var go = Build("StreetLight", mb, lib.darkMetal, lib.lampGlow);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.12f;
            col.height = h;
            col.center = new Vector3(0f, h * 0.5f, 0f);
            return go;
        }

        /// <summary>
        /// Sheep for the coastal stage's road crossings: woolly body, dark face and ears, and four legs as separate
        /// children pivoting at the hip so <see cref="Rally.Track.Sheep"/> can swing them while it walks.
        /// </summary>
        private static GameObject Sheep()
        {
            var wool = MaterialFactory.Opaque("Wool", new Color(0.93f, 0.91f, 0.86f), null, null, 0.08f, 0f, 1f, null, "Props");
            var face = MaterialFactory.Opaque("SheepFace", new Color(0.13f, 0.12f, 0.11f), null, null, 0.2f, 0f, 1f, null, "Props");

            var root = new GameObject("Sheep");
            root.layer = 2; // Ignore Raycast: the chase camera doesn't jump in front of it

            // Readable sheep silhouette: a fleece of wool puffs, a long black face with a muzzle, ears sticking out
            // sideways, a wool cap and a stubby tail (one smooth blob looked like a rock).
            var bodyMb = new MeshBuilder();
            bodyMb.Blob(0, new Vector3(0f, 0.8f, -0.05f), new Vector3(0.36f, 0.3f, 0.6f), 2, 0.04f, 91, false);
            var puffRng = new System.Random(95);
            for (int i = 0; i < 14; i++)
            {
                float a = (float)puffRng.NextDouble() * Mathf.PI * 2f;
                float along = -0.5f + (float)puffRng.NextDouble() * 0.95f;
                float up = 0.72f + (float)puffRng.NextDouble() * 0.3f;
                float r = 0.14f + (float)puffRng.NextDouble() * 0.08f;
                var c = new Vector3(Mathf.Cos(a) * 0.28f, up + Mathf.Max(0f, Mathf.Sin(a)) * 0.05f, along);
                bodyMb.Blob(0, c, new Vector3(r, r * 0.9f, r), 1, 0.06f, 100 + i, false);
            }
            bodyMb.Blob(0, new Vector3(0f, 0.86f, -0.66f), new Vector3(0.08f, 0.1f, 0.08f), 1, 0.05f, 96, false); // tail
            bodyMb.Blob(0, new Vector3(0f, 1.1f, 0.56f), new Vector3(0.13f, 0.09f, 0.12f), 1, 0.05f, 97, false);   // wool cap
            bodyMb.Blob(1, new Vector3(0f, 1.0f, 0.66f), new Vector3(0.12f, 0.14f, 0.19f), 1, 0.03f, 93, false);   // head
            bodyMb.Blob(1, new Vector3(0f, 0.92f, 0.82f), new Vector3(0.085f, 0.09f, 0.1f), 1, 0.03f, 94, false);  // muzzle
            bodyMb.Box(1, new Vector3(0.19f, 1.05f, 0.6f), new Vector3(0.16f, 0.04f, 0.08f));                      // ears, sideways
            bodyMb.Box(1, new Vector3(-0.19f, 1.05f, 0.6f), new Vector3(0.16f, 0.04f, 0.08f));
            var body = MeshObject("Body", SaveMesh(bodyMb.ToMesh("M_Sheep_Body")), wool, face);
            body.transform.SetParent(root.transform, false);
            body.layer = 2;

            var legMb = new MeshBuilder();
            legMb.Frustum(0, new Vector3(0f, -0.55f, 0f), 0.045f, 0.055f, 0.55f, 6);
            legMb.Box(0, new Vector3(0f, -0.53f, 0.02f), new Vector3(0.09f, 0.05f, 0.11f)); // hoof
            Mesh legMesh = SaveMesh(legMb.ToMesh("M_Sheep_Leg"));
            var legs = new Transform[4];
            Vector3[] hips = { new Vector3(0.17f, 0.58f, 0.3f), new Vector3(-0.17f, 0.58f, 0.3f), new Vector3(0.17f, 0.58f, -0.35f), new Vector3(-0.17f, 0.58f, -0.35f) };
            for (int i = 0; i < 4; i++)
            {
                var leg = MeshObject($"Leg{i}", legMesh, face);
                leg.transform.SetParent(root.transform, false);
                leg.transform.localPosition = hips[i];
                leg.layer = 2;
                legs[i] = leg.transform;
            }

            // Rounded underside so it slides up the step from the verge onto the road (a box caught on it).
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.center = new Vector3(0f, 0.45f, 0.08f);
            col.radius = 0.42f;
            col.height = 1.5f;
            root.AddComponent<Rigidbody>();
            var sheep = root.AddComponent<Rally.Track.Sheep>();
            var so = new SerializedObject(sheep);
            so.FindProperty("body").objectReferenceValue = body.transform;
            var legsProp = so.FindProperty("legs");
            legsProp.arraySize = 4;
            for (int i = 0; i < 4; i++) legsProp.GetArrayElementAtIndex(i).objectReferenceValue = legs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Curve warning sign on a post; the front (+z) faces approaching drivers.</summary>
        private static GameObject CurveSign(AssetLibrary lib, bool pointsLeft)
        {
            var mb = new MeshBuilder();
            mb.Box(0, new Vector3(0f, 1.1f, -0.03f), new Vector3(0.07f, 2.2f, 0.07f));
            mb.QuadUV(1, new Vector3(0.5f, 1.6f, 0.01f), new Vector3(-0.5f, 1.6f, 0.01f),
                new Vector3(-0.5f, 2.6f, 0.01f), new Vector3(0.5f, 2.6f, 0.01f), pointsLeft);
            var go = Build(pointsLeft ? "CurveSign_Left" : "CurveSign_Right", mb, lib.darkMetal, lib.curveSignFace);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.08f;
            col.height = 2.6f;
            col.center = new Vector3(0f, 1.3f, 0f);
            return go;
        }

        private static GameObject ChevronSign(AssetLibrary lib, bool pointsLeft)
        {
            var mb = new MeshBuilder();
            mb.Box(0, new Vector3(-0.6f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f));
            mb.Box(0, new Vector3(0.6f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f));
            mb.Box(1, new Vector3(0f, 1.22f, 0f), new Vector3(1.6f, 0.62f, 0.04f));
            // Front face (+z) seen by approaching drivers; arrows point to the viewer's right unless mirrored.
            mb.QuadUV(2, new Vector3(0.78f, 0.93f, 0.025f), new Vector3(-0.78f, 0.93f, 0.025f),
                new Vector3(-0.78f, 1.51f, 0.025f), new Vector3(0.78f, 1.51f, 0.025f), pointsLeft);
            var go = Build(pointsLeft ? "ChevronSign_Left" : "ChevronSign_Right", mb, lib.wood, lib.darkMetal, lib.chevron);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(1.6f, 1.8f, 0.12f);
            return go;
        }

        private static GameObject TapePost(AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            mb.Box(0, new Vector3(0f, 0.55f, 0f), new Vector3(0.06f, 1.1f, 0.06f));
            var go = Build("TapePost", mb, lib.wood);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0f);
            col.size = new Vector3(0.12f, 1.1f, 0.12f);
            Knockable(go, 5f);
            return go;
        }

        private static GameObject HayBale(AssetLibrary lib)
        {
            var mb = new MeshBuilder { Transform = Matrix4x4.TRS(new Vector3(-0.65f, 0.6f, 0f), Quaternion.Euler(0f, 0f, -90f), Vector3.one) };
            mb.Frustum(0, Vector3.zero, 0.6f, 0.6f, 1.3f, 14);
            var go = Build("HayBale", mb, lib.hay);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1.3f, 1.2f, 1.2f);
            return go;
        }

        private static GameObject TyreStack(AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                mb.Frustum(0, new Vector3(0f, i * 0.23f, 0f), 0.34f, 0.34f, 0.22f, 14, false, false);
                mb.Disc(1, new Vector3(0f, i * 0.23f + 0.22f, 0f), 0.34f, 14, true);
            }
            var go = Build("TyreStack", mb, lib.tyreRubber, lib.darkMetal);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.34f;
            col.height = 0.92f;
            col.center = new Vector3(0f, 0.46f, 0f);
            return go;
        }

        private static GameObject LogPile(AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            var rng = new System.Random(3);
            int row = 0;
            for (int count = 5; count > 1; count--, row++)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (count - 1) * 0.5f) * 0.5f;
                    float len = 4f + (float)rng.NextDouble() * 0.6f;
                    mb.Transform = Matrix4x4.TRS(new Vector3(x, 0.24f + row * 0.42f, -len * 0.5f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                    mb.Frustum(0, Vector3.zero, 0.24f, 0.22f, len, 9);
                }
            }
            var go = Build("LogPile", mb, lib.bark);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(2.5f, 1.6f, 4.4f);
            return go;
        }

        private static GameObject Fence(AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            mb.Box(0, new Vector3(-1.5f, 0.55f, 0f), new Vector3(0.1f, 1.1f, 0.1f));
            mb.Box(0, new Vector3(1.5f, 0.55f, 0f), new Vector3(0.1f, 1.1f, 0.1f));
            mb.Box(0, new Vector3(0f, 0.85f, 0.06f), new Vector3(3.1f, 0.1f, 0.04f));
            mb.Box(0, new Vector3(0f, 0.45f, 0.06f), new Vector3(3.1f, 0.1f, 0.04f));
            var go = Build("FenceSegment", mb, lib.wood);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0.03f);
            col.size = new Vector3(3.1f, 1.1f, 0.2f);
            Knockable(go, 25f);
            return go;
        }

        private static GameObject House(int variant, AssetLibrary lib)
        {
            float w = 7f + variant * 1.5f, d = 6f + variant, h = 3.2f + (variant == 2 ? 2.8f : 0f), roofH = 2.4f;
            var mb = new MeshBuilder();
            mb.Box(0, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), 0.5f);

            // Gable roof with overhang.
            float o = 0.45f;
            Vector3 fl = new Vector3(-w * 0.5f - o, h, d * 0.5f + o), fr = new Vector3(w * 0.5f + o, h, d * 0.5f + o);
            Vector3 bl = new Vector3(-w * 0.5f - o, h, -d * 0.5f - o), br = new Vector3(w * 0.5f + o, h, -d * 0.5f - o);
            Vector3 rf = new Vector3(0f, h + roofH, d * 0.5f + o), rb = new Vector3(0f, h + roofH, -d * 0.5f - o);
            mb.Quad(1, fl, bl, rb, rf, 0.5f);
            mb.Quad(1, br, fr, rf, rb, 0.5f);
            mb.Triangle(0, new Vector3(w * 0.5f, h, d * 0.5f), new Vector3(0f, h + roofH - 0.3f, d * 0.5f), new Vector3(-w * 0.5f, h, d * 0.5f), 0.5f);
            mb.Triangle(0, new Vector3(-w * 0.5f, h, -d * 0.5f), new Vector3(0f, h + roofH - 0.3f, -d * 0.5f), new Vector3(w * 0.5f, h, -d * 0.5f), 0.5f);

            // Door and windows on the front (+z).
            mb.Box(2, new Vector3(-w * 0.2f, 1.05f, d * 0.5f + 0.03f), new Vector3(1.0f, 2.1f, 0.08f));
            for (int i = 0; i < 2; i++)
            {
                float x = w * (0.1f + i * 0.25f);
                mb.Box(2, new Vector3(x, 1.7f, d * 0.5f + 0.03f), new Vector3(0.9f, 1.1f, 0.08f));
                if (variant == 2) mb.Box(2, new Vector3(x, 4.6f, d * 0.5f + 0.03f), new Vector3(0.9f, 1.1f, 0.08f));
            }
            mb.Box(2, new Vector3(w * 0.5f + 0.03f, 1.7f, 0f), new Vector3(0.08f, 1.1f, 0.9f));
            mb.Box(3, new Vector3(w * 0.25f, h + roofH * 0.8f, -d * 0.15f), new Vector3(0.6f, 1.6f, 0.6f));

            var go = Build($"House_{variant}", mb, lib.plaster, lib.roofTiles, lib.windowDark, lib.concrete);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (h + roofH) * 0.5f, 0f);
            col.size = new Vector3(w, h + roofH, d);
            return go;
        }

        private static GameObject Spectator(int variant, AssetLibrary lib)
        {
            var mb = new MeshBuilder();
            float s = 0.95f + (variant % 3) * 0.05f;
            // Legs, torso, arms, head.
            mb.Box(0, new Vector3(-0.1f, 0.43f, 0f) * s, new Vector3(0.14f, 0.86f, 0.16f) * s);
            mb.Box(0, new Vector3(0.1f, 0.43f, 0f) * s, new Vector3(0.14f, 0.86f, 0.16f) * s);
            mb.TaperedBox(1, new Vector3(0f, 1.18f, 0f) * s, new Vector3(0.44f, 0.64f, 0.26f) * s, new Vector2(1.08f, 0.95f));
            bool cheering = variant % 2 == 0;
            for (int side = -1; side <= 1; side += 2)
            {
                mb.Transform = Matrix4x4.TRS(new Vector3(side * 0.28f, 1.45f, 0f) * s,
                    Quaternion.Euler(0f, 0f, cheering ? side * 155f : side * 12f), Vector3.one * s);
                mb.Box(1, new Vector3(0f, -0.3f, 0f), new Vector3(0.12f, 0.6f, 0.13f));
            }
            mb.Transform = Matrix4x4.identity;
            mb.Blob(2, new Vector3(0f, 1.66f, 0f) * s, new Vector3(0.12f, 0.14f, 0.13f) * s, 1, 0.05f, variant);
            if (variant % 3 == 1) mb.Box(1, new Vector3(0f, 1.8f, 0.02f) * s, new Vector3(0.26f, 0.06f, 0.3f) * s); // cap
            var cloth = lib.clothing[variant % lib.clothing.Length];
            var go = Build($"Spectator_{variant}", mb, lib.clothing[4], cloth, lib.skin);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.3f * s;
            col.height = 1.85f * s;
            col.center = new Vector3(0f, 0.92f * s, 0f);
            return go;
        }

        /// <summary>Light prop a car knocks flying (trigger until hit; see <see cref="Rally.Track.Knockable"/>).</summary>
        private static void Knockable(GameObject go, float mass)
        {
            var k = go.AddComponent<Rally.Track.Knockable>();
            var so = new SerializedObject(k);
            so.FindProperty("mass").floatValue = mass;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
