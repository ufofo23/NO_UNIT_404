using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NO404.EditorTools
{
    /// <summary>
    /// Windows build automation (GDD 37 Day 1 item 7). Also callable from CI:
    /// Unity.exe -batchmode -quit -executeMethod NO404.EditorTools.BuildScript.BuildDevelopment
    ///
    /// A release build is not a development build with a flag off. It stamps the version,
    /// removes the folders Unity itself marks as "DoNotShip", refuses to leave the Steam
    /// development app id beside the player, and carries the licence notices the game is
    /// required to distribute. Those steps were the difference between the folder this
    /// produces and something that could go up on Steam.
    /// </summary>
    public static class BuildScript
    {
        const string OutputRoot = "Builds";
        const string ExeName = "NO_UNIT_404.exe";

        /// <summary>
        /// Unity writes these next to the player and names them for what to do with them.
        /// They are debug symbols; shipping them costs tens of megabytes and hands out the
        /// build's internals for nothing.
        /// </summary>
        static readonly string[] DoNotShipSuffixes = { "_BurstDebugInformation_DoNotShip", "_BackUpThisFolder_ButDontShipItWithYourGame" };

        [MenuItem("Tools/NO404/Build/Windows Development Build", priority = 100)]
        public static void BuildDevelopment()
        {
            Build(BuildOptions.Development | BuildOptions.AllowDebugging, "Development");
        }

        [MenuItem("Tools/NO404/Build/Windows Release Build", priority = 101)]
        public static void BuildRelease()
        {
            // Player settings are part of what a release build is, so they are applied rather
            // than assumed: a release produced from a machine where somebody had toggled the
            // splash back on is not the build anyone meant to make.
            ReleaseSetup.ApplyPlayerSettings();
            Build(BuildOptions.None, "Release");
        }

        /// <summary>
        /// Which scripting backend each kind of build uses.
        ///
        /// Development builds are Mono, and this is not a preference - IL2CPP hands the whole
        /// player to the Visual Studio C++ compiler, and that step is what has been failing on
        /// this machine: bee_backend dies with `Pipe is broken` in the middle of
        /// "Compiling Cpp for GameAssembly", leaving a folder that contains an .exe which
        /// starts and never writes a log line. Mono skips C++ entirely, builds in a fraction
        /// of the time, and is the right trade for something whose only job is to be run by a
        /// playtester this evening.
        ///
        /// Release stays on IL2CPP because that is what ships. If a release build starts
        /// failing the same way, the C++ toolchain is the thing to look at, not Unity.
        /// </summary>
        static void ApplyScriptingBackend(bool release)
        {
            var target = NamedBuildTarget.Standalone;
            var backend = release ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;

            if (PlayerSettings.GetScriptingBackend(target) == backend) return;

            PlayerSettings.SetScriptingBackend(target, backend);
            Debug.Log("[NO404] scripting backend set to " + backend + " for this " +
                      (release ? "release" : "development") + " build");
        }

        static void Build(BuildOptions options, string label)
        {
            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[NO404] No scenes in Build Settings. " +
                               "Run Tools > NO404 > Setup > Create Entry Scene And Build Settings first.");
                return;
            }

            bool release = (options & BuildOptions.Development) == 0;
            ApplyScriptingBackend(release);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var directory = Path.Combine(OutputRoot, label + "_" + PlayerSettings.bundleVersion + "_" + stamp);
            Directory.CreateDirectory(directory);

            var player = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(directory, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = options
            };

            var report = BuildPipeline.BuildPlayer(player);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[NO404] " + label + " build " + summary.result +
                               " with " + summary.totalErrors + " errors");
                return;
            }

            if (release) Sanitise(directory);
            CopyNotices(directory);

            long bytes = DirectorySize(directory);
            Debug.Log("[NO404] " + label + " build succeeded: " + directory +
                      " (" + (bytes / 1048576) + " MB on disk, " +
                      summary.totalTime.TotalSeconds.ToString("0") + "s)");

            if (release) Debug.Log("[NO404] run the readiness report before uploading: " +
                                   "Tools > NO404 > Release > Release Readiness Report");
        }

        /// <summary>
        /// Removes what must not ship. Two of these are actively harmful rather than merely
        /// wasteful: steam_appid.txt lets the player launch outside Steam as whatever app id
        /// it names, and the Burst debug folder is Unity telling you in the folder name.
        /// </summary>
        static void Sanitise(string directory)
        {
            foreach (var suffix in DoNotShipSuffixes)
            {
                foreach (var found in Directory.GetDirectories(directory, "*" + suffix + "*"))
                {
                    Directory.Delete(found, true);
                    Debug.Log("[NO404] removed " + Path.GetFileName(found));
                }
            }

            // Steamworks.NET's own steam_appid.txt travels with the plugin; the project root
            // copy is for the editor. Neither belongs beside a shipped player.
            var appId = Path.Combine(directory, "steam_appid.txt");
            if (File.Exists(appId))
            {
                File.Delete(appId);
                Debug.Log("[NO404] removed steam_appid.txt from the build");
            }
        }

        /// <summary>
        /// The credits screen reads its notices out of Resources, so the build already carries
        /// them. This puts a plain copy beside the exe as well, which is what a store review
        /// or a distribution check actually looks for.
        /// </summary>
        static void CopyNotices(string directory)
        {
            var notices = Path.Combine(directory, "Licenses");
            Directory.CreateDirectory(notices);

            var sources = new Dictionary<string, string>
            {
                { "Assets/_Project/Resources/NO404/Fonts/Pretendard-OFL.txt", "Pretendard-OFL.txt" },
                { "Assets/com.rlabrecque.steamworks.net/LICENSE.md", "Steamworks.NET-LICENSE.md" }
            };

            foreach (var pair in sources)
            {
                if (!File.Exists(pair.Key))
                {
                    Debug.LogWarning("[NO404] licence notice missing: " + pair.Key);
                    continue;
                }

                File.Copy(pair.Key, Path.Combine(notices, pair.Value), true);
            }
        }

        static long DirectorySize(string directory)
        {
            long total = 0;
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                total += new FileInfo(file).Length;

            return total;
        }

        static string[] EnabledScenes()
        {
            var list = new List<string>();
            var scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
                if (scenes[i].enabled) list.Add(scenes[i].path);

            return list.ToArray();
        }
    }
}
