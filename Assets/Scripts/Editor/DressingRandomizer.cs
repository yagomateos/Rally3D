using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Breaks up repetition in the stage dressing: random scale and Y rotation for rocks, and colour
    /// variation for rocks and terrain trees through a few tinted material variants. Variants (rather
    /// than MaterialPropertyBlocks) keep every renderer compatible with the SRP Batcher.
    /// </summary>
    public static class DressingRandomizer
    {
        private const string TreeVariantFolder = "Assets/Prefabs/Environment/Variants";
        private static string RockMaterialPath => $"Assets/Materials/Nature/{StageTheme.Name("Rock")}.mat";
        private const int VariantCount = 4;
        private const int Seed = 4242;
        private static readonly Vector2 ScaleRange = new Vector2(0.85f, 1.2f);

        [MenuItem("Rally/Randomize Stage Dressing")]
        public static void RandomizeOpenScene()
        {
            Apply();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Randomizes the open scene without saving it (also used by the stage builder).</summary>
        public static void Apply()
        {
            var rng = new System.Random(Seed);
            int rocks = RandomizeRocks(rng);
            int trees = RandomizeTrees(rng);
            Debug.Log($"[Rally] Randomized {rocks} rocks and {trees} trees.");
        }

        // ------------------------------------------------------------------ rocks

        private static int RandomizeRocks(System.Random rng)
        {
            var root = GameObject.Find("Stage Dressing");
            var baseRock = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
            if (root == null || baseRock == null) return 0;

            Material[] variants = CreateVariants(baseRock, rng, 0.12f, 0.08f);
            int count = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                // Only untouched rocks, so running the tool twice does not compound the scale.
                if (renderer.sharedMaterial != baseRock) continue;
                Transform t = renderer.transform;
                t.localScale *= Range(rng, ScaleRange.x, ScaleRange.y);
                t.rotation = Quaternion.AngleAxis(Range(rng, 0f, 360f), Vector3.up) * t.rotation;
                renderer.sharedMaterial = variants[rng.Next(variants.Length)];
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ terrain trees

        private static int RandomizeTrees(System.Random rng)
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return 0;
            TerrainData data = terrain.terrainData;
            var prototypes = new List<TreePrototype>(data.treePrototypes);
            foreach (var p in prototypes)
                if (p.prefab != null && p.prefab.name.Contains("_V")) return 0; // already randomized

            // Every original prototype gets VariantCount tinted copies; instances pick one at random.
            int originalCount = prototypes.Count;
            var variantIndices = new List<int>[originalCount];
            for (int i = 0; i < originalCount; i++)
            {
                variantIndices[i] = new List<int> { i };
                for (int v = 1; v < VariantCount; v++)
                {
                    GameObject prefab = CreateTreeVariant(prototypes[i].prefab, v, rng);
                    prototypes.Add(new TreePrototype { prefab = prefab });
                    variantIndices[i].Add(prototypes.Count - 1);
                }
            }
            data.treePrototypes = prototypes.ToArray();

            TreeInstance[] instances = data.treeInstances;
            for (int i = 0; i < instances.Length; i++)
            {
                var options = variantIndices[instances[i].prototypeIndex];
                float scale = Range(rng, ScaleRange.x, ScaleRange.y);
                instances[i].prototypeIndex = options[rng.Next(options.Count)];
                instances[i].heightScale = Mathf.Clamp(instances[i].heightScale * scale, 0.6f, 1.5f);
                instances[i].widthScale = Mathf.Clamp(instances[i].widthScale * scale, 0.6f, 1.5f);
                instances[i].rotation = Range(rng, 0f, Mathf.PI * 2f);
            }
            data.SetTreeInstances(instances, true);
            EditorUtility.SetDirty(data);
            return instances.Length;
        }

        private static GameObject CreateTreeVariant(GameObject source, int variant, System.Random rng)
        {
            Directory.CreateDirectory(TreeVariantFolder);
            string path = $"{TreeVariantFolder}/{source.name}_V{variant}.prefab";
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copy.name = $"{source.name}_V{variant}";

            // Foliage varies more than bark; the same tint is shared by LOD0 and LOD1.
            var tinted = new Dictionary<Material, Material>();
            foreach (var renderer in copy.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = renderer.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (!tinted.TryGetValue(mats[m], out var variantMat))
                    {
                        bool foliage = mats[m].name.Contains("Needles") || mats[m].name.Contains("Leaves");
                        variantMat = CreateVariant(mats[m], $"{copy.name}_{mats[m].name}", rng,
                            foliage ? 0.14f : 0.08f, foliage ? 0.1f : 0.04f);
                        tinted[mats[m]] = variantMat;
                    }
                    mats[m] = variantMat;
                }
                renderer.sharedMaterials = mats;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
            return prefab;
        }

        // ------------------------------------------------------------------ materials

        private static Material[] CreateVariants(Material source, System.Random rng, float valueRange, float hueWarmth)
        {
            var result = new Material[VariantCount];
            for (int v = 0; v < VariantCount; v++)
                result[v] = CreateVariant(source, $"{source.name}_V{v}", rng, valueRange, hueWarmth);
            return result;
        }

        /// <summary>Copy of the material with a slight brightness / warmth tint on _BaseColor.</summary>
        private static Material CreateVariant(Material source, string name, System.Random rng, float valueRange, float hueWarmth)
        {
            var mat = new Material(source) { name = name };
            Color c = source.GetColor("_BaseColor");
            float value = 1f + Range(rng, -valueRange, valueRange);
            float warm = Range(rng, -hueWarmth, hueWarmth);
            mat.SetColor("_BaseColor", new Color(c.r * value * (1f + warm), c.g * value, c.b * value * (1f - warm), c.a));
            return MaterialFactory.Save(mat, "Nature/Variants");
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
