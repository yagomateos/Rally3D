using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Look of the stage being built. The forest theme is the original one. With <see cref="Kind.Snow"/> every asset
    /// the builder saves gets a "_Snow" name (so stage 1's textures, materials, meshes, prefabs and terrain are never
    /// overwritten) and generated textures are recoloured for winter: snow on the ground, packed snow on the road,
    /// ice instead of tarmac, slush instead of mud and snow on the pine needles and roofs.
    /// </summary>
    public static class StageTheme
    {
        public enum Kind { Forest = 0, Snow = 1 }

        public static Kind Current { get; set; } = Kind.Forest;
        public static bool Snow => Current == Kind.Snow;
        private const string Suffix = "_Snow";

        /// <summary>Asset name for the current theme ("Road_Dirt" → "Road_Dirt_Snow" in the snow build).</summary>
        public static string Name(string baseName) =>
            Snow && !baseName.EndsWith(Suffix) ? baseName + Suffix : baseName;

        private static readonly Color SnowShadow = new Color(0.66f, 0.72f, 0.82f);
        private static readonly Color SnowLight = new Color(0.95f, 0.97f, 1f);

        private static Color SnowColor(float detail) => Color.Lerp(SnowShadow, SnowLight, Mathf.Clamp01(detail));
        private static float Luminance(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        private static float Patches(float u, float v, float scale, float from, float to) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, Mathf.PerlinNoise(u * scale + 13.1f, v * scale + 7.7f)));

        private static Color Keep(Color rgb, float alpha) => new Color(rgb.r, rgb.g, rgb.b, alpha);

        /// <summary>Winter version of one albedo pixel of the named generated texture (unchanged in the forest theme).</summary>
        public static Color Recolor(string texture, Color c, float u, float v)
        {
            if (!Snow) return c;
            float lum = Luminance(c);
            switch (texture)
            {
                // Terrain ground: fresh snow, keeping a trace of the original detail as soft shading.
                case "T_Grass":
                case "T_Meadow":
                case "T_ForestFloor":
                case "T_VergeDirt":
                case "T_Mud":
                    return Keep(SnowColor(0.55f + (lum - 0.3f) * 1.2f), Mathf.Max(c.a, 0.35f));
                case "T_Gravel":
                    return Keep(Color.Lerp(c, SnowColor(0.8f), 0.6f), c.a);
                case "T_Rock":
                    return Keep(Color.Lerp(c, SnowColor(0.85f), 0.85f * Patches(u, v, 8f, 0.35f, 0.6f)), c.a);

                // Road: packed snow with the ruts' shading, gravel showing through, slush, ice.
                case "T_Road_Dirt":
                    return Keep(Color.Lerp(new Color(0.76f, 0.79f, 0.84f), new Color(0.93f, 0.95f, 0.98f), Mathf.Clamp01(lum * 1.6f)), Mathf.Max(c.a, 0.45f));
                case "T_Road_Gravel":
                    return Keep(Color.Lerp(c, new Color(0.86f, 0.88f, 0.92f), 0.55f), Mathf.Max(c.a, 0.4f));
                case "T_Road_Mud":
                    return Keep(Color.Lerp(c, new Color(0.62f, 0.63f, 0.62f), 0.55f), Mathf.Max(c.a, 0.8f));
                case "T_Road_Asphalt":
                    return Keep(new Color(0.55f, 0.63f, 0.72f) * (0.8f + 0.4f * lum), Mathf.Max(c.a, 0.88f));
                case "T_Puddle":
                    return new Color(0.78f, 0.86f, 0.94f, c.a); // frozen puddles

                // Vegetation and buildings.
                case "T_Needles":
                    return Keep(Color.Lerp(c * 0.8f, SnowLight, 0.85f * Patches(u, v, 22f, 0.55f, 0.7f)), c.a);
                case "T_Leaves":
                    return Keep(Color.Lerp(c, new Color(0.45f, 0.36f, 0.28f), 0.6f), c.a); // dry winter leaves
                case "T_GrassBlades":
                    return new Color(Mathf.Lerp(c.r, 0.85f, 0.5f), Mathf.Lerp(c.g, 0.87f, 0.5f), Mathf.Lerp(c.b, 0.9f, 0.5f), c.a * 0.35f);
                case "T_RoofTiles":
                    return Keep(Color.Lerp(c, SnowLight, 0.8f * Patches(u, v, 6f, 0.3f, 0.5f)), c.a);

                // Sky: brighter, colder overcast.
                case "T_Sky_Overcast":
                    return Keep(Color.Lerp(c, new Color(0.84f, 0.87f, 0.92f), 0.45f) * 1.05f, c.a);
            }
            return c;
        }
    }
}
