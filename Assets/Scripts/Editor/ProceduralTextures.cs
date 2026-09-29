using Rally.Procedural;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// All generated textures of the project. Alpha of albedo maps stores smoothness (wetness).
    /// Every texture tiles seamlessly.
    /// </summary>
    public static class ProceduralTextures
    {
        private static float F(float u, float v, int period, int octaves, int seed, float gain = 0.5f) =>
            Noise.TileFbm(u, v, period, octaves, gain, seed) * 0.5f + 0.5f;

        private static float Cell(float u, float v, int cells, int seed) => Noise.TileCellular(u, v, cells, seed);

        private static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

        private static float S(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static Color WithAlpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

        // ------------------------------------------------------------------ terrain

        public static (Texture2D, Texture2D) Grass() => TextureBaker.Bake("T_Grass", 512, 512, (u, v) =>
        {
            float big = F(u, v, 4, 4, 1);
            float mid = F(u, v, 16, 3, 2);
            float blades = F(u * 1f, v * 1f, 64, 2, 3, 0.6f);
            Color dark = new Color(0.14f, 0.2f, 0.07f), green = new Color(0.26f, 0.36f, 0.12f), dry = new Color(0.42f, 0.4f, 0.2f);
            Color c = Mix(dark, green, mid * 1.3f);
            c = Mix(c, dry, S(0.55f, 0.8f, big) * 0.7f);
            c *= 0.75f + blades * 0.45f;
            return (WithAlpha(c, 0.25f + blades * 0.15f), blades * 0.6f + mid * 0.4f);
        }, 3f);

        public static (Texture2D, Texture2D) Meadow() => TextureBaker.Bake("T_Meadow", 512, 512, (u, v) =>
        {
            float big = F(u, v, 4, 4, 11);
            float streak = F(u * 1f, v * 1f, 48, 3, 12, 0.55f);
            Color straw = new Color(0.38f, 0.35f, 0.19f), olive = new Color(0.24f, 0.27f, 0.12f);
            Color c = Mix(olive, straw, big * 1.4f - 0.2f) * (0.7f + streak * 0.5f);
            return (WithAlpha(c, 0.2f), streak);
        }, 2.5f);

        public static (Texture2D, Texture2D) ForestFloor() => TextureBaker.Bake("T_ForestFloor", 512, 512, (u, v) =>
        {
            float big = F(u, v, 4, 4, 21);
            float needles = Mathf.Abs(Noise.TileFbm(u, v, 96, 2, 0.5f, 22));
            needles = 1f - S(0f, 0.08f, needles);
            float moss = S(0.55f, 0.72f, F(u, v, 8, 3, 23));
            Color soil = new Color(0.16f, 0.12f, 0.08f), needle = new Color(0.36f, 0.24f, 0.13f), mossC = new Color(0.15f, 0.22f, 0.07f);
            Color c = Mix(soil, needle, needles * 0.8f) * (0.8f + big * 0.4f);
            c = Mix(c, mossC, moss);
            return (WithAlpha(c, 0.3f + (1f - moss) * 0.15f), needles * 0.5f + big * 0.5f);
        }, 3f);

        public static (Texture2D, Texture2D) Gravel() => TextureBaker.Bake("T_Gravel", 512, 512, (u, v) =>
        {
            float big = F(u, v, 4, 4, 33);
            float grit = F(u, v, 96, 2, 32);
            float pebbleDist = Cell(u, v, 64, 31);
            float pebbleOn = Noise.Hash01(Mathf.FloorToInt(u * 64f), Mathf.FloorToInt(v * 64f), 34) < 0.5f ? 1f : 0f;
            float pebble = (1f - S(0.2f, 0.35f, pebbleDist)) * pebbleOn;
            Color c = Mix(new Color(0.4f, 0.38f, 0.34f), new Color(0.5f, 0.47f, 0.42f), big) * (0.88f + grit * 0.22f);
            c = Mix(c, new Color(0.58f, 0.56f, 0.52f), pebble * 0.7f);
            return (WithAlpha(c, 0.35f), grit * 0.4f + pebble * 0.6f);
        }, 2.5f);

        public static (Texture2D, Texture2D) Rock() => TextureBaker.Bake("T_Rock", 512, 512, (u, v) =>
        {
            float r = Noise.TileFbm(u, v, 6, 6, 0.55f, 41) * 0.5f + 0.5f;
            float cracks = S(0f, 0.06f, Mathf.Abs(Noise.TileFbm(u, v, 12, 3, 0.5f, 42)));
            float lichen = S(0.6f, 0.75f, F(u, v, 10, 3, 43));
            Color c = Mix(new Color(0.3f, 0.3f, 0.29f), new Color(0.5f, 0.49f, 0.46f), r) * (0.6f + cracks * 0.4f);
            c = Mix(c, new Color(0.35f, 0.38f, 0.22f), lichen * 0.6f);
            return (WithAlpha(c, 0.3f + (1f - cracks) * 0.2f), r * 0.7f + cracks * 0.3f);
        }, 6f);

        public static (Texture2D, Texture2D) Mud() => TextureBaker.Bake("T_Mud", 512, 512, (u, v) =>
        {
            float big = F(u, v, 4, 5, 51);
            float puddle = S(0.6f, 0.66f, big);
            float lumps = F(u, v, 32, 3, 52);
            Color mud = new Color(0.17f, 0.12f, 0.08f) * (0.8f + lumps * 0.4f);
            Color water = new Color(0.08f, 0.07f, 0.06f);
            Color c = Mix(mud, water, puddle);
            return (WithAlpha(c, Mathf.Lerp(0.55f, 0.97f, puddle)), (1f - puddle) * (lumps * 0.6f + big * 0.4f));
        }, 3f);

        public static (Texture2D, Texture2D) VergeDirt() => TextureBaker.Bake("T_VergeDirt", 512, 512, (u, v) =>
        {
            float big = F(u, v, 4, 4, 61);
            float pebbles = 1f - S(0.1f, 0.4f, Cell(u, v, 64, 62));
            Color c = Mix(new Color(0.27f, 0.2f, 0.14f), new Color(0.38f, 0.3f, 0.21f), big);
            c = Mix(c, new Color(0.45f, 0.42f, 0.38f), pebbles * 0.6f);
            return (WithAlpha(c, 0.45f), big * 0.5f + pebbles * 0.5f);
        }, 3f);

        // ------------------------------------------------------------------ roads (u = across, v = along)

        private static float Ruts(float u, float centerA, float centerB, float width) =>
            Mathf.Max(1f - S(0f, width, Mathf.Abs(u - centerA)), 1f - S(0f, width, Mathf.Abs(u - centerB)));

        public static (Texture2D, Texture2D) RoadDirt() => TextureBaker.Bake("T_Road_Dirt", 512, 1024, (u, v) =>
        {
            float wob = (F(0.3f, v, 6, 3, 71) - 0.5f) * 0.05f;
            float ruts = Ruts(u + wob, 0.28f, 0.72f, 0.1f);
            float big = F(u, v, 4, 4, 72);
            float grit = F(u, v, 128, 2, 75);
            float pebbleOn = Noise.Hash01(Mathf.FloorToInt(u * 70f), Mathf.FloorToInt(v * 70f), 76) < 0.3f ? 1f : 0f;
            float pebbles = (1f - S(0.15f, 0.3f, Cell(u, v, 70, 73))) * pebbleOn * (1f - ruts);
            float edge = S(0.1f, 0f, Mathf.Min(u, 1f - u));
            float puddle = S(0.66f, 0.7f, F(u, v, 3, 4, 74)) * ruts;

            Color dirt = Mix(new Color(0.36f, 0.28f, 0.19f), new Color(0.45f, 0.36f, 0.25f), big) * (0.9f + grit * 0.18f);
            Color rutC = new Color(0.27f, 0.2f, 0.14f);
            Color c = Mix(dirt, rutC, ruts * 0.75f);
            c = Mix(c, new Color(0.5f, 0.46f, 0.4f), pebbles * 0.6f);
            c = Mix(c, new Color(0.2f, 0.22f, 0.1f), edge * 0.6f);
            c = Mix(c, new Color(0.09f, 0.08f, 0.07f), puddle);
            float smooth = 0.3f + ruts * 0.25f + puddle * 0.6f;
            float h = big * 0.3f + grit * 0.15f + pebbles * 0.35f - ruts * 0.3f;
            return (WithAlpha(c, smooth), h);
        }, 2.5f, TextureWrapMode.Clamp);

        public static (Texture2D, Texture2D) RoadGravel() => TextureBaker.Bake("T_Road_Gravel", 512, 1024, (u, v) =>
        {
            float wob = (F(0.7f, v, 6, 3, 81) - 0.5f) * 0.06f;
            float ruts = Ruts(u + wob, 0.27f, 0.73f, 0.12f);
            float big = F(u, v, 4, 4, 83);
            float grit = F(u, v, 128, 2, 84);
            // Sparse loose pebbles on compacted grit; fewer in the wheel tracks.
            float pebbleDist = Cell(u, v, 90, 82);
            float pebbleOn = Noise.Hash01(Mathf.FloorToInt(u * 90f), Mathf.FloorToInt(v * 90f), 85) < 0.45f * (1f - ruts * 0.7f) ? 1f : 0f;
            float pebble = (1f - S(0.18f, 0.32f, pebbleDist)) * pebbleOn;
            float edge = S(0.08f, 0f, Mathf.Min(u, 1f - u));

            Color compact = Mix(new Color(0.47f, 0.44f, 0.39f), new Color(0.56f, 0.53f, 0.47f), big) * (0.88f + grit * 0.2f);
            Color c = Mix(compact, new Color(0.38f, 0.35f, 0.31f), ruts * 0.55f);
            Color stone = Mix(new Color(0.5f, 0.49f, 0.46f), new Color(0.64f, 0.62f, 0.57f), Noise.Hash01(Mathf.FloorToInt(u * 90f), Mathf.FloorToInt(v * 90f), 86));
            c = Mix(c, stone, pebble * 0.8f);
            c = Mix(c, new Color(0.25f, 0.26f, 0.15f), edge * 0.5f);
            return (WithAlpha(c, 0.3f + ruts * 0.15f), grit * 0.3f + pebble * 0.7f - ruts * 0.2f);
        }, 2.5f, TextureWrapMode.Clamp);

        public static (Texture2D, Texture2D) RoadMud() => TextureBaker.Bake("T_Road_Mud", 512, 1024, (u, v) =>
        {
            float wob = (F(0.5f, v, 6, 3, 91) - 0.5f) * 0.07f;
            float ruts = Ruts(u + wob, 0.28f, 0.72f, 0.12f);
            float big = F(u, v, 4, 5, 92);
            float lumps = F(u, v, 40, 3, 93);
            float puddle = Mathf.Max(S(0.62f, 0.66f, big), ruts * S(0.5f, 0.58f, big));
            Color mud = Mix(new Color(0.24f, 0.17f, 0.11f), new Color(0.32f, 0.24f, 0.15f), lumps);
            Color c = Mix(mud, new Color(0.12f, 0.09f, 0.06f), ruts * 0.7f);
            c = Mix(c, new Color(0.07f, 0.06f, 0.05f), puddle);
            float h = lumps * 0.5f * (1f - puddle) - ruts * 0.5f;
            return (WithAlpha(c, 0.45f + puddle * 0.5f), h);
        }, 4f, TextureWrapMode.Clamp);

        public static (Texture2D, Texture2D) RoadAsphalt() => TextureBaker.Bake("T_Road_Asphalt", 512, 1024, (u, v) =>
        {
            float aggregate = F(u, v, 128, 2, 101);
            float big = F(u, v, 3, 4, 102);
            float cracks = 1f - S(0f, 0.025f, Mathf.Abs(Noise.TileFbm(u, v, 5, 4, 0.5f, 103)));
            float patch = S(0.62f, 0.64f, F(u, v, 3, 3, 104));
            float wear = Ruts(u, 0.27f, 0.73f, 0.12f);

            Color c = new Color(0.14f, 0.14f, 0.145f) * (0.8f + aggregate * 0.45f);
            c = Mix(c, new Color(0.1f, 0.1f, 0.105f), patch);
            c = Mix(c, new Color(0.19f, 0.19f, 0.19f), wear * 0.35f * (1f - patch));
            c = Mix(c, new Color(0.05f, 0.05f, 0.05f), cracks * 0.8f);

            // Edge lines and a dashed centre line.
            float lineEdge = Mathf.Max(1f - S(0.006f, 0.012f, Mathf.Abs(u - 0.05f)), 1f - S(0.006f, 0.012f, Mathf.Abs(u - 0.95f)));
            float lineCenter = (1f - S(0.005f, 0.01f, Mathf.Abs(u - 0.5f))) * (v % 1f < 0.4f ? 1f : 0f);
            float paint = Mathf.Max(lineEdge, lineCenter) * (0.75f + aggregate * 0.25f);
            c = Mix(c, new Color(0.78f, 0.78f, 0.74f), paint);

            float smooth = 0.55f + wear * 0.15f + patch * 0.1f - paint * 0.2f;
            return (WithAlpha(c, smooth), aggregate * 0.5f - cracks * 0.5f);
        }, 3f, TextureWrapMode.Clamp);

        // ------------------------------------------------------------------ nature & props

        public static (Texture2D, Texture2D) Bark() => TextureBaker.Bake("T_Bark", 256, 512, (u, v) =>
        {
            float grooves = Mathf.Abs(Noise.TileFbm(u * 1f, v * 0.25f, 8, 3, 0.5f, 111));
            float g = S(0f, 0.25f, grooves);
            Color c = Mix(new Color(0.1f, 0.07f, 0.05f), new Color(0.3f, 0.22f, 0.15f), g);
            return (WithAlpha(c, 0.15f), g);
        }, 5f);

        public static (Texture2D, Texture2D) Needles() => TextureBaker.Bake("T_Needles", 256, 256, (u, v) =>
        {
            float n = F(u, v, 32, 3, 121);
            float clumps = F(u, v, 6, 3, 122);
            Color c = Mix(new Color(0.04f, 0.09f, 0.04f), new Color(0.11f, 0.2f, 0.08f), n * 0.7f + clumps * 0.5f);
            return (WithAlpha(c, 0.2f), n);
        }, 3f);

        public static (Texture2D, Texture2D) Leaves() => TextureBaker.Bake("T_Leaves", 256, 256, (u, v) =>
        {
            float n = 1f - Cell(u, v, 20, 131);
            float tint = F(u, v, 4, 3, 132);
            Color c = Mix(new Color(0.12f, 0.2f, 0.06f), new Color(0.3f, 0.36f, 0.1f), n * 0.6f + tint * 0.5f);
            return (WithAlpha(c, 0.3f), n);
        }, 3f);

        public static (Texture2D, Texture2D) BirchBark() => TextureBaker.Bake("T_BirchBark", 256, 512, (u, v) =>
        {
            float marks = S(0.62f, 0.7f, F(u * 0.5f, v * 1f, 16, 2, 141));
            Color c = Mix(new Color(0.78f, 0.77f, 0.72f), new Color(0.12f, 0.11f, 0.1f), marks);
            return (WithAlpha(c, 0.2f), 1f - marks);
        }, 2f);

        public static (Texture2D, Texture2D) Wood() => TextureBaker.Bake("T_Wood", 256, 256, (u, v) =>
        {
            float grain = Mathf.Sin((u * 40f + F(u, v, 4, 3, 151) * 8f) * Mathf.PI) * 0.5f + 0.5f;
            Color c = Mix(new Color(0.3f, 0.21f, 0.13f), new Color(0.44f, 0.32f, 0.2f), grain) * (0.8f + F(u, v, 16, 2, 152) * 0.3f);
            return (WithAlpha(c, 0.2f), grain);
        }, 2f);

        public static (Texture2D, Texture2D) Plaster() => TextureBaker.Bake("T_Plaster", 256, 256, (u, v) =>
        {
            float n = F(u, v, 8, 5, 161);
            float stains = S(0.6f, 0.8f, F(u, v, 3, 3, 162));
            Color c = Mix(new Color(0.82f, 0.79f, 0.72f), new Color(0.6f, 0.57f, 0.5f), n * 0.6f + stains * 0.5f);
            return (WithAlpha(c, 0.15f), n);
        }, 2f);

        public static (Texture2D, Texture2D) RoofTiles() => TextureBaker.Bake("T_RoofTiles", 256, 256, (u, v) =>
        {
            float row = (v * 16f) % 1f;
            float col = ((u * 8f) + (Mathf.Floor(v * 16f) % 2f) * 0.5f) % 1f;
            float shade = Mathf.Pow(row, 0.5f) * (0.7f + 0.3f * Mathf.Sin(col * Mathf.PI));
            float n = F(u, v, 8, 3, 171);
            Color c = Mix(new Color(0.35f, 0.14f, 0.09f), new Color(0.6f, 0.28f, 0.17f), shade * 0.8f + n * 0.3f);
            return (WithAlpha(c, 0.35f), shade);
        }, 4f);

        public static (Texture2D, Texture2D) Tyre() => TextureBaker.Bake("T_Tyre", 256, 256, (u, v) =>
        {
            float tread = Mathf.Abs(((u * 24f + Mathf.Abs(v - 0.5f) * 6f) % 1f) - 0.5f);
            float block = S(0.12f, 0.18f, tread);
            Color c = new Color(0.06f, 0.06f, 0.06f) * (0.7f + block * 0.5f);
            return (WithAlpha(c, 0.25f), block);
        }, 4f);

        public static Texture2D Chevron()
        {
            var (tex, _) = TextureBaker.Bake("T_Chevron", 256, 128, (u, v) =>
            {
                float x = (u * 3f) % 1f;
                float arrow = Mathf.Abs(v - 0.5f) * 0.9f;
                float d = x - arrow;
                bool red = d > 0.15f && d < 0.55f;
                return (red ? new Color(0.75f, 0.06f, 0.05f, 0.3f) : new Color(0.92f, 0.92f, 0.9f, 0.3f), 0f);
            }, 0f);
            return tex;
        }

        public static Texture2D Checker()
        {
            var (tex, _) = TextureBaker.Bake("T_Checker", 256, 64, (u, v) =>
            {
                bool black = ((int)(u * 16f) + (int)(v * 4f)) % 2 == 0;
                return (black ? new Color(0.05f, 0.05f, 0.05f, 0.3f) : new Color(0.93f, 0.93f, 0.93f, 0.3f), 0f);
            }, 0f);
            return tex;
        }

        public static Texture2D TapeStripes()
        {
            var (tex, _) = TextureBaker.Bake("T_Tape", 128, 32, (u, v) =>
            {
                bool red = ((u * 8f + v * 2f) % 1f) < 0.5f;
                return (red ? new Color(0.85f, 0.08f, 0.06f, 0.4f) : new Color(0.95f, 0.95f, 0.95f, 0.4f), 0f);
            }, 0f);
            return tex;
        }

        // ------------------------------------------------------------------ alpha sprites

        public static Texture2D DustPuff() => TextureBaker.Save("T_DustPuff", 128, 128, Sprite(128, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float n = Noise.TileFbm(u, v, 4, 4, 0.55f, 201) * 0.5f + 0.5f;
            float a = Mathf.Clamp01(1f - S(0.2f, 1f, r + (n - 0.5f) * 0.6f));
            return new Color(1f, 1f, 1f, a * a);
        }), TextureBaker.Kind.AlphaSprite, TextureWrapMode.Clamp, TextureWrapMode.Clamp);

        public static Texture2D SoftDot() => TextureBaker.Save("T_SoftDot", 64, 64, Sprite(64, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
            return new Color(1f, 1f, 1f, a * a);
        }), TextureBaker.Kind.AlphaSprite, TextureWrapMode.Clamp, TextureWrapMode.Clamp);

        public static Texture2D SkidMark() => TextureBaker.Save("T_SkidMark", 64, 64, Sprite(64, (u, v) =>
        {
            float edge = S(0f, 0.2f, Mathf.Min(u, 1f - u));
            float tread = 0.7f + 0.3f * Mathf.Sin(u * 30f);
            float n = Noise.TileFbm(u, v, 8, 2, 0.5f, 211) * 0.3f + 0.7f;
            return new Color(0.1f, 0.08f, 0.06f, edge * tread * n * 0.85f);
        }), TextureBaker.Kind.AlphaSprite, TextureWrapMode.Clamp, TextureWrapMode.Repeat);

        public static Texture2D Puddle() => TextureBaker.Save("T_Puddle", 256, 256, Sprite(256, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float n = Noise.TileFbm(u, v, 3, 4, 0.5f, 221) * 0.5f;
            float a = 1f - S(0.55f, 0.8f, r + n);
            return new Color(0.06f, 0.06f, 0.055f, a);
        }), TextureBaker.Kind.AlphaSprite, TextureWrapMode.Clamp, TextureWrapMode.Clamp);

        public static Texture2D GrassBlades() => TextureBaker.Save("T_GrassBlades", 256, 256, GrassSprite(),
            TextureBaker.Kind.AlphaSprite, TextureWrapMode.Clamp, TextureWrapMode.Clamp);

        private static Color[] Sprite(int size, System.Func<float, float, Color> f)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = f((x + 0.5f) / size, (y + 0.5f) / size);
            return px;
        }

        private static Color[] GrassSprite()
        {
            const int size = 256;
            var px = new Color[size * size];
            var rng = new System.Random(7);
            for (int b = 0; b < 46; b++)
            {
                float x0 = 0.08f + (float)rng.NextDouble() * 0.84f;
                float h = 0.45f + (float)rng.NextDouble() * 0.53f;
                float lean = ((float)rng.NextDouble() - 0.5f) * 0.35f;
                float w = 0.012f + (float)rng.NextDouble() * 0.014f;
                Color col = Color.Lerp(new Color(0.16f, 0.22f, 0.07f), new Color(0.42f, 0.42f, 0.2f), (float)rng.NextDouble() * 0.8f);
                for (int y = 0; y < size; y++)
                {
                    float t = (y + 0.5f) / size / h;
                    if (t > 1f) break;
                    float cx = x0 + lean * t * t;
                    float half = w * (1f - t);
                    int xa = Mathf.FloorToInt((cx - half) * size), xb = Mathf.CeilToInt((cx + half) * size);
                    for (int x = Mathf.Max(0, xa); x <= Mathf.Min(size - 1, xb); x++)
                    {
                        Color shaded = col * (0.55f + t * 0.6f);
                        shaded.a = 1f;
                        px[y * size + x] = shaded;
                    }
                }
            }
            // Bleed colour into transparent pixels to avoid dark halos when mip-mapped.
            for (int i = 0; i < px.Length; i++)
                if (px[i].a < 0.5f) px[i] = new Color(0.25f, 0.3f, 0.12f, 0f);
            return px;
        }

        // ------------------------------------------------------------------ sky

        public static Texture2D OvercastSky()
        {
            const int w = 2048, h = 1024;
            var px = new Color[w * h];
            Color zenith = new Color(0.47f, 0.52f, 0.58f);
            Color horizon = new Color(0.76f, 0.78f, 0.79f);
            Color cloudLight = new Color(0.86f, 0.87f, 0.88f);
            Color cloudDark = new Color(0.44f, 0.46f, 0.5f);
            Color ground = new Color(0.3f, 0.31f, 0.3f);
            Vector2 sun = new Vector2(0.62f, 0.72f);

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float elevation = (v - 0.5f) * 2f; // -1 .. 1
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    Color c;
                    if (elevation < 0f)
                    {
                        c = Color.Lerp(horizon, ground, S(0f, 0.15f, -elevation));
                    }
                    else
                    {
                        float e = Mathf.Max(0.02f, elevation);
                        // Perspective-ish squash towards the horizon.
                        float cy = Mathf.Log(1f / e + 1f) * 0.25f;
                        float n = Noise.TileFbm(u, cy % 1f, 6, 6, 0.55f, 301) * 0.5f + 0.5f;
                        float detail = Noise.TileFbm(u, (cy * 2f) % 1f, 24, 4, 0.5f, 302) * 0.5f + 0.5f;
                        float cover = S(0.3f, 0.62f, n + detail * 0.15f);
                        Color sky = Color.Lerp(horizon, zenith, Mathf.Pow(elevation, 0.6f));
                        Color cloud = Color.Lerp(cloudDark, cloudLight, S(0.35f, 0.85f, detail * 0.6f + n * 0.5f));
                        c = Color.Lerp(sky, cloud, cover * 0.92f);
                        // Haze to horizon.
                        c = Color.Lerp(c, horizon, 1f - S(0f, 0.22f, elevation));

                        float du = Mathf.Min(Mathf.Abs(u - sun.x), 1f - Mathf.Abs(u - sun.x));
                        float d = Mathf.Sqrt(du * du * 4f + (v - sun.y) * (v - sun.y));
                        c += new Color(1f, 0.97f, 0.9f) * (Mathf.Exp(-d * 9f) * 0.18f * (1.2f - cover));
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            return TextureBaker.Save("T_Sky_Overcast", w, h, px, TextureBaker.Kind.Albedo, TextureWrapMode.Repeat, TextureWrapMode.Clamp, 4096);
        }

        /// <summary>
        /// Clear desert sky: deep blue overhead fading into a warm, dusty horizon band (the calima), a bright sun
        /// halo and a few faint high streaks. Smaller than the overcast sky: a smooth gradient needs less detail.
        /// </summary>
        public static Texture2D ClearSky()
        {
            const int w = 1024, h = 512;
            var px = new Color[w * h];
            Color zenith = new Color(0.22f, 0.42f, 0.72f);
            Color midSky = new Color(0.47f, 0.64f, 0.84f);
            Color haze = new Color(0.9f, 0.82f, 0.68f);
            Color ground = new Color(0.62f, 0.5f, 0.36f);
            Vector2 sun = new Vector2(0.62f, 0.8f);

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float elevation = (v - 0.5f) * 2f; // -1 .. 1
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    Color c;
                    if (elevation < 0f)
                    {
                        c = Color.Lerp(haze, ground, S(0f, 0.2f, -elevation));
                    }
                    else
                    {
                        c = Color.Lerp(midSky, zenith, S(0.15f, 0.9f, elevation));
                        c = Color.Lerp(c, haze, 1f - S(0f, 0.28f, elevation)); // dusty band at the horizon

                        // Faint high streaks.
                        float e = Mathf.Max(0.05f, elevation);
                        float cy = Mathf.Log(1f / e + 1f) * 0.25f;
                        float streak = Noise.TileFbm(u, (cy * 3f) % 1f, 8, 4, 0.5f, 311) * 0.5f + 0.5f;
                        c = Color.Lerp(c, new Color(0.95f, 0.95f, 0.97f), S(0.62f, 0.8f, streak) * 0.25f * S(0.1f, 0.4f, elevation));

                        float du = Mathf.Min(Mathf.Abs(u - sun.x), 1f - Mathf.Abs(u - sun.x));
                        float d = Mathf.Sqrt(du * du * 4f + (v - sun.y) * (v - sun.y));
                        c += new Color(1f, 0.95f, 0.82f) * (Mathf.Exp(-d * 7f) * 0.35f + Mathf.Exp(-d * 40f) * 0.5f);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            return TextureBaker.Save("T_Sky_Clear", w, h, px, TextureBaker.Kind.Albedo, TextureWrapMode.Repeat, TextureWrapMode.Clamp, 2048);
        }
    }
}
