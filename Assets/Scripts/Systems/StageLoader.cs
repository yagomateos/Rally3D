using System.Collections;
using Rally.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rally.Systems
{
    /// <summary>
    /// Loads a stage scene. Stage 1 (it holds the main menu) is part of the game download; the others are Addressables
    /// scenes, downloaded only when the player picks them (web: from the build's StreamingAssets, then cached by the
    /// browser). While one downloads, a CARGANDO TRAMO screen shows the progress.
    /// </summary>
    public static class StageLoader
    {
        public static bool Loading { get; private set; }

        public static bool InBuild(string scene) =>
            SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{scene}.unity") >= 0;

        /// <summary>Starts loading <paramref name="scene"/> (replacing the current one).</summary>
        public static void Load(string scene)
        {
            if (Loading) return;
            Runner.Instance.StartCoroutine(LoadRoutine(scene));
        }

        /// <summary>Loads <paramref name="scene"/>; yield it to wait until the new scene is active.</summary>
        public static IEnumerator LoadRoutine(string scene)
        {
            Loading = true;
            try
            {
                if (InBuild(scene))
                {
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    yield break;
                }
                var overlay = LoadingOverlay.Show(scene);
                var handle = Addressables.LoadSceneAsync(scene, LoadSceneMode.Single);
                while (!handle.IsDone)
                {
                    overlay.SetProgress(handle.GetDownloadStatus().Percent * 0.8f + handle.PercentComplete * 0.2f);
                    yield return null;
                }
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[StageLoader] Could not load {scene}: {handle.OperationException}");
                    overlay.SetError();
                    yield return new WaitForSecondsRealtime(2.5f);
                    overlay.Close();
                    yield return SceneManager.LoadSceneAsync(StageCatalog.Stages[0].scene, LoadSceneMode.Single);
                    yield break;
                }
                overlay.Close();
            }
            finally { Loading = false; }
        }

        private class Runner : MonoBehaviour
        {
            private static Runner instance;
            public static Runner Instance
            {
                get
                {
                    if (instance == null)
                    {
                        var go = new GameObject("StageLoader") { hideFlags = HideFlags.HideInHierarchy };
                        Object.DontDestroyOnLoad(go);
                        instance = go.AddComponent<Runner>();
                    }
                    return instance;
                }
            }
        }

        /// <summary>Full-screen "CARGANDO TRAMO" with a progress bar, kept alive across the scene change.</summary>
        private class LoadingOverlay : MonoBehaviour
        {
            private RectTransform fill;
            private Text label;

            public static LoadingOverlay Show(string scene)
            {
                var go = new GameObject("LoadingOverlay");
                Object.DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                go.AddComponent<GraphicRaycaster>();

                var root = UIFactory.Stretch("Root", go.transform);
                var bg = root.gameObject.AddComponent<Image>();
                bg.color = new Color(0.02f, 0.02f, 0.03f, 1f);
                var c = new Vector2(0.5f, 0.5f);
                string name = scene;
                foreach (var s in StageCatalog.Stages) if (s.scene == scene) name = $"{s.number}  ·  {s.name}";
                UIFactory.Label("Title", root, c, new Vector2(0f, 60f), new Vector2(1600f, 60f), name, 44, TextAnchor.MiddleCenter, UIFactory.TextMain);
                var overlay = go.AddComponent<LoadingOverlay>();
                overlay.label = UIFactory.Label("Status", root, c, new Vector2(0f, -10f), new Vector2(1600f, 40f), "CARGANDO TRAMO…", 28, TextAnchor.MiddleCenter, UIFactory.TextDim);
                var bar = UIFactory.Panel("Bar", root, c, new Vector2(0f, -70f), new Vector2(700f, 10f), new Color(1f, 1f, 1f, 0.15f));
                overlay.fill = UIFactory.Panel("Fill", bar.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 10f), UIFactory.Accent).rectTransform;
                return overlay;
            }

            public void SetProgress(float p)
            {
                p = Mathf.Clamp01(p);
                fill.sizeDelta = new Vector2(700f * p, 10f);
                label.text = $"CARGANDO TRAMO…  {Mathf.RoundToInt(p * 100f)} %";
            }

            public void SetError() => label.text = "NO SE PUDO CARGAR EL TRAMO. VOLVIENDO AL MENÚ…";

            public void Close() => Destroy(gameObject);
        }
    }
}
