using NUnit.Framework;
using NO404.Anomalies;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Dialogue;
using NO404.Gameplay;
using NO404.Threat;

namespace NO404.Tests
{
    /// <summary>
    /// The chains v2.1 spec 30.3 calls out by name, each checked at the point where it used to
    /// break.
    ///
    /// The failure these exist for is not a crash. It is a flag that is set correctly, saved
    /// correctly, loaded correctly, and read by nothing - which is what four of these were.
    /// Every night still played, every number still moved, and none of it reached the world.
    /// So each test below pins the *reading* end: something the player can be standing in
    /// front of that is different because of what they did three nights ago.
    /// </summary>
    public sealed class PersistenceChainTests
    {
        // ---- M08 -> the way out of a burning building (spec 21.7) ------------

        [Test]
        public void SolvingM08OpensTheCourtyardAsAWayOut()
        {
            Assert.IsTrue(ThreatService.IsFireExit(ZoneIds.Playground, courtyardSafe: true));
            Assert.IsFalse(ThreatService.IsFireExit(ZoneIds.Playground, courtyardSafe: false));

            // The two that are always exits, whatever happened on the playground.
            Assert.IsTrue(ThreatService.IsFireExit(ZoneIds.Lobby, courtyardSafe: false));
            Assert.IsTrue(ThreatService.IsFireExit(ZoneIds.Office, courtyardSafe: false));
        }

        [Test]
        public void IgnoringM08MakesTheEscapeShorter()
        {
            int safe = ThreatService.FireEscapeWindowFor(true, false, 0);
            int unsafeCourtyard = ThreatService.FireEscapeWindowFor(false, false, 0);

            Assert.AreEqual(ThreatService.FireEscapeSeconds, safe);
            Assert.AreEqual(safe - ThreatService.CourtyardPenaltySeconds, unsafeCourtyard);
        }

        [Test]
        public void M08SetsTheFlagOnBothOutcomesSoTheChainIsNeverSilent()
        {
            // A flag left unset would read as "not safe" and give the right answer by
            // accident. Spec 21.7 assigns it explicitly either way, and the difference
            // matters the moment something else starts reading it too.
            var m08 = FindEvent(ManualEventIds.M08_ShadowChildren);

            Assert.IsTrue(Sets(m08.onCorrect, FlagIds.CourtyardEscapeSafe, true));
            Assert.IsTrue(Sets(m08.onWrong, FlagIds.CourtyardEscapeSafe, false));
        }

        // ---- T01 -> the fire lane, four nights later (spec 30.3) -------------

        [Test]
        public void IgnoringTheParkedCarLeavesItThereForTheRestOfTheGame()
        {
            var t01 = FindCase("T01");
            var ignore = t01.FindDecision("dec_ignore");
            Assert.IsNotNull(ignore, "T01 has no way to be got wrong");

            bool blocksTheLane = false;
            foreach (var c in ignore.consequences)
            {
                if (c == null || c.type != ConsequenceType.SetFlag) continue;
                if (c.targetId != FlagIds.FireLaneBlocked || !c.boolValue) continue;

                // The whole point is that it is not tonight's problem.
                Assert.IsTrue(c.nextNight, "the car should still be there tomorrow, not tonight");
                blocksTheLane = true;
            }

            Assert.IsTrue(blocksTheLane, "spec 30.3: T01 wrong has to block the fire lane");
        }

        [Test]
        public void ABlockedFireLaneCostsTimeOnTheLastNight()
        {
            int clear = ThreatService.FireEscapeWindowFor(true, false, 0);
            int blocked = ThreatService.FireEscapeWindowFor(true, true, 0);

            Assert.AreEqual(clear - ThreatService.FireLanePenaltySeconds, blocked);
        }

        [Test]
        public void NothingCanTakeTheLastFiveMinutesAway()
        {
            // Spec 24.2: a wrong answer must never close the main line off. Every penalty is
            // real; all of them together still leave an escape that can be made.
            int worst = ThreatService.FireEscapeWindowFor(false, true, 99);
            Assert.AreEqual(ThreatService.MinimumFireEscapeSeconds, worst);
            Assert.Greater(worst, 0);
        }

