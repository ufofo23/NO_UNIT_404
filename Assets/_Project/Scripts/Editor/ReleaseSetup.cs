using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using NO404.Core;

namespace NO404.EditorTools
{
    /// <summary>
    /// Player settings that have to be right in a shipped build, applied from code (GDD 27.1).
    ///
    /// These live in ProjectSettings.asset, which is a file nobody reads and everybody
    /// forgets. Every value here was wrong in the greybox: no icon, the Unity splash still on,
    /// a fixed non-resizable window, and a 0.1.0 version string that would have gone up on
    /// Steam. Setting them from a menu item means the state is reproducible and the reasons
    /// are written down next to the values.
    ///
    /// Run: Tools > NO404 > Release > Apply Player Settings
    /// CI:  -executeMethod NO404.EditorTools.ReleaseSetup.ApplyPlayerSettings
    /// </summary>
    public static class ReleaseSetup
    {
        /// <summary>
        /// The shipping version. Not 1.0.0: the systems and the content are complete but the
        /// art and audio production passes are not, so this is the build that goes to
        /// playtest and to the store page, not the one that goes on sale.
        /// </summary>
        public const string Version = "0.9.0";

        const string IconFolder = "Assets/_Project/Art/Icon/";

        [MenuItem("Tools/NO404/Release/Apply Player Settings", priority = 150)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "ProjectCaretaker";
            PlayerSettings.productName = "NO UNIT 404";
            PlayerSettings.bundleVersion = Version;

            // A horror game gets alt-tabbed out of. Borderless is the mode that survives it,
            // and a window the player cannot resize is a bug report waiting to happen
            // (GDD 16.16 / 28.6).
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.visibleInBackground = false;

            // One instance. Two copies of a game that writes a save file atomically is the
            // one way to still lose a save.
            PlayerSettings.forceSingleInstance = true;

            // Keep the player log: it is the only diagnostic a Steam user can send back.
            PlayerSettings.usePlayerLog = true;

            // Unity 6 made the "Made with Unity" splash optional for Personal licences as
            // part of the Runtime Fee cancellation (unity.com/blog/unity-is-canceling-the-
            // runtime-fee). On 2022 and earlier this toggle is quietly reverted at build time
            // for Personal, which is why it is worth confirming on a real player once.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            ApplyScriptingBackend();
            ApplyIcons();

            AssetDatabase.SaveAssets();
            Debug.Log("[NO404] player settings applied; version " + Version);
        }

        /// <summary>
        /// IL2CPP for the shipped player (GDD 20.20).
        ///
        /// Mono ships the game as IL in a DLL anyone can open in a decompiler, and it is the
        /// slower of the two at runtime. IL2CPP transpiles to C++ and compiles it, so the
        /// build needs the "Windows Build Support (IL2CPP)" module and an MSVC toolchain.
        /// Without the module the switch produces a build error that does not say what is
        /// wrong, so this refuses to make the change rather than leaving that trap set.
        /// </summary>
        static void ApplyScriptingBackend()
        {
            var target = NamedBuildTarget.Standalone;

            if (!Il2CppModuleInstalled())
            {
                Debug.LogWarning("[NO404] leaving the standalone backend on Mono: the " +
                                 "'Windows Build Support (IL2CPP)' module is not installed. " +
                                 "Install it from the Unity Hub and run this again.");
                return;
            }

            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);

            // Release, not Master. Master spends a great deal of compile time for a few percent
            // that a game bounded by a 12ms main thread budget will not notice.
            PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(target, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSpeed);

            // Low, not High. The save system round-trips through JsonUtility and the content
            // database is resolved by name; aggressive stripping removes exactly the types
            // that only reflection ever mentions, and the failure shows up as an empty save
            // rather than a compile error.
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);

