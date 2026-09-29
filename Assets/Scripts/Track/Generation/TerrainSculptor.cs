using Rally.Procedural;
using Rally.Systems;
using UnityEngine;

namespace Rally.Track.Generation
{
    /// <summary>
    /// Generates the heightfield around a route: natural hills, surrounding mountains, a road bed with
    /// ditches and embankments, and the road elevation profile (with jump crests).
    /// </summary>
    public class TerrainSculptor
    {
        public const float RoadSink = 0.1f;
        private const float ShoulderWidth = 1.4f;
        private const float EmbankmentWidth = 20f;
        private const float DitchDepth = 0.45f;
        private const float DitchOffset = 2.6f;
        private const int DistanceGridSize = 160;

        private readonly StageDefinition def;
        private readonly StageRoute route;
        private readonly float seed;

        public Vector3 Origin { get; private set; }
        public float Size { get; private set; }
        public float BaseHeight { get; private set; }

        private float[,] coarseDistance;
        private int[,] coarseNearest;
        private float coarseCell;

        public TerrainSculptor(StageDefinition def, StageRoute route)
        {
            this.def = def;
            this.route = route;
            seed = def.seed * 0.137f;

            Rect b = route.Bounds();
            Size = Mathf.Max(b.width, b.height) + def.terrainMargin * 2f;
            Vector2 center = b.center;
            BaseHeight = 40f;
            Origin = new Vector3(center.x - Size * 0.5f, 0f, center.y - Size * 0.5f);
            BuildCoarseDistance();
        }

        // ----------------------------------------------------------------- natural terrain

        /// <summary>Terrain height before any road carving (world units).</summary>
        public float NaturalHeight(float x, float z)
        {
            float s = def.hillScale;
            float hills = Noise.Fbm(x / s, z / s, 4, 2f, 0.5f, seed) * def.hillAmplitude;
            float detail = Noise.Fbm(x / 55f, z / 55f, 3, 2f, 0.5f, seed + 3f) * 2.2f;

            float d = RouteDistance(x, z);
            float mountainMask = Smooth(90f, 520f, d);
            float mountains = (Noise.Ridged(x / 420f, z / 420f, 4, seed + 9f) * 0.75f + 0.25f) * 120f * mountainMask;
            // Gentle valley so the stage sits a little lower than its surroundings.
            float valley = (1f - Smooth(10f, 160f, d)) * -4f;

            float dunes = def.theme == StageDefinition.DesertTheme ? Dunes(x, z) * Smooth(35f, 170f, d) : 0f;
            return BaseHeight + hills + detail + mountains + valley + dunes;
        }

        private const float DuneWavelength = 110f;
        private const float DuneHeight = 10f;

        /// <summary>
        /// Rows of wind-blown dunes: long gentle windward slopes and short steeper lee faces, with the crests
        /// bent by noise so they don't look ruled. Kept away from the road, which runs between them.
        /// </summary>
        private float Dunes(float x, float z)
        {
            const float windAngle = 0.6f;
            float along = x * Mathf.Cos(windAngle) + z * Mathf.Sin(windAngle);
            float warp = Noise.Fbm(x / 260f, z / 260f, 3, 2f, 0.5f, seed + 51f) * 70f;
            float phase = (along + warp) / DuneWavelength;
            float p = phase - Mathf.Floor(phase);
            const float leeFraction = 0.3f;
            float profile = p < 1f - leeFraction ? p / (1f - leeFraction) : (1f - p) / leeFraction;
            profile = profile * profile * (3f - 2f * profile);
            float size = 0.55f + 0.45f * (Noise.Fbm(x / 400f, z / 400f, 2, 2f, 0.5f, seed + 57f) * 0.5f + 0.5f);
            return profile * DuneHeight * size;
        }

        public float RouteDistance(float x, float z)
        {
            float gx = (x - Origin.x) / coarseCell, gz = (z - Origin.z) / coarseCell;
            int n = DistanceGridSize;
            gx = Mathf.Clamp(gx, 0f, n - 1.001f);
            gz = Mathf.Clamp(gz, 0f, n - 1.001f);
            int ix = (int)gx, iz = (int)gz;
            float fx = gx - ix, fz = gz - iz;
            float a = Mathf.Lerp(coarseDistance[ix, iz], coarseDistance[ix + 1, iz], fx);
            float b = Mathf.Lerp(coarseDistance[ix, iz + 1], coarseDistance[ix + 1, iz + 1], fx);
            return Mathf.Lerp(a, b, fz);
        }

        private void BuildCoarseDistance()
        {
            int n = DistanceGridSize;
            coarseCell = Size / (n - 1);
            coarseDistance = new float[n, n];
            coarseNearest = new int[n, n];
            for (int ix = 0; ix < n; ix++)
            for (int iz = 0; iz < n; iz++)
            {
                Vector2 p = new Vector2(Origin.x + ix * coarseCell, Origin.z + iz * coarseCell);
                float best = float.MaxValue;
                int bestIndex = 0;
                for (int i = 0; i < route.Count; i += 4)
                {
                    float sqr = (route.Planar[i] - p).sqrMagnitude;
                    if (sqr < best) { best = sqr; bestIndex = i; }
                }
                coarseDistance[ix, iz] = Mathf.Sqrt(best);
                coarseNearest[ix, iz] = bestIndex;
            }
        }

