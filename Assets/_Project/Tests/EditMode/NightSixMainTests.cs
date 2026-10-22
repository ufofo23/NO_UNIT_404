using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;
using NO404.ContentData;
using NO404.Evidence;
using NO404.Save;
using NO404.UI;

namespace NO404.Tests
{
    /// <summary>
    /// N6-M01, there was a unit 404 (GDD v5.1 15).
    ///
    /// The last shift. 404 is opened, the 2009 rescue plays over the burnt room to where it
    /// ends, and what is left of Park Dong-sik is there. There are minutes for him or for
    /// the original ledger - both only if the earlier nights left the building sound - and
    /// then the evidence goes out, which is the ending.
    ///
    /// He is never alive here, and the tests below hold that down as hard as anything else.
    /// </summary>
    public sealed class NightSixMainTests
    {
        const string CaseId = "N6-M01";

        readonly List<string> _cinematics = new List<string>();

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

        // ---- helpers ---------------------------------------------------------------

        /// <summary>
        /// Night 6 opened over a building left in the given state. The state has to be in
        /// place before the case starts: the balanced route is fixed when the night opens.
        /// </summary>
        void OpenTheLastNight(int buildingSafety = 70, string fireDoor = "SAFE")
        {
            ServiceHub.ResetPlaythrough();
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(1);

            ServiceHub.State.SetStat(StatIds.BuildingSafety, buildingSafety);
            if (fireDoor != null) ServiceHub.State.SetChoice(ChoiceIds.FireDoorStatus, fireDoor);

            ServiceHub.State.BeginNight(6);
            ServiceHub.Cases.BeginNight(6);

            _cinematics.Clear();
            EventBus.Subscribe<CinematicRequestedEvent>(evt => _cinematics.Add(evt.CinematicId));

            var runtime = ServiceHub.Cases.Find(CaseId);
            Assert.IsNotNull(runtime, CaseId + " was not dealt on night 6");
            if (runtime.State == CaseState.Dormant) Assert.IsTrue(ServiceHub.Cases.TryStartCase(CaseId));
        }

        static void GoIntoThe404()
        {
            ServiceHub.Cases.NotifyObjective(ObjectiveType.EnterZone, ZoneIds.Unit404);
        }

        static void WatchTheRescue()
        {
            ServiceHub.Evidence.Acquire("EV_N6_LAST_RESCUE", EvidenceSource.WorldPickup);
        }

        static void BringHimOut()
        {
            ServiceHub.Evidence.Acquire("EV_DONGSIK_REMAINS", EvidenceSource.WorldPickup);
        }

        static void TakeTheLedger()
        {
            ServiceHub.Evidence.Acquire("E19_ORIGINAL_LEDGER", EvidenceSource.WorldPickup);
        }

        static DecisionResult Send(string decisionId = "dec_send_police")
        {
            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            return ServiceHub.Cases.SubmitDecision(CaseId, decisionId, attached);
        }

        static bool Flag(string id) { return ServiceHub.State.GetFlag(id); }

        // ---- what is on screen -----------------------------------------------------

        [Test]
        public void EverythingTheQuestShowsIsInTheStringTableInBothLanguages()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
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

            keys.Add(definition.failSafe.notifyKey);

            foreach (var evidenceId in definition.evidenceIds)
            {
                var evidence = ServiceHub.Content.FindEvidence(evidenceId);
                Assert.IsNotNull(evidence, CaseId + " lists evidence that does not exist: " + evidenceId);
                keys.Add(evidence.displayNameKey);
                keys.Add(evidence.descriptionKey);
            }

            keys.Add("ui.prompt.watch_echo");
            keys.Add("ui.prompt.recover_remains");
            keys.AddRange(StorySequenceView.KeysOf(SeedContent.CinematicN6Echo));

            var missing = new List<string>();
            foreach (var language in new[] { "en", "ko" })
            {
                var loc = new LocalizationService();
                loc.Initialize(language);
                foreach (var key in keys)
                    if (string.IsNullOrEmpty(key) || !loc.HasKey(key) || string.IsNullOrWhiteSpace(loc.Get(key)))
                        missing.Add(language + ":" + key);
            }

            Assert.IsEmpty(missing, "missing strings: " + string.Join(", ", missing.ToArray()));
        }

