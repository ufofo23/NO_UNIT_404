using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NO404.EditorTools
{
    /// <summary>
    /// Builds a player and prints what actually went wrong.
    ///
    /// Unity's own failure line is "Internal build system error. Read the full binlog without
    /// getting a BuildFinishedMessage", which tells you nothing and points at a binlog nobody
    /// can read. The real messages are sitting in the BuildReport the whole time - one per
    /// step, with severity - and printing them turns an unactionable failure into a normal bug.
    ///
    /// Also builds to a deliberately short output path. This project lives at
    /// "D:\game_dev\NO UNIT 404\" and the product name has spaces in it, so Burst's debug
    /// directory ends up nested deep enough that MAX_PATH is a live suspect on Windows.
    /// Ruling that in or out costs one build, and it is the cheapest hypothesis to test.
    /// </summary>
    public static class BuildDiagnostics
    {
        const string ShortRoot = @"D:\n404build";

        [MenuItem("Tools/NO404/Build/Diagnose Windows Build", priority = 110)]
        public static void Diagnose()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);

            if (scenes.Count == 0)
            {
                Debug.LogError("[NO404-DIAG] no scenes in build settings");
                return;
            }

            Directory.CreateDirectory(ShortRoot);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path.Combine(ShortRoot, "n404.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development
            };

            Debug.Log("[NO404-DIAG] building to " + options.locationPathName);

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception e)
            {
                Debug.LogError("[NO404-DIAG] BuildPlayer threw: " + e);
                return;
            }

            Report(report);
        }

        static void Report(BuildReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[NO404-DIAG] result " + report.summary.result +
                          ", errors " + report.summary.totalErrors +
                          ", warnings " + report.summary.totalWarnings +
                          ", " + report.summary.totalTime.TotalSeconds.ToString("0") + "s");

            foreach (var step in report.steps)
            {
                bool interesting = false;
                foreach (var message in step.messages)
                {
                    if (message.type != LogType.Error && message.type != LogType.Exception &&
                        message.type != LogType.Assert) continue;

                    if (!interesting)
                    {
                        sb.AppendLine("---- step: " + step.name);
                        interesting = true;
                    }
                    sb.AppendLine("     [" + message.type + "] " + message.content);
                }
            }

            // Warnings only when nothing errored, so a failure's own noise does not bury it.
            if (report.summary.totalErrors == 0)
            {
                foreach (var step in report.steps)
                    foreach (var message in step.messages)
                        if (message.type == LogType.Warning)
                            sb.AppendLine("     [warn] " + step.name + ": " + message.content);
            }

            if (report.summary.result == BuildResult.Succeeded)
                sb.AppendLine("[NO404-DIAG] the short path worked. Output: " +
                              report.summary.outputPath);

            Debug.Log(sb.ToString());
        }
    }
}
