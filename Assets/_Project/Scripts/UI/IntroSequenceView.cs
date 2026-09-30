using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// The fifteen seconds before the first shift.
    ///
    /// The campaign used to open on a caretaker already sitting at a desk, which asked the
    /// player to care about a building they had been told nothing about. This is the smallest
    /// thing that fixes that: four lines, black screen, and then work.
    ///
    /// It is built rather than filmed. There is no video in this project and none of the tools
    /// here can make one, so this is a timed title sequence - which for fifteen seconds of
    /// text over a room tone is not a compromise so much as the same thing done in the engine,
    /// and it costs nothing to change a line later.
    ///
    /// <b>Voice.</b> Each line will play <c>Resources/NO404/Audio/VO/Intro_01..04</c> if that
    /// clip exists and run silently with its subtitle if it does not, so the sequence is
    /// correct either way and recording can happen whenever. The subtitle is never conditional
    /// on the voice: it is the accessible path, not the fallback.
    ///
    /// <b>Skippable from the first frame.</b> Fifteen seconds is short until it is the fourth
    /// time somebody has restarted the night, and an intro that cannot be dismissed is the
    /// thing playtesters remember instead of the game.
    /// </summary>
    public sealed class IntroSequenceView : MonoBehaviour
    {
        /// <summary>Where a recorded narration is looked for. One clip per line.</summary>
        public const string VoicePathPrefix = "NO404/Audio/VO/Intro_";

        /// <summary>
        /// The four lines and how long each holds, in real seconds.
        ///
        /// Fourteen and a half seconds of speech and half a second of black at the end, which
        /// reads as fifteen. The last line is the longest on purpose: it is the title, and the
        /// player should be looking at it when the screen goes.
        /// </summary>
        public static readonly string[] LineKeys =
        {
            "intro.line.01", "intro.line.02", "intro.line.03", "intro.line.04"
        };

        public static readonly float[] LineSeconds = { 3.5f, 3.5f, 3.5f, 4.0f };

        /// <summary>Real seconds of black after the last line, before the shift starts.</summary>
        public const float TailSeconds = 0.5f;

        public System.Action OnFinished;

        Text _line;
        Text _skipHint;
        AudioSource _voice;

        int _index = -1;
        float _nextAdvanceAt;
        bool _running;

        public bool IsRunning { get { return _running; } }

        public static float TotalSeconds
        {
            get
            {
                float total = TailSeconds;
                for (int i = 0; i < LineSeconds.Length; i++) total += LineSeconds[i];
                return total;
            }
        }

        public static IntroSequenceView Create(Transform parent)
        {
            // Above every other view: this is the only thing on screen while it runs.
            var canvas = UiFactory.CreateCanvas("Intro", parent, 900);
            var view = canvas.gameObject.AddComponent<IntroSequenceView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var black = UiFactory.CreatePanel("Black", root, Color.black);
            UiFactory.Stretch(black.rectTransform, 0f, 0f);

            _line = UiFactory.CreateText("Line", root, string.Empty, 26, TextAnchor.MiddleCenter);
            UiFactory.Pin(_line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          Vector2.zero, new Vector2(1000f, 160f));

            _skipHint = UiFactory.CreateText("Skip", root, string.Empty, 15, TextAnchor.LowerRight,
                                             UiFactory.TextMuted);
            UiFactory.Pin(_skipHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                          new Vector2(-32f, 28f), new Vector2(420f, 24f));

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;

            gameObject.SetActive(false);
        }

        public void Play()
        {
            gameObject.SetActive(true);
            _running = true;
            _index = -1;
            _nextAdvanceAt = 0f;

            _skipHint.text = Loc.T("intro.skip_hint");
            Advance();
        }

        void Update()
        {
            if (!_running) return;

            // The clock first, and unconditionally.
            //
            // This used to read the skip before the timer, and reading it threw every frame -
            // the project is on the Input System package alone, where UnityEngine.Input raises
            // rather than returns - so the intro drew its first line and then died there, with
            // no key able to help because the thing that read keys was the thing throwing.
            //
            // Advancing first means the sequence still ends on its own even if the skip is
            // broken again. An intro that cannot be got out of is the worst thing this file
            // could do, so it is now structurally incapable of it.
            if (Time.unscaledTime >= _nextAdvanceAt) { Advance(); return; }

            if (SkipPressed()) Finish();
        }

        /// <summary>
        /// Anything a person might press to mean "yes, I have read it".
        ///
        /// Devices are null-checked one at a time because a machine with no gamepad is the
        /// normal case, not an error, and asking an absent device for a button is how this
        /// would start throwing again.
        /// </summary>
        static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.startButton.wasPressedThisFrame
                                 || gamepad.buttonSouth.wasPressedThisFrame)) return true;

            return false;
        }

        void Advance()
        {
            _index++;

            if (_index >= LineKeys.Length)
            {
                // The tail is a beat of black, so the last line is not cut off by the world
                // appearing underneath it.
                if (_index == LineKeys.Length)
                {
                    _line.text = string.Empty;
                    _nextAdvanceAt = Time.unscaledTime + TailSeconds;
                    return;
                }

                Finish();
                return;
            }

            _line.text = Loc.T(LineKeys[_index]);
            _nextAdvanceAt = Time.unscaledTime + LineSeconds[_index];

            SpeakLine(_index);
        }

        /// <summary>
        /// Plays the recorded narration for this line, if somebody has recorded one.
        ///
        /// Resources.Load returning null is the expected case today, not an error - the
        /// sequence is authored to be complete without audio, and logging a missing clip every
        /// line would make a normal run look broken.
        /// </summary>
        void SpeakLine(int index)
        {
            var clip = Resources.Load<AudioClip>(VoicePathPrefix + (index + 1).ToString("00"));
            if (clip == null) return;

            _voice.volume = ServiceHub.Settings != null ? ServiceHub.Settings.Current.voiceVolume : 1f;
            _voice.PlayOneShot(clip);
        }

        void Finish()
        {
            if (!_running) return;
            _running = false;

            if (_voice != null) _voice.Stop();
            gameObject.SetActive(false);

            var cb = OnFinished;
            OnFinished = null;
            if (cb != null) cb();
        }

        /// <summary>Stops the sequence without running what it was going to start.</summary>
        public void Cancel()
        {
            OnFinished = null;
            _running = false;
            if (_voice != null) _voice.Stop();
            gameObject.SetActive(false);
        }
    }
}
