using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rally.Tests
{
    /// <summary>
    /// QA-05: static checks that the stage can go into a player build. They cannot replace an actual
    /// build (see the explicit test below), but catch the usual causes of pink or missing content.
    /// </summary>
    public class BuildReadinessTests
    {
        private const string ScenePath = "Assets/Scenes/Stage01.unity";

        [Test]
        public void QA05_Stage01_IsReadyForAPlayerBuild()
        {
            var problems = new System.Collections.Generic.List<string>();

            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                problems.Add("Stage01 is not an enabled scene in Build Settings.");

            string[] deps = AssetDatabase.GetDependencies(ScenePath, true);
            // Only project content: packages reference editor-only metadata/icons that Unity strips itself.
            foreach (string dep in deps.Where(d => d.StartsWith("Assets/")))
            {
                if (dep.Contains("/Editor/") && !dep.EndsWith(".cs"))
                    problems.Add($"Scene depends on an editor-only asset: {dep}");

                if (dep.EndsWith(".cs"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(dep);
                    var type = script != null ? script.GetClass() : null;
                    if (type != null && type.Assembly.GetName().Name.Contains("Editor"))
                        problems.Add($"Scene uses a component from an editor assembly: {type.FullName}");
                }

                if (dep.EndsWith(".mat"))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(dep);
                    if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                        problems.Add($"Material with a missing or broken shader: {dep}");
                }
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>Real build smoke test. Explicit: only runs when requested, it takes several minutes.</summary>
        [Test, Explicit("Makes a full macOS player build (several minutes).")]
        public void QA05_PlayerBuild_Succeeds()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Test/Rally3D.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            Assert.AreEqual(UnityEditor.Build.Reporting.BuildResult.Succeeded, report.summary.result,
                $"Build failed with {report.summary.totalErrors} errors.");
        }
    }
}
