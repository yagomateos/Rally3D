using System.IO;
using Rally.Systems;
using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>Creates Assets/Resources/DifficultyData.asset with the default levels (edit it in the Inspector).</summary>
    public static class DifficultyAsset
    {
        private const string Path = "Assets/Resources/DifficultyData.asset";

        [MenuItem("Rally/Create Difficulty Data")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<DifficultyData>(Path) != null) { Debug.Log("[Rally] " + Path + " already exists."); return; }
            Directory.CreateDirectory("Assets/Resources");
            var data = ScriptableObject.CreateInstance<DifficultyData>();
            data.levels = DifficultyData.Defaults();
            AssetDatabase.CreateAsset(data, Path);
            AssetDatabase.SaveAssets();
            Debug.Log("[Rally] Created " + Path);
        }

        /// <summary>Entry point for -executeMethod in batch mode.</summary>
        public static void CreateFromCommandLine()
        {
            Create();
            EditorApplication.Exit(0);
        }
    }
}
