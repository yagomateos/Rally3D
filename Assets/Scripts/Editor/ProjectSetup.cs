using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rally.EditorTools
{
    /// <summary>Project level configuration: layers, physics, render pipeline quality and player settings.</summary>
    public static class ProjectSetup
    {
        public const string VehicleLayer = "Vehicle";
        public const int VehicleLayerIndex = 8;

        public static void Apply()
        {
            EnsureLayer(VehicleLayerIndex, VehicleLayer);
            ResourceAssets.CreateLeaderboardConfig();

            PlayerSettings.companyName = "Rally3D";
            PlayerSettings.productName = "Rally 3D"; // several stages now; shown in the browser tab
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false; // ~2.7 MB and ~2 s less before the web game starts

            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;

            var urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            var quality = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            foreach (var asset in new[] { urp, quality })
            {
                if (asset == null) continue;
                asset.shadowDistance = 170f;
                asset.shadowCascadeCount = 4;
                asset.cascade4Split = new Vector3(0.06f, 0.18f, 0.45f);
                asset.msaaSampleCount = 4;
                asset.supportsHDR = true;
                asset.supportsCameraDepthTexture = true;
                EditorUtility.SetDirty(asset);
            }
            QualitySettings.lodBias = 1.5f;
            QualitySettings.vSyncCount = 1;
            AssetDatabase.SaveAssets();
        }

        private static void EnsureLayer(int index, string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var element = layers.GetArrayElementAtIndex(index);
            if (element.stringValue == name) return;
            element.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