        // ----------------------------------------------------------------- road profile

        public void ComputeRoadHeights()
        {
            int count = route.Count;
            var h = new float[count];
            for (int i = 0; i < count; i++)
                h[i] = NaturalHeight(route.Planar[i].x, route.Planar[i].y);

            int radius = Mathf.Max(1, Mathf.RoundToInt(def.roadSmoothingDistance / route.Spacing));
            for (int pass = 0; pass < 3; pass++) h = BoxBlur(h, radius);

            // Keep start and finish areas flat.
            FlattenRange(h, 0f, def.startLineDistance + 25f);
            FlattenRange(h, route.Length - def.finishLineOffset - 50f, route.Length);

            // Limit gradient in both directions so hills stay driveable (also smooths the flattened ends).
            float maxStep = def.maxRoadGradient * route.Spacing;
            for (int i = 1; i < count; i++) h[i] = Mathf.Clamp(h[i], h[i - 1] - maxStep, h[i - 1] + maxStep);
            for (int i = count - 2; i >= 0; i--) h[i] = Mathf.Clamp(h[i], h[i + 1] - maxStep, h[i + 1] + maxStep);
            h = BoxBlur(h, 4);

            foreach (var jump in route.Jumps) AddJump(h, jump.x, jump.y);
            route.Heights = h;
        }

        private void FlattenRange(float[] h, float from, float to)
        {
            int a = Mathf.Clamp(Mathf.RoundToInt(from / route.Spacing), 0, h.Length - 1);
            int b = Mathf.Clamp(Mathf.RoundToInt(to / route.Spacing), 0, h.Length - 1);
            float target = 0f;
            for (int i = a; i <= b; i++) target += h[i];
            target /= b - a + 1;
            int blend = Mathf.RoundToInt(40f / route.Spacing);
            for (int i = Mathf.Max(0, a - blend); i <= Mathf.Min(h.Length - 1, b + blend); i++)
            {
                float w = 1f;
                if (i < a) w = Smooth(a - blend, a, i);
                else if (i > b) w = 1f - Smooth(b, b + blend, i);
                h[i] = Mathf.Lerp(h[i], target, w);
            }
        }

        /// <summary>Asymmetric crest: a ramp that kicks the car up, then a steep drop to land on.</summary>
        private void AddJump(float[] h, float center, float height)
        {
            const float RampLength = 26f;
            const float DropLength = 17f;
            for (int i = 0; i < h.Length; i++)
            {
                float u = i * route.Spacing - center;
                float f = 0f;
                if (u > -RampLength && u <= 0f) f = Mathf.Pow((u + RampLength) / RampLength, 1.6f);
                else if (u > 0f && u < DropLength) { float t = 1f - u / DropLength; f = t * t; }
                h[i] += f * height;
            }
        }

