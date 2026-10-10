using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// The story sequences v5.1 8.5 calls CIN-N3, CIN-N4 and so on, as they exist today.
    ///
    /// v5.1 wants these as Timeline cinematics. Nothing in the project can author one yet -
    /// the same reason the intro is four title cards rather than a film - so a sequence here
    /// is what the intro is: two things held up against each other, a few lines over them,
    /// and the player's hands taken off the controls for as long as it lasts. Swapping one
    /// for a real Timeline is a change inside <see cref="Play"/> and nowhere else, because
    /// everything that asks for a sequence asks for it by id and waits for nothing.
    ///
    /// Presentation only. Whatever the sequence is about - the SAN it costs, the flag that
    /// says it happened - has already been applied by whoever asked for it, so a build with
    /// this view missing loses the picture and none of the game.
    /// </summary>
    public sealed class StorySequenceView : MonoBehaviour
    {
        sealed class Sequence
        {
            /// <summary>The two things being held up against each other.</summary>
            public string LeftKey;
            public string RightKey;
            public string[] LineKeys;
            public float SecondsPerLine;
        }

        /// <summary>A beat of the overlay alone before the first line, and after the last.</summary>
        const float LeadSeconds = 1.5f;
        const float TailSeconds = 1.0f;

        /// <summary>
        /// How long a key press is ignored for after the sequence opens. It is nearly always
        /// opened by a click - the report being filed - and that click must not also close it.
        /// </summary>
        const float SkipGuardSeconds = 0.75f;

        static readonly Dictionary<string, Sequence> Known = new Dictionary<string, Sequence>
        {
            // v5.1 8.5 CIN-N4, 20-30 seconds: the name on the 404 row and the name on her
            // staff card are the same name, and part of the night of the fire comes back.
            {
                "CIN-N4", new Sequence
                {
                    LeftKey = "cin.n4.staff_card",
                    RightKey = "cin.n4.db_row",
                    LineKeys = new[] { "cin.n4.line.01", "cin.n4.line.02", "cin.n4.line.03", "cin.n4.line.04" },
                    SecondsPerLine = 5f
                }
            }
        };

        public System.Action OnFinished;

        Text _left;
        Text _right;
        Text _line;
        Text _skipHint;

        Sequence _playing;
        int _index;
        float _nextAdvanceAt;
        float _skipAllowedAt;

        public bool IsRunning { get { return _playing != null; } }
        public string PlayingId { get; private set; }

        public static bool Knows(string cinematicId)
        {
            return !string.IsNullOrEmpty(cinematicId) && Known.ContainsKey(cinematicId);
        }

        /// <summary>Every string key a sequence shows, for the content checks.</summary>
        public static List<string> KeysOf(string cinematicId)
        {
            var keys = new List<string>();
            Sequence sequence;
            if (string.IsNullOrEmpty(cinematicId) || !Known.TryGetValue(cinematicId, out sequence)) return keys;

            keys.Add(sequence.LeftKey);
            keys.Add(sequence.RightKey);
            keys.AddRange(sequence.LineKeys);
            return keys;
        }

        public static StorySequenceView Create(Transform parent)
        {
            // Over the PC and the HUD, under the intro and the console.
            var canvas = UiFactory.CreateCanvas("StorySequence", parent, 850);
            var view = canvas.gameObject.AddComponent<StorySequenceView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var dim = UiFactory.CreatePanel("Dim", root, new Color(0f, 0f, 0f, 0.9f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f);

            _left = Card(root, "Left", -230f);
            _right = Card(root, "Right", 230f);

            _line = UiFactory.CreateText("Line", root, string.Empty, 26, TextAnchor.MiddleCenter);
            UiFactory.Pin(_line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0f, -150f), new Vector2(1100f, 120f));

            _skipHint = UiFactory.CreateText("Skip", root, string.Empty, 15, TextAnchor.LowerRight,
                                             UiFactory.TextMuted);
            UiFactory.Pin(_skipHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                          new Vector2(-32f, 28f), new Vector2(420f, 24f));

            gameObject.SetActive(false);
        }

        static Text Card(RectTransform root, string name, float x)
        {
            var panel = UiFactory.CreatePanel(name, root, UiFactory.Panel);
            UiFactory.Pin(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(x, 90f), new Vector2(420f, 150f));

            var text = UiFactory.CreateText("Text", panel.transform, string.Empty, 22, TextAnchor.MiddleCenter);
            UiFactory.Stretch(text.rectTransform, 16f, 12f);
            return text;
        }

        /// <summary>Starts a sequence. False when the id is not one this view can play.</summary>
        public bool Play(string cinematicId)
        {
            Sequence sequence;
            if (string.IsNullOrEmpty(cinematicId) || !Known.TryGetValue(cinematicId, out sequence))
            {
                Log.Warn("Story", "no sequence for " + cinematicId);
                return false;
            }

            _playing = sequence;
            PlayingId = cinematicId;
            _index = -1;

            _left.text = Loc.T(sequence.LeftKey);
            _right.text = Loc.T(sequence.RightKey);
            _line.text = string.Empty;
            _skipHint.text = Loc.T("intro.skip_hint");

            _nextAdvanceAt = Time.unscaledTime + LeadSeconds;
            _skipAllowedAt = Time.unscaledTime + SkipGuardSeconds;

            gameObject.SetActive(true);
            Log.Info("Story", "playing " + cinematicId);
            return true;
        }

        /// <summary>Ends whatever is playing, without calling it finished. For a night ending under it.</summary>
        public void Stop()
        {
            _playing = null;
            PlayingId = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_playing == null) return;

            // The clock first and unconditionally, for the reason the intro does it: a
            // sequence that can only be left by a key press is one a broken key press makes
            // permanent, and this one sits on top of the controls.
            if (Time.unscaledTime >= _nextAdvanceAt) { Advance(); return; }

            if (Time.unscaledTime >= _skipAllowedAt && SkipPressed()) Finish();
        }

        static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var gamepad = Gamepad.current;
            return gamepad != null && (gamepad.startButton.wasPressedThisFrame ||
                                       gamepad.buttonSouth.wasPressedThisFrame);
        }

        void Advance()
        {
            _index++;

            if (_index < _playing.LineKeys.Length)
            {
                _line.text = Loc.T(_playing.LineKeys[_index]);
                _nextAdvanceAt = Time.unscaledTime + _playing.SecondsPerLine;
                return;
            }

            if (_index == _playing.LineKeys.Length)
            {
                _line.text = string.Empty;
                _nextAdvanceAt = Time.unscaledTime + TailSeconds;
                return;
            }

            Finish();
        }

        void Finish()
        {
            Stop();
            var cb = OnFinished;
            if (cb != null) cb();
        }
    }
}