            Debug.Log("[NO404] standalone backend set to IL2CPP (Release, stripping Low)");
        }

        /// <summary>
        /// True when this editor can actually build an IL2CPP player. Unity offers no API for
        /// this, so it looks for the player variation the module installs.
        /// </summary>
        public static bool Il2CppModuleInstalled()
        {
            var variations = Path.Combine(
                Path.GetDirectoryName(EditorApplication.applicationPath) ?? string.Empty,
                "Data/PlaybackEngines/windowsstandalonesupport/Variations");

            return Directory.Exists(Path.Combine(variations, "win64_player_nondevelopment_il2cpp"));
        }

        /// <summary>
        /// Hands Unity the icon ladder from Tools/GenerateIcon.py. Unity asks for a specific
        /// set of sizes and scales whatever it is given, so every slot is filled with the
        /// nearest rendered size rather than one 1024 that gets shrunk into mush at 16 px -
        /// the small variants are drawn differently on purpose.
        /// </summary>
        static void ApplyIcons()
        {
            var target = NamedBuildTarget.Standalone;
            var sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);

            if (sizes == null || sizes.Length == 0)
            {
                Debug.LogWarning("[NO404] this editor reports no standalone icon slots");
                return;
            }

            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) icons[i] = NearestIcon(sizes[i]);

            PlayerSettings.SetIcons(target, icons, IconKind.Any);
            Debug.Log("[NO404] icons set for " + sizes.Length + " slots");
        }

        static Texture2D NearestIcon(int size)
        {
            Texture2D best = null;
            int bestDistance = int.MaxValue;

            foreach (var path in Directory.GetFiles(IconFolder, "AppIcon_*.png"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                int parsed;
                if (!int.TryParse(name.Substring("AppIcon_".Length), out parsed)) continue;

                // Prefer the exact size, then the next one up - a downscale is always cleaner
                // than an upscale.
                int distance = parsed >= size ? parsed - size : (size - parsed) * 4;
                if (distance >= bestDistance) continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
                if (texture == null) continue;

                bestDistance = distance;
                best = texture;
            }

            return best;
        }

        // =====================================================================
        // Readiness report
        // =====================================================================

        /// <summary>
        /// What is still wrong with this build, as a list. Deliberately not a pass/fail gate:
        /// the project ships with known gaps and the useful thing is to see all of them at
        /// once rather than to be stopped at the first.
        ///
        /// CI: -executeMethod NO404.EditorTools.ReleaseSetup.Report
        /// </summary>
        [MenuItem("Tools/NO404/Release/Release Readiness Report", priority = 151)]
        public static void Report()
        {
            List<string> blocking, warnings;
            Collect(out blocking, out warnings);

            var sb = new StringBuilder();
            sb.AppendLine("[NO404] release readiness — " + PlayerSettings.productName + " " +
                          PlayerSettings.bundleVersion);
            sb.AppendLine();

            Append(sb, "BLOCKING", blocking);
            Append(sb, "WARNING", warnings);

            if (blocking.Count == 0 && warnings.Count == 0) sb.AppendLine("  nothing outstanding");

            if (blocking.Count > 0) Debug.LogError(sb.ToString());
            else if (warnings.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        /// <summary>
        /// The same report, but it ends the process with the number of blocking items as the
        /// exit code. Report() is deliberately not a gate so a person can see everything at
        /// once; a build script needs the opposite, and calling Report from CI and reading the
        /// log for the word BLOCKING is how a release goes out with a placeholder app id.
        ///
        /// CI: -batchmode -quit -executeMethod NO404.EditorTools.ReleaseSetup.ReportForCi
        /// </summary>
        public static void ReportForCi()
        {
            List<string> blocking, warnings;
            Collect(out blocking, out warnings);

            Report();
            EditorApplication.Exit(blocking.Count == 0 ? 0 : 1);
        }

        static void Collect(out List<string> blocking, out List<string> warnings)
        {
            blocking = new List<string>();
            warnings = new List<string>();

            CheckPlayerSettings(blocking, warnings);
            CheckContentPipeline(blocking, warnings);
            CheckBuildTooling(blocking, warnings);
            CheckStorefront(blocking, warnings);
        }

        static void Append(StringBuilder sb, string heading, List<string> lines)
        {
            if (lines.Count == 0) return;

            sb.AppendLine(heading + " (" + lines.Count + ")");
            foreach (var line in lines) sb.AppendLine("  - " + line);
            sb.AppendLine();
        }

        static void CheckPlayerSettings(List<string> blocking, List<string> warnings)
        {
            if (PlayerSettings.bundleVersion == "0.1.0")
                blocking.Add("bundleVersion is still the scaffold default");

            if (!PlayerSettings.resizableWindow)
                warnings.Add("the window is not resizable (GDD 28.6 tests five resolutions)");

            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
            bool hasIcon = false;
            if (icons != null)
                foreach (var icon in icons) if (icon != null) { hasIcon = true; break; }

            if (!hasIcon) blocking.Add("no application icon is set");

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.IL2CPP)
            {
                blocking.Add(Il2CppModuleInstalled()
                    ? "the standalone backend is Mono; run Apply Player Settings to switch to IL2CPP"
                    : "the standalone backend is Mono and the 'Windows Build Support (IL2CPP)' " +
                      "module is not installed - add it from the Unity Hub");
            }

            if (PlayerSettings.SplashScreen.show)
                warnings.Add("the Unity splash screen is enabled");

            if (QualitySettings.names.Length < 3)
                warnings.Add("only " + QualitySettings.names.Length + " quality levels exist; " +
                             "GDD 16.16 offers a preset row and needs one URP asset per level");
        }

        static void CheckContentPipeline(List<string> blocking, List<string> warnings)
        {
            if (!File.Exists("Assets/_Project/Resources/NO404/Fonts/Pretendard-Regular.otf"))
                blocking.Add("the embedded UI font is missing; every Korean glyph will be a box");

            if (!File.Exists("Assets/_Project/Resources/NO404/Fonts/Pretendard-OFL.txt"))
                blocking.Add("the font licence is missing and SIL OFL 1.1 requires it to ship");

            var audio = "Assets/_Project/Resources/NO404/Audio";
            int clips = Directory.Exists(audio) ? Directory.GetFiles(audio, "*.wav").Length : 0;
            if (clips == 0) blocking.Add("no audio clips; run python Tools/GenerateAudio.py");

            if (!Directory.Exists("Assets/_Project/Prefabs") ||
                Directory.GetFiles("Assets/_Project/Prefabs", "*.prefab", SearchOption.AllDirectories).Length == 0)
                warnings.Add("no prefabs: the world is still primitives from WorldBuilder (art pass)");
        }

        /// <summary>
        /// The half of shipping that no amount of green tests covers: the Steamworks partner
        /// site, the store art and the rating. Each of these fails silently or fails late, and
        /// two of them have a lead time measured in weeks, so they are listed here rather than
        /// left in a document nobody opens on build day.
        /// </summary>
        static void CheckStorefront(List<string> blocking, List<string> warnings)
        {
            // 480 accepts every achievement call and records none. It cannot be caught by
            // playing the game, which is exactly why it is checked here.
            if (SteamAppInfo.IsPlaceholder)
                blocking.Add("Steam app id is still " + SteamAppInfo.PlaceholderAppId +
                             " (Valve's Spacewar test app): achievements will silently go " +
                             "nowhere. Set NO404.Core.SteamAppInfo.AppId and steam_appid.txt.");

            // steam_appid.txt is editor-and-dev only and BuildScript.Sanitise strips it from
            // release output, but while it disagrees with the code the editor talks to one app
            // and the shipped player to another.
            const string appIdFile = "steam_appid.txt";
            if (!File.Exists(appIdFile))
            {
                warnings.Add(appIdFile + " is missing; Steam will not initialise in the editor");
            }
            else
            {
                var text = File.ReadAllText(appIdFile).Trim();
                uint fileId;
                if (!uint.TryParse(text, out fileId))
                    blocking.Add(appIdFile + " does not contain a number (\"" + text + "\")");
                else if (fileId != SteamAppInfo.AppId)
                    blocking.Add(appIdFile + " says " + fileId + " but SteamAppInfo.AppId is " +
                                 SteamAppInfo.AppId + "; the editor and the player would use " +
                                 "different apps");
            }

            // Two icons per achievement, unlocked and locked, uploaded to the partner site.
            // Nothing in the game reads them, so only this check can notice they are absent.
            int wanted = AchievementIds.All.Length * 2;
            const string iconDir = "Assets/_Project/Art/Achievements";
            int found = Directory.Exists(iconDir)
                      ? Directory.GetFiles(iconDir, "*.png", SearchOption.AllDirectories).Length
                      : 0;

            if (found < wanted)
                warnings.Add("achievement icons: " + found + " of " + wanted + " (" +
                             AchievementIds.All.Length + " achievements x unlocked/locked, 64x64 PNG) " +
                             "in " + iconDir + " - art pass, see Docs/Release/ART_BRIEF.md");

            const string capsuleDir = "Assets/_Project/Art/Store";
            int capsules = Directory.Exists(capsuleDir)
                         ? Directory.GetFiles(capsuleDir, "*.png", SearchOption.AllDirectories).Length
                         : 0;

            if (capsules < 6)
                warnings.Add("store capsules: " + capsules + " of 6 in " + capsuleDir +
                             " - the store page cannot go live without them (Docs/Release/ART_BRIEF.md)");
        }

        static void CheckBuildTooling(List<string> blocking, List<string> warnings)
        {
            int enabled = 0;
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) enabled++;

            if (enabled == 0) blocking.Add("no scenes are enabled in Build Settings");

            if (EditorUserBuildSettings.development)
                warnings.Add("Build Settings still has Development Build ticked");
        }
    }
}
