using Rally.Track;
using Rally.Track.Generation;
using Rally.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Places <see cref="FogZone"/> triggers over the forest stretches of the stage and makes sure the
    /// Weather object has an <see cref="AtmosphereController"/>.
    /// </summary>
    public static class FogZoneBuilder
    {
        private const string RootName = "FogZones";
        private const string StagePath = "Assets/Settings/Rally/Stage01.asset";
        private const int ChunkSamples = 20;        // ~40 m per box, short enough to follow corners
        private const int MinRunSamples = 25;       // ignore forest stretches shorter than ~50 m
        private const float SideMargin = 15f;
        private const float Height = 24f;
        private const float Overlap = 6f;

        [MenuItem("Rally/Add Forest Fog Zones")]
        public static void AddToOpenScene()
        {
            var path = Object.FindFirstObjectByType<TrackPath>();
            var def = AssetDatabase.LoadAssetAtPath<StageDefinition>(StagePath);
            if (path == null || def == null)
            {
                Debug.LogError("[Rally] Fog zones need a TrackPath in the open scene and " + StagePath);
                return;
            }
            int count = Build(def, path);
            EnsureController();
            EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
            EditorSceneManager.SaveScene(path.gameObject.scene);
            Debug.Log($"[Rally] Added {count} forest fog zones.");
        }

        /// <summary>Rebuilds the zones under the track object. Returns how many were created.</summary>
        public static int Build(StageDefinition def, TrackPath path)
        {
            var old = path.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(path.transform, false);

            // The route is deterministic, so it gives us the roadside style of every TrackPath sample.
            var route = StageRoute.Build(def);
            int samples = Mathf.Min(route.Count, path.Count);
            int created = 0;
            int i = 0;
            while (i < samples)
            {
                if (route.Roadside[i] != RoadsideStyle.Forest) { i++; continue; }
                int start = i;
                while (i < samples && route.Roadside[i] == RoadsideStyle.Forest) i++;
                if (i - start < MinRunSamples) continue;

                for (int a = start; a < i - 1; a += ChunkSamples)
                {
                    int b = Mathf.Min(a + ChunkSamples, i - 1);
                    CreateZone(root, path, route, a, b, created++);
                }
            }
            return created;
        }

        private static void CreateZone(Transform root, TrackPath path, StageRoute route, int a, int b, int index)
        {
            Vector3 pa = path.GetPoint(a), pb = path.GetPoint(b);
            Vector3 along = pb - pa;
            along.y = 0f;
            float maxWidth = 0f;
            for (int k = a; k <= b; k++) maxWidth = Mathf.Max(maxWidth, route.Widths[k]);

            var go = new GameObject($"FogZone_{index:00}") { layer = 2 }; // Ignore Raycast
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation((pa + pb) * 0.5f + Vector3.up * (Height * 0.3f), Quaternion.LookRotation(along));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(maxWidth + SideMargin * 2f, Height, along.magnitude + Overlap);
            go.AddComponent<FogZone>();
        }

        public static void EnsureController()
        {
            var weather = Object.FindFirstObjectByType<WeatherEffects>();
            if (weather == null)
            {
                Debug.LogWarning("[Rally] No Weather object found; AtmosphereController not added.");
                return;
            }
            if (weather.GetComponent<AtmosphereController>() == null)
                weather.gameObject.AddComponent<AtmosphereController>();
        }
    }
}