        // ---- M09 -> the evacuation (spec 30.3) -------------------------------

        [Test]
        public void ForcingThePumpStopMakesTheEvacuationHarder()
        {
            const int healthy = 80;
            Assert.AreEqual(ThreatService.BaseEvacuationHands,
                            ThreatService.EvacuationHandsRequired(false, healthy));
            Assert.AreEqual(ThreatService.BaseEvacuationHands + 1,
                            ThreatService.EvacuationHandsRequired(true, healthy));
        }

        [Test]
        public void TheHelpEarnedOnEarlierNightsIsWhatGetsPeopleOut()
        {
            const int healthy = 80;
            int enough = ThreatService.EvacuationHandsRequired(false, healthy);

            Assert.IsTrue(ThreatService.EvacuationSucceeds(true, enough, false, healthy));
            Assert.IsFalse(ThreatService.EvacuationSucceeds(true, enough - 1, false, healthy));

            // The same hands are no longer enough once the water has gone.
            Assert.IsFalse(ThreatService.EvacuationSucceeds(true, enough, true, healthy));
        }

        [Test]
        public void ChoosingNotToEvacuateIsJudgedOnTheBuildingRatherThanOnHelpers()
        {
            // Spec 7.4 option C is not a failed evacuation. With no order given, no number of
            // willing neighbours is the question - the state of the building is.
            Assert.IsTrue(ThreatService.EvacuationSucceeds(false, 0, false, 80));
            Assert.IsFalse(ThreatService.EvacuationSucceeds(false, 4, false,
                                                           ThreatService.BlockedStairsSafety));
        }

        // ---- M03 -> what Ji-woo saw (spec 21.3) ------------------------------

        [Test]
        public void PostingTomorrowsNoticeChangesWhatJiwooHasToOffer()
        {
            var conversation = FindDialogue("D_N3_JIWOO_ELEVATOR");
            var node = conversation.FindNode("detail");
            Assert.IsNotNull(node, "Ji-woo has no detail node");

            var recording = FindChoice(node, "ask_recording");
            var corridor = FindChoice(node, "ask_corridor");

            Assert.IsNotNull(recording, "the recording line is missing");
            Assert.IsNotNull(corridor, "spec 21.3 needs a line for the night she never looked");

            // Exactly one of them can be on screen, and which one is the whole chain.
            Assert.IsTrue(Requires(recording, FlagIds.JiwooExposurePrevented, false),
                          "she should only offer a recording of a floor she actually saw");
            Assert.IsTrue(Requires(corridor, FlagIds.JiwooExposurePrevented, true),
                          "the corridor remark belongs to the night the notice went up");
        }

        [Test]
        public void SolvingM03PaysWhatSpec213Says()
        {
            var m03 = FindEvent(ManualEventIds.M03_TomorrowsLog);

            Assert.IsTrue(Sets(m03.onCorrect, FlagIds.JiwooExposurePrevented, true));
            Assert.AreEqual(2, StatDelta(m03.onCorrect, StatIds.CommunityTrust));
            Assert.AreEqual(-1, FloorRiskDelta(m03.onCorrect, FloorPlan.F6));
        }

        // ---- the count spec 32 asks for --------------------------------------

        [Test]
        public void AtLeastEightRoutineTasksCarryTheirMistakeIntoALaterNight()
        {
            // Spec 32: eight routine wrong answers have to become a physical change on a
            // following night. The bar is deliberately about deferral rather than about the
            // size of the penalty - a stat docked tonight is a number, and the same stat
            // docked tomorrow morning is the building being different when you walk into it.
            int carried = 0;

            foreach (var definition in AllCases())
            {
                if (definition.kind != CaseKind.RoutineTask || definition.decisions == null) continue;

                bool carries = false;
                foreach (var decision in definition.decisions)
                {
                    if (decision == null || decision.quality != DecisionQuality.Wrong) continue;
                    if (decision.consequences == null) continue;

                    foreach (var c in decision.consequences)
                        if (c != null && c.nextNight) carries = true;
                }

                if (carries) carried++;
            }

            Assert.GreaterOrEqual(carried, 8,
                "spec 32 wants eight routine tasks whose wrong answer reaches a later night; " +
                "found " + carried);
        }

