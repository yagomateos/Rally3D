using UnityEngine;

namespace Rally.Procedural
{
    /// <summary>Small deterministic noise helpers (fBm, ridged, tileable) used by generators.</summary>
    public static class Noise
    {
        public static float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f, float seed = 0f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(x * freq + seed + i * 17.13f, y * freq - seed + i * 31.7f) * 2f - 1f) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return sum / norm;
        }

        public static float Ridged(float x, float y, int octaves, float seed = 0f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * freq + seed + i * 11.3f, y * freq + seed * 0.7f - i * 5.1f) * 2f - 1f);
                sum += n * n * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        // --- Tileable gradient noise (period in lattice cells), used for seamless textures ---

        private static readonly int[] perm = BuildPermutation(1337);

        private static int[] BuildPermutation(int seed)
        {
            var rng = new System.Random(seed);
            var p = new int[512];
            var src = new int[256];
            for (int i = 0; i < 256; i++) src[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (src[i], src[j]) = (src[j], src[i]);
            }
            for (int i = 0; i < 512; i++) p[i] = src[i & 255];
            return p;
        }

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>Periodic Perlin noise in [-1, 1]. The pattern repeats every <paramref name="period"/> units.</summary>
        public static float TilePerlin(float x, float y, int period, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period);
            int y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            int s = seed & 255;

            int aa = perm[perm[x0 + s] + y0], ab = perm[perm[x0 + s] + y1];
            int ba = perm[perm[x1 + s] + y0], bb = perm[perm[x1 + s] + y1];

            float u = Fade(xf), v = Fade(yf);
            float l1 = Mathf.Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float l2 = Mathf.Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Mathf.Lerp(l1, l2, v) * 0.7f;
        }

        /// <summary>Periodic fBm in roughly [-1, 1]; u, v in [0, 1] map to one tile.</summary>
        public static float TileFbm(float u, float v, int basePeriod, int octaves, float gain = 0.5f, int seed = 0)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += TilePerlin(u * period, v * period, period, seed + i * 31) * amp;
                norm += amp;
                amp *= gain;
                period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Periodic cellular (Worley F1) noise in [0, ~1].</summary>
        public static float TileCellular(float u, float v, int cells, int seed = 0)
        {
            float x = u * cells, y = v * cells;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 10f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int cx = xi + ox, cy = yi + oy;
                int hx = Mod(cx, cells), hy = Mod(cy, cells);
                float px = cx + Hash01(hx, hy, seed);
                float py = cy + Hash01(hy, hx, seed + 7);
                float dx = px - x, dy = py - y;
                best = Mathf.Min(best, dx * dx + dy * dy);
            }
            return Mathf.Sqrt(best);
        }

        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;
    }
}
