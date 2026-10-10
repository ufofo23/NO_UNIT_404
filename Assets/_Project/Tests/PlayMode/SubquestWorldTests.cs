using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Cases;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Tests
{
    /// <summary>
    /// Nights 1/3/5 (v5.1 10, 12, 14) in the built world: every subquest prop is physically
    /// reachable, silent outside its quest, and the flows with rules of their own - a live
    /// panel, a looping corridor, a broadcast that needs a second witness - play through.
    /// </summary>
    public sealed class SubquestWorldTests
    {
        GameObject _root;

        static readonly string[] Streamed =
        {
            ZoneIds.Parking, ZoneIds.RecyclingYard, ZoneIds.Machinery, ZoneIds.PumpRoom, ZoneIds.Archive,
            ZoneIds.Floor03, ZoneIds.Floor04, ZoneIds.ServicePassage, ZoneIds.Floor05, ZoneIds.Floor06
        };

        [UnitySetUp]
        public IEnumerator Setup()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.Zones.UnloadAllStreamed();
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;
            // The office, lobby, lift, stairwell and laundry live in the core scene.
            ServiceHub.Zones.LoadCoreImmediate();
            yield return null;
            ServiceHub.ResetPlaythrough();
            ServiceHub.Dialogue.End();
            _root = new GameObject("SubquestWorld");
            WorldBuilder.Create(_root.transform).Build(Streamed);
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(_root);
            ServiceHub.ResetPlaythrough();
            yield return null;
        }

        static void Open(int night, string caseId)
        {
            ServiceHub.State.BeginNight(night);
            ServiceHub.Cases.BeginNight(night);
            ServiceHub.Cases.RestoreDraw(new[] { caseId });
            Assert.IsTrue(ServiceHub.Cases.TryStartCase(caseId), caseId + " did not start");
        }

        static int NightOf(string caseId) { return caseId[1] - '0'; }

        [UnityTest]
        public IEnumerator EveryPropIsReachableAndSilentOutsideItsQuest()
        {
            var props = Object.FindObjectsByType<SubquestInteractable>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(props.Length, 73, "night 1/3/5 props are missing from the world");

            var context = new PlayerContext();
            var problems = new List<string>();
            foreach (var prop in props)
            {
                string reason;
                if (prop.CanInteract(context, out reason)) problems.Add(prop.name + " speaks with its quest closed");

                // Seen from the side it faces, nothing else may stand in front of it.
                var collider = prop.GetComponent<Collider>();
                if (collider == null) { problems.Add(prop.name + " has no collider"); continue; }
                Vector3 front = prop.transform.rotation * Vector3.back;
                Vector3 centre = collider.bounds.center;
                RaycastHit hit;
                if (!Physics.Raycast(centre + front * 0.9f, -front, out hit, 1.2f))
                    problems.Add(prop.name + " cannot be hit");
                else if (hit.collider.gameObject != prop.gameObject)
                    problems.Add(prop.name + " is hidden behind " + hit.collider.name);
            }
            CollectionAssert.IsEmpty(problems, string.Join("; ", problems.ToArray()));

            foreach (var prop in props)
            {
                ServiceHub.ResetPlaythrough();
                Open(NightOf(prop.CaseId), prop.CaseId);
                if (!string.IsNullOrEmpty(prop.RequiredFlag)) ServiceHub.State.SetFlag(prop.RequiredFlag, true);
                if (prop.Action == "n3r08_marker" || prop.Action == "n5r10_marker" ||
                    prop.Action == "n3r08_follow_number" || prop.Action == "n3r08_run" || prop.Action == "n5r10_door")
                    continue;   // loop actions are driven in their own test

                string reason;
                if (!prop.CanInteract(context, out reason)) { problems.Add(prop.name + " is dead during " + prop.CaseId); continue; }
                prop.Interact(context);
                if (!string.IsNullOrEmpty(prop.EvidenceId) && !ServiceHub.Evidence.Has(prop.EvidenceId))
                    problems.Add(prop.name + " gave nothing");
            }
            CollectionAssert.IsEmpty(problems, string.Join("; ", problems.ToArray()));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePumpPanelWarnsThenShocksThenRepairsAndNightFiveRemembers()
        {
            Open(1, "N1-R02");
            int hp = ServiceHub.Vitals.Hp;

            SubquestRules.Act("n1r02_panel");
            Assert.AreEqual(hp, ServiceHub.Vitals.Hp, "the first touch is a warning");
            SubquestRules.Act("n1r02_panel");
            Assert.AreEqual(hp - 10, ServiceHub.Vitals.Hp, "v5.1 N1-R02: HP -10 without the breaker");
            Assert.IsFalse(ServiceHub.Evidence.Has("EV_N1R02_FILTER"));

            SubquestRules.Act("n1r02_breaker");
            SubquestRules.Act("n1r02_panel");
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N1R02_FILTER"), "repairable after the shock");

            ServiceHub.Evidence.Acquire("EV_N1R02_PRESSURE", Evidence.EvidenceSource.WorldPickup);
            ServiceHub.Cases.Tick();
            var result = ServiceHub.Cases.SubmitDecision("N1-R02", "dec_repair", new[] { "EV_N1R02_FILTER" });
            Assert.AreEqual(DecisionQuality.Correct, result.Quality);
            Assert.IsTrue(ServiceHub.State.ChoiceIs(ChoiceIds.PumpStatus, "REPAIRED"));

            Open(5, "N5-R03");
            Assert.IsTrue(ServiceHub.State.GetFlag(SubquestRules.N5PumpOk), "night 5 reads the night 1 repair");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheNursesDoorDecisionIsWhatTheEndingRemembers()
        {
            Open(1, "N1-R01");
            SubquestRules.VisitorDecided("vis_minseo", (int)Visitors.VisitorAccessLevel.Escorted);
            ServiceHub.Cases.NotifyObjective(ObjectiveType.JudgeVisitor, "vis_minseo");
            ServiceHub.Cases.Tick();

            var refused = ServiceHub.Cases.SubmitDecision("N1-R01", "dec_deny", null);
            Assert.IsFalse(refused.Accepted, "only the row matching the door is open");

            var result = ServiceHub.Cases.SubmitDecision("N1-R01", "dec_escort", null);
            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EndSunjaTrusted));
            Assert.IsTrue(ServiceHub.State.ChoiceIs(ChoiceIds.SunjaCare, "GOOD"));

            Open(5, "N5-R02");
            Assert.IsTrue(ServiceHub.State.GetFlag(SubquestRules.N5SunjaReady), "she is ready on night 5");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFifthFloorLoopHoldsUntilTheMarkerIsRead()
        {
            Open(3, "N3-R08");
            Assert.IsNotNull(SubquestRules.ReportBlockedKey("N3-R08"), "no report from inside the loop");

            int san = ServiceHub.Vitals.San;
            for (int i = 0; i < 6; i++) SubquestRules.Act("n3r08_follow_number");
            Assert.AreEqual(san - SubquestRules.LoopSanCap, ServiceHub.Vitals.San, "SAN -3 a loop, capped at -15");

            SubquestRules.Act("n3r08_marker");
            Assert.IsTrue(ServiceHub.State.GetFlag(SubquestRules.N3R08Looping), "the marker means nothing unread");

            ServiceHub.Evidence.Acquire("EV_N3R08_EXTINGUISHER", Evidence.EvidenceSource.WorldPickup);
            SubquestRules.Act("n3r08_marker");
            Assert.IsNull(SubquestRules.ReportBlockedKey("N3-R08"));

            ServiceHub.Cases.Tick();
            var result = ServiceHub.Cases.SubmitDecision("N3-R08", "dec_invariant", new[] { "EV_N3R08_EXTINGUISHER" });
            Assert.AreEqual(DecisionQuality.Correct, result.Quality);
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.SpaceRuleConfirmed));
            yield return null;
        }

        /// <summary>
        /// v5.1 N1-R10 reads the facility log, and the facility app's 304 water chart is it.
        /// A playtester read the chart for N1-M01, saw "304호 수도 사용량" filed, and watched
        /// N1-R10 stay grey on the home screen all night.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFacilityWaterChartIsTheLogTheWaterQuestAsksFor()
        {
            // The chart first, the quest later.
            ServiceHub.Evidence.Acquire("EV_304_WATER", Evidence.EvidenceSource.Facility);
            Open(1, "N1-R10");
            SubquestRules.Tick(0.1f);
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N1R10_LOG"), "a chart already read is the log");
            ServiceHub.Cases.Tick();
            Assert.IsTrue(ServiceHub.Cases.SubmitDecision("N1-R10", "dec_remote_shutoff", null).Accepted,
                          "the report opens on the facility chart alone");

            // The quest first, the chart later.
            ServiceHub.ResetPlaythrough();
            Open(1, "N1-R10");
            SubquestRules.Tick(0.1f);
            Assert.IsFalse(ServiceHub.Evidence.Has("EV_N1R10_LOG"));
            ServiceHub.Evidence.Acquire("EV_304_WATER", Evidence.EvidenceSource.Facility);
            SubquestRules.Tick(0.1f);
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N1R10_LOG"), "reading the chart mid-quest files the log");

            // And on a night without the quest the chart stays N1-M01's alone.
            ServiceHub.ResetPlaythrough();
            ServiceHub.Evidence.Acquire("EV_304_WATER", Evidence.EvidenceSource.Facility);
            SubquestRules.Tick(0.1f);
            Assert.IsFalse(ServiceHub.Evidence.Has("EV_N1R10_LOG"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBroadcastIsOnlyVerifiedWithASecondWitness()
        {
            ServiceHub.State.SetFlag(SubquestRules.WallResponse404, true);   // answered on night 3
            Open(5, "N5-R13");
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N5R13_KNOCK"), "night 3's knock is in the log");

            ServiceHub.Evidence.Acquire("EV_N5R13_PATTERN", Evidence.EvidenceSource.WorldPickup);
            Assert.IsFalse(ServiceHub.State.GetFlag(SubquestRules.N5R13CrossChecked), "a pattern alone is noise");
            ServiceHub.Evidence.Acquire("EV_N5R13_POSITION", Evidence.EvidenceSource.WorldPickup);
            Assert.IsTrue(ServiceHub.State.GetFlag(SubquestRules.N5R13CrossChecked));

            ServiceHub.Cases.Tick();
            var result = ServiceHub.Cases.SubmitDecision("N5-R13", "dec_crosscheck",
                                                          new[] { "EV_N5R13_PATTERN", "EV_N5R13_POSITION" });
            Assert.AreEqual(DecisionQuality.Correct, result.Quality);
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EndDongsikBroadcastVerified));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NightOneRunsInTheOrderTheGddLaysOut()
        {
            ServiceHub.State.BeginNight(1);
            ServiceHub.Cases.BeginNight(1);

            var chain = new List<string>(ServiceHub.Cases.NightChain);
            Assert.That(chain.Count, Is.InRange(5, 6), "v5.1 4.1: five or six quests");
            Assert.AreEqual("N1-R01", chain[1], "v5.1 4.2: random A, then the story subquest");
            Assert.AreEqual("N1-M01", chain[3], "then random B, then the main");

            Assert.IsTrue(NightPoolService.HasAuthoredPool(1));
            Assert.AreEqual(0, ServiceHub.ManualEvents.ActiveEvents.Count,
                            "the old night-response chain does not run on a v5.1 night");
            yield return null;
        }
    }
}
