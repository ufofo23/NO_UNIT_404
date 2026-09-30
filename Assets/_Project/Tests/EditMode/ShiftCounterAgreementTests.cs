using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// The task counter and the clock-off button, read side by side (GDD 6.3 step 8).
    ///
    /// They sit on the same screen, a few centimetres apart, and they are computed by two
    /// different pieces of code over the same list. When they disagree the caretaker is looking
    /// at "6 / 6" above a button that refuses, and there is nothing on the screen that
    /// reconciles them - no card to open, no task to finish, no way to find out what is wrong.
    ///
    /// ShiftBlockerTests holds the button's own behaviour. These hold the agreement.
    /// </summary>
    public sealed class ShiftCounterAgreementTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        static List<CaseRuntime> Tonight(int night)
        {
            var list = new List<CaseRuntime>();
            foreach (var runtime in ServiceHub.Cases.AllCases)
                if (runtime.Definition.nightIndex == night) list.Add(runtime);
            return list;
        }

        /// <summary>Counted exactly the way PcShellView prints it.</summary>
        static void CountAsTheScreenDoes(IEnumerable<CaseRuntime> cases, int night,
                                         out int done, out int total)
        {
            done = 0;
            total = 0;
            foreach (var runtime in cases)
            {
                if (runtime.Definition.nightIndex != night) continue;
                total++;
                if (runtime.State.IsResolved()) done++;
            }
        }

        [Test]
        public void EveryCaseStateReadsTheSameToTheCounterAndToTheButton()
        {
            // Walked state by state rather than argued about, because the pair that broke it
            // would be a pair nobody thought to consider. Available is the interesting one: it
            // is neither resolved nor Dormant, so the button counts it as work the caretaker
            // is holding - and the counter has to agree that it is not done.
            ServiceHub.Cases.BeginNight(1);
            var tonight = Tonight(1);
            Assert.Greater(tonight.Count, 0, "night 1 should roster something");

            var probe = tonight[0];

            foreach (CaseState state in System.Enum.GetValues(typeof(CaseState)))
            {
                // Everything else finished, so the verdict is about this one case.
                for (int i = 1; i < tonight.Count; i++)
                    tonight[i].SetState(CaseState.ConsequenceApplied, GameClock.ShiftStartSecond);
                probe.SetState(state, GameClock.ShiftStartSecond);

                int done, total;
                CountAsTheScreenDoes(tonight, 1, out done, out total);
                bool screenSaysFinished = done == total;

                bool buttonSaysFinished =
                    GameLoop.BlockerForRoster(tonight, 1, false) == GameLoop.ShiftBlocker.None;

                Assert.AreEqual(screenSaysFinished, buttonSaysFinished,
                    "state " + state + ": the screen says " +
                    (screenSaysFinished ? "6/6" : "not finished") + " and the button says " +
                    (buttonSaysFinished ? "you may go" : "you may not"));
            }
        }

        [Test]
        public void AFullCounterWithACallerStillDueIsTheOneCaseTheyDisagreeOnByDesign()
        {
            // And it is the reported symptom. Every task closed, the counter full, and the
            // shift still open because what is holding it was never a task: a caller the night
            // has booked and not yet sent to the door.
            //
            // The disagreement is correct - a caretaker who leaves at 23:00 abandons four
            // people at the interphone - so what has to be true is that the button says which
            // of the two it is, rather than blaming the task list.
            ServiceHub.Cases.BeginNight(1);
            var tonight = Tonight(1);

            foreach (var runtime in tonight)
                runtime.SetState(CaseState.ConsequenceApplied, GameClock.ShiftStartSecond);

            int done, total;
            CountAsTheScreenDoes(tonight, 1, out done, out total);
            Assert.AreEqual(total, done, "the screen should read a full count");

            var blocker = GameLoop.BlockerForRoster(tonight, 1, anyPendingVisitor: true);

            Assert.AreEqual(GameLoop.ShiftBlocker.NotYetScheduled, blocker);
            Assert.AreNotEqual(GameLoop.ShiftBlockerKey(GameLoop.ShiftBlocker.CaseOpen),
                               GameLoop.ShiftBlockerKey(blocker),
                               "it must not accuse the task list the caretaker has just emptied");
        }
    }

    /// <summary>
    /// Night 1 as it is actually rostered.
    ///
    /// A case that can never be issued, or one whose window opens after the shift is over,
    /// holds the night open with nothing the player can do - and the task counter would never
    /// show it, because a Dormant case counts toward the total.
    /// </summary>
    public sealed class NightOneRosterTests
    {
        static List<CaseDefinition> NightOneCases()
        {
            var found = new List<CaseDefinition>();
            foreach (var definition in SeedContent.BuildCases())
                if (definition.nightIndex == 1) found.Add(definition);
            foreach (var definition in SeedContent.BuildLateCases())
                if (definition.nightIndex == 1) found.Add(definition);
            return found;
        }

        [Test]
        public void EveryCaseRosteredForNightOneCanBeClosed()
        {
            foreach (var definition in NightOneCases())
            {
                Assert.IsNotNull(definition.decisions, definition.caseId + " has no decisions");
                Assert.Greater(definition.decisions.Length, 0,
                               definition.caseId + " can be opened and never closed");
            }
        }

        [Test]
        public void NoNightOneCaseIsScheduledAfterTheShiftEnds()
        {
            foreach (var definition in NightOneCases())
                Assert.Less(definition.startWindowBegin, GameClock.ShiftEndSecond,
                            definition.caseId + " opens after 06:00, so it could never be issued");
        }

        [Test]
        public void NightOneRostersTheSixTheCounterPromises()
        {
            // Not a magic number for its own sake: the count the player sees is the count of
            // cases carrying nightIndex 1, so if that ever changes the counter changes with it
            // and this is the line that says so out loud.
            Assert.AreEqual(6, NightOneCases().Count,
                            "the home screen reads N/6 on night 1; the roster has to match");
        }
    }
}
