using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Evidence;
using R = NO404.Cases.SubquestRules;

namespace NO404.Tests
{
    /// <summary>
    /// The night 2 / 4 / 6 subquests (GDD v5.1 11, 13, 15) where something happens that the
    /// report cannot see on its own: a circuit that bites, a call that has to be answered, a
    /// conversation that decides how much is said, smoke that keeps costing.
    ///
    /// The pool's shape - fifteen a night, one story subquest, the quota, ending flags only
    /// on the story rows - is already held for all six nights by NightPoolContentTests. These
    /// are the rules those tests cannot reach.
    /// </summary>
    public sealed class EvenNightSubquestTests
    {
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
            ServiceHub.Dialogue.End();
            EventBus.Clear();
        }

        // ---- helpers ---------------------------------------------------------------

        static void Open(int night, string caseId)
        {
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(1);
            ServiceHub.State.BeginNight(night);
            ServiceHub.Cases.BeginNight(night);
            ServiceHub.Cases.RestoreDraw(new[] { caseId });
            Assert.IsTrue(ServiceHub.Cases.TryStartCase(caseId), caseId + " did not start");
        }

        static DecisionResult File(string caseId, string decisionId)
        {
            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            return ServiceHub.Cases.SubmitDecision(caseId, decisionId, attached);
        }

        static void Say(string choiceId)
        {
            for (int guard = 0; guard < 16 && ServiceHub.Dialogue.IsActive && !ServiceHub.Dialogue.CurrentLine.HasChoices; guard++)
                ServiceHub.Dialogue.Advance();
            Assert.IsTrue(ServiceHub.Dialogue.IsActive && ServiceHub.Dialogue.CurrentLine.HasChoices,
                          "the conversation never offered a choice");
            ServiceHub.Dialogue.Choose(choiceId);
            for (int guard = 0; guard < 16 && ServiceHub.Dialogue.IsActive; guard++)
                ServiceHub.Dialogue.Advance();
        }

        /// <summary>The way GameLoop rings a call: the case opens, then the phone ticks.</summary>
        static void RingWith(int night, string caseId)
        {
            ServiceHub.Phone.ScheduleNight(night);
            ServiceHub.Phone.NotifyCaseStarted(caseId);
            ServiceHub.Phone.Tick();
        }

        static bool Flag(string id) { return ServiceHub.State.GetFlag(id); }

        // ---- night 2 ---------------------------------------------------------------

        /// <summary>v5.1 N2-R03: HP -8 for touching it live, once; isolated, it is a repair.</summary>
        [Test]
        public void TheHotBreakerBitesUntilTheFeederIsOff()
        {
            Open(2, "N2-R03");
            ServiceHub.Evidence.Acquire("EV_N2R03_LOAD", EvidenceSource.WorldPickup);
            int hp = ServiceHub.Vitals.Hp;

            R.Act("n2r03_rebalance");
            Assert.AreEqual(hp - 8, ServiceHub.Vitals.Hp);
            Assert.IsFalse(ServiceHub.Evidence.Has("EV_N2R03_REBALANCED"));

            R.Act("n2r03_rebalance");
            Assert.AreEqual(hp - 8, ServiceHub.Vitals.Hp, "the same live board bit twice");

            R.Act("n2r03_isolate");
            R.Act("n2r03_rebalance");
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N2R03_REBALANCED"));

            int safety = ServiceHub.State.GetStat(StatIds.BuildingSafety);
            Assert.IsTrue(File("N2-R03", "dec_rebalance").Accepted);
            Assert.AreEqual(safety + 5, ServiceHub.State.GetStat(StatIds.BuildingSafety));
        }

        /// <summary>
        /// v5.1 N2-R12: the dead extension rings with its quest, and staying on the line is
        /// the call the ending keeps.
        /// </summary>
        [Test]
        public void KeepingThe2009CallerOnTheLineIsWhatTheEndingRemembers()
        {
            Open(2, "N2-R12");
            RingWith(2, "N2-R12");
            Assert.IsTrue(ServiceHub.Phone.IsRinging, "the 2009 call does not ring with its quest");
            Assert.AreEqual("CALL_N2R12_2009", ServiceHub.Phone.Active.callId);

            int san = ServiceHub.Vitals.San;
            ServiceHub.Phone.Answer();
            Say("ask_where");

            Assert.IsTrue(Flag(R.N2R12Asked));
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N2R12_CALL"));
            Assert.IsFalse(File("N2-R12", "dec_let_go").Accepted, "the report offered a call that was not had");
            Assert.IsTrue(File("N2-R12", "dec_kept_line").Accepted);
            Assert.IsTrue(Flag(FlagIds.End2009CallPreserved));
            Assert.AreEqual(san - 6, ServiceHub.Vitals.San);
        }

