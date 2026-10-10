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
    /// N2-M01 played from the first ring to clocking off, through the same services a
    /// player's hands reach (GDD v5.1 11, 30.5).
    ///
    /// The unit tests each hold one rule still. None of them shows that the rules meet: that
    /// the quest starts, that the two calls come in the right order, that everything it asks
    /// for can be done in the built night, that doing it puts exactly one report on the form,
    /// and that filing it leaves a shift that can end. A quest can pass every rule and still
    /// be unreachable, and this is the test that would say so.
    /// </summary>
    public sealed class NightTwoMainPlaythroughTests
    {
        const string CaseId = "N2-M01";
        const string Echo = "vis_junho_echo";
        const string Real = "vis_junho_real";
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

        /// <summary>One question to whoever is at the door, the way the interphone app asks it.</summary>
        static void Ask(string conversationId, string choiceId)
        {
            Assert.IsTrue(ServiceHub.Dialogue.Start(conversationId), conversationId + " would not open");
            ServiceHub.Dialogue.Choose(choiceId);
            ServiceHub.Dialogue.End();
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

        static void WalkDownToTheLobby()
        {
            ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            Net.NetShift.Request(Net.NetShift.ShiftAct.EnterZone, ZoneIds.Lobby);
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

        static IEnumerator FileTheOnlyReport(string expected)
        {
            int frames = 0;
            while (ServiceHub.Cases.Find(CaseId).State != CaseState.DecisionReady && ++frames < PatienceFrames)
                yield return null;

            Assert.AreEqual(CaseState.DecisionReady, ServiceHub.Cases.Find(CaseId).State,
                            "everything was done and the report never became ready");

            CollectionAssert.AreEqual(new[] { expected }, OnTheReportForm());

            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            Net.NetShift.RequestDecision(CaseId, expected, attached);
            yield return null;

            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsResolved(), "the report was not accepted");
        }

        /// <summary>The body the building has put somewhere for this visitor, if any.</summary>
        static GameObject BodyOf(string namePrefix, string visitorId)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (transform.name == namePrefix + visitorId) return transform.gameObject;

            return null;
        }

        /// <summary>
        /// The right answer, start to finish: the first call checked and turned away, the man
        /// let as far as the lobby, the rule confirmed, the shift closeable.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFirstCallTurnedAwayAndTheManLetInConfirmsTheRule()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            yield return OpenNightTwo(loop);
            int sanAtStart = ServiceHub.Vitals.San;

            // ---- the first ring: ask, look, refuse -----------------------------------
            yield return WaitForCaller(Echo);
            yield return null;

            // He is on the door camera and not on the doorstep. Walking down to the glass
            // finds nobody, which is the one check that needs no question asked.
            var atTheDoor = BodyOf("CALLER_", Echo);
            Assert.IsNotNull(atTheDoor, "nothing is ringing the bell at all");
            if (Layers.CctvOnly >= 0)
                Assert.AreEqual(Layers.CctvOnly, atTheDoor.layer, "the replay is standing on the real doorstep");

            Ask("D_N2_JUNHO_ENTRY", "check_invoice");
            Ask("D_N2_JUNHO_ENTRY", "ask_weather");
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_WAYBILL_ORDER"), "asking for the waybills recorded nothing");
            Assert.IsTrue(ServiceHub.Evidence.Has("E06_CCTV_WEATHER_MISMATCH"), "asking about the rain recorded nothing");

            ServiceHub.Interphone.Grant(VisitorAccessLevel.Reject);
            yield return null;

            Assert.IsNull(ServiceHub.ActiveVisitors.Find(Echo), "he was turned away and is in the building");

            // ---- the second ring: the same man, and this time he is there ------------
            yield return WaitForCaller(Real);
            yield return null;

            Assert.AreEqual(-5, ServiceHub.Vitals.San - sanAtStart, "the same man ringing again should cost five");

            var theMan = BodyOf("CALLER_", Real);
            Assert.IsNotNull(theMan);
            if (Layers.CctvOnly >= 0)
                Assert.AreNotEqual(Layers.CctvOnly, theMan.layer, "the real courier cannot be seen at the door");

            ServiceHub.Interphone.Grant(VisitorAccessLevel.LobbyOnly);
            yield return null;

            // ---- the lobby, and the report -------------------------------------------
            WalkDownToTheLobby();
            ReadTheFixture("EV_LOBBY_FOOTPRINTS");
            yield return null;

            yield return FileTheOnlyReport("dec_verified_lobby_only");

            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed));
            Assert.AreEqual("TRUSTED", ServiceHub.State.GetChoice(ChoiceIds.JunhoStatus));
            Assert.AreEqual(0, ServiceHub.State.GetStat(DebtIds.Access));
            Assert.AreEqual(-5 + 3, ServiceHub.Vitals.San - sanAtStart);

            int frames = 0;
            while (loop.CurrentShiftBlocker != GameLoop.ShiftBlocker.None && ++frames < PatienceFrames)
                yield return null;

            Assert.AreEqual(GameLoop.ShiftBlocker.None, loop.CurrentShiftBlocker,
                            "the main is closed and the shift still cannot be clocked off");
        }

        /// <summary>
        /// The other way through: the first call let in unchecked. The door releases, the log
        /// and the lobby camera have him inside, the lobby has nobody in it - and then he
        /// rings again.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFirstCallLetInIsOnTheCameraAndNotInTheLobby()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop);

            yield return OpenNightTwo(loop);

            yield return WaitForCaller(Echo);
            ServiceHub.Interphone.Grant(VisitorAccessLevel.LobbyOnly);

            // A few frames for the building to put him wherever it is going to put him.
            for (int i = 0; i < 5; i++) yield return null;

            var tracked = ServiceHub.ActiveVisitors.Find(Echo);
            Assert.IsNotNull(tracked, "the door released and the board has nobody");
            Assert.AreEqual(ZoneIds.Lobby, tracked.CurrentZone);

            var inTheLobby = BodyOf("VISITOR_", Echo);
            Assert.IsNotNull(inTheLobby, "he is on the board and on no camera");
            if (Layers.CctvOnly >= 0)
                Assert.AreEqual(Layers.CctvOnly, inTheLobby.layer,
                                "somebody is physically standing in the lobby");

            // And now the lobby feed has something on it worth reporting. The wall holds an
            // anomaly back until its channel is actually in front of somebody, so the
            // caretaker sits down at the PC and pulls CAM-02 up full screen.
            ServiceHub.Player.SetPcMode(true);
            ServiceHub.Player.OpenApp(AppIds.Cctv);
            ServiceHub.Cctv.SetGridMode(false);
            ServiceHub.Cctv.Select("CAM-02");
            int frames = 0;
            while (!ServiceHub.Cctv.HasReportable("CAM-02") && ++frames < PatienceFrames) yield return null;

            Assert.IsTrue(ServiceHub.Cctv.HasReportable("CAM-02"),
                          "he was let in and the lobby camera has nothing to report");

            ServiceHub.Cctv.SetGridMode(true);
            ServiceHub.Player.SetPcMode(false);

            // The same man, at the door, again.
            yield return WaitForCaller(Real);
            ServiceHub.Interphone.Grant(VisitorAccessLevel.LobbyOnly);
            yield return null;

            WalkDownToTheLobby();
            yield return null;

            yield return FileTheOnlyReport("dec_both_in");

            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.EchoRuleConfirmed),
                           "the rule was confirmed by somebody who let the replay in");
            Assert.AreEqual(0, ServiceHub.State.GetStat(DebtIds.Access));
        }
    }
}
