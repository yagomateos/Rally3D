using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Stage 1 (with the main menu) stays in the game download; stages 2+ become Addressables scenes, one bundle each,
    /// built into the web build's StreamingAssets and downloaded when picked. Keeps the Build Settings and the
    /// Addressables group in step with the scenes in Assets/Scenes.
    /// </summary>
    public static class AddressableStages
    {
        public const string BootScene = "Assets/Scenes/Stage01.unity";
        private const string GroupName = "Stages";

        [MenuItem("Rally/Sync Addressable Stages")]
        public static void Sync()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately; // one download per stage
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;      // WebGL can't decompress LZMA bundles
            bundled.IncludeInBuild = true;

            var stages = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileName(p).StartsWith("Stage"))
                .OrderBy(p => p)
                .ToList();
            foreach (var path in stages)
            {
                if (path == BootScene) continue;
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
                entry.address = System.IO.Path.GetFileNameWithoutExtension(path); // "Stage02"
            }

            // Only stage 1 in the player build; the rest come from the bundles.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Rally] Addressable stages: {string.Join(", ", stages.Where(p => p != BootScene).Select(System.IO.Path.GetFileNameWithoutExtension))}");
        }

        /// <summary>Builds the stage bundles (called by the web build). False on error.</summary>
        public static bool BuildContent()
        {
            Sync();
            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError("[Rally] Addressables build failed: " + result.Error);
                return false;
            }
            Debug.Log($"[Rally] Addressables built in {result.Duration:0.0}s.");
            return true;
        }

        public static void SyncFromCommandLine()
        {
            Sync();
            EditorApplication.Exit(0);
        }
    }
}
