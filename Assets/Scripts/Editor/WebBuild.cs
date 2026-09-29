using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Command-line Web build for itch.io: builds the enabled scenes to the folder given by
    /// -buildOutput and exits with a non-zero code on failure.
    /// </summary>
    public static class WebBuild
    {
        public static void Build()
        {
            string output = GetArg("-buildOutput");
            if (string.IsNullOrEmpty(output)) throw new ArgumentException("Missing -buildOutput <folder>.");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[WebBuild] {summary.result}: {summary.totalSize / (1024f * 1024f):0.0} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalTime.TotalSeconds:0}s -> {output}");
            if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
