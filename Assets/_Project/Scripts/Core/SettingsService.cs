using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NO404.Core
{
    /// <summary>
    /// GDD 16.16. Borderless is its own mode rather than a flag on fullscreen: it is what
    /// players alt-tabbing to a walkthrough actually want, and it is the one a horror game
    /// gets asked for most.
    /// </summary>
    public enum WindowMode
    {
        Fullscreen = 0,
        Borderless = 1,
        Windowed = 2
    }

    [Serializable]
    public sealed class GameSettings
    {
        /// <summary>
        /// 1 -> 2 replaced the fullscreen bool with windowMode, because "not fullscreen"
        /// is two different windows and players ask for both (GDD 16.16).
        /// </summary>
        public const int CurrentSchema = 2;

        public int schemaVersion = CurrentSchema;

        // Language (GDD 20.18). "ko" is source, "en" is the reference translation.
        public string language = "ko";

        // Camera / controls (GDD 8.2)
        public float fieldOfView = 68f;      // 60..90
        public float mouseSensitivity = 1.0f; // 0.1..5.0
        public float headBobAmount = 0.20f;   // 0..1
        public bool invertY = false;
        public bool holdToSprint = true;

        // Audio (0..1)
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float voiceVolume = 1f;

        // Video (GDD 16.16). Resolution 0x0 means "whatever this monitor is", which is the
        // only sane first run: the game must not force 1920x1080 onto a 3440x1440 display
        // before the player has ever seen the options screen.
        public int qualityLevel = 2;
        public int resolutionWidth = 0;
        public int resolutionHeight = 0;
        // Borderless by default, matching PlayerSettings in Editor/ReleaseSetup.cs. A horror
        // game gets alt-tabbed out of, and exclusive fullscreen is the mode that punishes it.
        public int windowMode = (int)WindowMode.Borderless;
        public int targetFrameRate = 60;
        public bool vsync = true;

        /// <summary>
        /// Schema 1 only knew fullscreen on or off. Read once during migration and then left
        /// alone - it is not written back, so a downgrade loses the borderless choice rather
        /// than the whole file.
        /// </summary>
        public bool fullscreen = true;

        // Difficulty (GDD 24.2)
        public int difficulty = (int)Difficulty.Standard;

        /// <summary>
        /// Longest endless run so far. Kept with the settings rather than in a save file
        /// because an endless run has no save - it is a single sitting and a number.
        /// </summary>
        public int endlessBestNights = 0;

        // Accessibility (GDD 16.16 / 16.17 / 24.2 / 24.3)
        public bool subtitles = true;
        public float subtitleScale = 1f;          // 0.9 - 1.6
        public bool subtitleSpeakerNames = true;
        public bool ambientSubtitles = true;
        public bool colorBlindPatterns = false;
        public bool reduceMotion = false;
        public float cameraShake = 1f;
        public int hintMode = (int)HintMode.Delayed;
        public bool noChoiceTimers = false;
        public bool easierChase = false;
        /// <summary>GDD 16.16: halves how fast the night closes in and what it costs (15.4).</summary>
        public bool calmNights = false;
        public bool streamerMode = false;
        public float brightness = 1f;             // 0.75 - 1.5
        public float hudOpacity = 0.65f;

        // Meta progression (GDD 6.5 / 16.15). Lives here rather than in a save slot so the
        // ending gallery survives deleting a playthrough.
        public List<string> unlockedEndings = new List<string>();
        public string lastEndingId = "";

        // GDD 16.2 minimum supported resolution.
        public const int MinimumWidth = 1280;
        public const int MinimumHeight = 720;

        /// <summary>GDD 16.16 offers exactly these. Zero is unlimited.</summary>
        public static readonly int[] FrameRateOptions = { 30, 60, 120, 0 };

        /// <summary>
        /// Snaps an arbitrary saved value onto the ladder. A file hand-edited to 45, or one
        /// written by a future build with more options, has to land somewhere sensible rather
        /// than being silently reset to 60.
        /// </summary>
        public static int NearestFrameRateOption(int value)
        {
            if (value <= 0) return 0;

            int best = FrameRateOptions[0];
            int bestDistance = int.MaxValue;

            for (int i = 0; i < FrameRateOptions.Length; i++)
            {
                if (FrameRateOptions[i] == 0) continue;

                int distance = Mathf.Abs(FrameRateOptions[i] - value);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = FrameRateOptions[i];
            }

            return best;
        }

        public GameSettings Clone()
        {
            var copy = (GameSettings)MemberwiseClone();
            copy.unlockedEndings = new List<string>(unlockedEndings);
            return copy;
        }

        public void Sanitize()
        {
            if (unlockedEndings == null) unlockedEndings = new List<string>();
            if (lastEndingId == null) lastEndingId = "";

            fieldOfView = Mathf.Clamp(fieldOfView, 60f, 90f);
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.1f, 5f);
            headBobAmount = Mathf.Clamp01(headBobAmount);
            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            voiceVolume = Mathf.Clamp01(voiceVolume);
            subtitleScale = Mathf.Clamp(subtitleScale, 0.9f, 1.6f);   // GDD 16.16
            cameraShake = Mathf.Clamp01(cameraShake);
            brightness = Mathf.Clamp(brightness, 0.75f, 1.5f);
            difficulty = Mathf.Clamp(difficulty, 0, 2);
            hintMode = Mathf.Clamp(hintMode, 0, 2);
            hudOpacity = Mathf.Clamp(hudOpacity, 0.2f, 1f);
            qualityLevel = Mathf.Clamp(qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            windowMode = Mathf.Clamp(windowMode, 0, 2);
            targetFrameRate = NearestFrameRateOption(targetFrameRate);

            // GDD 16.2 sets the floor at 1280x720. A zero pair is the untouched default and
            // has to survive Sanitize, or the first Apply would never ask the monitor.
            if (resolutionWidth != 0 || resolutionHeight != 0)
            {
                resolutionWidth = Mathf.Max(resolutionWidth, MinimumWidth);
                resolutionHeight = Mathf.Max(resolutionHeight, MinimumHeight);
            }

            if (string.IsNullOrEmpty(language)) language = "ko";
        }
    }

    /// <summary>
    /// Loads/stores settings as JSON next to the saves. Kept separate from the save file so
    /// that deleting a playthrough never resets accessibility options (GDD 20.17).
    /// </summary>
    public sealed class SettingsService
    {
        public const string FileName = "settings.json";

        public GameSettings Current { get; private set; }
        public event Action<GameSettings> OnApplied;

        string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

        public SettingsService()
        {
            Current = new GameSettings();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var loaded = Deserialize(json);
                    if (loaded != null) Current = loaded;
                }
            }
            catch (Exception e)
            {
                Log.Warn("Settings", "Load failed, using defaults: " + e.Message);
                Current = new GameSettings();
            }

            Current.Sanitize();
            Apply();
        }

        public void Save()
        {
            try
            {
                Current.Sanitize();
                File.WriteAllText(FilePath, Serialize(Current));
                Log.Info("Settings", "saved to " + FilePath);
            }
            catch (Exception e)
            {
                Log.Error("Settings", "Save failed: " + e.Message);
            }
        }

        public void Apply()
        {
            Current.Sanitize();

            QualitySettings.vSyncCount = Current.vsync ? 1 : 0;

            // targetFrameRate 0 is "unlimited" in the options and -1 in Unity; with vsync on
            // the display sets the rate and the cap has to get out of the way.
            Application.targetFrameRate =
                Current.vsync || Current.targetFrameRate <= 0 ? -1 : Current.targetFrameRate;

            AudioListener.volume = Current.masterVolume;

            ApplyQuality();
            ApplyDisplayMode();

            var cb = OnApplied;
            if (cb != null) cb(Current);
        }

        /// <summary>
        /// The preset was saved, loaded and then ignored - nothing ever called into
        /// QualitySettings, so the option did nothing at all on a shipped build.
        /// </summary>
        void ApplyQuality()
        {
            if (QualitySettings.names.Length == 0) return;

            int level = Mathf.Clamp(Current.qualityLevel, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() == level) return;

            // applyExpensiveChanges: false - this runs on every slider drag in the options
            // screen, and a full quality reload there is a visible hitch.
            QualitySettings.SetQualityLevel(level, false);
        }

        /// <summary>
        /// Resolution and window mode (GDD 16.16 / 28.6). Skipped in batch mode, where there
        /// is no display to set and asking for one throws.
        /// </summary>
        void ApplyDisplayMode()
        {
            if (Application.isBatchMode) return;

            var mode = ModeFor((WindowMode)Current.windowMode);

            int width = Current.resolutionWidth;
            int height = Current.resolutionHeight;

            // Untouched settings follow the monitor rather than the GDD's reference size.
            if (width <= 0 || height <= 0)
            {
                width = Display.main.systemWidth;
                height = Display.main.systemHeight;
            }

            if (width < GameSettings.MinimumWidth) width = GameSettings.MinimumWidth;
            if (height < GameSettings.MinimumHeight) height = GameSettings.MinimumHeight;

            if (Screen.width == width && Screen.height == height && Screen.fullScreenMode == mode)
                return;

            Screen.SetResolution(width, height, mode);
            Log.Info("Settings", "display " + width + "x" + height + " " + (WindowMode)Current.windowMode);
        }

        static FullScreenMode ModeFor(WindowMode mode)
        {
            switch (mode)
            {
                case WindowMode.Borderless: return FullScreenMode.FullScreenWindow;
                case WindowMode.Windowed:   return FullScreenMode.Windowed;
                default:                    return FullScreenMode.ExclusiveFullScreen;
            }
        }

        public void ResetToDefaults()
        {
            Current = new GameSettings();
            Apply();
            Save();
        }

        // Exposed for tests.
        public static string Serialize(GameSettings settings) { return JsonUtility.ToJson(settings, true); }

        public static GameSettings Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var result = JsonUtility.FromJson<GameSettings>(json);
            if (result == null) return null;

            Migrate(result);
            result.Sanitize();
            return result;
        }

        /// <summary>
        /// Brings an older settings file forward. Migrated, never discarded: someone who has
        /// already set their subtitle size and their hint mode must not lose them because the
        /// display options grew (CLAUDE.md / GDD 20.17).
        /// </summary>
        public static void Migrate(GameSettings settings)
        {
            if (settings.schemaVersion >= GameSettings.CurrentSchema)
            {
                settings.schemaVersion = GameSettings.CurrentSchema;
                return;
            }

            if (settings.schemaVersion < 2)
            {
                // Schema 1 had a bool. Everyone who had it off wanted a window; borderless
                // did not exist to be chosen, so plain Windowed is the honest reading.
                settings.windowMode = settings.fullscreen
                    ? (int)WindowMode.Fullscreen
                    : (int)WindowMode.Windowed;

                // Schema 1 never stored a resolution, so leave the zero pair and let the
                // first Apply ask the monitor.
                Log.Info("Settings", "migrated settings 1 -> 2");
            }

            settings.schemaVersion = GameSettings.CurrentSchema;
        }
    }
}
