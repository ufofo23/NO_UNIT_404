using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NO404.Cases;
using NO404.Core;
using NO404.Endings;
using NO404.Phone;

namespace NO404.Tests
{
    /// <summary>
    /// Brings the real service graph up once for tests that need condition evaluation.
    /// SceneService is the only service that wants a coroutine runner and nothing here loads
    /// a scene, so null is a valid runner.
    /// </summary>
    static class TestServices
    {
        static GameObject _host;

        public static void Ensure()
        {
            if (ServiceHub.Ready) return;

            _host = new GameObject("NO404TestHost") { hideFlags = HideFlags.HideAndDontSave };
            ServiceHub.Initialize(null, _host.transform);
        }
    }

    public sealed class EndingSelectionTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        [Test]
        public void AllFiveGddEndingsExist()
        {
            for (int i = 0; i < EndingIds.All.Length; i++)
                Assert.IsNotNull(ServiceHub.Content.FindEnding(EndingIds.All[i]),
                                 "missing " + EndingIds.All[i]);
        }

        [Test]
        public void EvaluationOrderIsUniqueAndAscending()
        {
            int previous = int.MinValue;
            foreach (var ending in ServiceHub.Endings.AllOrdered)
            {
                Assert.Greater(ending.evaluationOrder, previous,
                               ending.endingId + " shares or precedes another ending's order");
                previous = ending.evaluationOrder;
            }
        }

        /// <summary>
        /// The failure result is authored and reachable but is not one of the four (v5.0 8.1).
        /// </summary>
        [Test]
        public void TheFailureResultExistsAndIsNotAGrade()
        {
            Assert.IsNotNull(ServiceHub.Content.FindEnding(EndingIds.NoEnding));
            CollectionAssert.DoesNotContain(EndingIds.All, EndingIds.NoEnding);
        }

        /// <summary>
        /// v5.0 8.3: the grade is HP and SAN, and the choices change the epilogue instead.
        ///
        /// This is what replaced the old condition-matched endings. Their claim - that six
        /// nights of decisions are worth something at the end - is still the claim; it is just
        /// no longer made by moving somebody between letters.
        /// </summary>
        [Test]
        public void AnEmptyCampaignEarnsNoEpilogueLines()
        {
            CollectionAssert.IsEmpty(ServiceHub.Endings.EpilogueModifiers());
        }

        [Test]
        public void RescuingDongsikIsNamedInTheEpilogue()
        {
            ServiceHub.State.SetFlag(FlagIds.DongsikRescued, true);
            CollectionAssert.Contains(ServiceHub.Endings.EpilogueModifiers(),
                                      "ending.epilogue.dongsik_survived");
        }

        [Test]
        public void PreservingHarinsRecordPutsHerNameBack()
        {
            ServiceHub.State.SetFlag(FlagIds.HarinRecordPreserved, true);
            CollectionAssert.Contains(ServiceHub.Endings.EpilogueModifiers(),
                                      "ending.epilogue.harin_named");
        }

        [Test]
        public void TheArchiveAndTheResidentsSpeakOnlyWhenTheyWereEarned()
        {
            var modifiers = ServiceHub.Endings.EpilogueModifiers();
            CollectionAssert.DoesNotContain(modifiers, "ending.epilogue.records_verified");
            CollectionAssert.DoesNotContain(modifiers, "ending.epilogue.residents_testify");

            ServiceHub.State.SetStat(StatIds.ArchiveIntegrity, 70);
            ServiceHub.State.SetStat(StatIds.CommunityTrust, 70);

            modifiers = ServiceHub.Endings.EpilogueModifiers();
            CollectionAssert.Contains(modifiers, "ending.epilogue.records_verified");
            CollectionAssert.Contains(modifiers, "ending.epilogue.residents_testify");
        }

