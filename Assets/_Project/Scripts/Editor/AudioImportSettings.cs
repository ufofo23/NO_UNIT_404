using UnityEditor;
using UnityEngine;

namespace NO404.EditorTools
{
    /// <summary>
    /// Import settings for the cue bank (GDD 19 / 20.20).
    ///
    /// Two things have to be true of every clip under Resources/NO404/Audio, and neither is
    /// Unity's default:
    ///
    ///  - it must be readable at runtime, because AudioService measures each clip once and
    ///    plays it at the gain its class calls for. A Streaming clip cannot be read, so the
    ///    loudness policy in GDD 19.3 would quietly stop being enforced;
    ///  - it must be mono. These are room tones and single objects in a 3D space; a stereo
    ///    file doubles the memory and defeats the positional voices.
    ///
    /// Applying this from an importer rather than by hand means a regenerated or re-recorded
    /// clip lands correctly without anyone remembering to click through the inspector.
    /// </summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        const string CueFolder = "Assets/_Project/Resources/NO404/Audio/";

        /// <summary>Above this length a cue is a bed, and is worth compressing in memory.</summary>
        const float LongClipSeconds = 8f;

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(CueFolder)) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;

            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

            // The four room tones are ~13 s each and the only clips large enough to be worth
            // trading a little CPU for; everything else is under three seconds and stays PCM
            // so a knock never waits on a decode.
            if (LengthSecondsOf(assetPath) < LongClipSeconds)
                settings.compressionFormat = AudioCompressionFormat.PCM;

            importer.defaultSampleSettings = settings;
        }

        /// <summary>
        /// Length from the file on disk rather than from the imported clip: during a first
        /// import there is no clip to ask, and reading the wrong answer once would leave a
        /// room tone stored as PCM forever, because nothing reimports it again.
        ///
        /// Tools/GenerateAudio.py writes 44.1 kHz 16-bit mono, so the size is the length.
        /// </summary>
        static float LengthSecondsOf(string path)
        {
            const float BytesPerSecond = 44100f * 2f;

            var info = new System.IO.FileInfo(path);
            if (!info.Exists) return 0f;

            return info.Length / BytesPerSecond;
        }
    }
}
