using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using NO404.Anomalies;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// The risk model seen from inside the building (v2.1 spec 30.2 paths 8 and 9, spec 0.10.3).
    ///
    /// These are the two QA paths a tester cannot reach by playing well or badly - a floor
    /// already at 4, an exposure already past 75 - and they are the two whose failure mode is
    /// the most misleading: the counter is right, the save is right, and the corridor is
    /// spotless. Every one of the links between those two facts has been separately broken
    /// during this work, so the whole chain is walked here in a real built world rather than
    /// asserted a piece at a time.
    ///
    /// PlayMode because none of it exists otherwise. FloorDressing does its work in OnEnable
    /// and Update, and neither runs in edit mode - which is exactly how an earlier version of
    /// this passed every EditMode test while dressing nothing.
    /// </summary>
    public sealed class RiskDressingWorldTests
    {
        GameObject _root;

        static string[] StreamedZones()
        {
            var zones = new List<string>();
            for (int i = 0; i < ZoneGroups.Streamed.Length; i++)
                zones.AddRange(ZoneGroups.ZonesIn(ZoneGroups.Streamed[i]));
            return zones.ToArray();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;

            ServiceHub.Zones.UnloadAllStreamed();
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;

            ServiceHub.Zones.LoadCoreImmediate();
            yield return null;

            _root = new GameObject("DressingFloors");
            WorldBuilder.Create(_root.transform).Build(StreamedZones());
            yield return null;

            ClearRisk();
        }

        [TearDown]
        public void TearDown()
        {
            ClearRisk();

            var built = StreamedZones();
            for (int i = 0; i < built.Length; i++) ZoneRegistry.Unregister(built[i]);

            if (_root != null) Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// Put the building back the way it was found.
        ///
        /// Risk is deliberately the one thing that does not decay between nights (spec 0.10),
        /// so a test that raised it and walked away would hand the next test a hostile floor
        /// and no reason for it.
        /// </summary>
        static void ClearRisk()
        {
            var risk = ServiceHub.Risk;
            var order = FloorPlan.Order;
            for (int i = 0; i < order.Length; i++)
            {
                int value = risk.FloorRisk(order[i]);
                if (value != 0) risk.AddFloorRisk(order[i], -value, "test");
            }

            if (risk.Exposure != 0) risk.AddExposure(-risk.Exposure, "test");
        }

        static List<FloorDressing> DressingsOn(string floorId)
        {
            var found = new List<FloorDressing>();
            var all = Object.FindObjectsByType<FloorDressing>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].FloorId == floorId) found.Add(all[i]);
            return found;
        }

        static int ActiveRiskProps(FloorDressing dressing)
        {
            int count = 0;
            foreach (Transform child in dressing.transform)
                if (child.name.StartsWith("Risk") && child.gameObject.activeSelf) count++;
            return count;
        }

        // ---- the wiring ------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryRoomThatBelongsToAFloorCanBeDressed()
        {
            // The line that attaches these lives in WorldBuilder and is one `if` away from
            // silently covering nothing. A zone missing its dressing is a room that stays
            // clean however badly its floor is handled.
            for (int i = 0; i < ZoneIds.All.Length; i++)
            {
                var zoneId = ZoneIds.All[i];
                var floorId = FloorPlan.FloorOfZone(zoneId);
                if (string.IsNullOrEmpty(floorId)) continue;      // the lift and the shaft
                if (!ZoneRegistry.IsLoaded(zoneId)) continue;     // not part of this build

                var root = ZoneRegistry.Find(zoneId);
                var dressing = root.GetComponent<FloorDressing>();

                Assert.IsNotNull(dressing, zoneId + " has no dressing, so its floor can never show");
                Assert.AreEqual(floorId, dressing.FloorId, zoneId + " is dressed for the wrong floor");
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator TransitZonesAreNotDressedBecauseTheyBelongToNoFloor()
        {
            // The lift and the shaft cross every storey, so a risk tier for them would be a
            // tier for nowhere (spec 0.7.1 / 27).
            foreach (var zoneId in new[] { ZoneIds.Elevator, ZoneIds.Stairwell })
            {
                if (!ZoneRegistry.IsLoaded(zoneId)) continue;
                Assert.IsNull(ZoneRegistry.Find(zoneId).GetComponent<FloorDressing>(),
                              zoneId + " belongs to no floor and should carry no tier");
            }

            yield break;
        }

        // ---- path 9: a floor already at 4 ------------------------------------

        [UnityTest]
        public IEnumerator RaisingAFloorDressesEveryRoomOnItAndNothingElse()
        {
            var target = DressingsOn(FloorPlan.F5);
            var untouched = DressingsOn(FloorPlan.F2);

            Assert.Greater(target.Count, 0, "the fifth floor should have rooms to dress");
            Assert.Greater(untouched.Count, 0, "and the second should have rooms to leave alone");

            ServiceHub.Risk.AddFloorRisk(FloorPlan.F5, 4, "test");
            yield return null;

            foreach (var dressing in target)
            {
                Assert.AreEqual(FloorRiskTier.Hostile, dressing.AppliedTier,
                                dressing.ZoneId + " did not hear about its floor");
                Assert.Greater(ActiveRiskProps(dressing), 0, dressing.ZoneId + " looks fine and should not");
            }

            foreach (var dressing in untouched)
            {
                Assert.AreEqual(FloorRiskTier.Clear, dressing.AppliedTier,
                                dressing.ZoneId + " is on a different floor and should be untouched");
                Assert.AreEqual(0, ActiveRiskProps(dressing));
            }
        }

        [UnityTest]
        public IEnumerator TheTiersArriveInTheOrderSpec0103Lists()
        {
            var dressing = DressingsOn(FloorPlan.F4)[0];

            ServiceHub.Risk.AddFloorRisk(FloorPlan.F4, 1, "test");
            yield return null;
            Assert.AreEqual(0, ActiveRiskProps(dressing), "tier 1 is sound and light only");

            ServiceHub.Risk.AddFloorRisk(FloorPlan.F4, 1, "test");
            yield return null;
            int atTwo = ActiveRiskProps(dressing);
            Assert.Greater(atTwo, 0, "tier 2 puts an afterimage in the room");

            ServiceHub.Risk.AddFloorRisk(FloorPlan.F4, 1, "test");
            yield return null;
            Assert.Greater(ActiveRiskProps(dressing), atTwo, "tier 3 adds the door that is not one");
        }

        [UnityTest]
        public IEnumerator PuttingAFloorRightTakesItAllBackOff()
        {
            // The ladder has to run both ways, or nothing the caretaker does to repair a floor
            // ever reads as repair.
            var dressing = DressingsOn(FloorPlan.B2)[0];

            ServiceHub.Risk.AddFloorRisk(FloorPlan.B2, 5, "test");
            yield return null;
            Assert.Greater(ActiveRiskProps(dressing), 0);

            ServiceHub.Risk.AddFloorRisk(FloorPlan.B2, -5, "test");
            yield return null;

            Assert.AreEqual(FloorRiskTier.Clear, dressing.AppliedTier);
            Assert.AreEqual(0, ActiveRiskProps(dressing), "a repaired floor should look repaired");
        }

        [UnityTest]
        public IEnumerator TheDoorThatIsNotADoorRefusesRatherThanBeingAbsent()
        {
            // Spec 0.4: the world does not mark the wrong answer. The false door has to be
            // reachable and has to say no, not be a wall the player never notices.
            ServiceHub.Risk.AddFloorRisk(FloorPlan.F5, 3, "test");
            yield return null;

            FalseDoor door = null;
            foreach (var dressing in DressingsOn(FloorPlan.F5))
                foreach (Transform child in dressing.transform)
                {
                    var candidate = child.GetComponent<FalseDoor>();
                    if (candidate != null && child.gameObject.activeSelf) door = candidate;
                }

            Assert.IsNotNull(door, "tier 3 owes the floor a false door");

            var context = default(Interaction.PlayerContext);
            string reason;
            Assert.IsTrue(door.CanInteract(context, out reason), "it has to be worth trying");
        }

        // ---- path 8: exposure already past 75 --------------------------------

        [UnityTest]
        public IEnumerator TheStairSignsCanBeMisread()
        {
            var labels = Object.FindObjectsByType<DistortedLabel>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None);
            Assert.Greater(labels.Length, 0, "the stairwell should carry labels that can go wrong");

            foreach (var label in labels)
                Assert.IsFalse(string.IsNullOrEmpty(label.Truth),
                               "a label that does not know its own truth can never be restored");

            yield break;
        }

        [UnityTest]
        public IEnumerator ACleanCaretakerSeesCleanSigns()
        {
            var label = Object.FindObjectsByType<DistortedLabel>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None)[0];
            var text = label.GetComponent<Text>();

            for (int i = 0; i < 30; i++)
            {
                ServiceHub.Clock.AdvanceSeconds(DistortedLabel.HoldSeconds + 1);
                yield return null;
                Assert.AreEqual(label.Truth, text.text, "nothing should be wrong at exposure 0");
                Assert.IsFalse(label.IsDistorted);
            }
        }

        [UnityTest]
        public IEnumerator AtHighExposureASignEventuallyLiesAndThenCorrectsItself()
        {
            ServiceHub.Risk.AddExposure(80, "test");
            Assert.AreEqual(DistortionBand.Severe, ServiceHub.Risk.Band);

            var labels = Object.FindObjectsByType<DistortedLabel>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None);
            bool sawALie = false;
            bool sawItCorrected = false;

            // Each attempt is one interval apart on the game clock, and an attempt only lands
            // some of the time - which is the point, since a sign that was always wrong would
            // be a sign nobody reads. Sixty attempts across every label makes never seeing one
            // a real failure rather than bad luck.
            for (int i = 0; i < 60 && !(sawALie && sawItCorrected); i++)
            {
                ServiceHub.Clock.AdvanceSeconds(DistortedLabel.IntervalSeconds + 1);
                yield return null;

                foreach (var label in labels)
                {
                    if (label.IsDistorted) sawALie = true;
                    else if (sawALie) sawItCorrected = true;
                }
            }

            Assert.IsTrue(sawALie, "at exposure 80 a landing sign should sometimes be wrong");
            Assert.IsTrue(sawItCorrected, "and looking again has to put it right (spec 0.10.4)");
        }

        [UnityTest]
        public IEnumerator BringingTheExposureBackDownRestoresEverySign()
        {
            ServiceHub.Risk.AddExposure(90, "test");

            var labels = Object.FindObjectsByType<DistortedLabel>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None);
            for (int i = 0; i < 20; i++)
            {
                ServiceHub.Clock.AdvanceSeconds(DistortedLabel.IntervalSeconds + 1);
                yield return null;
            }

            ServiceHub.Risk.AddExposure(-ServiceHub.Risk.Exposure, "test");
            yield return null;
            yield return null;

            foreach (var label in labels)
            {
                Assert.IsFalse(label.IsDistorted, label.name + " stayed wrong after the exposure cleared");
                Assert.AreEqual(label.Truth, label.GetComponent<Text>().text);
            }
        }

        [UnityTest]
        public IEnumerator ASignNeverNamesAFloorTheBuildingDoesNotHave()
        {
            // The one thing a misreading may never do (spec 0.7.1). Sixty rounds at the band
            // where misreading is most likely, checking every label each time.
            ServiceHub.Risk.AddExposure(99, "test");

            var labels = Object.FindObjectsByType<DistortedLabel>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None);
            var legal = new HashSet<string>();
            for (int i = 0; i < FloorPlan.Order.Length; i++)
            {
                legal.Add(Loc.T(DistortedLabel.LandingKeyOf(FloorPlan.Order[i])));
                legal.Add(Loc.T("world.stairs." + FloorPlan.Order[i].ToLowerInvariant()));
            }

            for (int round = 0; round < 60; round++)
            {
                ServiceHub.Clock.AdvanceSeconds(DistortedLabel.IntervalSeconds + 1);
                yield return null;

                foreach (var label in labels)
                {
                    var shown = label.GetComponent<Text>().text;

                    // A corrupted glyph is not a floor claim - it is the truth with one
                    // character knocked out, and it keeps its length.
                    if (shown.Length == label.Truth.Length && shown != label.Truth) continue;

                    Assert.IsTrue(legal.Contains(shown),
                                  "a sign read '" + shown + "', which is no floor of this building");
                }
            }
        }
    }
}
