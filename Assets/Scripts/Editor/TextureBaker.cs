using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>Renders procedural textures to PNG assets and configures their importers.</summary>
    public static class TextureBaker
    {
        public const string Folder = "Assets/Art/Textures/Generated";

        public enum Kind { Albedo, Normal, AlphaSprite, Linear }

        public struct Result
        {
            public Color[] albedo;
            public float[] height;
        }

        /// <summary>Generate albedo (+ optional normal map from height) and save both.</summary>
        public static (Texture2D albedo, Texture2D normal) Bake(string name, int width, int height,
            Func<float, float, (Color color, float height)> sample, float normalStrength = 0f,
            TextureWrapMode wrapU = TextureWrapMode.Repeat, TextureWrapMode wrapV = TextureWrapMode.Repeat,
            Kind kind = Kind.Albedo)
        {
            var colors = new Color[width * height];
            var heights = new float[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var (c, h) = sample((x + 0.5f) / width, (y + 0.5f) / height);
                colors[y * width + x] = c;
                heights[y * width + x] = h;
            }

            Texture2D albedo = Save(name, width, height, colors, kind, wrapU, wrapV);
            Texture2D normal = null;
            if (normalStrength > 0f)
            {
                var normals = HeightToNormal(heights, width, height, normalStrength);
                normal = Save(name + "_N", width, height, normals, Kind.Normal, wrapU, wrapV);
            }
            return (albedo, normal);
        }

        public static Texture2D Save(string name, int width, int height, Color[] pixels, Kind kind,
            TextureWrapMode wrapU = TextureWrapMode.Repeat, TextureWrapMode wrapV = TextureWrapMode.Repeat, int maxSize = 2048)
        {
            Directory.CreateDirectory(Folder);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, kind != Kind.Albedo && kind != Kind.AlphaSprite);
            tex.SetPixels(pixels);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = kind == Kind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind == Kind.Albedo || kind == Kind.AlphaSprite;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = kind == Kind.AlphaSprite;
            importer.mipmapEnabled = true;
            importer.wrapModeU = wrapU;
            importer.wrapModeV = wrapV;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color[] HeightToNormal(float[] h, int w, int hgt, float strength)
        {
            var result = new Color[w * hgt];
            for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                float l = h[y * w + (x - 1 + w) % w], r = h[y * w + (x + 1) % w];
                float d = h[((y - 1 + hgt) % hgt) * w + x], u = h[((y + 1) % hgt) * w + x];
                Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                result[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return result;
        }
    }
}