        [Test]
        public void EveryRoutineTaskThatCarriesSomethingAlsoSaysSo()
        {
            // A change the caretaker never notices is not a consequence. Every deferred
            // payload has to arrive with something that tells them the building is different,
            // or the whole chain is invisible and might as well not be there.
            foreach (var definition in AllCases())
            {
                if (definition.kind != CaseKind.RoutineTask || definition.decisions == null) continue;

                foreach (var decision in definition.decisions)
                {
                    if (decision == null || decision.consequences == null) continue;

                    bool carries = false;
                    bool announces = false;
                    foreach (var c in decision.consequences)
                    {
                        if (c == null || !c.nextNight) continue;
                        carries = true;
                        if (c.type == ConsequenceType.Notify) announces = true;
                    }

                    if (carries)
                        Assert.IsTrue(announces, definition.caseId + "/" + decision.decisionId +
                                                 " changes the next night silently");
                }
            }
        }

        // ---- helpers ---------------------------------------------------------

        static ManualEventDefinition FindEvent(string eventId)
        {
            foreach (var definition in SeedContent.BuildManualEvents())
                if (definition.eventId == eventId) return definition;

            Assert.Fail("no manual event " + eventId);
            return null;
        }

        static CaseDefinition[] AllCases()
        {
            var early = SeedContent.BuildCases();
            var late = SeedContent.BuildLateCases();
            var all = new CaseDefinition[early.Length + late.Length];
            early.CopyTo(all, 0);
            late.CopyTo(all, early.Length);
            return all;
        }

        static CaseDefinition FindCase(string caseId)
        {
            foreach (var definition in AllCases())
                if (definition.caseId == caseId) return definition;

            Assert.Fail("no case " + caseId);
            return null;
        }

        static DialogueDefinition FindDialogue(string conversationId)
        {
            foreach (var definition in SeedContent.BuildLateDialogues())
                if (definition.conversationId == conversationId) return definition;

            Assert.Fail("no conversation " + conversationId);
            return null;
        }

        static DialogueChoice FindChoice(DialogueNode node, string choiceId)
        {
            if (node.choices == null) return null;
            for (int i = 0; i < node.choices.Length; i++)
                if (node.choices[i] != null && node.choices[i].choiceId == choiceId) return node.choices[i];
            return null;
        }

        static bool Requires(DialogueChoice choice, string flagId, bool expected)
        {
            if (choice.conditions == null) return false;
            for (int i = 0; i < choice.conditions.Length; i++)
            {
                var c = choice.conditions[i];
                if (c != null && c.type == ConditionType.FlagEquals &&
                    c.keyA == flagId && c.boolValue == expected) return true;
            }
            return false;
        }

        static bool Sets(ConsequenceDefinition[] consequences, string flagId, bool value)
        {
            if (consequences == null) return false;
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c != null && c.type == ConsequenceType.SetFlag &&
                    c.targetId == flagId && c.boolValue == value) return true;
            }
            return false;
        }

        static int StatDelta(ConsequenceDefinition[] consequences, string statId)
        {
            int total = 0;
            if (consequences == null) return 0;
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c != null && c.type == ConsequenceType.StatDelta && c.targetId == statId)
                    total += c.amount;
            }
            return total;
        }

        static int FloorRiskDelta(ConsequenceDefinition[] consequences, string floorId)
        {
            int total = 0;
            if (consequences == null) return 0;
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c != null && c.type == ConsequenceType.AddFloorRisk && c.targetId == floorId)
                    total += c.amount;
            }
            return total;
        }
    }
}
