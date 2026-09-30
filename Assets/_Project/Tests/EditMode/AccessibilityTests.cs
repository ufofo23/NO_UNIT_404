using NUnit.Framework;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// These options used to exist only as switches in the menu. The tests exist so they
    /// cannot quietly go back to doing nothing.
    /// </summary>
    public sealed class DifficultyTests
    {
        int _savedDifficulty;
        bool _savedEasierChase;

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            _savedDifficulty = ServiceHub.Settings.Current.difficulty;
            _savedEasierChase = ServiceHub.Settings.Current.easierChase;
        }

        [TearDown]
        public void TearDown()
        {
            ServiceHub.Settings.Current.difficulty = _savedDifficulty;
            ServiceHub.Settings.Current.easierChase = _savedEasierChase;
        }

        [Test]
        public void APursuerIsNeverFasterThanTheSpecAllows()
        {
            for (int level = 0; level <= 2; level++)
            {
                ServiceHub.Settings.Current.difficulty = level;
                float factor = DifficultyProfile.ChaseSpeedFactor;

                Assert.LessOrEqual(factor, 0.95f, "GDD 15.2 caps pursuit at 95% of the player's sprint");
                Assert.Greater(factor, 0f);
            }
        }

        [Test]
        public void StoryModeMakesPursuitEscapableAndRemovesTimers()
        {
            ServiceHub.Settings.Current.difficulty = (int)Difficulty.Story;

            Assert.AreEqual(0.75f, DifficultyProfile.ChaseSpeedFactor, 0.001f);   // GDD 24.2
            Assert.IsTrue(DifficultyProfile.ChoiceTimersDisabled);
            Assert.IsTrue(DifficultyProfile.HighlightKeyEvidence);
            Assert.Greater(DifficultyProfile.DeadlineFactor, 1f, "story mode gives more time");
        }

        [Test]
        public void TheHardestSettingTightensDeadlinesAndQuietensAlerts()
        {
            ServiceHub.Settings.Current.difficulty = (int)Difficulty.NightSupervisor;

            Assert.AreEqual(0.8f, DifficultyProfile.DeadlineFactor, 0.001f);      // GDD 24.2: -20%
            Assert.AreEqual(0.95f, DifficultyProfile.ChaseSpeedFactor, 0.001f);
            Assert.IsTrue(DifficultyProfile.ReducedCctvAlerts);
            Assert.Greater(DifficultyProfile.HintDelayFactor, 1f);
        }

        [Test]
        public void TheAccessibilityToggleOverridesTheDifficultyForPursuit()
        {
            ServiceHub.Settings.Current.difficulty = (int)Difficulty.NightSupervisor;
            ServiceHub.Settings.Current.easierChase = true;

            Assert.AreEqual(0.75f, DifficultyProfile.ChaseSpeedFactor, 0.001f,
                            "an accessibility need outranks the chosen difficulty");
        }
    }

    public sealed class HintLadderTests
    {
        HintService _hints;
        int _savedDifficulty;
        int _savedHintMode;

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            _savedDifficulty = ServiceHub.Settings.Current.difficulty;
            _savedHintMode = ServiceHub.Settings.Current.hintMode;

            ServiceHub.Settings.Current.difficulty = (int)Difficulty.Standard;
            ServiceHub.Settings.Current.hintMode = (int)HintMode.Delayed;

            ServiceHub.ResetPlaythrough();
            ServiceHub.State.BeginNight(1);
            ServiceHub.Cases.BeginNight(1);
            ServiceHub.Clock.SetGameSecond(SeedContent.At(22, 30));

            Assert.IsTrue(ServiceHub.Cases.TryStartCase("N1-M01"));
            ServiceHub.Cases.SetTracked("N1-M01");

            _hints = ServiceHub.Hints;
            _hints.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceHub.Settings.Current.difficulty = _savedDifficulty;
            ServiceHub.Settings.Current.hintMode = _savedHintMode;
        }

        [Test]
        public void NothingIsOfferedBeforeTheFirstThreeMinutes()
        {
            _hints.Tick(0f);
            Assert.AreEqual(0, _hints.Level);
            Assert.IsEmpty(_hints.CurrentHint);

            _hints.Tick(120f);
            Assert.AreEqual(0, _hints.Level, "GDD 24.3 waits three minutes before the first hint");
        }

        [Test]
        public void HintsEscalateThroughAllFourStages()
        {
            _hints.Tick(0f);

            _hints.Tick(HintService.FirstStepSeconds + 1f);
            Assert.AreEqual(1, _hints.Level);
            Assert.IsNotEmpty(_hints.CurrentHint);

            _hints.Tick(HintService.FirstStepSeconds + HintService.StepSeconds + 1f);
            Assert.AreEqual(2, _hints.Level);

            _hints.Tick(HintService.FirstStepSeconds + HintService.StepSeconds * 2f + 1f);
            Assert.AreEqual(3, _hints.Level);

            _hints.Tick(HintService.FirstStepSeconds + HintService.StepSeconds * 3f + 1f);
            Assert.AreEqual(4, _hints.Level, "the ladder tops out at the exact menu path");
        }

        [Test]
        public void TurningHintsOffReallyTurnsThemOff()
        {
            ServiceHub.Settings.Current.hintMode = (int)HintMode.Off;

            _hints.Tick(0f);
            _hints.Tick(10000f);

            Assert.AreEqual(0, _hints.Level);
            Assert.IsEmpty(_hints.CurrentHint);
        }

        [Test]
        public void AlwaysOnSkipsTheWait()
        {
            ServiceHub.Settings.Current.hintMode = (int)HintMode.Always;

            _hints.Tick(0f);
            _hints.Tick(1f);

            Assert.GreaterOrEqual(_hints.Level, 1);
            Assert.IsNotEmpty(_hints.CurrentHint);
        }

        [Test]
        public void TwoWrongCallsInARowStartTheLadderHigher()
        {
            _hints.RegisterDecision(DecisionQuality.Wrong);
            _hints.RegisterDecision(DecisionQuality.Wrong);
            Assert.AreEqual(2, _hints.ConsecutiveWrongDecisions);

            _hints.Tick(0f);
            _hints.Tick(HintService.FirstStepSeconds + 1f);

            Assert.AreEqual(2, _hints.Level, "GDD 24.1 strengthens help after two wrong judgements");
        }

        [Test]
        public void ACorrectCallResetsTheStreak()
        {
            _hints.RegisterDecision(DecisionQuality.Wrong);
            _hints.RegisterDecision(DecisionQuality.Correct);

            Assert.AreEqual(0, _hints.ConsecutiveWrongDecisions);
        }

        [Test]
        public void ProgressClearsTheHint()
        {
            _hints.Tick(0f);
            _hints.Tick(HintService.FirstStepSeconds + 1f);
            Assert.AreEqual(1, _hints.Level);

            // Completing the objective moves the tracked objective on.
            var runtime = ServiceHub.Cases.Find("N1-M01");
            var objective = runtime.CurrentVisibleObjective();
            ServiceHub.Cases.TryAdvanceObjective("N1-M01", objective.objectiveId);

            _hints.Tick(HintService.FirstStepSeconds + 2f);
            Assert.AreEqual(0, _hints.Level, "a new objective restarts the ladder");
        }
    }

    public sealed class CaptionTests
    {
        CaptionService _captions;
        bool _savedSubtitles;
        bool _savedAmbient;
        bool _savedNames;

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();

            var settings = ServiceHub.Settings.Current;
            _savedSubtitles = settings.subtitles;
            _savedAmbient = settings.ambientSubtitles;
            _savedNames = settings.subtitleSpeakerNames;

            settings.subtitles = true;
            settings.ambientSubtitles = true;
            settings.subtitleSpeakerNames = true;

            _captions = ServiceHub.Captions;
            _captions.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            var settings = ServiceHub.Settings.Current;
            settings.subtitles = _savedSubtitles;
            settings.ambientSubtitles = _savedAmbient;
            settings.subtitleSpeakerNames = _savedNames;
            _captions.Clear();
        }

        [Test]
        public void SpeechCarriesTheSpeakerInBrackets()
        {
            _captions.Speak("speaker.dongsik", "dlg.radio.dongsik.001");

            StringAssert.StartsWith("[", _captions.Current);         // GDD 16.17
            StringAssert.Contains(Loc.T("speaker.dongsik"), _captions.Current);
            Assert.IsFalse(_captions.CurrentIsAmbient);
        }

        [Test]
        public void SpeakerNamesCanBeSwitchedOff()
        {
            ServiceHub.Settings.Current.subtitleSpeakerNames = false;
            _captions.Speak("speaker.dongsik", "dlg.radio.dongsik.001");

            Assert.IsFalse(_captions.Current.StartsWith("["));
            Assert.AreEqual(Loc.T("dlg.radio.dongsik.001"), _captions.Current);
        }

        [Test]
        public void AmbientCuesAreBracketedAndSeparatelyToggleable()
        {
            _captions.Ambient("caption.elevator_stops");
            StringAssert.StartsWith("[", _captions.Current);
            Assert.IsTrue(_captions.CurrentIsAmbient);

            _captions.Clear();
            ServiceHub.Settings.Current.ambientSubtitles = false;
            _captions.Ambient("caption.elevator_stops");

            Assert.IsEmpty(_captions.Current, "ambient captions have their own switch");
        }

        [Test]
        public void TurningSubtitlesOffSilencesBothKinds()
        {
            ServiceHub.Settings.Current.subtitles = false;

            _captions.Speak("speaker.dongsik", "dlg.radio.dongsik.001");
            Assert.IsEmpty(_captions.Current);

            _captions.Ambient("caption.footsteps");
            Assert.IsEmpty(_captions.Current);
        }
    }

    public sealed class FireEscapeTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
        }

        [Test]
        public void AWellMaintainedBuildingKeepsItsStairs()
        {
            ServiceHub.State.SetStat(StatIds.BuildingSafety, 80);
            ServiceHub.Threat.BeginFireEscape();

            Assert.AreEqual(Threat.ThreatKind.Fire, ServiceHub.Threat.Kind);
            Assert.IsFalse(ServiceHub.Threat.StairsBlocked);
            Assert.IsFalse(ServiceHub.Threat.IsRouteBlocked(ZoneIds.Stairwell));
        }

        [Test]
        public void ANeglectedBuildingForcesTheOtherRoute()
        {
            ServiceHub.State.SetStat(StatIds.BuildingSafety, 30);
            ServiceHub.Threat.BeginFireEscape();

            Assert.IsTrue(ServiceHub.Threat.StairsBlocked, "GDD 9.7: low safety blocks part of the stairs");
            Assert.IsTrue(ServiceHub.Threat.IsRouteBlocked(ZoneIds.Stairwell));
            Assert.IsFalse(ServiceHub.Threat.IsRouteBlocked(ZoneIds.Elevator),
                           "the elevator has to stay open or there is no way out");
        }

        [Test]
        public void ReachingTheLobbyEndsTheFireWithoutLosingTheLedger()
        {
            ServiceHub.Evidence.Acquire("E19_ORIGINAL_LEDGER", Evidence.EvidenceSource.WorldPickup);
            ServiceHub.Threat.BeginFireEscape();

            ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            ServiceHub.Threat.Tick();

            Assert.AreEqual(Threat.ThreatKind.None, ServiceHub.Threat.Kind);
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.FireEscaped));
            Assert.IsTrue(ServiceHub.Evidence.Has("E19_ORIGINAL_LEDGER"));
        }

        [Test]
        public void RunningOutOfTimeCostsTheLedgerRatherThanTheRun()
        {
            ServiceHub.Evidence.Acquire("E19_ORIGINAL_LEDGER", Evidence.EvidenceSource.WorldPickup);
            ServiceHub.Player.EnterZone(ZoneIds.Unit404);
            ServiceHub.Threat.BeginFireEscape();

            ServiceHub.Clock.AdvanceSeconds(Threat.ThreatService.FireEscapeSeconds + 10);
            ServiceHub.Threat.Tick();

            Assert.AreEqual(Threat.ThreatKind.None, ServiceHub.Threat.Kind);
            Assert.IsFalse(ServiceHub.Evidence.Has("E19_ORIGINAL_LEDGER"),
                           "GDD 15.2: failure costs evidence, never a life");
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.FireEscaped));
        }
    }
}
