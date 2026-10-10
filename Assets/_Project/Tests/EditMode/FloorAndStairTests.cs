using System.Collections.Generic;
using NUnit.Framework;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// The compressed building and its stairs (v2.1 spec 0.7, 0.8, 27, 30.1).
    ///
    /// The single behaviour worth testing here is the one spec 0.8.1 bans: a stair door that
    /// comes out somewhere other than the floor next to the one it was entered from. It is
    /// worth an EditMode test rather than a play-through because it is invisible until someone
    /// walks it, and because it is exactly the kind of thing a later floor addition breaks.
    /// </summary>
    public sealed class FloorPlanTests
    {
        [Test]
        public void BuildingIsB1ToSixF()
        {
            // v5.1 3.1: seven floors and nothing else.
            CollectionAssert.AreEqual(
                new[] { "B1", "F1", "F2", "F3", "F4", "F5", "F6" },
                FloorPlan.Order);
        }

        [Test]
        public void ThirteenthFloorIsNotAFloor()
        {
            // Spec 0.7.1 / 32. Not a fixture the code checks for - simply absent, so nothing
            // that resolves a destination can return it.
            Assert.IsFalse(FloorPlan.Exists(FloorPlan.PhantomF13));
            Assert.AreEqual(-1, FloorPlan.IndexOf(FloorPlan.PhantomF13));
            Assert.IsFalse(FloorPlan.IsElevatorDestination(FloorPlan.PhantomF13));
            Assert.IsNull(FloorPlan.Up(FloorPlan.PhantomF13));
            Assert.IsNull(FloorPlan.Down(FloorPlan.PhantomF13));
        }

        [Test]
        public void ElevatorPanelCannotNameTheThirteenthFloor()
        {
            foreach (var button in Gameplay.ElevatorController.Panel)
            {
                Assert.AreNotEqual(FloorPlan.PhantomF13, button.FloorId);
                Assert.IsTrue(FloorPlan.IsElevatorDestination(button.FloorId),
                              button.FloorId + " is on the panel but not a lift stop");
            }
        }

        [Test]
        public void SixthFloorIsTheTop()
        {
            // v5.1 3.1: nothing is built above 6F.
            Assert.IsTrue(FloorPlan.IsElevatorDestination(FloorPlan.F6));
            Assert.IsNull(FloorPlan.Up(FloorPlan.F6));
            Assert.AreEqual(FloorPlan.F5, FloorPlan.Down(FloorPlan.F6));
        }

        [Test]
        public void B1IsTheBottomAndHoldsThePlantAndRecords()
        {
            Assert.IsNull(FloorPlan.Down(FloorPlan.B1));
            Assert.AreEqual(FloorPlan.F1, FloorPlan.Up(FloorPlan.B1));

            foreach (var zone in new[] { ZoneIds.Machinery, ZoneIds.PumpRoom, ZoneIds.Archive, ZoneIds.Parking })
                Assert.AreEqual(FloorPlan.B1, FloorPlan.FloorOfZone(zone), zone);
        }

        [Test]
        public void EveryFloorHasAPrimaryZoneAndNoZoneIsSharedBetweenFloors()
        {
            var owner = new Dictionary<string, string>();

            foreach (var floor in FloorPlan.All)
            {
                Assert.IsNotEmpty(floor.PrimaryZoneId, floor.FloorId + " has no primary zone");
                Assert.AreEqual(floor.FloorId, FloorPlan.FloorOfZone(floor.PrimaryZoneId));

                foreach (var zone in floor.NormalZones) Claim(owner, zone, floor.FloorId);
                foreach (var zone in floor.ConditionalZones) Claim(owner, zone, floor.FloorId);
            }
        }

        static void Claim(Dictionary<string, string> owner, string zoneId, string floorId)
        {
            string existing;
            if (owner.TryGetValue(zoneId, out existing))
                Assert.Fail("zone " + zoneId + " is on both " + existing + " and " + floorId);
            owner[zoneId] = floorId;
        }

        [Test]
        public void EveryFloorZoneHasAStreamingGroup()
        {
            foreach (var floor in FloorPlan.All)
            {
                foreach (var zone in floor.NormalZones)
                    Assert.IsNotNull(ZoneGroups.GroupOf(zone), zone + " is in no scene group");
                foreach (var zone in floor.ConditionalZones)
                    Assert.IsNotNull(ZoneGroups.GroupOf(zone), zone + " is in no scene group");
            }
        }

        [Test]
        public void TransitZonesBelongToNoFloor()
        {
            // The car and the shaft move between floors, so neither has one. Anything that
            // asked "which floor is the stairwell on" would be asking the wrong question.
            Assert.IsNull(FloorPlan.FloorOfZone(ZoneIds.Elevator));
            Assert.IsNull(FloorPlan.FloorOfZone(ZoneIds.Stairwell));
        }
    }

    /// <summary>The spec 30.1 stair matrix, walked in both directions.</summary>
    public sealed class StairNavigatorTests
    {
        StairNavigator _stairs;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _stairs = new StairNavigator();
        }

        [TearDown] public void TearDown() { EventBus.Clear(); }

        [Test]
        public void FirstLandingIsAlwaysTheFloorYouEnteredFrom()
        {
            // Spec 0.8.2, and the whole of the bug spec 0.8.1 describes.
            foreach (var floorId in FloorPlan.Order)
            {
                Assert.IsTrue(_stairs.EnterFrom(floorId));
                Assert.AreEqual(floorId, _stairs.CurrentLanding);
                Assert.AreEqual(floorId, _stairs.EntryFloor);
                Assert.AreEqual(FloorPlan.SignOf(floorId), _stairs.LandingSign);
            }
        }

        [Test]
        public void EnteringFromTheFourthFloorNeverReachesTheLobby()
        {
            _stairs.EnterFrom(FloorPlan.F4);
            Assert.AreEqual("4F", _stairs.LandingSign);
            Assert.AreEqual(ZoneIds.Floor04, _stairs.ExitZone);

            Assert.AreEqual(FloorPlan.F5, _stairs.Ascend());
            Assert.AreEqual(FloorPlan.F4, _stairs.Descend());
            Assert.AreEqual(FloorPlan.F3, _stairs.Descend());
            Assert.AreEqual(ZoneIds.Floor03, _stairs.ExitZone);
        }

        [Test]
        public void WalksTheWholeSpec301Matrix()
        {
            // B1 -> up -> ... -> 6F, then all the way back down. Every rung of the ladder, in
            // both directions, from a single entry.
            _stairs.EnterFrom(FloorPlan.B1);
            for (int i = 1; i < FloorPlan.Order.Length; i++)
                Assert.AreEqual(FloorPlan.Order[i], _stairs.Ascend(),
                                "climbing from " + FloorPlan.Order[i - 1]);

            for (int i = FloorPlan.Order.Length - 2; i >= 0; i--)
                Assert.AreEqual(FloorPlan.Order[i], _stairs.Descend(),
                                "descending to " + FloorPlan.Order[i]);
        }

        [Test]
        public void ShaftEndsAreDeadEnds()
        {
            _stairs.EnterFrom(FloorPlan.B1);
            Assert.IsNull(_stairs.Descend());
            Assert.AreEqual(FloorPlan.B1, _stairs.CurrentLanding);

            _stairs.EnterFrom(FloorPlan.F6);
            Assert.IsNull(_stairs.Ascend());
            Assert.AreEqual(FloorPlan.F6, _stairs.CurrentLanding);
        }

        [Test]
        public void EmergencyVariantDoesNotChangeAdjacency()
        {
            // Spec 30.1: an outage must not resurrect the teleport-to-lobby behaviour.
            _stairs.EnterFrom(FloorPlan.F5, StairVariant.Emergency);
            Assert.AreEqual(StairVariant.Emergency, _stairs.Variant);
            Assert.AreEqual(FloorPlan.F4, _stairs.Descend());
            Assert.AreEqual(FloorPlan.F5, _stairs.Ascend());
        }

        [Test]
        public void SaveAndLoadKeepsTheEntryFloorAndLanding()
        {
            _stairs.EnterFrom(FloorPlan.F6);
            _stairs.Descend();
            _stairs.Descend();

            var restored = new StairNavigator();
            restored.LoadFrom(_stairs.EntryFloor, _stairs.CurrentLanding, (int)_stairs.DirectionHint,
                              (int)_stairs.Variant, _stairs.InStairwell, _stairs.FlightsWalked);

            Assert.AreEqual(FloorPlan.F6, restored.EntryFloor);
            Assert.AreEqual(FloorPlan.F4, restored.CurrentLanding);
            Assert.IsTrue(restored.InStairwell);
            Assert.AreEqual(2, restored.FlightsWalked);
        }

        [Test]
        public void OverrideCanRepeatTheFloorTheWalkStartedOn()
        {
            // Spec 0.8.4 M07: entering at 5F and descending gives 5F again, and the player
            // can tell because they know which floor they started on.
            _stairs.EnterFrom(FloorPlan.F5);
            _stairs.SetOverride("M07", (from, direction, normalTarget, flights) =>
                flights < 2 ? from : null);

            Assert.AreEqual(FloorPlan.F5, _stairs.Descend());
            Assert.AreEqual(FloorPlan.F5, _stairs.Descend());
            Assert.AreEqual(StairVariant.Distorted, _stairs.Variant);

            // Third flight: the override yields and normal adjacency comes back (spec 30.1).
            Assert.AreEqual(FloorPlan.F4, _stairs.Descend());

            _stairs.ClearOverride("M07");
            Assert.AreEqual(StairVariant.Normal, _stairs.Variant);
            Assert.AreEqual(FloorPlan.F3, _stairs.Descend());
        }

        [Test]
        public void OnlyOneAnomalyMayHoldTheShaft()
        {
            _stairs.EnterFrom(FloorPlan.F5);
            _stairs.SetOverride("M07", (f, d, n, c) => f);

            // The refusal is logged as an error, because two anomalies bending one shaft is a
            // content bug rather than a game state.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                _stairs.SetOverride("M01", (f, d, n, c) => FloorPlan.B1);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            Assert.AreEqual("M07", _stairs.OverrideOwner);
            _stairs.ClearOverride("M01");
            Assert.IsTrue(_stairs.HasOverride, "a second event must not be able to clear the first");
        }

        [Test]
        public void LeavingTheShaftDropsTheOverride()
        {
            _stairs.EnterFrom(FloorPlan.F5);
            _stairs.SetOverride("M07", (f, d, n, c) => f);
            _stairs.Exit();

            Assert.IsFalse(_stairs.InStairwell);
            Assert.IsFalse(_stairs.HasOverride);
        }

        [Test]
        public void ResidentGroupsCoverTheLandingAndOneFlightEitherSide()
        {
            // Spec 0.8.3: two segments resident, not the whole shaft.
            _stairs.EnterFrom(FloorPlan.F4);
            var groups = _stairs.ResidentGroups();

            CollectionAssert.Contains(groups, ZoneGroups.Floor04);
            CollectionAssert.Contains(groups, ZoneGroups.Floor03);
            CollectionAssert.Contains(groups, ZoneGroups.Floor05);
            CollectionAssert.DoesNotContain(groups, ZoneGroups.B1);
        }

        [Test]
        public void RefusesToEnterFromSomethingThatIsNotAFloor()
        {
            // The refusal is logged as an error because it is a build mistake, not a game
            // state - so the test has to say it expects one.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.IsFalse(_stairs.EnterFrom(FloorPlan.PhantomF13));
                Assert.IsFalse(_stairs.InStairwell);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