        /// <summary>A missed call is still a closable quest - and keeps nothing for the ending.</summary>
        [Test]
        public void AMissed2009CallStillLetsTheQuestClose()
        {
            Open(2, "N2-R12");
            RingWith(2, "N2-R12");
            ServiceHub.Clock.SetGameSecond(ServiceHub.Clock.GameSecond + ServiceHub.Phone.Active.ringSeconds + 1);
            ServiceHub.Phone.Tick();

            Assert.IsFalse(ServiceHub.Phone.IsRinging);
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N2R12_CALL"), "the missed call left nothing in the log");
            Assert.IsFalse(File("N2-R12", "dec_kept_line").Accepted);
            Assert.IsTrue(File("N2-R12", "dec_let_go").Accepted);
            Assert.IsFalse(Flag(FlagIds.End2009CallPreserved));
        }

        /// <summary>v5.1 N2-R14: a parcel sent back comes back another way on night 4.</summary>
        [Test]
        public void AReturned404ParcelMakesNightFourMoreLikelyToFindIt()
        {
            var n4r12 = ServiceHub.Content.FindCase("N4-R12");
            Assert.AreEqual(0, R.WeightBonus(n4r12));

            Open(2, "N2-R14");
            ServiceHub.Evidence.Acquire("EV_N2R14_INVOICE", EvidenceSource.WorldPickup);
            Assert.IsTrue(File("N2-R14", "dec_return").Accepted);

            Assert.Greater(R.WeightBonus(n4r12), 0);
        }

        // ---- night 4 ---------------------------------------------------------------

        /// <summary>v5.1 13: the 404 call is N4-R03's now, not the main's.</summary>
        [Test]
        public void TheCallFrom404RingsWithItsOwnQuest()
        {
            ServiceHub.State.BeginNight(4);
            RingWith(4, "N4-M01");
            Assert.AreEqual("CALL_N4_CHAIRMAN", ServiceHub.Phone.Active.callId);
            Assert.AreEqual(1, ServiceHub.Phone.WaitingCount, "the 404 call still rings with the main");

            Open(4, "N4-R03");
            RingWith(4, "N4-R03");
            Assert.AreEqual("CALL_N4_404", ServiceHub.Phone.Active.callId);

            int harin = ServiceHub.State.GetStat(StatIds.HarinResonance);
            ServiceHub.Phone.Answer();
            Say("ask_name");

            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N4R03_CALL"));
            Assert.IsFalse(File("N4-R03", "dec_hung_up").Accepted);
            Assert.IsTrue(File("N4-R03", "dec_asked_name").Accepted);
            Assert.AreEqual(harin + 6, ServiceHub.State.GetStat(StatIds.HarinResonance));
        }

        /// <summary>
        /// v5.1 N4-R10: how much she says depends on night 1 and on trust, and only the whole
        /// testimony is the ending's shot.
        /// </summary>
        [TestCase("GOOD", 50, true)]
        [TestCase("DELAYED", 70, true)]
        [TestCase("DENIED", 40, false)]
        public void SunjaGivesTheWholeTestimonyOnlyToSomebodySheTrusts(string care, int trust, bool full)
        {
            ServiceHub.State.SetChoice(ChoiceIds.SunjaCare, care);
            ServiceHub.State.SetStat(StatIds.CommunityTrust, trust);
            Open(4, "N4-R10");

            Assert.IsTrue(R.CanAct("n4r10_door"));
            R.Act("n4r10_door");
            Assert.IsTrue(ServiceHub.Dialogue.IsActive, "knocking on 303 started no conversation");
            Say("consent");

            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N4R10_TESTIMONY"));
            Assert.IsTrue(File("N4-R10", full ? "dec_recorded_full" : "dec_recorded_partial").Accepted);
            Assert.AreEqual(full, Flag(FlagIds.EndSunjaTestimonyFull));
            Assert.IsTrue(ServiceHub.State.ChoiceIs(ChoiceIds.SunjaTestimony, full ? "FULL" : "PARTIAL"));
            Assert.IsTrue(Flag(FlagIds.SunjaTestimony), "night 5 cannot use a testimony that was given");
        }

        /// <summary>v5.1 N4-R01 feeds N4-R10: somebody who went to see her is somebody she talks to.</summary>
        [Test]
        public void VisitingHerEarlierInTheNightOpensTheWholeTestimony()
        {
            ServiceHub.State.SetChoice(ChoiceIds.SunjaCare, "DENIED");
            ServiceHub.State.SetStat(StatIds.CommunityTrust, 30);
            Open(4, "N4-R01");
            ServiceHub.Evidence.Acquire("EV_N4R01_STATE", EvidenceSource.WorldPickup);
            Assert.IsTrue(File("N4-R01", "dec_visit").Accepted);

            ServiceHub.Cases.RestoreDraw(new[] { "N4-R10" });
            Assert.IsTrue(ServiceHub.Cases.TryStartCase("N4-R10"));
            Assert.IsTrue(Flag(R.N4R10FullPossible));
        }

        // ---- night 6 ---------------------------------------------------------------

