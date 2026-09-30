using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// The night is a queue, not a timetable (GDD 15.7).
    ///
    /// Authored start times sort the night's work and are not supposed to gate it: a case
    /// begins when the one before it is closed, so a caretaker who works quickly meets the
    /// whole shift quickly. What these hold is that the clock genuinely has no say - the last
    /// job of night 1 carries an authored slot of 00:10, and a player who has finished
    /// everything by 22:40 must have it in front of them at 22:40.
    /// </summary>
    public sealed class NightChainPacingTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        static CaseRuntime Runtime(string caseId) { return ServiceHub.Cases.Find(caseId); }

        /// <summary>Close a case the way the chain cares about, without going through a decision.</summary>
        static void Close(string caseId)
        {
            Runtime(caseId).SetState(CaseState.ConsequenceApplied, ServiceHub.Clock.GameSecond);
        }

        /// <summary>
        /// The chain is the night's timed work, in the order it will be handed out.
        ///
        /// Asserted as a property of whatever the night drew rather than against a list of
        /// case ids. The old version named night 1's five cases; v5.0 draws a different night
        /// every campaign, so a test that knows the answer in advance can only be wrong.
        /// </summary>
        [Test]
        public void TheChainIsOrderedAndContainsOnlyTonightsWork()
        {
            ServiceHub.Cases.BeginNight(1);

            var chain = new List<string>(ServiceHub.Cases.NightChain);
            Assert.Greater(chain.Count, 0, "night 1 has no timed work at all");

            int previousWindow = int.MinValue;
            foreach (var id in chain)
            {
                var definition = ServiceHub.Content.FindCase(id);
                Assert.IsNotNull(definition, id + " is in the chain but not in the content");
                Assert.AreEqual(1, definition.nightIndex, id + " belongs to another night");
                Assert.AreEqual(CaseTrigger.Time, definition.trigger, id + " is not timed work");

                Assert.GreaterOrEqual(definition.startWindowBegin, previousWindow,
                                      "the chain is out of order at " + id);
                previousWindow = definition.startWindowBegin;
            }

            // v5.0 4.1: the night's spine is always in it.
            CollectionAssert.Contains(chain, "N1-M01");
        }

        /// <summary>
        /// Finishing one job brings the next forward, whatever the hour (GDD 6.3).
        ///
        /// Walks the chain the night actually drew. With a thin pool this is a short walk;
        /// the claim it makes does not depend on how long the chain is, which is the point of
        /// writing it this way rather than against five named cases.
        /// </summary>
        [Test]
        public void EachJobArrivesTheMomentTheOneBeforeItIsClosed()
        {
            ServiceHub.Cases.BeginNight(1);
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond);

            int openedAt = ServiceHub.Clock.GameSecond;
            var chain = new List<string>(ServiceHub.Cases.NightChain);

            for (int i = 0; i < chain.Count; i++)
            {
                ServiceHub.Cases.Tick();

                Assert.IsTrue(Runtime(chain[i]).State.IsActive(),
                    chain[i] + " should be in the caretaker's hands once the one before it is closed");

                Close(chain[i]);
            }

            Assert.AreEqual(openedAt, ServiceHub.Clock.GameSecond,
                "and none of it should have taken a single game second of waiting");
        }

        /// <summary>The other half of a queue: nothing arrives out of turn.</summary>
        [Test]
        public void NoCaseStartsBeforeTheOneAheadOfItIsDone()
        {
            ServiceHub.Cases.BeginNight(1);
            ServiceHub.Cases.Tick();

            var chain = new List<string>(ServiceHub.Cases.NightChain);
            Assert.IsTrue(Runtime(chain[0]).State.IsActive(), "the head of the chain opens the shift");

            for (int i = 1; i < chain.Count; i++)
                Assert.AreEqual(CaseState.Dormant, Runtime(chain[i]).State,
                                chain[i] + " should still be waiting its turn");
        }

        [Test]
        public void EveryNightIsAQueueRatherThanAClock()
        {
            // Not only night 1. A shift that finished early on any night should hand over its
            // remaining work rather than making the caretaker wait for an authored hour.
            for (int night = 1; night <= 6; night++)
            {
                ServiceHub.ResetPlaythrough();
                ServiceHub.Cases.BeginNight(night);
                ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond);

                var chain = new List<string>(ServiceHub.Cases.NightChain);
                if (chain.Count < 2) continue;

                for (int i = 0; i < chain.Count - 1; i++)
                {
                    ServiceHub.Cases.Tick();
                    Close(chain[i]);
                }
                ServiceHub.Cases.Tick();

                var last = Runtime(chain[chain.Count - 1]);
                Assert.IsTrue(last.State.IsActive() || last.State.IsResolved(),
                    "night " + night + ": " + last.CaseId + " never arrived, so the shift " +
                    "could not be finished at the pace the caretaker set");
                Assert.AreEqual(GameClock.ShiftStartSecond, ServiceHub.Clock.GameSecond,
                    "night " + night + " should not have needed the clock at all");
            }
        }
    }
}
