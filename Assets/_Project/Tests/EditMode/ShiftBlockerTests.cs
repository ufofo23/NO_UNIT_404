using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// The clock-off button told a caretaker who had done every single thing the night had
    /// given them that they still had work open. It was counting cases the shift had not
    /// issued yet - night 1 does not hand out T03 until 00:10 and has callers booked until
    /// 01:35 - as if the player had abandoned them.
    ///
    /// Blocking is correct; a shift is a shift. Saying why is what these hold.
    /// </summary>
    public sealed class ShiftBlockerTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        static IEnumerable<CaseRuntime> Tonight(int night)
        {
            var list = new List<CaseRuntime>();
            foreach (var runtime in ServiceHub.Cases.AllCases)
                if (runtime.Definition.nightIndex == night) list.Add(runtime);
            return list;
        }

        [Test]
        public void AShiftThatHasNotIssuedItsWorkYetIsNotTheCaretakersFault()
        {
            ServiceHub.Cases.BeginNight(1);

            // Nothing has triggered yet, so every case for tonight is Dormant.
            var blocker = GameLoop.BlockerForRoster(Tonight(1), 1, false);

            Assert.AreEqual(GameLoop.ShiftBlocker.NotYetScheduled, blocker,
                "work the night has not handed out must never read as work the player left open");
        }

        [Test]
        public void WorkThePlayerIsHoldingOutranksWorkTheNightHasNotIssued()
        {
            ServiceHub.Cases.BeginNight(1);

            // Start one case and leave it open; the rest stay Dormant.
            var runtime = ServiceHub.Cases.Find("N1-M01");
            Assert.IsNotNull(runtime);
            ServiceHub.Cases.TryStartCase("N1-M01");

            if (!runtime.State.IsActive())
            {
                // A conversation can defer the start; force it so the test is about the
                // blocker and not about the scheduler.
                runtime.SetState(CaseState.Investigating, GameClock.ShiftStartSecond);
            }

            var blocker = GameLoop.BlockerForRoster(Tonight(1), 1, false);

            Assert.AreEqual(GameLoop.ShiftBlocker.CaseOpen, blocker,
                "the actionable reason has to win - it is the only one the player can do anything about");
        }

        [Test]
        public void ACallerStillBookedForTonightIsScheduledWorkNotOpenWork()
        {
            ServiceHub.Cases.BeginNight(1);

            // Resolve everything, then say a caller is still on the roster.
            foreach (var runtime in Tonight(1))
                runtime.SetState(CaseState.ConsequenceApplied, GameClock.ShiftStartSecond);

            Assert.AreEqual(GameLoop.ShiftBlocker.None,
                            GameLoop.BlockerForRoster(Tonight(1), 1, false));

            Assert.AreEqual(GameLoop.ShiftBlocker.NotYetScheduled,
                            GameLoop.BlockerForRoster(Tonight(1), 1, true),
                            "a caller who has not arrived yet is the roster, not a mistake");
        }

        [Test]
        public void AFinishedNightLetsTheCaretakerGoHome()
        {
            ServiceHub.Cases.BeginNight(1);

            foreach (var runtime in Tonight(1))
                runtime.SetState(CaseState.ConsequenceApplied, GameClock.ShiftStartSecond);

            Assert.AreEqual(GameLoop.ShiftBlocker.None,
                            GameLoop.BlockerForRoster(Tonight(1), 1, false));
        }

        [Test]
        public void ANightWithNothingRosteredIsNotAShift()
        {
            Assert.AreEqual(GameLoop.ShiftBlocker.NotOnShift,
                            GameLoop.BlockerForRoster(new List<CaseRuntime>(), 1, false));
        }

        [Test]
        public void EveryReasonHasSomethingToSay()
        {
            foreach (GameLoop.ShiftBlocker blocker in
                     System.Enum.GetValues(typeof(GameLoop.ShiftBlocker)))
            {
                var key = GameLoop.ShiftBlockerKey(blocker);
                Assert.IsTrue(ServiceHub.Localization.HasKey(key),
                              "no string for " + blocker + " (" + key + ")");
            }
        }

        /// <summary>
        /// The two messages have to be different sentences. If they read the same, splitting
        /// the reasons apart bought nothing.
        /// </summary>
        [Test]
        public void TheTwoKindsOfOutstandingWorkDoNotReadTheSame()
        {
            var open = Loc.T(GameLoop.ShiftBlockerKey(GameLoop.ShiftBlocker.CaseOpen));
            var pending = Loc.T(GameLoop.ShiftBlockerKey(GameLoop.ShiftBlocker.NotYetScheduled));

            Assert.AreNotEqual(open, pending);
        }
    }
}