        /// <summary>v5.1 N6-R05: only opening the door gives the ending its memory shot.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void OnlyOpeningTheUnplannedDoorGivesTheMemory(bool open)
        {
            Open(6, "N6-R05");
            ServiceHub.Evidence.Acquire("EV_N6R05_DOOR", EvidenceSource.WorldPickup);
            int san = ServiceHub.Vitals.San;

            if (open)
            {
                R.Act("n6r05_open");
                Assert.AreEqual(san - 10, ServiceHub.Vitals.San);
                Assert.IsFalse(File("N6-R05", "dec_noted_closed").Accepted);
                Assert.IsTrue(File("N6-R05", "dec_opened").Accepted);
            }
            else
            {
                Assert.IsFalse(File("N6-R05", "dec_opened").Accepted);
                Assert.IsTrue(File("N6-R05", "dec_noted_closed").Accepted);
            }

            Assert.AreEqual(open, Flag(FlagIds.EndHarinDoorOpened));
            CollectionAssert.Contains(ServiceHub.Endings.EndingShots(),
                                      open ? "ending.shot.harin_door.memory" : "ending.shot.harin_door.slipper");
        }

        /// <summary>v5.1 N6-R08: HP -2 every five seconds in the smoke, at most -20, until the door is shut.</summary>
        [Test]
        public void SmokeCostsUntilTheFireDoorIsShutAndNeverMoreThanTheCap()
        {
            ServiceHub.State.SetStat(StatIds.BuildingSafety, 40);
            Open(6, "N6-R08");
            Assert.IsTrue(Flag(R.N6R08Smoke));
            ServiceHub.Player.EnterZone(ZoneIds.Floor04);
            int hp = ServiceHub.Vitals.Hp;

            for (int i = 0; i < 3; i++) R.Tick(R.SmokeIntervalSeconds);
            Assert.AreEqual(hp - 3 * R.SmokeDamage, ServiceHub.Vitals.Hp);

            for (int i = 0; i < 30; i++) R.Tick(R.SmokeIntervalSeconds);
            Assert.AreEqual(hp - R.SmokeDamageCap, ServiceHub.Vitals.Hp, "the smoke went past its cap");

            R.Act("n6r08_firedoor");
            Assert.IsTrue(File("N6-R08", "dec_door_secured").Accepted == false,
                          "the door row needs the temperature read first");
        }

        [Test]
        public void ShuttingTheFireDoorStopsTheSmoke()
        {
            ServiceHub.State.SetStat(StatIds.BuildingSafety, 40);
            Open(6, "N6-R08");
            ServiceHub.Player.EnterZone(ZoneIds.Floor04);
            ServiceHub.Evidence.Acquire("EV_N6R08_TEMP", EvidenceSource.WorldPickup);

            R.Act("n6r08_firedoor");
            int hp = ServiceHub.Vitals.Hp;
            for (int i = 0; i < 5; i++) R.Tick(R.SmokeIntervalSeconds);
            Assert.AreEqual(hp, ServiceHub.Vitals.Hp);

            Assert.IsTrue(File("N6-R08", "dec_door_secured").Accepted);
        }

        [Test]
        public void AWellKeptBuildingKeepsTheSmokeOut()
        {
            ServiceHub.State.SetStat(StatIds.BuildingSafety, 80);
            ServiceHub.State.SetChoice(ChoiceIds.FireDoorStatus, "SAFE");
            Open(6, "N6-R08");
            ServiceHub.Player.EnterZone(ZoneIds.Floor04);
            int hp = ServiceHub.Vitals.Hp;

            for (int i = 0; i < 5; i++) R.Tick(R.SmokeIntervalSeconds);
            Assert.IsFalse(Flag(R.N6R08Smoke));
            Assert.AreEqual(hp, ServiceHub.Vitals.Hp);
        }

        /// <summary>v5.1 N6-R06 changes the way in, never the ending flag N6-M01 owns.</summary>
        [Test]
        public void TheCollapseNeverDecidesTheRemainsShot()
        {
            Open(6, "N6-R06");
            ServiceHub.Evidence.Acquire("EV_N6R06_NAMEPLATE", EvidenceSource.WorldPickup);
            int hp = ServiceHub.Vitals.Hp;

            R.Act("n6r06_clear");
            Assert.AreEqual(hp - 8, ServiceHub.Vitals.Hp);
            Assert.IsFalse(R.CanAct("n6r06_stabilise"), "the passage was cleared and shored at once");
            Assert.IsTrue(File("N6-R06", "dec_cleared").Accepted);
            Assert.IsFalse(Flag(FlagIds.EndDongsikRemainsRecovered));
        }

        // ---- the props ------------------------------------------------------------

        [Test]
        public void EveryEvenNightActionBelongsToARealQuest()
        {
            foreach (var action in new[]
                     {
                         "n2r03_isolate", "n2r03_rebalance", "n2r08_deviate", "n4r10_door", "n4r11_scrape",
                         "n4r12_open", "n4r13_watch", "n6r05_open", "n6r06_stabilise", "n6r06_clear",
                         "n6r08_firedoor", "n6r08_vent", "n6r09_cut", "n6r09_force"
                     })
            {
                var caseId = R.CaseOf(action);
                Assert.IsNotNull(ServiceHub.Content.FindCase(caseId), action + " belongs to no quest");
                Assert.IsFalse(R.CanAct(action), action + " works with its quest closed");
            }
        }
    }
}
