using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rally.EditorTools
{
    /// <summary>Creates and saves URP materials with correctly configured keywords.</summary>
    public static class MaterialFactory
    {
        public const string Folder = "Assets/Materials";

        private static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        public static Material Save(Material mat, string subFolder = null)
        {
            string folder = subFolder == null ? Folder : $"{Folder}/{subFolder}";
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{mat.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = mat.shader;
                existing.CopyPropertiesFromMaterial(mat);
                existing.shaderKeywords = mat.shaderKeywords;
                existing.renderQueue = mat.renderQueue;
                existing.enableInstancing = mat.enableInstancing;
                existing.doubleSidedGI = mat.doubleSidedGI;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mat);
                return existing;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Opaque URP Lit. Smoothness is read from the albedo alpha when a texture is given.</summary>
        public static Material Opaque(string name, Color color, Texture2D albedo = null, Texture2D normal = null,
            float smoothness = 0.3f, float metallic = 0f, float normalScale = 1f, Vector2? tiling = null, string folder = null)
        {
            var mat = new Material(Lit) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetFloat("_SmoothnessTextureChannel", 1f);
                mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", normalScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (tiling.HasValue) mat.SetTextureScale("_BaseMap", tiling.Value);
            return Save(mat, folder);
        }

        public static Material Paint(string name, Color color, string folder = "Cars")
        {
            var complex = Shader.Find("Universal Render Pipeline/Complex Lit");
            var mat = new Material(complex != null ? complex : Lit) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.72f);
            mat.SetFloat("_Metallic", 0.25f);
            if (complex != null)
            {
                mat.SetFloat("_ClearCoat", 1f);
                mat.SetFloat("_ClearCoatMask", 1f);
                mat.SetFloat("_ClearCoatSmoothness", 0.93f);
                mat.EnableKeyword("_CLEARCOAT");
            }
            return Save(mat, folder);
        }

        public static Material Emissive(string name, Color color, Color emission, string folder = "Cars")
        {
            var mat = new Material(Lit) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.9f);
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return Save(mat, folder);
        }

        public static Material Cutout(string name, Color color, Texture2D albedo, float cutoff = 0.5f, bool doubleSided = true,
            string folder = null, float smoothness = 0.15f)
        {
            var mat = new Material(Lit) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", cutoff);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            if (doubleSided)
            {
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.doubleSidedGI = true;
            }
            return Save(mat, folder);
        }

        /// <summary>Alpha blended lit surface (glass, puddles).</summary>
        public static Material Transparent(string name, Color color, float smoothness, Texture2D albedo = null, string folder = null)
        {
            var mat = new Material(Lit) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return Save(mat, folder);
        }

        /// <summary>URP unlit particle material (alpha blended, soft particles).</summary>
        public static Material Particle(string name, Texture2D texture, Color color, bool soft = true)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (soft)
            {
                mat.SetFloat("_SoftParticlesEnabled", 1f);
                mat.SetFloat("_SoftParticlesNearFadeDistance", 0f);
                mat.SetFloat("_SoftParticlesFarFadeDistance", 1.2f);
                mat.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 1.2f, 0f, 0f));
                mat.EnableKeyword("_SOFTPARTICLES_ON");
            }
            return Save(mat, "VFX");
        }

        public static Material Unlit(string name, Color color, Texture2D texture = null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            mat.SetColor("_BaseColor", color);
            if (texture != null) mat.SetTexture("_BaseMap", texture);
            return Save(mat);
        }
    }
}
