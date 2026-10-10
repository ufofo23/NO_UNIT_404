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
            MainOnlyMode.Set(false);
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainOnlyMode.Set(false);
            yield return null;
        }

        /// <summary>
        /// Closes every case the night drew, by picking the first decision the night allows.
        ///
        /// Deliberately the first rather than the best: a campaign that can only be completed
        /// by playing well is a campaign with a dead end in it for everybody else. "Allows"
        /// matters since v5.1: a report row can depend on what was done at the door, and a
        /// looping corridor has to be walked out of before it can be reported - which the walk
        /// does the way a player would, by reading the marker.
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

                if (SubquestRules.ReportBlockedKey(definition.caseId) != null)
                    SubquestRules.Act(definition.caseId == "N3-R08" ? "n3r08_marker" : "n5r10_marker");

                var result = DecisionResult.Rejected("no decisions");
                for (int d = 0; d < definition.decisions.Length && !result.Accepted; d++)
                    result = ServiceHub.Cases.SubmitDecision(definition.caseId,
                                                             definition.decisions[d].decisionId, attached);

                Assert.IsTrue(result.Accepted,
                    "night " + night + ": " + definition.caseId + " refused every decision (" +
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

        /// <summary>
        /// The spine alone, so the arithmetic below measures the mains and nothing the draw
        /// added on top. The whole nights are walked in the test after this one.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryNightCanBeFinishedAndTheCampaignReachesAnEnding()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            MainOnlyMode.Set(true);
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

            // Six mains, first decision each, cost -3 -2 -2 -8 -13 -5. v5.1 adds
            // five SAN when Park's final rescue is cross-verified. Night 6 closes, and the shift
            // gives seven back (v5.0 7.3) - so 100 - 33 + 7.
            //
            // Only one recovery lands here because this walk jumps between nights rather than
            // clocking off each one; a played campaign collects five more. Which is the point
            // worth keeping: even the harshest reading of the current content leaves a
            // caretaker who takes no physical risk comfortably above the gate.
            CollectionAssert.AreEqual(new[] { "N1:-3", "N2:-2", "N3:-2", "N4:-8", "N5:-13", "N6:-5" },
                                      sanByNight,
                                      "the SAN cost of a night's main changed");

            Assert.AreEqual(100 - 33 + VitalService.NightEndCalm, san,
                            "the six mains and one end-of-shift recovery should leave SAN here");
        }

        /// <summary>
        /// v5.1 4.1: nights 1, 3 and 5 played with their whole draw - main, story subquest and
        /// three or four randoms - and every one of them closes.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryWholeNightCanBeFinished()
        {
            var loop = GameLoop.Instance;
            yield return BuildTheWorld(loop);

            var resolved = new List<string>();
            foreach (int night in new[] { 1, 2, 3, 4, 5, 6 })
            {
                Assert.IsTrue(loop.DebugStartNight(night), "night " + night + " would not start");
                yield return null;
                ListenToWhateverIsTalking();

                var drawn = new List<string>(ServiceHub.Cases.DrawnTonight);
                if (night % 2 == 1)
                    Assert.That(drawn.Count, Is.InRange(5, 6), "night " + night + " is not a v5.1 night: " +
                                                                  string.Join(",", drawn.ToArray()));

                ResolveEverythingDrawn(night, resolved);
                yield return null;

                foreach (var id in drawn)
                    CollectionAssert.Contains(resolved, id, "night " + night + " left " + id + " open");
                Assert.AreNotEqual(GameLoop.ShiftBlocker.CaseOpen, loop.CurrentShiftBlocker,
                                   "night " + night + " cannot be clocked off");
            }

            CollectionAssert.Contains(resolved, "N1-R01");
            CollectionAssert.Contains(resolved, "N3-R12");
            CollectionAssert.Contains(resolved, "N5-R13");
            Assert.Greater(ServiceHub.Vitals.Hp, 0, "the walk took no risks it could not survive");
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