        private static float[] BoxBlur(float[] src, int radius)
        {
            var dst = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                float sum = 0f; int n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int j = Mathf.Clamp(i + k, 0, src.Length - 1);
                    sum += src[j]; n++;
                }
                dst[i] = sum / n;
            }
            return dst;
        }

        // ----------------------------------------------------------------- heightmap

        public struct RoadInfluence
        {
            public float distance;   // planar distance to the centre line
            public float roadHeight; // road elevation at the closest sample
            public float halfWidth;
            public int sample;
        }

        /// <summary>For every heightmap cell: distance to the road and the road height there.</summary>
        public RoadInfluence[,] ComputeInfluence(int resolution)
        {
            var map = new RoadInfluence[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                map[y, x].distance = float.MaxValue;

            float cell = Size / (resolution - 1);
            float reach = EmbankmentWidth + ShoulderWidth + 8f;
            for (int i = 0; i < route.Count; i++)
            {
                Vector2 p = route.Planar[i];
                float half = route.Widths[i] * 0.5f;
                float r = half + reach;
                int cx = Mathf.RoundToInt((p.x - Origin.x) / cell), cz = Mathf.RoundToInt((p.y - Origin.z) / cell);
                int cr = Mathf.CeilToInt(r / cell);
                for (int z = Mathf.Max(0, cz - cr); z <= Mathf.Min(resolution - 1, cz + cr); z++)
                for (int x = Mathf.Max(0, cx - cr); x <= Mathf.Min(resolution - 1, cx + cr); x++)
                {
                    float wx = Origin.x + x * cell, wz = Origin.z + z * cell;
                    float d = Vector2.Distance(p, new Vector2(wx, wz));
                    if (d >= map[z, x].distance) continue;
                    map[z, x] = new RoadInfluence { distance = d, roadHeight = route.Heights[i], halfWidth = half, sample = i };
                }
            }
            return map;
        }

        /// <summary>Normalised heights [z, x] ready for TerrainData.SetHeights.</summary>
        public float[,] BuildHeightmap(int resolution, RoadInfluence[,] influence)
        {
            var heights = new float[resolution, resolution];
            float cell = Size / (resolution - 1);
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float wx = Origin.x + x * cell, wz = Origin.z + z * cell;
                float natural = NaturalHeight(wx, wz);
                float h = natural;
                var inf = influence[z, x];
                if (inf.distance < float.MaxValue)
                    h = CarvedHeight(natural, inf);
                heights[z, x] = Mathf.Clamp01(h / def.terrainMaxHeight);
            }
            return heights;
        }

        private static float CarvedHeight(float natural, RoadInfluence inf)
        {
            float edge = inf.halfWidth + ShoulderWidth;
            float bed = inf.roadHeight - RoadSink;
            if (inf.distance <= edge) return bed;

            // Drainage ditch just outside the shoulder, then an embankment back to natural ground.
            float ditchT = Mathf.Clamp01(1f - Mathf.Abs(inf.distance - (edge + DitchOffset)) / DitchOffset);
            float ditch = -DitchDepth * ditchT * ditchT * (3f - 2f * ditchT);
            float blend = Smooth(edge, edge + EmbankmentWidth, inf.distance);
            return Mathf.Lerp(bed + ditch, natural, blend);
        }

        // ----------------------------------------------------------------- splat map

        public enum Layer { Grass = 0, Meadow = 1, ForestFloor = 2, Gravel = 3, Rock = 4, Mud = 5, RoadDirt = 6 }
        public const int LayerCount = 7;

        public float[,,] BuildSplatmap(int resolution, TerrainData data)
        {
            var splat = new float[resolution, resolution, LayerCount];
            float cell = Size / (resolution - 1);
            var weights = new float[LayerCount];

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float wx = Origin.x + x * cell, wz = Origin.z + z * cell;
                float nx = (float)x / (resolution - 1), nz = (float)z / (resolution - 1);
                float slope = data.GetSteepness(nx, nz);
                float d = RouteDistance(wx, wz);
                float n1 = Noise.Fbm(wx / 40f, wz / 40f, 3, 2f, 0.5f, seed + 21f) * 0.5f + 0.5f;
                float n2 = Noise.Fbm(wx / 13f, wz / 13f, 2, 2f, 0.5f, seed + 37f) * 0.5f + 0.5f;

                int sample = NearestSample(wx, wz);
                RoadsideStyle style = route.Roadside[sample];
                SurfaceType surface = route.Surfaces[sample];
                float half = route.Widths[sample] * 0.5f;

                System.Array.Clear(weights, 0, LayerCount);

                bool forest = style == RoadsideStyle.Forest || d > 140f;
                weights[(int)Layer.Grass] = forest ? 0.25f + n1 * 0.3f : 0.55f + n1 * 0.3f;
                weights[(int)Layer.Meadow] = forest ? 0.05f : Mathf.Clamp01(n2 * 1.4f - 0.3f);
                weights[(int)Layer.ForestFloor] = forest ? 0.75f + n2 * 0.4f : 0.1f * n2;

                // Road bed, shoulders and tyre-worn verges.
                float roadMask = 1f - Smooth(half + 0.5f, half + 3.2f + n2 * 1.5f, d);
                if (roadMask > 0f)
                {
                    Layer roadLayer = surface == SurfaceType.Gravel || surface == SurfaceType.Asphalt ? Layer.Gravel
                        : surface == SurfaceType.Mud ? Layer.Mud : Layer.RoadDirt;
                    for (int l = 0; l < LayerCount; l++) weights[l] *= 1f - roadMask;
                    weights[(int)roadLayer] += roadMask;
                }

                // Random mud patches in the verges of damp sections.
                float mudPatch = Smooth(0.62f, 0.75f, n1) * (1f - Smooth(half + 6f, half + 16f, d));
                if (surface == SurfaceType.Mud) mudPatch = Mathf.Max(mudPatch, 1f - Smooth(half + 2f, half + 12f, d));
                weights[(int)Layer.Mud] += mudPatch * 0.9f;

                float rock = Smooth(28f, 40f, slope + n2 * 6f);
                for (int l = 0; l < LayerCount; l++) weights[l] *= 1f - rock;
                weights[(int)Layer.Rock] += rock;

                float total = 0f;
                for (int l = 0; l < LayerCount; l++) total += weights[l];
                for (int l = 0; l < LayerCount; l++) splat[z, x, l] = weights[l] / Mathf.Max(0.0001f, total);
            }
            return splat;
        }

        public int NearestSample(float x, float z)
        {
            int n = DistanceGridSize;
            int ix = Mathf.Clamp(Mathf.RoundToInt((x - Origin.x) / coarseCell), 0, n - 1);
            int iz = Mathf.Clamp(Mathf.RoundToInt((z - Origin.z) / coarseCell), 0, n - 1);
            int guess = coarseNearest[ix, iz];

            Vector2 p = new Vector2(x, z);
            int best = guess;
            float bestSqr = float.MaxValue;
            int from = Mathf.Max(0, guess - 16), to = Mathf.Min(route.Count - 1, guess + 16);
            for (int i = from; i <= to; i++)
            {
                float sqr = (route.Planar[i] - p).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }
            return best;
        }

        public static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