        /// <summary>
        /// A building left unsafe says so, whatever letter the caretaker walked away with.
        /// </summary>
        [Test]
        public void AnUnsafeBuildingIsReportedInTheEpilogue()
        {
            ServiceHub.State.SetStat(StatIds.BuildingSafety, 30);
            CollectionAssert.Contains(ServiceHub.Endings.EpilogueModifiers(),
                                      "ending.epilogue.residents_hurt");
        }
    }

    public sealed class PhoneServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        /// <summary>
        /// Roster the night and open the case the named call belongs to.
        ///
        /// A call now waits for its case before it becomes due (GDD 15.7): the handset is half
        /// of a beat and the task card is the other half, and they used to arrive on two
        /// separate schedules. These tests are about what the phone does once a call is due -
        /// ringing, priority, answering, being missed - so each opens exactly the case it needs
        /// and leaves the tie itself to CaseBeatTests.
        ///
        /// Opening them all at once would be worse than useless here: night 4 would release
        /// the chairman and the call from 404 in the same instant, which is a race the game
        /// never runs, and the impossible call would outrank the chairman in every test.
        /// </summary>
        static void ScheduleNightAndOpen(int night, params string[] callIds)
        {
            ServiceHub.Phone.ScheduleNight(night);

            foreach (var callId in callIds)
                foreach (var definition in ServiceHub.Content.PhoneCalls)
                    if (definition.callId == callId)
                        ServiceHub.Phone.NotifyCaseStarted(definition.caseId);
        }

        [Test]
        public void ACallRingsWhenItsCaseOpens()
        {
            ServiceHub.State.BeginNight(4);
            ServiceHub.Phone.ScheduleNight(4);

            // No hour brings it. The chairman's call is about night 4's main, and waits for it.
            ServiceHub.Clock.SetGameSecond(ContentData.SeedContent.At(1, 0));
            ServiceHub.Phone.Tick();
            Assert.IsFalse(ServiceHub.Phone.IsRinging,
                           "a call is not owed to the clock, however late it gets");

            ServiceHub.Phone.NotifyCaseStarted("N4-M01");
            ServiceHub.Phone.Tick();

            Assert.IsTrue(ServiceHub.Phone.IsRinging);
            Assert.AreEqual("CALL_N4_CHAIRMAN", ServiceHub.Phone.Active.callId);
        }

        [Test]
        public void TheImpossibleCallOutranksTheChairman()
        {
            ServiceHub.State.BeginNight(4);
            // Both cases open, which is what puts two calls in the queue at once.
            ScheduleNightAndOpen(4, "CALL_N4_CHAIRMAN", "CALL_N4_404");

            // Jump past both scheduled times so they queue together.
            ServiceHub.Clock.SetGameSecond(ContentData.SeedContent.At(0, 35));
            ServiceHub.Phone.Tick();

            Assert.IsTrue(ServiceHub.Phone.WaitingCount >= 2);
            Assert.AreEqual("CALL_N4_404", ServiceHub.Phone.Active.callId,
                            "priority must decide the order, not the schedule");
        }

        [Test]
        public void AnsweringStartsTheConversationAndAdvancesTheCase()
        {
            ServiceHub.State.BeginNight(4);
            ServiceHub.Clock.SetGameSecond(ContentData.SeedContent.At(22, 25));
            ScheduleNightAndOpen(4, "CALL_N4_CHAIRMAN");
            ServiceHub.Phone.Tick();

            ServiceHub.Phone.Answer();

            Assert.IsTrue(ServiceHub.Phone.InCall);
            Assert.IsTrue(ServiceHub.Dialogue.IsActive);
            Assert.AreEqual("D_N4_CHAIRMAN_DELETE", ServiceHub.Dialogue.Current.conversationId);
        }

        [Test]
        public void AnUnansweredCallIsMissedAndCostsSomething()
        {
            ServiceHub.State.BeginNight(4);
            ServiceHub.Clock.SetGameSecond(ContentData.SeedContent.At(22, 25));
            ScheduleNightAndOpen(4, "CALL_N4_CHAIRMAN");

            ServiceHub.Phone.Tick();
            Assert.IsTrue(ServiceHub.Phone.IsRinging);

            int alertBefore = ServiceHub.State.GetStat(StatIds.ChairmanAlert);

            // Ring out.
            ServiceHub.Clock.SetGameSecond(ContentData.SeedContent.At(22, 25) + 600);
            ServiceHub.Phone.Tick();

            Assert.IsTrue(ServiceHub.Phone.WasMissed("CALL_N4_CHAIRMAN"));
            Assert.Greater(ServiceHub.State.GetStat(StatIds.ChairmanAlert), alertBefore,
                           "missing the chair's call has a consequence");
        }

        [Test]
        public void DecliningRemovesTheCallWithoutStartingAConversation()
        {
            ServiceHub.State.BeginNight(3);
            ScheduleNightAndOpen(3, "CALL_N3_CHAIRMAN");
            ServiceHub.Clock.SetGameSecond(ContentData.SeedContent.At(22, 40));
            ServiceHub.Phone.Tick();

            Assert.IsTrue(ServiceHub.Phone.IsRinging);
            ServiceHub.Phone.Decline();

            Assert.IsFalse(ServiceHub.Dialogue.IsActive);
        }

        [Test]
        public void EveryCallPointsAtAConversationThatExists()
        {
            foreach (var call in ServiceHub.Content.PhoneCalls)
            {
                Assert.IsNotNull(ServiceHub.Content.FindDialogue(call.conversationId),
                                 call.callId + " -> " + call.conversationId);
                Assert.Greater(call.ringSeconds, 0, call.callId + " never rings");
            }
        }
    }

    public sealed class NightCoverageTests
    {
        ContentData.ContentDatabase _content;

        [SetUp]
        public void SetUp()
        {
            _content = new ContentData.ContentDatabase();
            _content.Load();
        }

        /// <summary>
        /// v5.0 4.1: every night has exactly one fixed main, and it is the same one every run.
        ///
        /// This replaces the old assertion that C00..C14 all exist. The campaign no longer has
        /// a fixed roster of fifteen cases spread over six nights - it has six spines with a
        /// pool drawn around each - so the thing worth guarding is that no night has lost its
        /// spine and no night has grown a second one.
        /// </summary>
        [Test]
        public void EveryNightHasExactlyOneFixedMain()
        {
            for (int night = 1; night <= 6; night++)
            {
                int mains = 0;
                string found = null;

                foreach (var definition in _content.Cases)
                {
                    if (definition.nightIndex != night || !definition.isFixedMain) continue;
                    mains++;
                    found = definition.caseId;
                }

                Assert.AreEqual(1, mains, "night " + night + " should have exactly one fixed main");
                Assert.AreEqual("N" + night + "-M01", found);
            }
        }

        /// <summary>
        /// A main is placed, never drawn (v5.0 4.4 step 1), so it must not also carry a weight
        /// that would let the random half of the night pick it a second time.
        /// </summary>
        [Test]
        public void NoFixedMainIsAlsoADrawCandidate()
        {
            foreach (var definition in _content.Cases)
            {
                if (!definition.isFixedMain) continue;
                Assert.AreEqual(0, definition.baseWeight, definition.caseId + " is both placed and drawable");
                Assert.AreEqual(Cases.QuestType.Main, definition.questType);
            }
        }

        /// <summary>
        /// Every main must be reachable by the night chain (v5.0 4.2).
        ///
        /// Night 2's opens with somebody at the entrance, and authoring it as an
        /// interphone-triggered case left the chain unable to see it: nothing paced the night,
        /// nothing hung its callers, and the shift had no last job to close. A main is the
        /// night's spine and the spine has to be on the timetable.
        /// </summary>
        [Test]
        public void EveryFixedMainIsOnTheTimetable()
        {
            foreach (var definition in _content.Cases)
            {
                if (!definition.isFixedMain) continue;
                Assert.AreEqual(Cases.CaseTrigger.Time, definition.trigger,
                                definition.caseId + " would be invisible to the night chain");
            }
        }

        /// <summary>
        /// Nights 1 to 6. Night 0 was the prologue and the prologue is gone (GDD 9.1) - the
        /// game opens on night 1, so night 0 is not a shift that can be reached and must not
        /// be required to carry content.
        /// </summary>
        [Test]
        public void EveryNightHasWork()
        {
            for (int night = 1; night <= 6; night++)
            {
                int count = 0;
                foreach (var definition in _content.Cases) if (definition.nightIndex == night) count++;
                Assert.Greater(count, 0, "night " + night + " has nothing to do");
            }
        }

        [Test]
        public void TheFinalNightCanBeCompleted()
        {
            var final = _content.FindCase("N6-M01");

            Assert.IsNotNull(final);
            Assert.AreEqual(6, final.nightIndex);
            Assert.IsTrue(final.isFixedMain);

            // v5.0 15 lists four ways out of 404: the man, the ledger, both, or neither.
            Assert.GreaterOrEqual(final.decisions.Length, 4,
                                  "the last night has to offer a real choice about what to carry out");
        }

        [Test]
        public void EveryPlayableSpaceOfTheCompressedBuildingIsDefined()
        {
            // v2.1 spec 0.7.1 replaced the twelve zones of the old fifteen-storey block with
            // the nine floors listed there. Asserting the floors rather than a zone count is
            // what the spec actually constrains: sub-rooms get added to a floor as content
            // needs them, and a floor appearing or disappearing is the thing that must not
            // happen quietly.
            foreach (var floor in Gameplay.FloorPlan.All)
                Assert.Contains(floor.PrimaryZoneId, ZoneIds.All,
                                floor.FloorId + " has a primary zone that is not a zone id");

            Assert.Contains(ZoneIds.Elevator, ZoneIds.All);
            Assert.Contains(ZoneIds.Stairwell, ZoneIds.All);
            Assert.Contains(ZoneIds.Archive, ZoneIds.All);
            Assert.Contains(ZoneIds.ServicePassage, ZoneIds.All);
            Assert.Contains(ZoneIds.Unit404, ZoneIds.All);

            // Present as a place, absent as a floor (spec 0.7.1).
            Assert.Contains(ZoneIds.PhantomFloor13, ZoneIds.All);
            Assert.IsNull(Gameplay.FloorPlan.FloorOfZone(ZoneIds.PhantomFloor13));
        }

        /// <summary>
        /// v5.0 19.2: a main is never answerable off one screen.
        ///
        /// This replaces the assertion that all eighteen routine tasks exist. T01-T18 were
        /// authored against a fixed nightly roster that v5.0 removed; what has to stay true is
        /// that each remaining main gives the player more than one way to be sure.
        /// </summary>
        [Test]
        public void EveryMainOffersAtLeastThreeEvidenceChannels()
        {
            foreach (var definition in _content.Cases)
            {
                if (!definition.isFixedMain) continue;

                int channels = definition.evidenceIds.Length + definition.objectives.Length;
                Assert.GreaterOrEqual(channels, 3,
                    definition.caseId + " can be settled on one screen (v5.0 19.2)");
            }
        }

        [Test]
        public void AllThirtySixAnomalyTypesAreScheduled()
        {
            var covered = new HashSet<int>();
            foreach (var anomaly in _content.Anomalies) covered.Add(anomaly.typeNumber);

            for (int type = 1; type <= CCTV.AnomalyCatalogue.TypeCount; type++)
                Assert.IsTrue(covered.Contains(type), "anomaly type " + type + " is never scheduled (GDD 12.3)");
        }

        [Test]
        public void AnomalyInstanceIdsAreUniqueSoNoneOverwritesAnother()
        {
            var seen = new HashSet<string>();
            foreach (var anomaly in _content.Anomalies)
                Assert.IsTrue(seen.Add(anomaly.anomalyId), "duplicate anomaly instance " + anomaly.anomalyId);
        }

        [Test]
        public void EveryZoneBelongsToExactlyOneStreamingGroup()
        {
            for (int i = 0; i < ZoneIds.All.Length; i++)
            {
                var group = Gameplay.ZoneGroups.GroupOf(ZoneIds.All[i]);
                Assert.IsNotNull(group, ZoneIds.All[i] + " is in no streaming group and could never load");
            }
        }

        [Test]
        public void ThePowerBudgetIsTighterThanTheNumberOfSystems()
        {
            // GDD 9.6: five systems, at most three at once - the choice must actually bite.
            Assert.AreEqual(5, CircuitIds.All.Length);
            Assert.Less(CircuitIds.MaxSimultaneous, CircuitIds.All.Length);
        }
    }
}
