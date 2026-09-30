using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rally.EditorTools
{
    /// <summary>
    /// Creates Assets/Resources/GhostCar.mat: the translucent, unlit, light-blue material of the best-run ghost.
    /// It uses the same transparent particle shader as the dust, so the web build already carries its variant.
    /// Run from the menu, from the command line, or by any stage build (ProjectSetup).
    /// </summary>
    public static class GhostAsset
    {
        public const string Path = "Assets/Resources/GhostCar.mat";

        [MenuItem("Rally/Create Ghost Material")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(Path) != null) return;
            Directory.CreateDirectory("Assets/Resources");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "GhostCar" };
            mat.SetColor("_BaseColor", new Color(0.55f, 0.85f, 1f, 0.35f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, Path);
            AssetDatabase.SaveAssets();
            Debug.Log("[Rally] Created " + Path);
        }

        /// <summary>Assets/Resources/LeaderboardConfig.asset with no server (times stay on the device until a URL is set).</summary>
        [MenuItem("Rally/Create Leaderboard Config")]
        public static void CreateLeaderboardConfig()
        {
            const string path = "Assets/Resources/LeaderboardConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<Rally.Systems.LeaderboardConfig>(path) != null) return;
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Rally.Systems.LeaderboardConfig>(), path);
            AssetDatabase.SaveAssets();
        }

        public static void CreateFromCommandLine()
        {
            Create();
            CreateLeaderboardConfig();
            EditorApplication.Exit(0);
        }
    }
}
