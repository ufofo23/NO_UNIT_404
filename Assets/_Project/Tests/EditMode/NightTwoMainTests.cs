using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;
using NO404.Evidence;
using NO404.Save;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// N2-M01, the two Seo Jun-hos (GDD v5.1 11).
    ///
    /// He rings twice. The first call is a replay of him at this door years ago, on the
    /// cameras and nowhere else; the second is the man, who has been standing in the rain the
    /// whole time. The right answer is nobody in for the first and the lobby for the second.
    ///
    /// The campaign walk closes this quest by answering the door the plainest way there is,
    /// which proves the night can end and says nothing about what the caretaker is shown on
    /// the way. This file is for the quest as something a person reads and plays.
    /// </summary>
    public sealed class NightTwoMainTests
    {
        const string CaseId = "N2-M01";

        /// <summary>The first call: the replay.</summary>
        const string Echo = "vis_junho_echo";
        /// <summary>The second call: the courier himself.</summary>
        const string Real = "vis_junho_real";

        static readonly string[] Invariants =
        {
            "E06_CCTV_WEATHER_MISMATCH", "EV_WAYBILL_ORDER", "EV_LOBBY_CLOCK", "EV_LOBBY_FOOTPRINTS"
        };

        readonly List<string> _notices = new List<string>();

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

        // ---- what is on screen ---------------------------------------------------

        /// <summary>
        /// Every line the quest puts on screen is in the string table, in both languages.
        ///
        /// The main quests went in with their keys written and their strings not: the task
        /// card, every report option and every reason on the night summary came up as the key
        /// itself. Nothing failed, because a missing string is a warning in the log and a
        /// build that runs.
        /// </summary>
        [Test]
        public void EverythingTheQuestShowsIsInTheStringTableInBothLanguages()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            Assert.IsNotNull(definition, CaseId + " is not in the content database");

            var keys = new List<string> { definition.titleKey, definition.summaryKey };

            foreach (var objective in definition.objectives)
            {
                keys.Add(objective.titleKey);
                AddReasons(objective.onComplete, keys);
            }

            foreach (var decision in definition.decisions)
            {
                keys.Add(decision.labelKey);
                keys.Add(decision.resultKey);
                AddReasons(decision.consequences, keys);
            }

            AddReasons(definition.consequences, keys);
            if (definition.failSafe != null && definition.failSafe.enabled)
                keys.Add(definition.failSafe.notifyKey);

            foreach (var evidenceId in definition.evidenceIds)
            {
                var evidence = ServiceHub.Content.FindEvidence(evidenceId);
                Assert.IsNotNull(evidence, CaseId + " lists evidence that does not exist: " + evidenceId);
                keys.Add(evidence.displayNameKey);
                keys.Add(evidence.descriptionKey);
            }

            // The two callers, and everything they can say or be checked against.
            foreach (var visitorId in new[] { Echo, Real })
            {
                var visitor = ServiceHub.Content.FindVisitor(visitorId);
                Assert.IsNotNull(visitor, visitorId + " is not in the content database");

                AddReasons(visitor.onArrive, keys);
                AddReasons(visitor.onFoundOffRoute, keys);
                if (!string.IsNullOrEmpty(visitor.foundOffRouteNoticeKey)) keys.Add(visitor.foundOffRouteNoticeKey);

                foreach (var check in visitor.checks)
                {
                    keys.Add(check.labelKey);
                    keys.Add(check.valueKey);
                }

                foreach (var node in ServiceHub.Content.FindDialogue(visitor.conversationId).nodes)
                {
                    keys.Add(node.textKey);
                    foreach (var choice in node.choices) keys.Add(choice.textKey);
                }
            }

            keys.Add("ui.access.notify.found_off_route");
            keys.Add(CaseService.EchoRuleMemoKey);
            keys.Add("ui.memo.n2_paper_format");
            keys.Add("ui.memo.n2_package_complaint");

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

        static void AddReasons(ConsequenceDefinition[] consequences, List<string> keys)
        {
            if (consequences == null) return;

            foreach (var consequence in consequences)
                if (consequence != null && !string.IsNullOrEmpty(consequence.reasonKey))
                    keys.Add(consequence.reasonKey);
        }

        // ---- who the two callers are ---------------------------------------------

        /// <summary>
        /// The order is the quest. The replay rings first and is the one nobody should let
        /// in; the man rings second and belongs in the lobby. Swap either fact and the night
        /// teaches the opposite of what it means to.
        /// </summary>
        [Test]
        public void TheFirstCallIsAReplayAndTheSecondIsTheMan()
        {
            var echo = ServiceHub.Content.FindVisitor(Echo);
            var real = ServiceHub.Content.FindVisitor(Real);

            Assert.IsTrue(echo.isHistoricalReplay, "the first call is not marked as a replay");
            Assert.AreEqual(VisitorAccessLevel.Reject, echo.correctAccess);
            Assert.IsFalse(echo.canBeEscorted, "nobody can walk beside somebody who is not there");

            Assert.IsFalse(real.isHistoricalReplay);
            Assert.AreEqual(VisitorAccessLevel.LobbyOnly, real.correctAccess);

            Assert.Less(echo.arrivalGameSecond, real.arrivalGameSecond, "the man rings before his own replay");
        }

        /// <summary>
        /// Nothing about the replay can be seen through the glass, because there is nothing
        /// behind the glass. A tell that needed the caretaker to be standing in the lobby
        /// would be a tell on an empty doorstep.
        /// </summary>
        [Test]
        public void NothingAboutTheReplayIsReadThroughTheGlass()
        {
            foreach (var tell in ServiceHub.Content.FindVisitor(Echo).tells)
                Assert.AreNotEqual(ReadChannel.Glass, tell.channel, tell.tellId + " can only be seen in person");
        }

        // ---- evidence channels (GDD v5.1 19.2) -----------------------------------

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

            CollectionAssert.AreEquivalent(Invariants, invariants);

            // A man on the lobby feed is the result of the mistake, not a check against it.
            CollectionAssert.DoesNotContain(invariants, "EV_CAM02_DOUBLE");
            CollectionAssert.Contains(definition.evidenceIds, "EV_CAM02_DOUBLE");
        }

        /// <summary>
        /// Two invariants can be had from the desk by asking the first caller, and two more
        /// only by going down to the lobby. The second caller's answers are tonight's answers
        /// and prove nothing about anybody but him.
        /// </summary>
        [Test]
        public void TheFirstCallerCanBeAskedForTwoInvariantsAndTheSecondForNone()
        {
            CollectionAssert.AreEquivalent(new[] { "EV_WAYBILL_ORDER", "E06_CCTV_WEATHER_MISMATCH" },
                                           GrantedByTalkingTo("D_N2_JUNHO_ENTRY"));
            CollectionAssert.IsEmpty(GrantedByTalkingTo("D_N2_JUNHO_SECOND"));
        }

        /// <summary>
        /// The lobby feed has nothing on it while the first caller is still asking at the
        /// door. A replay cannot be outside requesting entry and inside sorting boxes; what
        /// CAM-02 shows is what letting him in produces.
        /// </summary>
        [Test]
        public void TheLobbyFeedShowsNothingUntilTheFirstCallIsLetIn()
        {
            OpenTheQuest();

            var onTheLobbyFeed = new List<CCTV.AnomalyDefinition>();
            foreach (var anomaly in ServiceHub.Content.Anomalies)
                if (anomaly.nightIndex == 2 && anomaly.caseId == CaseId) onTheLobbyFeed.Add(anomaly);

            Assert.AreEqual(2, onTheLobbyFeed.Count, "the quest's camera anomalies changed");

            foreach (var anomaly in onTheLobbyFeed)
            {
                string unmet;
                Assert.AreEqual("CAM-02", anomaly.cameraId);
                Assert.IsFalse(ConditionEvaluator.EvaluateAll(anomaly.conditions, out unmet),
                               anomaly.anomalyId + " can fire before anybody has been let in");
            }

            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.LobbyOnly);
            foreach (var anomaly in onTheLobbyFeed)
            {
                string unmet;
                Assert.IsFalse(ConditionEvaluator.EvaluateAll(anomaly.conditions, out unmet),
                               anomaly.anomalyId + " fires although the first call was turned away");
            }

            TheDoorGave(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.LobbyOnly);
            foreach (var anomaly in onTheLobbyFeed)
            {
                string unmet;
                Assert.IsTrue(ConditionEvaluator.EvaluateAll(anomaly.conditions, out unmet),
                              anomaly.anomalyId + " never fires: " + unmet);
            }
        }

        // ---- the door and the report (GDD v5.1 11) -------------------------------

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

        /// <summary>Leaves the door log as though the two calls had been answered this way.</summary>
        static void TheDoorGave(VisitorAccessLevel toTheReplay, VisitorAccessLevel toTheMan, bool held = false)
        {
            ServiceHub.Interphone.LoadFrom(new[]
            {
                new VisitorSaveEntry { visitorId = Echo, decision = (int)toTheReplay },
                new VisitorSaveEntry { visitorId = Real, decision = (int)toTheMan }
            });

            if (held) ServiceHub.State.SetFlag(InterphoneService.HeldFlag(Echo), true);
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
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.LobbyOnly, false, "dec_verified_lobby_only")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.Vestibule, false, "dec_verified_lobby_only")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.Escorted, false, "dec_verified_lobby_only")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.LobbyOnly, true, "dec_wait_outside")]
        [TestCase(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.LobbyOnly, false, "dec_both_in")]
        [TestCase(VisitorAccessLevel.Vestibule, VisitorAccessLevel.Escorted, true, "dec_both_in")]
        [TestCase(VisitorAccessLevel.FloorPass, VisitorAccessLevel.LobbyOnly, false, "dec_full_pass")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.FloorPass, false, "dec_full_pass")]
        [TestCase(VisitorAccessLevel.FloorPass, VisitorAccessLevel.Reject, true, "dec_full_pass")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject, false, "dec_reject")]
        [TestCase(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.Reject, false, "dec_reject")]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject, true, "dec_reject")]
        public void TheFormOffersExactlyTheReportThatMatchesTheDoor(
            VisitorAccessLevel toTheReplay, VisitorAccessLevel toTheMan, bool held, string expected)
        {
            OpenTheQuest();
            TheDoorGave(toTheReplay, toTheMan, held);

            CollectionAssert.AreEqual(new[] { expected }, OnTheReportForm());
        }

        [Test]
        public void NothingCanBeFiledBeforeBothCallsAreAnswered()
        {
            OpenTheQuest();
            CollectionAssert.IsEmpty(OnTheReportForm());
            Assert.IsFalse(File("dec_verified_lobby_only").Accepted,
                           "a report about the door was accepted before the door was answered");

            // One call answered is half a report. He has not rung the second time yet.
            ServiceHub.Interphone.LoadFrom(new[]
            {
                new VisitorSaveEntry { visitorId = Echo, decision = (int)VisitorAccessLevel.Reject }
            });
            CollectionAssert.IsEmpty(OnTheReportForm());
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
            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.LobbyOnly);
            Find(found);

            // By now the same man has rung twice, and that has already cost five: a caretaker
            // at full nerve has nothing for the +3 to give back.
            ServiceHub.Vitals.Strain(5, "reason.saw_the_same_man_twice");

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
            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.LobbyOnly, true);
            Find(2);

            int trust = ServiceHub.State.GetStat(StatIds.CommunityTrust);
            Assert.IsTrue(File("dec_wait_outside").Accepted);

            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual("NEUTRAL", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
            Assert.AreEqual(-3, ServiceHub.State.GetStat(StatIds.CommunityTrust) - trust);
        }

        /// <summary>
        /// Letting the replay into the lobby as well loses nothing and learns nothing. The
        /// delivery happens, there is no debt - and the rule stays unconfirmed however much
        /// was in the tray, because none of it changed what was done at the door.
        /// </summary>
        [Test]
        public void LettingBothInIsNotPunishedAndConfirmsNothing()
        {
            OpenTheQuest();
            TheDoorGave(VisitorAccessLevel.LobbyOnly, VisitorAccessLevel.LobbyOnly);
            Find(4);
            ServiceHub.Vitals.Strain(5, "reason.saw_the_same_man_twice");

            int before = ServiceHub.Vitals.San;
            Assert.IsTrue(File("dec_both_in").Accepted);

            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual(0, ServiceHub.State.GetStat(DebtIds.Access));
            Assert.AreEqual(0, ServiceHub.Vitals.San - before);
            Assert.AreEqual("TRUSTED", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
        }

        /// <summary>A pass past the lobby is an access debt, whichever of them it went to.</summary>
        [TestCase(VisitorAccessLevel.FloorPass, VisitorAccessLevel.LobbyOnly)]
        [TestCase(VisitorAccessLevel.Reject, VisitorAccessLevel.FloorPass)]
        public void APassPastTheLobbyLeavesADebtAndConfirmsNothing(VisitorAccessLevel toTheReplay,
                                                                  VisitorAccessLevel toTheMan)
        {
            OpenTheQuest();
            TheDoorGave(toTheReplay, toTheMan);
            Find(4);

            int before = ServiceHub.Vitals.San;
            Assert.IsTrue(File("dec_full_pass").Accepted);

            Assert.AreEqual(1, ServiceHub.State.GetStat(DebtIds.Access));
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual(0, ServiceHub.Vitals.San - before,
                            "what following him upstairs costs is charged upstairs, not on the form");
        }

        /// <summary>v5.1 11: refusing the real courier leads to a lost-package follow-up.</summary>
        [Test]
        public void TurningTheManAwayIsRememberedAndLeavesTheParcelsUnaccountedFor()
        {
            OpenTheQuest();
            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.Reject);
            Assert.IsTrue(File("dec_reject").Accepted);

            Assert.AreEqual("REJECTED", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
            Assert.AreEqual(1, ServiceHub.State.GetStat(DebtIds.Trust));
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.JunhoPackageLost));

            bool queued = false;
            foreach (var deferred in ServiceHub.Cases.NextNightQueue)
                if (deferred.type == ConsequenceType.Notify && deferred.targetId == "ui.memo.n2_package_complaint")
                    queued = true;

            Assert.IsTrue(queued, "nothing about the parcels is waiting for the next shift");
        }

        // ---- the calls themselves ------------------------------------------------

        /// <summary>
        /// v5.1 11: SAN -5 the first time the contradiction is seen. That is the second ring
        /// - the same man at the door again - and it lands whatever was decided about the
        /// first, once.
        /// </summary>
        [TestCase(VisitorAccessLevel.Reject)]
        [TestCase(VisitorAccessLevel.LobbyOnly)]
        public void TheSecondRingCostsNerveWhateverWasDoneAboutTheFirst(VisitorAccessLevel toTheReplay)
        {
            OpenTheQuest();
            ServiceHub.Player.EnterZone(ZoneIds.Office);

            ServiceHub.Interphone.Enqueue(Echo);
            ServiceHub.Interphone.Enqueue(Real);
            Assert.AreEqual(Echo, ServiceHub.Interphone.Active.visitorId, "the replay did not ring first");

            int before = ServiceHub.Vitals.San;
            ServiceHub.Interphone.Grant(toTheReplay);

            Assert.IsNotNull(ServiceHub.Interphone.Active, "he never rang the second time");
            Assert.AreEqual(Real, ServiceHub.Interphone.Active.visitorId);
            Assert.AreEqual(-5, ServiceHub.Vitals.San - before);

            ServiceHub.Interphone.Grant(VisitorAccessLevel.LobbyOnly);
            Assert.AreEqual(-5, ServiceHub.Vitals.San - before, "the second ring was charged twice");
        }

        /// <summary>
        /// What the caretaker can put to the man depends on what they did about the first
        /// call. Somebody who turned it away cannot ask "did you not just go in?" - nobody
        /// went in - and somebody who opened the door did not watch him leave.
        /// </summary>
        [TestCase(VisitorAccessLevel.Reject, "just_left", "already_inside")]
        [TestCase(VisitorAccessLevel.LobbyOnly, "already_inside", "just_left")]
        [TestCase(VisitorAccessLevel.FloorPass, "already_inside", "just_left")]
        public void TheManIsAskedAboutWhatActuallyHappenedAtTheDoor(VisitorAccessLevel toTheReplay,
                                                                   string offered, string notOffered)
        {
            OpenTheQuest();
            ServiceHub.Interphone.LoadFrom(new[]
            {
                new VisitorSaveEntry { visitorId = Echo, decision = (int)toTheReplay }
            });

            Assert.IsTrue(ServiceHub.Dialogue.Start("D_N2_JUNHO_SECOND"));

            var questions = new List<string>();
            foreach (var choice in ServiceHub.Dialogue.CurrentLine.Choices) questions.Add(choice.choiceId);
            ServiceHub.Dialogue.End();

            CollectionAssert.Contains(questions, offered);
            CollectionAssert.DoesNotContain(questions, notOffered);
        }

        /// <summary>
        /// Letting a replay in must not put anybody loose in the building. The intruder the
        /// door normally injects for a caller who should have been refused is a person in a
        /// corridor, and this caller is not a person in anything.
        /// </summary>
        [Test]
        public void LettingTheReplayInPutsNobodyLooseInTheBuilding()
        {
            OpenTheQuest();
            ServiceHub.Player.EnterZone(ZoneIds.Office);
            ServiceHub.Interphone.Enqueue(Echo);

            int intruders = ServiceHub.Pressure.IntruderCount;
            ServiceHub.Interphone.Grant(VisitorAccessLevel.LobbyOnly);

            var tracked = ServiceHub.ActiveVisitors.Find(Echo);
            Assert.IsNotNull(tracked, "the door released and the log has nobody");
            Assert.AreNotEqual(0, (int)(tracked.Flags & VisitorFlags.HistoricalReplay),
                               "he is being tracked as though he were here");
            Assert.AreEqual(intruders, ServiceHub.Pressure.IntruderCount);
        }

        // ---- when it goes wrong, and what it leaves behind (GDD v5.1 11, 16) -----

        void HearNotices()
        {
            _notices.Clear();
            EventBus.Subscribe<NotificationEvent>(evt => _notices.Add(evt.BodyKey));
        }

        /// <summary>Lets one of them in at this level, with the caretaker back at the desk.</summary>
        static ActiveVisitorService.Tracked LetIn(string visitorId, VisitorAccessLevel level)
        {
            ServiceHub.Player.EnterZone(ZoneIds.Office);
            ServiceHub.ActiveVisitors.Admit(ServiceHub.Content.FindVisitor(visitorId), level, "P1");

            var tracked = ServiceHub.ActiveVisitors.Find(visitorId);
            Assert.IsNotNull(tracked, visitorId + " was let in and is not being tracked");
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
        /// v5.1 11: a wrong pass is not game over - it has to be walked upstairs and undone,
        /// and following the wrong one up costs eight. The screen says the replay went to the
        /// fourth floor; nothing fixes that from the desk, and going up is both what ends it
        /// and what it costs.
        /// </summary>
        [Test]
        public void AFloorPassForTheReplayHasToBeFollowedUpstairs()
        {
            OpenTheQuest();
            HearNotices();
            var echo = LetIn(Echo, VisitorAccessLevel.FloorPass);

            TimePasses(1);
            Assert.AreEqual(VisitorState.Deviating, echo.State);
            Assert.AreEqual(ZoneIds.Floor04, echo.CurrentZone);

            TimePasses(6);
            Assert.AreEqual(VisitorState.Deviating, echo.State, "it resolved itself with nobody going up");

            int before = ServiceHub.Vitals.San;
            ServiceHub.Player.EnterZone(ZoneIds.Floor04);
            ServiceHub.ActiveVisitors.Tick();

            Assert.AreEqual(VisitorState.Leaving, echo.State);
            Assert.AreEqual(-8, ServiceHub.Vitals.San - before);
            CollectionAssert.Contains(_notices, "ui.memo.n2_nobody_upstairs",
                                      "the caretaker reached the fourth floor and was told nothing");

            ServiceHub.ActiveVisitors.Tick();
            Assert.AreEqual(-8, ServiceHub.Vitals.San - before, "going up was charged twice");

            TimePasses(1);
            Assert.IsNull(ServiceHub.ActiveVisitors.Find(Echo), "he is still on the board");
        }

        /// <summary>
        /// The man given the floors goes the same way, and is a man when the caretaker gets
        /// there: found, sent home, and no cost to anybody's nerve.
        /// </summary>
        [Test]
        public void AFloorPassForTheManEndsWithHimFoundAndSentHome()
        {
            OpenTheQuest();
            var real = LetIn(Real, VisitorAccessLevel.FloorPass);

            TimePasses(1);
            Assert.AreEqual(VisitorState.Deviating, real.State);
            Assert.AreEqual(ZoneIds.Floor04, real.CurrentZone);

            int before = ServiceHub.Vitals.San;
            ServiceHub.Player.EnterZone(ZoneIds.Floor04);
            ServiceHub.ActiveVisitors.Tick();

            Assert.AreEqual(VisitorState.Leaving, real.State, "being found did not send him out");
            Assert.AreEqual(0, ServiceHub.Vitals.San - before);

            TimePasses(1);
            Assert.IsNull(ServiceHub.ActiveVisitors.Find(Real), "he was found and never left the building");
        }

        /// <summary>The right answer has no recovery, because there is nothing to recover.</summary>
        [Test]
        public void HeldToTheLobbyTheManDeliversAndLeaves()
        {
            OpenTheQuest();
            var real = LetIn(Real, VisitorAccessLevel.LobbyOnly);

            for (int i = 0; i < 8 && ServiceHub.ActiveVisitors.Find(Real) != null; i++)
            {
                TimePasses(1);
                Assert.AreNotEqual(VisitorState.Deviating, real.State);
                Assert.AreEqual(ZoneIds.Lobby, real.CurrentZone);
            }

            Assert.IsNull(ServiceHub.ActiveVisitors.Find(Real), "he never left the lobby at all");
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
        /// v5.1 16: keeping the 404 bill on night 1 is a hint on night 2. When the first
        /// caller holds up his waybills they are the same paper - and only a caretaker who
        /// kept the bill has anything to hold them against.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void TheKeptBillIsANoteWhenTheFirstCallersWaybillsAreRead(bool billKept)
        {
            OpenTheQuest();
            ServiceHub.State.SetFlag(FlagIds.BillPreserved404, billKept);
            HearNotices();

            var node = ServiceHub.Content.FindDialogue("D_N2_JUNHO_ENTRY").FindNode("invoice");
            Assert.IsNotNull(node);
            ServiceHub.Cases.ApplyConsequences(node.onEnter);

            Assert.AreEqual(billKept, _notices.Contains("ui.memo.n2_paper_format"));
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_WAYBILL_ORDER"), "the waybills are read either way");
        }

        // ---- save and load (GDD v5.1 21) ------------------------------------------

        /// <summary>
        /// A shift saved in the middle of the quest comes back in the middle of the quest.
        ///
        /// Everything the report depends on has to survive: what is in the tray, what the
        /// door gave each of them and that somebody was kept waiting first. If any one of
        /// them is lost the form offers a different report after a reload than it did before
        /// - which is a save that changes the answer.
        /// </summary>
        [Test]
        public void AShiftSavedHalfwayComesBackOfferingTheSameReport()
        {
            OpenTheQuest();
            Find(2);
            TheDoorGave(VisitorAccessLevel.Reject, VisitorAccessLevel.LobbyOnly, true);
            ServiceHub.Vitals.Strain(5, "reason.saw_the_same_man_twice");

            int san = ServiceHub.Vitals.San;
            CollectionAssert.AreEqual(new[] { "dec_wait_outside" }, OnTheReportForm());

            // Through the file format and back, not just through memory.
            var saved = ServiceHub.Save.Capture(0, SaveReason.Manual);
            var reloaded = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(saved));
            ServiceHub.Save.Restore(reloaded);

            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsActive(), "the quest did not come back running");
            Assert.AreEqual(san, ServiceHub.Vitals.San);
            CollectionAssert.AreEqual(new[] { "dec_wait_outside" }, OnTheReportForm(),
                                      "the form offers a different report after a reload");

            Assert.IsTrue(File("dec_wait_outside").Accepted);
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed),
                          "the two invariants found before the save did not count after it");
        }
    }
}
