using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Short engine loops in Resources/Audio are imported uncompressed (PCM): compressed formats (AAC on the
    /// web, Vorbis elsewhere) add encoder padding at the start, which is heard as a gap every time a 0.6 s loop
    /// repeats. They are tiny, so PCM costs almost nothing.
    /// </summary>
    public class EngineAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            foreach (var platform in new[] { "WebGL", "Standalone", "Android", "iPhone" })
                importer.ClearSampleSettingOverride(platform);
        }
    }
}
