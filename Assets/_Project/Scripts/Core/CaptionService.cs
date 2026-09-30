using UnityEngine;

namespace NO404.Core
{
    /// <summary>
    /// Subtitles and environmental captions (GDD 16.17).
    ///
    /// Two kinds of line, controlled separately in the options because players who can hear
    /// speech may still want the ambient cues:
    ///  - speech: <c>[박동식] 들리나.</c>
    ///  - ambient: <c>[엘리베이터가 위층에서 멈춘다]</c>
    ///
    /// Directional cues carry their direction in the text itself, which is the only way it
    /// works for a player who is not using the audio at all.
    /// </summary>
    public sealed class CaptionService
    {
        public const float SpeechSeconds = 4.5f;
        public const float AmbientSeconds = 3f;

        string _current = string.Empty;
        float _expiresAtRealtime;
        bool _currentIsAmbient;

        public string Current
        {
            get { return Time.realtimeSinceStartup < _expiresAtRealtime ? _current : string.Empty; }
        }

        public bool CurrentIsAmbient { get { return _currentIsAmbient; } }

        bool SubtitlesOn
        {
            get { return ServiceHub.Settings != null && ServiceHub.Settings.Current.subtitles; }
        }

        bool AmbientOn
        {
            get { return SubtitlesOn && ServiceHub.Settings.Current.ambientSubtitles; }
        }

        /// <summary>A spoken line. The speaker name is bracketed, never colour-coded (GDD 16.17).</summary>
        public void Speak(string speakerKey, string textKey)
        {
            if (!SubtitlesOn || string.IsNullOrEmpty(textKey)) return;

            var body = Loc.T(textKey);
            bool named = ServiceHub.Settings.Current.subtitleSpeakerNames && !string.IsNullOrEmpty(speakerKey);

            Show(named ? "[" + Loc.T(speakerKey) + "] " + body : body, SpeechSeconds, false);
        }

        /// <summary>An environmental cue. Always shown inside brackets.</summary>
        public void Ambient(string textKey)
        {
            if (!AmbientOn || string.IsNullOrEmpty(textKey)) return;
            Show("[" + Loc.T(textKey) + "]", AmbientSeconds, true);
        }

        public void Clear()
        {
            _current = string.Empty;
            _expiresAtRealtime = 0f;
        }

        void Show(string text, float seconds, bool ambient)
        {
            _current = text;
            _currentIsAmbient = ambient;
            _expiresAtRealtime = Time.realtimeSinceStartup + seconds;
        }
    }
}
