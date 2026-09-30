using NO404.Core;

namespace NO404.Anomalies
{
    /// <summary>
    /// What carrying the building's distortion does to what the caretaker sees
    /// (v2.1 spec 0.10.4).
    ///
    /// Exposure had exactly one effect before this: at 100 it opened the correction procedure.
    /// Everything below that - the deformed glyph, the footsteps behind you, the landing that
    /// says the wrong number - was described in the spec and implemented nowhere, so a
    /// caretaker at 74 and one at 4 played precisely the same game.
    ///
    /// The one rule that governs all of it is spec 0.10.4's last line and spec 32's: none of
    /// this may change a real answer. It can make the caretaker doubt a reading; it can never
    /// make the reading wrong. So everything here is presentation the player can re-check, and
    /// the two effects that could mislead - the misread landing and the corrupted glyph - are
    /// both on surfaces that correct themselves on a second look.
    /// </summary>
    public sealed class DistortionDirector
    {
        /// <summary>Game seconds between the distant footsteps at Faint and above.</summary>
        public const int FootstepIntervalSeconds = 900;

        readonly RiskService _risk;
        readonly GameClock _clock;

        int _nextFootstepSecond;

        public DistortionDirector(RiskService risk, GameClock clock)
        {
            _risk = risk;
            _clock = clock;
        }

        public DistortionBand Band { get { return _risk.Band; } }

        // ---- what other systems ask it ---------------------------------------

        /// <summary>
        /// How often a glyph on a lit panel deforms for a frame (spec 0.10.4, 25+).
        ///
        /// Returned as a chance rather than as a schedule so the surfaces that use it - the
        /// HUD clock, the management PC - stay in charge of when they redraw. Zero below the
        /// first band, which is what keeps a clean run visually clean.
        /// </summary>
        public float GlyphCorruptionChance
        {
            get
            {
                switch (Band)
                {
                    case DistortionBand.Faint: return 0.04f;
                    case DistortionBand.Marked: return 0.10f;
                    case DistortionBand.Severe: return 0.18f;
                    case DistortionBand.Critical: return 0.25f;
                    default: return 0f;
                }
            }
        }

        /// <summary>
        /// Whether a stair landing may show a floor it is not (spec 0.10.4, 50+).
        ///
        /// Only ever the sign. StairNavigator still knows exactly where the player is, and the
        /// next landing is computed from the truth - spec 0.8.1 bans anything else, and a
        /// misread that actually moved someone would be that.
        /// </summary>
        public bool MayMisreadFloor { get { return Band >= DistortionBand.Marked; } }

        /// <summary>
        /// Extra flights M07 swallows because of what the caretaker is carrying
        /// (spec 0.10.4: stairs repeat more often).
        /// </summary>
        public int ExtraLoopFlights
        {
            get { return Band >= DistortionBand.Marked ? (int)Band - 1 : 0; }
        }

        /// <summary>
        /// Whether a task card can arrive for work nobody filed (spec 0.10.4, 50+).
        ///
        /// The card is false; the way to tell is to check the source the way the manual says,
        /// which is the same check the real ones survive. Spec 0.4 will not let the UI mark
        /// it, and spec 32 will not let it hide a real one.
        /// </summary>
        public bool MayShowFalseTaskCards { get { return Band >= DistortionBand.Marked; } }

        /// <summary>
        /// Whether the difference between a real clue and a planted one stops being obvious
        /// (spec 0.10.4, 75+).
        /// </summary>
        public bool CluesAreHardToTellApart { get { return Band >= DistortionBand.Severe; } }

        /// <summary>
        /// The task card for work nobody filed, or null (spec 0.10.4, 50+).
        ///
        /// One per night at most, and the same one all night: a card that came and went every
        /// time the caretaker opened the tablet would read as a UI fault rather than as the
        /// building, and could never be checked against anything.
        ///
        /// It is deliberately indistinguishable on the tablet - spec 0.4 will not let the UI
        /// mark a wrong answer, and marking this one would be exactly that. The way to tell is
        /// the way the manual would have you tell: the id is on no other screen in the game,
        /// so a caretaker who cross-checks the management PC finds nothing, and a caretaker who
        /// walks to the unit finds nobody who called.
        /// </summary>
        public FalseTask FalseTaskCardFor(int nightIndex)
        {
            if (!MayShowFalseTaskCards || nightIndex <= 0) return default(FalseTask);

            int index = (nightIndex - 1) % FalseTasks.Length;
            return FalseTasks[index];
        }

        /// <summary>A task card with nothing behind it.</summary>
        public struct FalseTask
        {
            public string Id;
            public string TitleKey;
            public string ObjectiveKey;

            public bool Exists { get { return !string.IsNullOrEmpty(Id); } }
        }

        // Ids outside the T01..T18 range the routine tasks use, so a false card can never
        // collide with a real one and the two can always be told apart in a log.
        static readonly FalseTask[] FalseTasks =
        {
            new FalseTask { Id = "T21", TitleKey = "task.false.t21.title",
                            ObjectiveKey = "task.false.t21.objective" },
            new FalseTask { Id = "T24", TitleKey = "task.false.t24.title",
                            ObjectiveKey = "task.false.t24.objective" },
            new FalseTask { Id = "T27", TitleKey = "task.false.t27.title",
                            ObjectiveKey = "task.false.t27.objective" }
        };

        // ---- the part that happens on its own --------------------------------

        /// <summary>Called once per frame by GameLoop, after the risk model has settled.</summary>
        public void Tick()
        {
            if (Band < DistortionBand.Faint) { _nextFootstepSecond = 0; return; }

            int now = _clock.GameSecond;
            if (_nextFootstepSecond == 0) { _nextFootstepSecond = now + FootstepIntervalSeconds; return; }
            if (now < _nextFootstepSecond) return;

            // Closer intervals as it gets worse, but never a constant noise: the footsteps
            // are meant to be noticed, and something heard every minute stops being heard.
            int interval = FootstepIntervalSeconds - (int)Band * 150;
            _nextFootstepSecond = now + (interval < 300 ? 300 : interval);

            ServiceHub.Captions.Ambient("caption.distant_footsteps");
            ServiceHub.Audio.PlayCue(AudioCue.FootstepsDistant);
        }

        public void Reset() { _nextFootstepSecond = 0; }

        /// <summary>
        /// One frame of a deformed glyph (spec 0.10.4, 25+).
        ///
        /// Deliberately a single character, and deliberately reversible: the caller redraws
        /// next frame and the text is right again. That is the whole effect - the caretaker
        /// is never given a wrong number to act on, only a moment of not trusting a right one.
        /// </summary>
        public string Corrupt(string text)
        {
            float chance = GlyphCorruptionChance;
            if (chance <= 0f || string.IsNullOrEmpty(text)) return text;
            if (UnityEngine.Random.value > chance) return text;

            int index = UnityEngine.Random.Range(0, text.Length);
            if (text[index] == '\n') return text;

            var glyphs = Glyphs;
            var chars = text.ToCharArray();
            chars[index] = glyphs[UnityEngine.Random.Range(0, glyphs.Length)];
            return new string(chars);
        }

        static readonly char[] Glyphs = { '4', '0', '#', '?', '█' };
    }
}
