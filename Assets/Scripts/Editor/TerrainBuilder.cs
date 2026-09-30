using System.Collections.Generic;
using System.IO;
using Rally.Procedural;
using Rally.Systems;
using Rally.Track;
using Rally.Track.Generation;
using UnityEditor;
using UnityEngine;
using Layer = Rally.Track.Generation.TerrainSculptor.Layer;

namespace Rally.EditorTools
{
    /// <summary>Creates the Unity Terrain: heights, splat layers, grass details, trees and surface map.</summary>
    public static class TerrainBuilder
    {
        private const string Folder = "Assets/Art/Terrain";
        // Sized for the web download: each stage's terrain was 19.8 MB at 2049 / 1024 / 1024, most of the web build.
        // The road is its own mesh, so ~2 m between height samples is enough (QA14 checks the terrain stays below it).
        private const int HeightmapResolution = 1025;
        private const int AlphamapResolution = 512;
        private const int DetailResolution = 512;
        private const float GrassPerCellAt1024 = 12f;
        private const float DesertPlantDensity = 0.12f; // scattered cacti and scrub instead of a forest
        private const float CoastPlantDensity = 0.45f;  // open Mediterranean pine woods: the sea and the road stay in view
        private const int SurfaceMapResolution = 512;

        public static void CreateLayers(AssetLibrary lib)
        {
            Directory.CreateDirectory(Folder);
            var (grass, grassN) = ProceduralTextures.Grass();
            var (meadow, meadowN) = ProceduralTextures.Meadow();
            var (forest, forestN) = ProceduralTextures.ForestFloor();
            var (gravel, gravelN) = ProceduralTextures.Gravel();
            var (rock, rockN) = ProceduralTextures.Rock();
            var (mud, mudN) = ProceduralTextures.Mud();
            var (verge, vergeN) = ProceduralTextures.VergeDirt();

            // Order must match TerrainSculptor.Layer.
            lib.terrainLayers.Clear();
            lib.terrainLayers.Add(CreateLayer("TL_Grass", grass, grassN, 4f, 0.5f));
            lib.terrainLayers.Add(CreateLayer("TL_Meadow", meadow, meadowN, 5f, 0.4f));
            lib.terrainLayers.Add(CreateLayer("TL_ForestFloor", forest, forestN, 4f, 0.55f));
            lib.terrainLayers.Add(CreateLayer("TL_Gravel", gravel, gravelN, 3f, 0.6f));
            lib.terrainLayers.Add(CreateLayer("TL_Rock", rock, rockN, 9f, 0.6f));
            lib.terrainLayers.Add(CreateLayer("TL_Mud", mud, mudN, 4f, 0.95f));
            lib.terrainLayers.Add(CreateLayer("TL_VergeDirt", verge, vergeN, 3.5f, 0.7f));
            lib.grassDetail = ProceduralTextures.GrassBlades();
        }

        private static TerrainLayer CreateLayer(string name, Texture2D albedo, Texture2D normal, float tile, float smoothness)
        {
            string path = $"{Folder}/{StageTheme.SharedName(name)}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = albedo;
            layer.normalMapTexture = normal;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        public static Terrain Build(StageDefinition def, StageRoute route, TerrainSculptor sculptor, AssetLibrary lib)
        {
            var data = new TerrainData { name = StageTheme.Name("StageTerrainData") };
            data.heightmapResolution = HeightmapResolution;
            data.size = new Vector3(sculptor.Size, def.terrainMaxHeight, sculptor.Size);
            data.alphamapResolution = AlphamapResolution;
            data.baseMapResolution = 1024;
            data.SetDetailResolution(DetailResolution, 32);

            EditorUtility.DisplayProgressBar("Rally", "Carving terrain", 0.3f);
            var influence = sculptor.ComputeInfluence(HeightmapResolution);
            data.SetHeights(0, 0, sculptor.BuildHeightmap(HeightmapResolution, influence));

            EditorUtility.DisplayProgressBar("Rally", "Painting terrain", 0.4f);
            data.terrainLayers = lib.terrainLayers.ToArray();
            float[,,] splat = sculptor.BuildSplatmap(AlphamapResolution, data);
            data.SetAlphamaps(0, 0, splat);

            string dataPath = $"{Folder}/{data.name}.asset";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(data, dataPath);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.position = sculptor.Origin;
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 350f;
            terrain.treeDistance = 560f;
            terrain.treeBillboardDistance = 560f;
            terrain.treeMaximumFullLODCount = 20000;
            terrain.detailObjectDistance = 120f;
            terrain.detailObjectDensity = 1f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

            // Grass only in the forest: nearly invisible in the snow, out of place in the desert, and each
            // detail layer costs download size.
            EditorUtility.DisplayProgressBar("Rally", "Growing grass", 0.5f);
            if (StageTheme.ForestLike) PaintGrass(data, splat, route, sculptor, lib);

            var surfaceMap = go.AddComponent<TerrainSurfaceMap>();
            surfaceMap.SetData(SurfaceMapResolution, sculptor.Origin, sculptor.Size, BuildSurfaceMap(splat));
            return terrain;
        }

        private static Material TerrainMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "M_Terrain" };
            mat.EnableKeyword("_NORMALMAP");
            return MaterialFactory.Save(mat, "Terrain");
        }

