using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Look of the stage being built. The forest theme is the original one. With <see cref="Kind.Snow"/> every asset
    /// the builder saves gets a "_Snow" name (so stage 1's textures, materials, meshes, prefabs and terrain are never
    /// overwritten) and generated textures are recoloured for winter: snow on the ground, packed snow on the road,
    /// ice instead of tarmac, slush instead of mud and snow on the pine needles and roofs.
    /// <see cref="Kind.Desert"/> works the same way with a "_Desert" suffix: sand dunes, packed-sand tracks,
    /// red sandstone, sun-faded tarmac, cactus-green "needles" and dry leaves. <see cref="Kind.Coast"/> ("_Coast"):
    /// dry Mediterranean grass, pale limestone, dark fresh tarmac with painted lines and whitewashed walls.
    /// </summary>
    public static class StageTheme
    {
        public enum Kind { Forest = 0, Snow = 1, Desert = 2, Coast = 3, Night = 4 }

        public static Kind Current { get; set; } = Kind.Forest;
        public static bool Snow => Current == Kind.Snow;
        public static bool Desert => Current == Kind.Desert;
        public static bool Coast => Current == Kind.Coast;
        /// <summary>The pine forest at night: stage 1's textures, materials and terrain layers, its own everything else.</summary>
        public static bool Night => Current == Kind.Night;
        public static bool ForestLike => Current == Kind.Forest || Night;
        private static string Suffix => Snow ? "_Snow" : Desert ? "_Desert" : Coast ? "_Coast" : Night ? "_Night" : "";

        /// <summary>
        /// Name for assets saved in place (textures, materials, terrain layers): the night stage shares stage 1's,
        /// which keeps the download small. Other themes get their own copies.
        /// </summary>
        public static string SharedName(string baseName) => Night ? baseName : Name(baseName);

        /// <summary>Asset name for the current theme ("Road_Dirt" → "Road_Dirt_Snow" in the snow build).</summary>
        public static string Name(string baseName) =>
            Suffix.Length > 0 && !baseName.EndsWith(Suffix) ? baseName + Suffix : baseName;

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
            if (Desert) return RecolorDesert(texture, c, u, v);
            if (Coast) return RecolorCoast(texture, c, u, v);
            if (!Snow) return c;
            float lum = Luminance(c);
            switch (texture)
            {
                // Terrain ground: fresh snow, keeping a trace of the original detail as soft shading.
                case "T_Grass":
                case "T_Meadow":
                case "T_ForestFloor":
                case "T_Mud":
                    return Keep(SnowColor(0.55f + (lum - 0.3f) * 1.2f), Mathf.Max(c.a, 0.35f));
                case "T_VergeDirt": // trodden, grubby snow along the road edges
                    return Keep(Color.Lerp(new Color(0.55f, 0.56f, 0.6f), new Color(0.75f, 0.77f, 0.82f), Mathf.Clamp01(lum * 1.4f)), c.a);
                case "T_Gravel":
                    return Keep(Color.Lerp(c, SnowColor(0.8f), 0.6f), c.a);
                case "T_Rock":
                    return Keep(Color.Lerp(c, SnowColor(0.85f), 0.85f * Patches(u, v, 8f, 0.35f, 0.6f)), c.a);

                // Road: packed snow with the ruts' shading, gravel showing through, slush, ice.
                // Packed, dirty snow with darker ruts: clearly greyer than the fresh snow around it, so the road reads.
                case "T_Road_Dirt":
                    return Keep(Color.Lerp(new Color(0.42f, 0.43f, 0.46f), new Color(0.7f, 0.72f, 0.76f), Mathf.Clamp01(lum * 1.6f)), Mathf.Max(c.a, 0.45f));
                case "T_Road_Gravel":
                    return Keep(Color.Lerp(c, new Color(0.62f, 0.64f, 0.68f), 0.55f), Mathf.Max(c.a, 0.4f));
                case "T_Road_Mud":
                    return Keep(Color.Lerp(c, new Color(0.62f, 0.63f, 0.62f), 0.55f), Mathf.Max(c.a, 0.8f));
                case "T_Road_Asphalt":
                    return Keep(new Color(0.55f, 0.63f, 0.72f) * (0.8f + 0.4f * lum), Mathf.Max(c.a, 0.88f));
                case "T_Puddle":
                    return new Color(0.78f, 0.86f, 0.94f, c.a); // frozen puddles

                // Vegetation and buildings.
                case "T_Needles":
                    return Keep(Color.Lerp(c * 0.8f, SnowLight, 0.85f * Patches(u, v, 22f, 0.55f, 0.7f)), c.a);
                case "T_Leaves": // scrub and birches under snow: dark evergreen with thick snow on top (olive read as rocks)
                    return Keep(Color.Lerp(new Color(0.16f, 0.24f, 0.18f) * (0.7f + 0.6f * lum), SnowLight, 0.7f * Patches(u, v, 14f, 0.4f, 0.6f)), c.a);
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

        private static readonly Color SandShadow = new Color(0.74f, 0.57f, 0.38f);
        private static readonly Color SandLight = new Color(0.93f, 0.8f, 0.6f);
        private static Color SandColor(float detail) => Color.Lerp(SandShadow, SandLight, Mathf.Clamp01(detail));

        /// <summary>Coastal version of one albedo pixel (the asphalt also gets its painted lines here).</summary>
        private static Color RecolorCoast(string texture, Color c, float u, float v)
        {
            float lum = Luminance(c);
            switch (texture)
            {
                case "T_Grass":
                case "T_Meadow":
                    return Keep(Color.Lerp(c, new Color(0.62f, 0.6f, 0.36f), 0.55f), c.a); // sun-dried grass
                case "T_ForestFloor":
                    return Keep(Color.Lerp(c, new Color(0.46f, 0.38f, 0.26f), 0.45f), c.a); // pine-needle litter
                case "T_Rock":
                    return Keep(new Color(0.74f, 0.7f, 0.62f) * (0.6f + 0.7f * lum), c.a); // limestone cliffs
                case "T_Gravel":
                case "T_Road_Gravel":
                    return Keep(Color.Lerp(c, new Color(0.7f, 0.68f, 0.64f), 0.5f), c.a);
                case "T_Mud":
                case "T_Road_Mud":
                    return Keep(Color.Lerp(c, new Color(0.56f, 0.46f, 0.34f), 0.6f), c.a); // dry earth
                case "T_VergeDirt":
                case "T_Road_Dirt":
                    return Keep(Color.Lerp(c, new Color(0.62f, 0.54f, 0.42f), 0.5f), c.a);
                case "T_Road_Asphalt":
                    // Mid-grey rather than black: in the low sunset light darker tarmac read as a black hole on the web.
                    return RoadLines(Keep(new Color(0.38f, 0.38f, 0.39f) * (0.75f + 0.55f * lum), c.a), u, v);
                case "T_Needles":
                    return Keep(Color.Lerp(c, new Color(0.24f, 0.34f, 0.2f), 0.4f), c.a); // Mediterranean pines
                case "T_Plaster":
                    return Keep(new Color(0.94f, 0.92f, 0.87f) * (0.9f + 0.15f * lum), c.a); // whitewashed walls
            }
            return c;
        }

        /// <summary>
        /// White edge lines and a dashed centre line. u runs across the road (0..1 edge to edge), v along it
        /// (one texture length = 9 m, so the dash is 4.5 m on, 4.5 m off).
        /// </summary>
        private static Color RoadLines(Color c, float u, float v)
        {
            var paint = new Color(0.9f, 0.9f, 0.86f, c.a);
            bool edge = (u > 0.035f && u < 0.06f) || (u > 0.94f && u < 0.965f);
            bool centre = Mathf.Abs(u - 0.5f) < 0.012f && v < 0.5f;
            return edge || centre ? paint : c;
        }

        /// <summary>Desert version of one albedo pixel: the original detail survives as shading.</summary>
        private static Color RecolorDesert(string texture, Color c, float u, float v)
        {
            float lum = Luminance(c);
            switch (texture)
            {
                // Terrain: dunes, darker pebbly sand, soft powder sand, packed verges, tan gravel, red sandstone.
                case "T_Grass":
                case "T_Meadow":
                    return Keep(SandColor(0.5f + (lum - 0.3f) * 1.3f), Mathf.Max(c.a, 0.35f));
                case "T_ForestFloor":
                    return Keep(Color.Lerp(SandColor(0.3f + lum), new Color(0.55f, 0.42f, 0.3f), 0.35f), c.a);
                case "T_Mud":
                    return Keep(new Color(0.92f, 0.83f, 0.66f) * (0.88f + 0.3f * lum), Mathf.Max(c.a, 0.3f));
                case "T_VergeDirt":
                    return Keep(Color.Lerp(new Color(0.62f, 0.47f, 0.32f), SandShadow, Mathf.Clamp01(lum * 1.5f)), c.a);
                case "T_Gravel":
                    return Keep(Color.Lerp(c, new Color(0.72f, 0.6f, 0.45f), 0.6f), c.a);
                case "T_Rock":
                    return Keep(new Color(0.66f, 0.4f, 0.26f) * (0.65f + 0.7f * lum), c.a);

                // Road: packed-sand piste, rocky hamada, soft sand drifts, old sun-faded tarmac.
                case "T_Road_Dirt": // darker, redder packed sand than the dunes, so the piste reads from a distance
                    return Keep(Color.Lerp(new Color(0.55f, 0.4f, 0.28f), new Color(0.74f, 0.58f, 0.41f), Mathf.Clamp01(lum * 1.6f)), c.a);
                case "T_Road_Gravel":
                    return Keep(Color.Lerp(c, new Color(0.74f, 0.62f, 0.47f), 0.55f), c.a);
                case "T_Road_Mud":
                    return Keep(new Color(0.9f, 0.79f, 0.6f) * (0.85f + 0.3f * lum), Mathf.Max(c.a, 0.8f));
                case "T_Road_Asphalt":
                    return Keep(Color.Lerp(c, new Color(0.5f, 0.46f, 0.41f), 0.45f) * 1.1f, c.a);

                // Vegetation and buildings: cactus green, dry olive leaves, adobe walls.
                case "T_Needles":
                    return Keep(new Color(0.3f, 0.46f, 0.26f) * (0.7f + 0.6f * lum), c.a);
                case "T_Leaves": // dry scrub and acacia leaves
                    return Keep(Color.Lerp(c, new Color(0.56f, 0.5f, 0.32f), 0.8f), c.a);
                case "T_Bark":
                    return Keep(Color.Lerp(c, new Color(0.34f, 0.46f, 0.28f), 0.75f), c.a); // cactus arms and trunks
                case "T_Plaster":
                    return Keep(Color.Lerp(c, new Color(0.86f, 0.7f, 0.5f), 0.55f), c.a);
            }
            return c;
        }
    }
}