        static void AddReasons(ConsequenceDefinition[] consequences, List<string> keys)
        {
            if (consequences == null) return;
            foreach (var c in consequences)
                if (c != null && !string.IsNullOrEmpty(c.reasonKey)) keys.Add(c.reasonKey);
        }

        // ---- he is not alive ---------------------------------------------------------

        /// <summary>
        /// v5.1 0.13, 29-19, 30.4: nothing on night 6 rescues him, and no line of the quest
        /// sets anything that says he survived. Checked against every consequence the quest
        /// can apply, not just the ones a test happens to walk through.
        /// </summary>
        [Test]
        public void NothingInTheQuestCanRescueHim()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            var everything = new List<ConsequenceDefinition>();

            everything.AddRange(definition.onStart);
            everything.AddRange(definition.consequences);
            foreach (var objective in definition.objectives) everything.AddRange(objective.onComplete);
            foreach (var decision in definition.decisions) everything.AddRange(decision.consequences);

            foreach (var c in everything)
            {
                if (c == null || c.type != ConsequenceType.SetFlag) continue;
                Assert.AreNotEqual(FlagIds.DongsikRescued, c.targetId, "N6-M01 rescues Park Dong-sik");
                Assert.AreNotEqual(FlagIds.DongsikLocated, c.targetId,
                                   "N6-M01 marks him as somebody still to be found alive");
            }

