using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using NO404.Cases;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// Night 1 run at speed, with the clock held still (GDD 15.7).
    ///
    /// This is the report that produced it: a caretaker cleared the whole shift by 22:05, and
    /// then the building carried on happening at them - the complaint about 304 rang after the
    /// 304 job was filed, and callers kept coming after the task list read six of six.
    ///
    /// Every arrival used to be a timestamp. The times were replaced by shares of the night's
    /// cases and the shares were still handed out as timestamps a few minutes ahead, which is
    /// the same bug wearing the fix's clothes. So the clock is not advanced anywhere in this
    /// test: if a single arrival is still waiting on one, it can never come, and the counts
    /// below will say so.
    /// </summary>
    public sealed class NightOneNoClockTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.ResetPlaythrough();

            // Deliberately no EventBus.Clear(). GameLoop's own subscriptions live on that bus,
            // and clearing it unsubscribes the thing under test: every arrival in this file is
            // released by GameLoop watching CaseStateChangedEvent. With the bus cleared the
            // first version of these tests passed on the stranded-caller safety valve instead
            // of the path a player actually takes, which is the worst way for a test to be
            // green.
            
        }

        static void CloseEveryOpenCase()
        {
            foreach (var runtime in ServiceHub.Cases.AllCases)
            {
                if (runtime.Definition.nightIndex != 1) continue;
                if (runtime.State.IsResolved() || runtime.State == CaseState.Dormant) continue;
                runtime.SetState(CaseState.ConsequenceApplied, ServiceHub.Clock.GameSecond);
            }
        }

        static void JudgeWhoeverIsAtTheDoor()
        {
            int guard = 0;
            while (ServiceHub.Interphone.Active != null && ++guard < 40)
                ServiceHub.Interphone.Grant(Visitors.VisitorAccessLevel.FloorPass);
        }

        static int NightOneDone()
        {
            int done = 0;
            foreach (var runtime in ServiceHub.Cases.AllCases)
                if (runtime.Definition.nightIndex == 1 && runtime.State.IsResolved()) done++;
            return done;
        }

        static int NightOneTotal()
        {
            int total = 0;
            foreach (var runtime in ServiceHub.Cases.AllCases)
                if (runtime.Definition.nightIndex == 1) total++;
            return total;
        }

        [UnityTest]
        public IEnumerator TheWholeShiftCanBeClearedWithoutTheClockMovingAtAll()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "the loop has to exist for the night to be started");

            Assert.IsTrue(loop.DebugStartNight(1), "night 1 would not start");
            yield return null;

            // The radio is talking when night 1 opens, and CaseService holds a case start
            // behind an important conversation. That hold is measured in game seconds, so the
            // clock has to be allowed to run - it simply must not be allowed to matter.
            ServiceHub.Dialogue.End();
            int startedAt = ServiceHub.Clock.GameSecond;

            // Work the shift as fast as it can be worked: close whatever is open, deal with
            // whoever that brings to the door, and go round again. Nothing waits for an hour.
            for (int round = 0; round < 60; round++)
            {
                JudgeWhoeverIsAtTheDoor();
                CloseEveryOpenCase();
                yield return null;
            }

            Assert.AreEqual(NightOneTotal(), NightOneDone(),
                "every job on night 1 has to be reachable at the caretaker's pace: " +
                NightOneDone() + " of " + NightOneTotal());

            // The half the player actually saw: six of six, and the door still going.
            Assert.AreEqual(0, loop.PendingVisitorCount,
                "callers were still queued behind a clock after the night was finished");

            Assert.IsFalse(ServiceHub.Interphone.HasWaitingVisitor,
                "somebody is still at the door after every job was closed");

            // Sixty frames of clock, and no more. If any arrival were still scheduled into
            // the night this could not have finished inside it.
            int elapsed = ServiceHub.Clock.GameSecond - startedAt;
            Assert.Less(elapsed, 5 * 60,
                "the shift took " + (elapsed / 60) + " game minutes, so something waited on the clock");
        }

        [UnityTest]
        public IEnumerator TheNurseArrivesWithTheVacantUnitJobRatherThanAnHourLater()
        {
            var loop = GameLoop.Instance;
            Assert.IsTrue(loop.DebugStartNight(1));
            yield return null;

            ServiceHub.Dialogue.End();

            // C00 first, so the queue reaches C01 - the vacant unit.
            JudgeWhoeverIsAtTheDoor();
            ServiceHub.Cases.Find("C00").SetState(CaseState.ConsequenceApplied,
                                                  ServiceHub.Clock.GameSecond);

            int guard = 0;
            while (!ServiceHub.Cases.Find("C01").State.IsActive() && ++guard < 30)
                yield return null;

            Assert.IsTrue(ServiceHub.Cases.Find("C01").State.IsActive(), "C01 should be the next job");

            // Min-seo is the whole of C02. She has to be here now, not at 22:48.
            Assert.IsTrue(ServiceHub.Interphone.HasWaitingVisitor,
                          "the nurse did not arrive with the job she belongs to");

            JudgeWhoeverIsAtTheDoor();
            yield return null;

            Assert.AreNotEqual(CaseState.Dormant, ServiceHub.Cases.Find("C02").State,
                "night 1's sixth job never opened, so the shift could not be finished");
        }
    }
}