        private static void PaintGrass(TerrainData data, float[,,] splat, StageRoute route, TerrainSculptor sculptor, AssetLibrary lib)
        {
            var grass = new DetailPrototype
            {
                prototypeTexture = lib.grassDetail,
                renderMode = DetailRenderMode.GrassBillboard,
                usePrototypeMesh = false,
                healthyColor = new Color(0.62f, 0.7f, 0.45f),
                dryColor = new Color(0.72f, 0.68f, 0.45f),
                minWidth = 0.5f, maxWidth = 1.0f,
                minHeight = 0.35f, maxHeight = 0.8f,
                noiseSpread = 0.4f,
                useInstancing = false
            };
            data.detailPrototypes = new[] { grass };
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            int res = DetailResolution;
            var layer = new int[res, res];
            // Same blades per square metre whatever the detail resolution.
            float perCell = GrassPerCellAt1024 * (1024f / res) * (1024f / res);
            int alphaRes = splat.GetLength(0);
            float cell = sculptor.Size / res;
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = sculptor.Origin.x + (x + 0.5f) * cell, wz = sculptor.Origin.z + (z + 0.5f) * cell;
                float coarse = sculptor.RouteDistance(wx, wz);
                if (coarse > 160f) continue;
                if (coarse < 40f)
                {
                    // Keep the driving surface and shoulders clear of grass.
                    int nearest = sculptor.NearestSample(wx, wz);
                    float exact = Vector2.Distance(route.Planar[nearest], new Vector2(wx, wz));
                    if (exact < route.Widths[nearest] * 0.5f + 2.2f) continue;
                }
                int ax = Mathf.Min(alphaRes - 1, x * alphaRes / res), az = Mathf.Min(alphaRes - 1, z * alphaRes / res);
                float green = splat[az, ax, (int)Layer.Grass] + splat[az, ax, (int)Layer.Meadow] * 1.2f + splat[az, ax, (int)Layer.ForestFloor] * 0.25f;
                float n = Noise.Hash01(x, z, 99);
                layer[z, x] = Mathf.FloorToInt(Mathf.Clamp01(green - 0.2f) * perCell * (0.5f + n));
            }
            data.SetDetailLayer(0, 0, 0, layer);
        }

        private static byte[] BuildSurfaceMap(float[,,] splat)
        {
            int res = SurfaceMapResolution;
            int alphaRes = splat.GetLength(0);
            var cells = new byte[res * res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int ax = x * alphaRes / res, az = z * alphaRes / res;
                int best = 0;
                float bestW = -1f;
                for (int l = 0; l < TerrainSculptor.LayerCount; l++)
                {
                    float w = splat[az, ax, l];
                    if (w > bestW) { bestW = w; best = l; }
                }
                SurfaceType type;
                switch ((Layer)best)
                {
                    case Layer.Gravel: type = SurfaceType.Gravel; break;
                    case Layer.Mud: type = SurfaceType.Mud; break;
                    case Layer.RoadDirt: type = SurfaceType.Dirt; break;
                    case Layer.Rock: type = SurfaceType.Gravel; break;
                    default: type = SurfaceType.Grass; break;
                }
                cells[z * res + x] = (byte)type;
            }
            return cells;
        }

        // ------------------------------------------------------------------ horizon

        /// <summary>
        /// A ring of hills and mountains round the terrain, out to ~2 km, so the map never ends in a visible edge
        /// against the sky (on phones the draw distance is shorter and the edge showed through the fog). It starts
        /// just under the terrain border, rises into hills and fades into the fog; on the coast it stays under the
        /// sea wherever the border is at sea level.
        /// </summary>
        public static void BuildHorizon(Terrain terrain, float seaLevel)
        {
            const int perSide = 64;
            float[] ringDistance = { 0f, 180f, 650f, 2200f };
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 centre = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);

            float hills = StageTheme.Snow ? 190f : StageTheme.Desert ? 70f : StageTheme.Coast ? 100f : 150f;
            Color colour = StageTheme.Night ? new Color(0.05f, 0.07f, 0.06f)
                : StageTheme.Snow ? new Color(0.84f, 0.87f, 0.93f)
                : StageTheme.Desert ? new Color(0.8f, 0.64f, 0.44f)
                : StageTheme.Coast ? new Color(0.42f, 0.4f, 0.27f)
                : new Color(0.2f, 0.27f, 0.17f);

            // Walk round the terrain border.
            var border = new List<Vector3>();
            for (int side = 0; side < 4; side++)
                for (int i = 0; i < perSide; i++)
                {
                    float t = (float)i / perSide;
                    Vector3 p = side switch
                    {
                        0 => new Vector3(t * size.x, 0f, 0f),
                        1 => new Vector3(size.x, 0f, t * size.z),
                        2 => new Vector3((1f - t) * size.x, 0f, size.z),
                        _ => new Vector3(0f, 0f, (1f - t) * size.z)
                    };
                    p += origin;
                    p.y = terrain.SampleHeight(p) + origin.y;
                    border.Add(p);
                }

            int n = border.Count, rows = ringDistance.Length;
            var vertices = new Vector3[n * rows];
            for (int i = 0; i < n; i++)
            {
                Vector3 b = border[i];
                Vector3 outward = new Vector3(b.x - centre.x, 0f, b.z - centre.z).normalized;
                float angle = (float)i / n;
                bool underSea = b.y < seaLevel + 1f;
                for (int r = 0; r < rows; r++)
                {
                    float rise = r == 0 ? -0.6f
                        : (0.25f + 0.75f * Mathf.PerlinNoise(angle * 9f + r * 3.1f, r * 1.7f)) * hills * (r == 1 ? 0.25f : r == 2 ? 1f : 0.8f);
                    float y = b.y + rise;
                    if (underSea) y = r == 0 ? b.y - 0.6f : seaLevel - 4f; // the sea covers it
                    vertices[i * rows + r] = b + outward * ringDistance[r] + Vector3.up * (y - b.y);
                }
            }
            var triangles = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                for (int r = 0; r < rows - 1; r++)
                {
                    int a0 = i * rows + r, a1 = i * rows + r + 1, b0 = j * rows + r, b1 = j * rows + r + 1;
                    triangles.Add(a0); triangles.Add(a1); triangles.Add(b0);
                    triangles.Add(b0); triangles.Add(a1); triangles.Add(b1);
                }
            }
            var mesh = new Mesh { name = "M_Horizon", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mat = MaterialFactory.Opaque(StageTheme.Night ? "Horizon_Night" : "Horizon", colour, null, null, 0.05f, 0f, 1f, null, "Environment");
            var go = PropFactory.MeshObject("Horizon", PropFactory.SaveMesh(mesh), mat);
            go.isStatic = true;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            // Winding check: the surface must face up; flip if the first face points down.
            if (Vector3.Dot(mesh.normals[1], Vector3.up) < 0f)
            {
                var tris = mesh.triangles;
                for (int k = 0; k < tris.Length; k += 3) (tris[k + 1], tris[k + 2]) = (tris[k + 2], tris[k + 1]);
                mesh.triangles = tris;
                mesh.RecalculateNormals();
            }
        }

        // ------------------------------------------------------------------ trees

        public static void PlantTrees(Terrain terrain, StageRoute route, TerrainSculptor sculptor, AssetLibrary lib, System.Func<Vector3, bool> blocked)
        {
            var data = terrain.terrainData;
            var prototypes = new List<TreePrototype>();
            foreach (var p in lib.pines) prototypes.Add(new TreePrototype { prefab = p });
            foreach (var p in lib.broadleaf) prototypes.Add(new TreePrototype { prefab = p });
            foreach (var p in lib.bushes) prototypes.Add(new TreePrototype { prefab = p });
            data.treePrototypes = prototypes.ToArray();
            int pineStart = 0, broadStart = lib.pines.Count, bushStart = broadStart + lib.broadleaf.Count;

            var trees = new List<TreeInstance>();
            var rng = new System.Random(route.Count);
            float size = sculptor.Size;
            const float spacing = 5.5f;
            int steps = Mathf.FloorToInt(size / spacing);

            for (int iz = 0; iz < steps; iz++)
            for (int ix = 0; ix < steps; ix++)
            {
                float wx = sculptor.Origin.x + (ix + (float)rng.NextDouble()) * spacing;
                float wz = sculptor.Origin.z + (iz + (float)rng.NextDouble()) * spacing;
                float d = sculptor.RouteDistance(wx, wz);
                int sample = sculptor.NearestSample(wx, wz);
                float half = route.Widths[sample] * 0.5f;
                RoadsideStyle style = route.Roadside[sample];
                if (d < 40f) d = Vector2.Distance(route.Planar[sample], new Vector2(wx, wz));

                float clearance = half + (style == RoadsideStyle.Forest ? 5.5f : 9f);
                if (d < clearance) continue;

                float density;
                if (d > 150f) density = Mathf.Lerp(0.34f, 0.18f, Mathf.InverseLerp(150f, 500f, d));
                else if (style == RoadsideStyle.Forest) density = 0.62f;
                else if (style == RoadsideStyle.Village) density = 0.06f;
                else density = 0.1f;
                if (StageTheme.Desert) density *= DesertPlantDensity;
                if (StageTheme.Coast) density *= CoastPlantDensity;
                // Natural clearings.
                float clearing = Noise.Fbm(wx / 90f, wz / 90f, 2, 2f, 0.5f, 7f);
                density *= Mathf.Clamp01(0.65f + clearing * 1.4f);

                Vector3 world = new Vector3(wx, 0f, wz);
                world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
                Vector2 normPos = new Vector2((wx - sculptor.Origin.x) / size, (wz - sculptor.Origin.z) / size);
                float steep = data.GetSteepness(normPos.x, normPos.y);
                if (steep > 38f) density *= 0.2f;
                if ((float)rng.NextDouble() > density) continue;
                if (blocked(world)) continue;

                int proto;
                double roll = rng.NextDouble();
                bool open = style != RoadsideStyle.Forest && d < 150f;
                if (roll < (open ? 0.35 : 0.12)) proto = broadStart + rng.Next(lib.broadleaf.Count);
                else if (roll < (open ? 0.6 : 0.2)) proto = bushStart + rng.Next(lib.bushes.Count);
                else proto = pineStart + rng.Next(lib.pines.Count);

                float scale = 0.75f + (float)rng.NextDouble() * 0.55f;
                trees.Add(new TreeInstance
                {
                    position = new Vector3(normPos.x, 0f, normPos.y),
                    prototypeIndex = proto,
                    widthScale = scale * (0.9f + (float)rng.NextDouble() * 0.2f),
                    heightScale = scale,
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }

            // Extra undergrowth close to the road.
            for (int i = 0; i < route.Count; i += 2)
            {
                if (rng.NextDouble() > 0.35) continue;
                Vector2 p = route.Planar[i];
                Vector2 t = route.Tangent(i);
                Vector2 right = new Vector2(t.y, -t.x);
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float off = route.Widths[i] * 0.5f + 4f + (float)rng.NextDouble() * 10f;
                Vector2 q = p + right * side * off;
                Vector3 world = new Vector3(q.x, 0f, q.y);
                world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
                if (sculptor.RouteDistance(q.x, q.y) < route.Widths[i] * 0.5f + 3.5f || blocked(world)) continue;
                trees.Add(new TreeInstance
                {
                    position = new Vector3((q.x - sculptor.Origin.x) / size, 0f, (q.y - sculptor.Origin.z) / size),
                    prototypeIndex = bushStart + rng.Next(lib.bushes.Count),
                    widthScale = 0.7f + (float)rng.NextDouble() * 0.6f,
                    heightScale = 0.7f + (float)rng.NextDouble() * 0.5f,
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }

            data.SetTreeInstances(trees.ToArray(), true);
            Debug.Log($"[Rally] Planted {trees.Count} trees.");
        }
    }
}