            Assert.IsTrue(StorySequenceView.Knows(SeedContent.CinematicN6Echo));
        }

        // ---- going in ----------------------------------------------------------------

        /// <summary>v5.1 15: SAN -5 for going in, and smoke if the fire door was left open.</summary>
        [TestCase("SAFE", 0)]
        [TestCase("JAMMED", 0)]
        [TestCase("OPEN", -10)]
        public void GoingInCostsNerveAndTheFireDoorDecidesTheSmoke(string fireDoor, int hp)
        {
            OpenTheLastNight(70, fireDoor);
            int san = ServiceHub.Vitals.San, health = ServiceHub.Vitals.Hp;

            GoIntoThe404();

            Assert.AreEqual(-5, ServiceHub.Vitals.San - san);
            Assert.AreEqual(hp, ServiceHub.Vitals.Hp - health);
        }

        /// <summary>
        /// v5.1 15: the 2009 rescue, to where it ends. SAN -6, the sequence, and the place his
        /// remains are - found, not rescued.
        /// </summary>
        [Test]
        public void WatchingTheRescueEndsWhereHeFell()
        {
            OpenTheLastNight();
            GoIntoThe404();
            int san = ServiceHub.Vitals.San;

            WatchTheRescue();

            Assert.AreEqual(-6, ServiceHub.Vitals.San - san);
            CollectionAssert.AreEqual(new[] { SeedContent.CinematicN6Echo }, _cinematics);
            Assert.IsTrue(Flag(FlagIds.DongsikRemainsFound));
            Assert.IsFalse(Flag(FlagIds.DongsikRescued));
        }

        // ---- the minutes ---------------------------------------------------------------

        [TestCase(70, "SAFE", true)]
        [TestCase(59, "SAFE", false)]
        [TestCase(90, "JAMMED", false)]
        [TestCase(90, null, false)]
        public void OnlyAWellKeptBuildingLeavesTimeForBoth(int safety, string fireDoor, bool balanced)
        {
            OpenTheLastNight(safety, fireDoor);
            Assert.AreEqual(balanced, Flag(FlagIds.N6BalancedRoute));
        }

        /// <summary>v5.1 15 "시간 부족": whichever goes first, the other is lost.</summary>
        [Test]
        public void TakingTheLedgerFirstLeavesHimBehind()
        {
            OpenTheLastNight(40, "JAMMED");
            GoIntoThe404();
            WatchTheRescue();

            int archive = ServiceHub.State.GetStat(StatIds.ArchiveIntegrity);
            TakeTheLedger();

            Assert.IsTrue(Flag(FlagIds.LedgerSecured));
            Assert.IsTrue(Flag(FlagIds.EndLedgerSecured));
            Assert.AreEqual(10, ServiceHub.State.GetStat(StatIds.ArchiveIntegrity) - archive);
            Assert.IsTrue(Flag(FlagIds.N6RemainsLeftBehind), "there was time to go back for him as well");
            Assert.IsFalse(Flag(FlagIds.N6LedgerLeftBehind));
        }

        [Test]
        public void BringingHimOutFirstLosesTheLedger()
        {
            OpenTheLastNight(40, "JAMMED");
            GoIntoThe404();
            WatchTheRescue();

            BringHimOut();

            Assert.IsTrue(Flag(FlagIds.EndDongsikRemainsRecovered));
            Assert.IsTrue(Flag(FlagIds.N6LedgerLeftBehind), "there was time for the ledger as well");
            Assert.IsFalse(Flag(FlagIds.N6RemainsLeftBehind));
            Assert.IsFalse(Flag(FlagIds.DongsikRescued));
        }

        [Test]
        public void TheBalancedRouteBringsOutBoth()
        {
            OpenTheLastNight(70, "SAFE");
            GoIntoThe404();
            WatchTheRescue();

            BringHimOut();
            TakeTheLedger();

            Assert.IsTrue(Flag(FlagIds.EndDongsikRemainsRecovered));
            Assert.IsTrue(Flag(FlagIds.EndLedgerSecured));
            Assert.IsFalse(Flag(FlagIds.N6RemainsLeftBehind));
            Assert.IsFalse(Flag(FlagIds.N6LedgerLeftBehind));
        }

        // ---- sending it out ------------------------------------------------------------

        [TestCase("dec_send_police", "SubmittedToPolice")]
        [TestCase("dec_send_media", "SubmittedToMedia")]
        [TestCase("dec_send_residents", "SubmittedToResidents")]
        public void SendingItOutIsTheEndingWhicheverChannel(string decision, string channelFlag)
        {
            OpenTheLastNight();
            GoIntoThe404();
            WatchTheRescue();

            Assert.IsTrue(Send(decision).Accepted);

            Assert.IsTrue(Flag(FlagIds.ExternalEvidenceSent));
            Assert.IsTrue(Flag(FlagIds.EchoReleaseSequence));
            Assert.IsTrue(Flag(channelFlag));
            Assert.AreEqual(EndingIds.SingleEnding404Existed, ServiceHub.Endings.Evaluate().endingId);
        }

        /// <summary>
        /// v5.1 15 실패 / 수습: neither him nor the ledger brought out is still the ending -
        /// the place, the photographs and the radio record go with the send.
        /// </summary>
        [Test]
        public void SendingWithNeitherBroughtOutIsStillTheEnding()
        {
            OpenTheLastNight();
            GoIntoThe404();
            WatchTheRescue();

            Assert.IsTrue(Send().Accepted);
            Assert.IsFalse(Flag(FlagIds.EndDongsikRemainsRecovered));
            Assert.IsFalse(Flag(FlagIds.EndLedgerSecured));
            Assert.AreEqual(EndingIds.SingleEnding404Existed, ServiceHub.Endings.Evaluate().endingId);
        }

        /// <summary>v5.1 8.3: her name is printed only if the record survived night 4.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void HerNameGoesOntoTheListFromTheRecordNightFourKept(bool kept)
        {
            OpenTheLastNight();
            ServiceHub.State.SetFlag(FlagIds.HarinRecordPreserved, kept);
            GoIntoThe404();
            WatchTheRescue();

            Assert.IsTrue(Send().Accepted);
            Assert.AreEqual(kept, Flag(FlagIds.EndHarinRecordFirstRegistered));
        }

        // ---- save and load -------------------------------------------------------------

        /// <summary>
        /// The minutes stay spent through a reload. A save taken after the ledger cannot be
        /// loaded into a night where there is time to go back for him.
        /// </summary>
        [Test]
        public void ALostChanceStaysLostThroughASave()
        {
            OpenTheLastNight(40, "JAMMED");
            GoIntoThe404();
            WatchTheRescue();
            TakeTheLedger();

            var saved = ServiceHub.Save.Capture(0, SaveReason.Manual);
            var reloaded = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(saved));
            ServiceHub.Save.Restore(reloaded);

            Assert.IsTrue(Flag(FlagIds.N6RemainsLeftBehind));
            Assert.IsTrue(Flag(FlagIds.DongsikRemainsFound));
            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsActive());
            Assert.IsTrue(Send().Accepted);
            Assert.AreEqual(EndingIds.SingleEnding404Existed, ServiceHub.Endings.Evaluate().endingId);
        }
    }
}
