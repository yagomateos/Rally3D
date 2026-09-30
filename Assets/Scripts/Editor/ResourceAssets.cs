using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>Assets the game loads from Resources at runtime. Run from the menu, the command line or any stage build.</summary>
    public static class ResourceAssets
    {
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
    }
}
