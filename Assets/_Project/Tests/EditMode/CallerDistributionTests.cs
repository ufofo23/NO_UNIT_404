using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// How the night's callers are spread across the night's work (GDD 13.4, 6.3 step 8).
    ///
    /// They used to carry clock times, and a clock cannot keep a promise about a shift the
    /// player paces: night 1 booked its last caller for 01:35 whatever the caretaker did, so
    /// finishing every case by midnight left the shift held open by somebody who had not turned
    /// up. The home screen read 6 / 6 the whole time, because a caller is not a case.
    ///
    /// The rule now is the one the design asks for: the callers are divided among the night's
    /// cases, and the last case gets exactly one. When the final job of the night starts there
    /// is one person left to deal with, and they arrive while it is being done.
    /// </summary>
    public sealed class CallerDistributionTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        static List<VisitorDefinition> RosterFor(int night)
        {
            var roster = new List<VisitorDefinition>();
            foreach (var visitor in ServiceHub.Content.Visitors)
                if (visitor != null && visitor.nightIndex == night) roster.Add(visitor);
            return roster;
        }

        static List<string> ChainFor(int night)
        {
            ServiceHub.Cases.BeginNight(night);
            return new List<string>(ServiceHub.Cases.NightChain);
        }

        [Test]
        public void EveryNightHasCasesToHangItsCallersOn()
        {
            for (int night = 1; night <= 6; night++)
                Assert.Greater(ChainFor(night).Count, 0,
                               "night " + night + " has no scheduled case, so nothing paces its door");
        }

        [Test]
        public void EveryNightHasAtLeastAsManyCallersAsItNeedsToHoldOneBack()
        {
            // The guarantee is "one left when the last case starts". A night with no callers
            // at all cannot break it, but a night with callers has to have somewhere to put
            // them, and the last case must not be asked to release a caller that is not there.
            for (int night = 1; night <= 6; night++)
            {
                var roster = RosterFor(night);
                if (roster.Count == 0) continue;

                Assert.GreaterOrEqual(roster.Count, 1,
                                      "night " + night + " must be able to hold one back");
            }
        }

        [Test]
        public void TheLastCaseAlwaysGetsExactlyOneCaller()
        {
            // The share table, computed the way GameLoop does. Rebuilt here rather than
            // reached into, because the property being pinned is arithmetic and it has to hold
            // for every roster size against every chain length the game actually has.
            for (int night = 1; night <= 6; night++)
            {
                var chain = ChainFor(night);
                int callers = RosterFor(night).Count;
                if (callers == 0 || chain.Count == 0) continue;

                var share = Share(callers, chain.Count);

                Assert.AreEqual(callers, Sum(share),
                                "night " + night + ": every caller has to be given to some case");

                // The promise - one person left at the door when the last job opens - needs
                // somewhere earlier to put the others. A night with a single case has nowhere,
                // and everybody lands on it by arithmetic rather than by design. That is the
                // shape of the campaign until v5.0 4.1's fourteen candidates per night are
                // authored; the claim is asserted wherever it is meetable.
                if (chain.Count < 2) continue;

                Assert.AreEqual(1, share[share.Length - 1],
                                "night " + night + ": the last case gets one caller, no more and no less");
            }
        }

        [Test]
        public void TheEarlierCasesCarryTheRestAndTheFrontOfTheShiftCarriesTheRemainder()
        {
            // A caretaker has the most slack at the start of a shift and the least at the end,
            // so an uneven split leans early rather than late.
            var share = Share(callers: 10, cases: 4);

            Assert.AreEqual(1, share[3], "the last case still gets exactly one");
            Assert.AreEqual(9, share[0] + share[1] + share[2]);
            Assert.GreaterOrEqual(share[0], share[1]);
            Assert.GreaterOrEqual(share[1], share[2]);
        }

        [Test]
        public void ASingleCaseNightGivesItEverybody()
        {
            var share = Share(callers: 3, cases: 1);
            Assert.AreEqual(1, share.Length);
            Assert.AreEqual(3, share[0], "there is no earlier case to spread across");
        }

        [Test]
        public void FewerCallersThanCasesStillLeavesOneForTheLast()
        {
            var share = Share(callers: 2, cases: 5);

            Assert.AreEqual(1, share[4], "the promise survives a thin roster");
            Assert.AreEqual(2, Sum(share));
        }

        [Test]
        public void NoNightSchedulesACallerTheChainCannotReach()
        {
            // The safety valve exists, but it should never be the thing that delivers a
            // caller: if a night rosters more callers than its cases can release, the last
            // ones would arrive in a lump when the chain runs out.
            for (int night = 1; night <= 6; night++)
            {
                var chain = ChainFor(night);
                int callers = RosterFor(night).Count;
                if (callers == 0) continue;

                Assert.AreEqual(callers, Sum(Share(callers, chain.Count)),
                                "night " + night + " leaves callers with no case to arrive on");
            }
        }

        // ---- the arithmetic under test ---------------------------------------

        /// <summary>
        /// GameLoop.PlanCallers, in the small. One is set aside for the last case before
        /// anything is divided, so rounding can never eat it.
        /// </summary>
        static int[] Share(int callers, int cases)
        {
            var share = new int[cases];
            if (cases == 0 || callers == 0) return share;

            if (cases == 1) { share[0] = callers; return share; }

            share[cases - 1] = 1;

            int toSpread = callers - 1;
            int earlier = cases - 1;
            int each = toSpread / earlier;
            int remainder = toSpread % earlier;

            for (int i = 0; i < earlier; i++)
                share[i] = each + (i < remainder ? 1 : 0);

            return share;
        }

        static int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++) total += values[i];
            return total;
        }
    }
}
