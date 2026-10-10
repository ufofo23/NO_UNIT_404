using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Cases;
using NO404.CCTV;
using NO404.Core;
using NO404.Interaction;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// N2-M01 played from the first card to clocking off, through the same services a
    /// player's hands reach (GDD v5.1 11, 30.5).
    ///
    /// The unit tests each hold one rule still. None of them shows that the rules meet: that
    /// the quest starts, that everything it asks for can be done in the built night, that
    /// doing it puts exactly one report on the form, and that filing it leaves a shift that
    /// can end. A quest can pass every rule and still be unreachable, and this is the test
    /// that would say so.
    /// </summary>
    public sealed class NightTwoMainPlaythroughTests
    {
        const string CaseId = "N2-M01";
        const int PatienceFrames = 600;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.ResetPlaythrough();
        }

        [TearDown]
        public void TearDown()
        {
            MainOnlyMode.Set(false);
            ServiceHub.Save.WritesSuspended = false;

            var loop = GameLoop.Instance;
            if (loop != null) loop.DebugReturnToMenu();
        }

        static IEnumerator OpenNightTwo(GameLoop loop)
        {
            Assert.IsTrue(loop.DebugNewGameAt(new DevStartOptions
            {
                Night = 2,
                MainOnly = true,
                PriorNights = DevPriorNights.HandledWell
            }));

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   !(loop.Mode == GameMode.Playing && ServiceHub.State.NightIndex == 2))
                yield return null;

            Assert.AreEqual(2, ServiceHub.State.NightIndex, "night 2 never started");

            // The quest is handed out by the night's own queue, not by the test.
            int frames = 0;
            while (!ServiceHub.Cases.Find(CaseId).State.IsActive() && ++frames < PatienceFrames)
                yield return null;

            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsActive(),
                          "the night started and never handed out its main quest");
        }

        static IEnumerator WaitForCaller(string visitorId)
        {
            int frames = 0;
            while ((ServiceHub.Interphone.Active == null ||
                    ServiceHub.Interphone.Active.visitorId != visitorId) && ++frames < PatienceFrames)
                yield return null;

            Assert.IsNotNull(ServiceHub.Interphone.Active, visitorId + " never rang");
            Assert.AreEqual(visitorId, ServiceHub.Interphone.Active.visitorId);
        }

        /// <summary>Puts an anomaly on CAM-02, looks at it, and files it under its own family.</summary>
        static void ReportOnTheLobbyCamera(int typeNumber)
        {
            string anomalyId = "ANOMALY_" + typeNumber.ToString("00") + "_N2";
            Assert.IsTrue(ServiceHub.Cctv.TriggerAnomaly("CAM-02", anomalyId), anomalyId + " would not play");

            ServiceHub.Cctv.Select("CAM-02");
            Assert.AreEqual(ReportOutcome.Correct,
                            ServiceHub.Cctv.Report("CAM-02", AnomalyCatalogue.CategoryOf(typeNumber)),
                            anomalyId + " was on screen and could not be reported");
        }

        static void ReadTheFixture(string evidenceId)
        {
            foreach (var pickup in Object.FindObjectsByType<EvidencePickup>(FindObjectsSortMode.None))
            {
                if (pickup.EvidenceId != evidenceId) continue;
                pickup.Interact(new PlayerContext());
                return;
            }

            Assert.Fail("nothing in the building gives " + evidenceId);
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

        [UnityTest]
        public IEnumerator CheckedProperlyTheNightConfirmsTheRuleAndCanBeClockedOff()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            yield return OpenNightTwo(loop);
            int sanAtStart = ServiceHub.Vitals.San;

            // ---- the camera wall: the sighting, and the one invariant it can give --------
            ReportOnTheLobbyCamera(9);
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_CAM02_DOUBLE"));
            Assert.AreEqual(-5, ServiceHub.Vitals.San - sanAtStart, "seeing him twice should cost five");

            ReportOnTheLobbyCamera(17);
            Assert.IsTrue(ServiceHub.Evidence.Has("E06_CCTV_WEATHER_MISMATCH"));

            // ---- the door: ask for the waybills, then as far as the lobby ----------------
            yield return WaitForCaller("vis_junho_real");

            Assert.IsTrue(ServiceHub.Dialogue.Start("D_N2_JUNHO_ENTRY"), "he would not talk");
            ServiceHub.Dialogue.Choose("check_invoice");
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_WAYBILL_ORDER"), "asking for the waybills recorded nothing");

            ServiceHub.Interphone.Grant(VisitorAccessLevel.LobbyOnly);
            yield return null;

            yield return WaitForCaller("vis_junho_second");
            ServiceHub.Interphone.Grant(VisitorAccessLevel.LobbyOnly);
            yield return null;

            // ---- the lobby itself ------------------------------------------------------
            ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            Net.NetShift.Request(Net.NetShift.ShiftAct.EnterZone, ZoneIds.Lobby);
            ReadTheFixture("EV_LOBBY_CLOCK");
            yield return null;
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_LOBBY_CLOCK"));

            // ---- the report --------------------------------------------------------------
            int frames = 0;
            while (ServiceHub.Cases.Find(CaseId).State != CaseState.DecisionReady && ++frames < PatienceFrames)
                yield return null;

            Assert.AreEqual(CaseState.DecisionReady, ServiceHub.Cases.Find(CaseId).State,
                            "everything was done and the report never became ready");

            CollectionAssert.AreEqual(new[] { "dec_verified_lobby_only" }, OnTheReportForm());

            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            Net.NetShift.RequestDecision(CaseId, "dec_verified_lobby_only", attached);
            yield return null;

            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsResolved(), "the report was not accepted");
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual("TRUSTED", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
            Assert.AreEqual(0, ServiceHub.State.GetStat(DebtIds.Access));
            Assert.AreEqual(-5 + 3, ServiceHub.Vitals.San - sanAtStart);

            // ---- and the shift can end ---------------------------------------------------
            frames = 0;
            while (loop.CurrentShiftBlocker != GameLoop.ShiftBlocker.None && ++frames < PatienceFrames)
                yield return null;

            Assert.AreEqual(GameLoop.ShiftBlocker.None, loop.CurrentShiftBlocker,
                            "the main is closed and the shift still cannot be clocked off");
        }
    }
}
