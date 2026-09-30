using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Endings;

namespace NO404.Tests
{
    /// <summary>
    /// GDD 28.3's smoke test, run by the machine.
    ///
    /// The checklist in the GDD is twenty minutes of a person clicking, and it was never run
    /// on most builds for exactly that reason. What it is really asking is one question - can
    /// a playthrough get from New Game to an ending without stopping - and that question can
    /// be asked every build for a few seconds of CPU.
    ///
    /// This drives the real GameLoop: the real night start, the real services, the real
    /// end-of-shift bookkeeping, and the real ending resolution. It fast-forwards the clock
    /// rather than the flow, so a night that soft-locks still soft-locks here.
    /// </summary>
    public sealed class FullPlaythroughSmokeTests
    {
        /// <summary>Frames to wait for an async step before calling it a hang.</summary>
        const int PatienceFrames = 600;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            while (GameLoop.Instance == null) yield return null;

            // A full playthrough unlocks most of the catalogue. Left connected, this run would
            // push those at whatever app id the developer's Steam client is signed into, and
            // the test would then pass or fail depending on whether Steam was open - which is
            // exactly the kind of result that teaches people to ignore a red test.
            ServiceHub.Steam.ForwardToSteam = false;
        }

        /// <summary>
        /// Puts the building back. A playthrough walks through most of the streamed groups and
        /// leaves them resident, and the next test in the run is entitled to a clean world -
        /// leaving one loaded is how this suite started failing a corridor test it never
        /// touched.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!ServiceHub.Ready) yield break;

            ServiceHub.Steam.ForwardToSteam = true;
            ServiceHub.Zones.UnloadAllStreamed();

            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < PatienceFrames) yield return null;
        }

        [UnityTest]
        public IEnumerator ANewGameReachesAnEndingWithoutStalling()
        {
            var loop = GameLoop.Instance;

            loop.NewGame();
            yield return WaitForMode(loop, GameMode.Playing, "New Game never started a shift");

            Assert.AreEqual(1, ServiceHub.State.NightIndex,
                            "a new game begins in the middle of night 1 (GDD 9.1 cold open)");

            for (int night = 1; night <= GameLoop.FinalNight; night++)
            {
                Assert.AreEqual(night, ServiceHub.State.NightIndex,
                                "night " + night + " did not begin after the one before it");
                Assert.AreEqual(GameMode.Playing, loop.Mode,
                                "night " + night + " is not in a playable state");

                yield return RunShiftToEnd(loop, night);

                if (night < GameLoop.FinalNight)
                {
                    Assert.AreEqual(GameMode.NightSummary, loop.Mode,
                                    "night " + night + " ended without a summary");
                    loop.DebugAdvanceNight();
                    yield return WaitForMode(loop, GameMode.Playing,
                                             "night " + (night + 1) + " never started");
                }
            }

            // The last shift resolves straight into an ending rather than a summary.
            if (loop.Mode == GameMode.NightSummary) loop.DebugAdvanceNight();
            yield return WaitForMode(loop, GameMode.Ending, "the final night never reached an ending");

            Assert.IsNotNull(ServiceHub.Endings.Reached, "an ending was shown but none was resolved");
            Assert.IsNotEmpty(ServiceHub.Endings.Reached.endingId);
        }

        /// <summary>
        /// GDD 11.2 / 14.4: a playthrough that judges nothing at all must still finish. This
        /// is the fail-safe path, and it is the one a player actually hits by walking away
        /// from the desk - so it gets its own run rather than being assumed from the one above.
        /// </summary>
        [UnityTest]
        public IEnumerator APlaythroughThatDecidesNothingStillEnds()
        {
            var loop = GameLoop.Instance;

            loop.NewGame();
            yield return WaitForMode(loop, GameMode.Playing, "New Game never started a shift");

            for (int night = 1; night <= GameLoop.FinalNight; night++)
            {
                yield return RunShiftToEnd(loop, night);

                if (loop.Mode != GameMode.NightSummary) break;
                loop.DebugAdvanceNight();
                yield return null;
            }

            yield return WaitForMode(loop, GameMode.Ending,
                                     "doing nothing for seven nights left the game with nowhere to go");

            Assert.IsNotNull(ServiceHub.Endings.Reached,
                             "GDD 11.2: a wrong or absent judgement must never leave the run without an ending");
        }

        /// <summary>
        /// Pushes the clock to 06:00 and lets GameLoop notice on its own. The night is not
        /// skipped - every service still ticks the frames around the transition.
        /// </summary>
        static IEnumerator RunShiftToEnd(GameLoop loop, int night)
        {
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftEndSecond);

            int frames = 0;
            while (loop.Mode == GameMode.Playing)
            {
                if (++frames > PatienceFrames)
                    Assert.Fail("night " + night + " never ended; the shift is stuck at " +
                                ServiceHub.Clock.GameSecond);

                // The clock is reset by StartNight, so re-asserting it is harmless and keeps
                // a night that rewinds its own clock from spinning here forever.
                ServiceHub.Clock.SetGameSecond(GameClock.ShiftEndSecond);
                yield return null;
            }
        }

        static IEnumerator WaitForMode(GameLoop loop, GameMode expected, string failure)
        {
            int frames = 0;
            while (loop.Mode != expected)
            {
                if (++frames > PatienceFrames) Assert.Fail(failure + " (mode is " + loop.Mode + ")");
                yield return null;
            }
        }
    }
}
