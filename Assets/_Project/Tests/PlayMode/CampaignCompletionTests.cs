using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using NO404.Cases;
using NO404.Core;
using NO404.Endings;

namespace NO404.Tests
{
    /// <summary>
    /// The whole campaign, start to ending (v5.0 30.4).
    ///
    /// Every other test in this project checks one piece. None of them answers the only
    /// question that matters to somebody sitting down to play: can you get from the first
    /// night to an ending at all? A campaign can be made entirely of passing tests and still
    /// have a night that never hands out its work, a main that cannot be closed, or a final
    /// quest that leaves the shift unable to end - and each of those looks like a working
    /// build until somebody spends an hour finding out.
    ///
    /// So this walks all six nights the way the game does: start the night, resolve what it
    /// drew, clock off, advance. If any night cannot be finished the test says which one and
    /// why, rather than leaving it to be discovered in play.
    /// </summary>
    public sealed class CampaignCompletionTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.ResetPlaythrough();

            // DebugStartNight refuses unless the console is enabled, which it is in the editor
            // and in a development build - the two places this test can run.
            Assert.IsTrue(DevConsole.Enabled, "the campaign walk needs the debug hooks");
        }

        /// <summary>
        /// Listens to whatever the night opens with, the way a player does.
        ///
        /// Night 1 starts the radio transmission, and CaseService holds a case back while a
        /// conversation is blocking - correctly, since dropping a task card over somebody
        /// mid-sentence is what that rule exists to prevent. A test that never advances the
        /// dialogue is a test that never lets night 1 start, which is exactly how the first
        /// version of this file passed while night 1's main had silently never run.
        /// </summary>
        static void ListenToWhateverIsTalking()
        {
            for (int guard = 0; guard < 64 && ServiceHub.Dialogue.IsActive; guard++)
            {
                var before = ServiceHub.Dialogue.CurrentLine;
                ServiceHub.Dialogue.Advance();

                // A node with choices does not advance on its own; the campaign walk is not
                // testing dialogue branches, so it takes the conversation's own exit.
                if (ServiceHub.Dialogue.IsActive && ServiceHub.Dialogue.CurrentLine == before)
                {
                    ServiceHub.Dialogue.End();
                    return;
                }
            }
        }

        /// <summary>
        /// Closes every case the night drew, by picking each one's first decision.
        ///
        /// Deliberately the first rather than the best: a campaign that can only be completed
        /// by playing well is a campaign with a dead end in it for everybody else.
        /// </summary>
        static void ResolveEverythingDrawn(int night, List<string> log)
        {
            for (int guard = 0; guard < 32; guard++)
            {
                ServiceHub.Cases.Tick();

                CaseRuntime open = null;
                foreach (var runtime in ServiceHub.Cases.AllCases)
                {
                    if (runtime.Definition.nightIndex != night) continue;
                    if (!runtime.State.IsActive()) continue;
                    open = runtime;
                    break;
                }

                if (open == null) return;

                var definition = open.Definition;
                Assert.Greater(definition.decisions.Length, 0,
                    "night " + night + ": " + definition.caseId + " can be opened and never closed");

                // Everything the shift owns, so an evidence requirement cannot be the thing
                // that makes the campaign impossible.
                foreach (var id in definition.evidenceIds)
                    ServiceHub.Evidence.Acquire(id, Evidence.EvidenceSource.FailSafe);

                foreach (var objective in definition.objectives)
                    ServiceHub.Cases.TryAdvanceObjective(definition.caseId, objective.objectiveId);

                var attached = new List<string>();
                foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);

                var result = ServiceHub.Cases.SubmitDecision(definition.caseId,
                                                             definition.decisions[0].decisionId,
                                                             attached);

                Assert.IsTrue(result.Accepted,
                    "night " + night + ": " + definition.caseId + " refused its own first decision (" +
                    result.RejectReason + ")");

                log.Add(definition.caseId);
                open.SetState(CaseState.ConsequenceApplied, ServiceHub.Clock.GameSecond);
            }

            Assert.Fail("night " + night + " never ran out of work");
        }

        /// <summary>
        /// DebugStartNight refuses until the building exists, so the campaign is opened the
        /// way a player opens it - minus the intro, which is fifteen seconds of nothing to a
        /// test.
        /// </summary>
        static IEnumerator BuildTheWorld(GameLoop loop)
        {
            loop.NewGame(false);

            float deadline = UnityEngine.Time.realtimeSinceStartup + 20f;
            while (!loop.WorldBuilt && UnityEngine.Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(loop.WorldBuilt, "the world never finished building");
        }

        [UnityTest]
        public IEnumerator EveryNightCanBeFinishedAndTheCampaignReachesAnEnding()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            yield return BuildTheWorld(loop);

            var resolved = new List<string>();
            var sanByNight = new List<string>();

            for (int night = 1; night <= GameLoop.FinalNight; night++)
            {
                Assert.IsTrue(loop.DebugStartNight(night), "night " + night + " would not start");
                yield return null;

                ListenToWhateverIsTalking();
                Assert.AreEqual(night, ServiceHub.State.NightIndex);

                // v5.0 4.1: the night's spine is always dealt.
                CollectionAssert.Contains(new List<string>(ServiceHub.Cases.DrawnTonight),
                                          "N" + night + "-M01",
                                          "night " + night + " did not deal its main quest");

                int sanBefore = ServiceHub.Vitals.San;
                ResolveEverythingDrawn(night, resolved);
                sanByNight.Add("N" + night + ":" + (ServiceHub.Vitals.San - sanBefore));
                yield return null;

                // Nothing the caretaker is holding should still be blocking the door.
                Assert.AreNotEqual(GameLoop.ShiftBlocker.CaseOpen, loop.CurrentShiftBlocker,
                                   "night " + night + " cannot be clocked off: work is still open");
            }

            Assert.AreEqual(GameLoop.FinalNight, ServiceHub.State.NightIndex);

            // Every night's spine, not just the last one. Checking only N6 let a campaign pass
            // in which nights had quietly handed out nothing - the missing quests take their
            // consequences with them, and the ending is made of those consequences.
            for (int night = 1; night <= GameLoop.FinalNight; night++)
                CollectionAssert.Contains(resolved, "N" + night + "-M01",
                                          "night " + night + " never closed its main quest");

            // What six nights of first-choice decisions actually cost, pinned as numbers.
            //
            // Asserting only the letter hid the arithmetic: a grade is HP and SAN, and a test
            // that never states them cannot tell a correct grade from a grade reached because
            // half the consequences quietly did not apply.
            int hp = ServiceHub.Vitals.Hp;
            int san = ServiceHub.Vitals.San;

            var ending = ServiceHub.Endings.Evaluate();
            Assert.IsNotNull(ending, "the campaign finished and no ending was reachable");

            Assert.AreEqual(EndingService.GradeFor(hp, san), ending.endingId,
                            "the ending disagrees with the HP/SAN it was supposedly graded on");

            Assert.AreEqual(100, hp,
                            "no first-choice decision on any night costs health, so HP should be full");

            // Six mains, first decision each, cost -3 -2 -2 -8 -8 -5 by v5.0 10-15's own
            // tables. Night 6 then ends the moment its main closes, and a shift that ends
            // gives seven back (v5.0 7.3) - so 100 - 28 + 7.
            //
            // Only one recovery lands here because this walk jumps between nights rather than
            // clocking off each one; a played campaign collects five more. Which is the point
            // worth keeping: even the harshest reading of the current content leaves a
            // caretaker who takes no physical risk comfortably above the gate.
            CollectionAssert.AreEqual(new[] { "N1:-3", "N2:-2", "N3:-2", "N4:-8", "N5:-8", "N6:-5" },
                                      sanByNight,
                                      "the SAN cost of a night's main changed");

            Assert.AreEqual(100 - 28 + VitalService.NightEndCalm, san,
                            "the six mains and one end-of-shift recovery should leave SAN here");
        }

        /// <summary>
        /// The same six nights, walked by somebody the building has worn down.
        ///
        /// v5.0 8.1's gate is the one part of the ending that can make a finished campaign
        /// produce no ending at all, so it is worth proving it fires rather than trusting the
        /// arithmetic to have been wired to something.
        /// </summary>
        [UnityTest]
        public IEnumerator ACaretakerBelowTheGateFinishesTheWorkAndGetsNoEnding()
        {
            var loop = GameLoop.Instance;
            yield return BuildTheWorld(loop);

            var resolved = new List<string>();

            for (int night = 1; night <= GameLoop.FinalNight; night++)
            {
                Assert.IsTrue(loop.DebugStartNight(night));
                yield return null;
                ListenToWhateverIsTalking();
                ResolveEverythingDrawn(night, resolved);
            }

            // Below thirty SAN at the final reveal (v5.0 8.1).
            ServiceHub.Vitals.Strain(ServiceHub.Vitals.San - 20, "test");
            Assert.AreEqual(EndingGate.MentalBreakdown, ServiceHub.Vitals.Gate);

            var ending = ServiceHub.Endings.Evaluate();
            Assert.IsNotNull(ending);
            Assert.AreEqual(EndingIds.NoEnding, ending.endingId,
                            "finishing the work does not by itself earn an ending");
        }
    }
}
