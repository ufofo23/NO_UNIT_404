using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;
using NO404.Dialogue;
using NO404.Evidence;
using NO404.Save;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// N2-M01, the two Seo Jun-hos (GDD v5.1 11).
    ///
    /// The campaign walk closes this quest by submitting a decision straight into
    /// CaseService, which proves the night can end and says nothing about what the caretaker
    /// is shown on the way. This file is for the quest as something a person reads and plays.
    /// </summary>
    public sealed class NightTwoMainTests
    {
        const string CaseId = "N2-M01";

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        /// <summary>
        /// Every line the quest puts on screen is in the string table, in both languages.
        ///
        /// The main quests went in with their keys written and their strings not: the task
        /// card, all four report options and every reason on the night summary came up as
        /// the key itself. Nothing failed, because a missing string is a warning in the log
        /// and a build that runs.
        /// </summary>
        [Test]
        public void EverythingTheQuestShowsIsInTheStringTableInBothLanguages()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            Assert.IsNotNull(definition, CaseId + " is not in the content database");

            var keys = new List<string> { definition.titleKey, definition.summaryKey };

            foreach (var objective in definition.objectives) keys.Add(objective.titleKey);

            foreach (var decision in definition.decisions)
            {
                keys.Add(decision.labelKey);
                keys.Add(decision.resultKey);
                AddReasons(decision.consequences, keys);
            }

            AddReasons(definition.consequences, keys);
            if (definition.failSafe != null && definition.failSafe.enabled)
                keys.Add(definition.failSafe.notifyKey);

            // What the quest says after the report: being found upstairs, and the notes.
            foreach (var visitorId in new[] { Real, Again })
                AddReasons(ServiceHub.Content.FindVisitor(visitorId).onFoundOffRoute, keys);

            keys.Add("ui.access.notify.found_off_route");
            keys.Add(CaseService.EchoRuleMemoKey);
            keys.Add("ui.memo.n2_paper_format");
            keys.Add("ui.memo.n2_package_complaint");

            foreach (var evidenceId in definition.evidenceIds)
            {
                var evidence = ServiceHub.Content.FindEvidence(evidenceId);
                Assert.IsNotNull(evidence, CaseId + " lists evidence that does not exist: " + evidenceId);
                keys.Add(evidence.displayNameKey);
                keys.Add(evidence.descriptionKey);
            }

            var missing = new List<string>();
            foreach (var language in new[] { "en", "ko" })
            {
                var loc = new LocalizationService();
                loc.Initialize(language);

                foreach (var key in keys)
                {
                    if (string.IsNullOrEmpty(key)) { missing.Add("(empty key)"); continue; }
                    if (!loc.HasKey(key) || string.IsNullOrWhiteSpace(loc.Get(key)))
                        missing.Add(language + ":" + key);
                }
            }

            Assert.IsEmpty(missing, "missing strings: " + string.Join(", ", missing.ToArray()));
        }

        // ---- evidence channels (GDD v5.1 19.2) ---------------------------------

        static List<string> InvariantsOf(CaseDefinition definition)
        {
            var invariants = new List<string>();
            foreach (var evidenceId in definition.evidenceIds)
            {
                var evidence = ServiceHub.Content.FindEvidence(evidenceId);
                if (evidence == null || evidence.tags == null) continue;
                if (System.Array.IndexOf(evidence.tags, EvidenceTags.Invariant) >= 0) invariants.Add(evidenceId);
            }
            return invariants;
        }

        /// <summary>The night-2 camera anomalies that hand over a piece of evidence.</summary>
        static HashSet<string> GrantedByTheCameraWall()
        {
            var granted = new HashSet<string>();
            foreach (var anomaly in ServiceHub.Content.Anomalies)
                if (anomaly.nightIndex == 2 && !string.IsNullOrEmpty(anomaly.evidenceId))
                    granted.Add(anomaly.evidenceId);
            return granted;
        }

        /// <summary>What talking to this caller can put in the tray.</summary>
        static HashSet<string> GrantedByTalkingTo(string conversationId)
        {
            var granted = new HashSet<string>();
            var conversation = ServiceHub.Content.FindDialogue(conversationId);
            Assert.IsNotNull(conversation, conversationId + " is not in the content database");

            foreach (var node in conversation.nodes)
                foreach (var consequence in node.onEnter)
                    if (consequence != null && consequence.type == ConsequenceType.GrantEvidence)
                        granted.Add(consequence.targetId);

            return granted;
        }

        /// <summary>
        /// The scenario names four invariants and the rule wants two of them, so there have
        /// to be four to choose from: a caretaker who only ever finds two has no slack, and
        /// the "at least two" rule quietly becomes "exactly these two".
        /// </summary>
        [Test]
        public void TheQuestOffersFourInvariantsAndTheSightingIsNotOneOfThem()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            var invariants = InvariantsOf(definition);

            CollectionAssert.AreEquivalent(
                new[] { "E06_CCTV_WEATHER_MISMATCH", "EV_WAYBILL_ORDER", "EV_LOBBY_CLOCK", "EV_LOBBY_FOOTPRINTS" },
                invariants);

            // Seeing two of him is the question, not an answer to it.
            CollectionAssert.DoesNotContain(invariants, "EV_CAM02_DOUBLE");
            CollectionAssert.Contains(definition.evidenceIds, "EV_CAM02_DOUBLE");
        }

        /// <summary>
        /// v5.1 19.2: a main cannot be settled from one screen. The camera wall hands over
        /// the sighting and one invariant; the rest have to come from the door and the room.
        /// </summary>
        [Test]
        public void TheCameraWallAloneCannotReachTwoInvariants()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            var onCamera = GrantedByTheCameraWall();

            CollectionAssert.Contains(onCamera, "EV_CAM02_DOUBLE", "nothing on the wall records the sighting");
            CollectionAssert.Contains(onCamera, "E06_CCTV_WEATHER_MISMATCH");

            int fromCameraAlone = 0;
            foreach (var invariant in InvariantsOf(definition))
                if (onCamera.Contains(invariant)) fromCameraAlone++;

            Assert.AreEqual(1, fromCameraAlone,
                "two invariants off the camera wall would let the quest be judged without leaving the desk");
        }

        /// <summary>
        /// Either Jun-ho can be asked for the waybills. Which of them the caretaker happens
        /// to question must not decide whether that invariant exists this run.
        /// </summary>
        [Test]
        public void TheWaybillsCanBeReadOffEitherCaller()
        {
            CollectionAssert.Contains(GrantedByTalkingTo("D_N2_JUNHO_ENTRY"), "EV_WAYBILL_ORDER");
            CollectionAssert.Contains(GrantedByTalkingTo("D_N2_JUNHO_SECOND"), "EV_WAYBILL_ORDER");
        }

        // ---- the door and the report (GDD v5.1 11) ------------------------------

        const string Real = "vis_junho_real";
        const string Again = "vis_junho_second";

        static readonly string[] Invariants =
        {
            "E06_CCTV_WEATHER_MISMATCH", "EV_WAYBILL_ORDER", "EV_LOBBY_CLOCK", "EV_LOBBY_FOOTPRINTS"
        };

        /// <summary>Night 2 with its main quest running and nobody judged yet.</summary>
        static void OpenTheQuest()
        {
            ServiceHub.ResetPlaythrough();
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(1);

            ServiceHub.State.BeginNight(2);
            ServiceHub.Cases.BeginNight(2);

            var runtime = ServiceHub.Cases.Find(CaseId);
            Assert.IsNotNull(runtime, CaseId + " was not dealt on night 2");
            if (runtime.State == CaseState.Dormant)
                Assert.IsTrue(ServiceHub.Cases.TryStartCase(CaseId), CaseId + " would not start");
        }

        /// <summary>Leaves the door log as though both calls had been answered this way.</summary>
        static void TheDoorGave(VisitorAccessLevel first, VisitorAccessLevel again, bool heldFirst = false)
        {
            ServiceHub.Interphone.LoadFrom(new[]
            {
                new VisitorSaveEntry { visitorId = Real, decision = (int)first },
                new VisitorSaveEntry { visitorId = Again, decision = (int)again }
            });

            if (heldFirst) ServiceHub.State.SetFlag(InterphoneService.HeldFlag(Real), true);
        }

        static void Find(int invariants)
        {
            for (int i = 0; i < invariants; i++)
                ServiceHub.Evidence.Acquire(Invariants[i], EvidenceSource.WorldPickup);
        }

        static List<string> OnTheReportForm()
        {
            var offered = new List<string>();
            foreach (var decision in ServiceHub.Content.FindCase(CaseId).decisions)
            {
                string unmet;
                if (ConditionEvaluator.EvaluateAll(decision.availability, out unmet))
                    offered.Add(decision.decisionId);
            }
            return offered;
        }

        static DecisionResult File(string decisionId)
        {
            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            return ServiceHub.Cases.SubmitDecision(CaseId, decisionId, attached);
        }

        /// <summary>
        /// The report is the log entry for what the door did. Whatever was done, the form
        /// offers the one option that says so - never none, never a choice between two.
        /// </summary>
        [TestCase(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.LobbyOnly, false, "dec_verified_lobby_only")]
        [TestCase(VisitorAccessLevel.Vestibule, VisitorAccessLevel.Reject, false, "dec_verified_lobby_only")]
        [TestCase(VisitorAccessLevel.Escorted, VisitorAccessLevel.LobbyOnly, false, "dec_verified_lobby_only")]
        [TestCase(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.LobbyOnly, true, "dec_wait_outside")]
        [TestCase(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.FloorPass, false, "dec_full_pass")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.FullTemporary, true, "dec_full_pass")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject, false, "dec_reject")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject, true, "dec_reject")]
        public void TheFormOffersExactlyTheReportThatMatchesTheDoor(
            VisitorAccessLevel first, VisitorAccessLevel again, bool held, string expected)
        {
            OpenTheQuest();
            TheDoorGave(first, again, held);

            CollectionAssert.AreEqual(new[] { expected }, OnTheReportForm());
        }

        [Test]
        public void NothingCanBeFiledBeforeAnybodyIsJudged()
        {
            OpenTheQuest();

            CollectionAssert.IsEmpty(OnTheReportForm());
            Assert.IsFalse(File("dec_verified_lobby_only").Accepted,
                           "a report about the door was accepted before the door was answered");
        }

        /// <summary>
        /// v5.1 11: ECHO_RULE_CONFIRMED needs two invariants. The same answer reached on one
        /// is still the right answer - it just was not checked, and the campaign remembers
        /// the difference.
        /// </summary>
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(4, true)]
        public void TheEchoRuleIsConfirmedOnlyOnTwoInvariants(int found, bool confirmed)
        {
            OpenTheQuest();
            TheDoorGave(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.LobbyOnly);
            Find(found);

            // He has been seen twice by now, and that has already cost five: a caretaker at
            // full nerve has nothing for the +3 to give back.
            ServiceHub.Cases.TryAdvanceObjective(CaseId, "obj_watch_cam02");

            int before = ServiceHub.Vitals.San;
            Assert.IsTrue(File("dec_verified_lobby_only").Accepted);

            Assert.AreEqual(confirmed, ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual(confirmed ? 3 : 0, ServiceHub.Vitals.San - before,
                            "SAN +3 is for a check that came out right, and only for that");
            Assert.AreEqual("TRUSTED", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
        }

        [Test]
        public void KeepingHimOutsideCanConfirmTheRuleTooAndCostsTheDelay()
        {
            OpenTheQuest();
            TheDoorGave(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.LobbyOnly, true);
            Find(2);

            int trust = ServiceHub.State.GetStat(StatIds.CommunityTrust);
            Assert.IsTrue(File("dec_wait_outside").Accepted);

            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual("NEUTRAL", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
            Assert.AreEqual(-3, ServiceHub.State.GetStat(StatIds.CommunityTrust) - trust);
        }

        /// <summary>
        /// A pass past the lobby is an access debt however well it was checked: he is the
        /// real courier and the lobby is as far as a courier goes.
        /// </summary>
        [Test]
        public void APassPastTheLobbyLeavesADebtAndConfirmsNothing()
        {
            OpenTheQuest();
            TheDoorGave(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.FloorPass);
            Find(4);

            int before = ServiceHub.Vitals.San;
            Assert.IsTrue(File("dec_full_pass").Accepted);

            Assert.AreEqual(1, ServiceHub.State.GetStat(DebtIds.Access));
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual(0, ServiceHub.Vitals.San - before,
                            "what following him upstairs costs is charged upstairs, not on the form");
        }

        [Test]
        public void TurningHimAwayIsRememberedAgainstTheCaretaker()
        {
            OpenTheQuest();
            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject);

            Assert.IsTrue(File("dec_reject").Accepted);

            Assert.AreEqual("REJECTED", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
            Assert.AreEqual(1, ServiceHub.State.GetStat(DebtIds.Trust));
        }

        /// <summary>
        /// v5.1 11: SAN -5 on first seeing it. Charged on the step, once, and not again by
        /// whichever report is filed afterwards.
        /// </summary>
        [Test]
        public void SeeingHimTwiceCostsNerveOnceAndBeforeAnythingIsDecided()
        {
            OpenTheQuest();
            int before = ServiceHub.Vitals.San;

            Assert.IsTrue(ServiceHub.Cases.TryAdvanceObjective(CaseId, "obj_watch_cam02"));
            Assert.AreEqual(-5, ServiceHub.Vitals.San - before);

            Assert.IsFalse(ServiceHub.Cases.TryAdvanceObjective(CaseId, "obj_watch_cam02"));
            Assert.AreEqual(-5, ServiceHub.Vitals.San - before, "the same sighting was charged twice");

            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject);
            Assert.IsTrue(File("dec_reject").Accepted);
            Assert.AreEqual(-5, ServiceHub.Vitals.San - before, "the report charged for the sighting again");
        }

        // ---- when it goes wrong, and what it leaves behind (GDD v5.1 11, 16) -----

        readonly List<string> _notices = new List<string>();

        void HearNotices()
        {
            _notices.Clear();
            EventBus.Subscribe<NotificationEvent>(evt => _notices.Add(evt.BodyKey));
        }

        /// <summary>Lets Jun-ho in at this level, with the caretaker back at the desk.</summary>
        static ActiveVisitorService.Tracked LetHimIn(VisitorAccessLevel level)
        {
            ServiceHub.Player.EnterZone(ZoneIds.Office);
            ServiceHub.ActiveVisitors.Admit(ServiceHub.Content.FindVisitor(Real), level, "P1");

            var tracked = ServiceHub.ActiveVisitors.Find(Real);
            Assert.IsNotNull(tracked, "he was let in and is not in the building");
            return tracked;
        }

        static void TimePasses(int legs)
        {
            for (int i = 0; i < legs; i++)
            {
                ServiceHub.Clock.AdvanceSeconds(ActiveVisitorService.SecondsPerLeg + 1);
                ServiceHub.ActiveVisitors.Tick();
            }
        }

        /// <summary>
        /// v5.1 11: a wrong pass is not game over - he has to be found again. He goes up to
        /// the fourth floor and stays there for as long as nobody comes, and going up is both
        /// what ends it and what costs the eight.
        /// </summary>
        [Test]
        public void APassPastTheLobbyHasToBeWalkedUpstairsAndUndone()
        {
            OpenTheQuest();
            var junho = LetHimIn(VisitorAccessLevel.FloorPass);

            TimePasses(1);
            Assert.AreEqual(VisitorState.Deviating, junho.State);
            Assert.AreEqual(ZoneIds.Floor04, junho.CurrentZone);

            // Nothing fixes it from the desk. He is still there an hour's worth of legs later.
            TimePasses(6);
            Assert.AreEqual(VisitorState.Deviating, junho.State, "he left on his own");
            Assert.AreEqual(ZoneIds.Floor04, junho.CurrentZone);

            int before = ServiceHub.Vitals.San;
            ServiceHub.Player.EnterZone(ZoneIds.Floor04);
            ServiceHub.ActiveVisitors.Tick();

            Assert.AreEqual(VisitorState.Leaving, junho.State, "being found did not send him out");
            Assert.AreEqual(-8, ServiceHub.Vitals.San - before);

            ServiceHub.ActiveVisitors.Tick();
            Assert.AreEqual(-8, ServiceHub.Vitals.San - before, "finding him was charged twice");

            TimePasses(1);
            Assert.IsNull(ServiceHub.ActiveVisitors.Find(Real), "he was found and never left the building");
        }

        /// <summary>The right answer has no recovery, because there is nothing to recover.</summary>
        [Test]
        public void HeldToTheLobbyHeDeliversAndLeaves()
        {
            OpenTheQuest();
            var junho = LetHimIn(VisitorAccessLevel.LobbyOnly);

            for (int i = 0; i < 8 && ServiceHub.ActiveVisitors.Find(Real) != null; i++)
            {
                TimePasses(1);
                Assert.AreNotEqual(VisitorState.Deviating, junho.State);
                Assert.AreEqual(ZoneIds.Lobby, junho.CurrentZone);
            }

            Assert.IsNull(ServiceHub.ActiveVisitors.Find(Real), "he never left the lobby at all");
        }

        /// <summary>v5.1 11: refusing the real courier leads to a lost-package follow-up.</summary>
        [Test]
        public void TurningHimAwayLeavesTheParcelsUnaccountedForTomorrow()
        {
            OpenTheQuest();
            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject);
            Assert.IsTrue(File("dec_reject").Accepted);

            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.JunhoPackageLost));

            bool queued = false;
            foreach (var deferred in ServiceHub.Cases.NextNightQueue)
                if (deferred.type == ConsequenceType.Notify && deferred.targetId == "ui.memo.n2_package_complaint")
                    queued = true;

            Assert.IsTrue(queued, "nothing about the parcels is waiting for the next shift");
        }

        /// <summary>
        /// v5.1 11: once the ECHO rule is confirmed, later ECHO cases open with one line of
        /// the caretaker's own note - and a caretaker who never confirmed it gets nothing.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ALaterEchoCaseOpensWithTheNoteOnlyIfTheRuleWasConfirmed(bool confirmed)
        {
            ServiceHub.ResetPlaythrough();
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(1);
            ServiceHub.State.SetFlag(FlagIds.EchoRuleConfirmed, confirmed);

            ServiceHub.State.BeginNight(5);
            ServiceHub.Cases.BeginNight(5);
            HearNotices();

            var later = ServiceHub.Cases.Find("N5-M01");
            Assert.AreEqual(AnomalyFamily.Echo, later.Definition.family, "N5-M01 is no longer an ECHO case");
            if (later.State == CaseState.Dormant) Assert.IsTrue(ServiceHub.Cases.TryStartCase("N5-M01"));

            Assert.AreEqual(confirmed, _notices.Contains(CaseService.EchoRuleMemoKey));
        }

        /// <summary>
        /// v5.1 16: keeping the 404 bill on night 1 is a hint on night 2. Asking either caller
        /// for the waybills is when the two pieces of paper are side by side.
        /// </summary>
        [TestCase("D_N2_JUNHO_ENTRY", "invoice", true)]
        [TestCase("D_N2_JUNHO_ENTRY", "invoice", false)]
        [TestCase("D_N2_JUNHO_SECOND", "number", true)]
        public void TheKeptBillIsANoteWhenTheWaybillsAreRead(string conversationId, string nodeId, bool billKept)
        {
            OpenTheQuest();
            ServiceHub.State.SetFlag(FlagIds.BillPreserved404, billKept);
            HearNotices();

            var node = ServiceHub.Content.FindDialogue(conversationId).FindNode(nodeId);
            Assert.IsNotNull(node, conversationId + " has no node " + nodeId);
            ServiceHub.Cases.ApplyConsequences(node.onEnter);

            Assert.AreEqual(billKept, _notices.Contains("ui.memo.n2_paper_format"));
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_WAYBILL_ORDER"), "the waybills are read either way");
        }

        static void AddReasons(ConsequenceDefinition[] consequences, List<string> keys)
        {
            if (consequences == null) return;

            foreach (var consequence in consequences)
                if (consequence != null && !string.IsNullOrEmpty(consequence.reasonKey))
                    keys.Add(consequence.reasonKey);
        }
    }
}
