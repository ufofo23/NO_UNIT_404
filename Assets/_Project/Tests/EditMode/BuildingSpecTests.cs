using NUnit.Framework;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// Locks the blockout to the module specification in GDD 17.4 / 17.5. These numbers are
    /// what make a 복도식 corridor feel like one, and letting them drift silently is how a
    /// greybox becomes impossible to replace with real modules.
    /// </summary>
    public sealed class BuildingSpecTests
    {
        static readonly string[] Corridors = { ZoneIds.Floor04, ZoneIds.Floor03, ZoneIds.PhantomFloor13 };

        [Test]
        public void CorridorsAreTheSpecifiedWidth()
        {
            for (int i = 0; i < Corridors.Length; i++)
            {
                var size = WorldBuilder.SizeOf(Corridors[i]);
                Assert.AreEqual(2.4f, size.y, 0.001f,
                                Corridors[i] + " corridor width must be 2.4m (GDD 17.4)");
            }
        }

        /// <summary>
        /// GDD 17.5.4. The service passage is meant to feel like the building's back of house,
        /// and it only does that by being tighter than the corridors it runs behind. Widening
        /// the corridors without checking this is how that contrast quietly disappears.
        /// </summary>
        [Test]
        public void TheServicePassageIsStillTighterThanACorridor()
        {
            Assert.Less(WorldBuilder.SizeOf(ZoneIds.ServicePassage).y, BuildingSpec.CorridorWidth,
                        "the crawl behind the fourth floor has to stay the cramped one");
        }

        [Test]
        public void RoomsUseTheSpecifiedWallHeight()
        {
            // The stairwell is not on this list any more. It is a shaft rather than a room
            // (GDD 17.5.8): five landings a flight apart, so its ceiling is four storeys up.
            string[] standardHeight =
            {
                ZoneIds.Office, ZoneIds.Lobby,
                ZoneIds.Parking, ZoneIds.Archive,
                ZoneIds.Floor04, ZoneIds.Floor03, ZoneIds.PhantomFloor13
            };

            for (int i = 0; i < standardHeight.Length; i++)
                Assert.AreEqual(2.6f, WorldBuilder.HeightOf(standardHeight[i]), 0.001f,
                                standardHeight[i] + " must use the 2.6m wall height (GDD 17.4)");
        }

        [Test]
        public void EveryPlanDimensionSitsOnTheHalfMetreGrid()
        {
            for (int i = 0; i < ZoneIds.All.Length; i++)
            {
                var size = WorldBuilder.SizeOf(ZoneIds.All[i]);

                // The corridor width is a spec value in its own right, not a grid multiple.
                if (Mathf.Abs(size.y - BuildingSpec.CorridorWidth) > 0.001f)
                    Assert.IsTrue(BuildingSpec.IsOnGrid(size.y),
                                  ZoneIds.All[i] + " depth " + size.y + " is off the 0.5m grid");

                Assert.IsTrue(BuildingSpec.IsOnGrid(size.x),
                              ZoneIds.All[i] + " width " + size.x + " is off the 0.5m grid");
            }
        }

        [Test]
        public void TheOfficeAndParkingMatchTheirArtSpecSizes()
        {
            var office = WorldBuilder.SizeOf(ZoneIds.Office);
            Assert.AreEqual(8f, office.x, 0.001f, "GDD 17.5.1: about 8m x 6m");
            Assert.AreEqual(6f, office.y, 0.001f);

            var parking = WorldBuilder.SizeOf(ZoneIds.Parking);
            Assert.AreEqual(25f, parking.x, 0.001f, "GDD 17.5.3: a 25m x 18m section");
            Assert.AreEqual(18f, parking.y, 0.001f);
        }

        [Test]
        public void Unit404IsEighteenSquareMetres()
        {
            var size = WorldBuilder.SizeOf(ZoneIds.Unit404);
            Assert.AreEqual(18f, size.x * size.y, 0.01f, "GDD 17.5.7: 18 square metres");
        }

        [Test]
        public void TheWallHidingUnit404IsExactlyOnePointTwoMetresWider()
        {
            float standardGap = BuildingSpec.StandardDoorX[3] - BuildingSpec.StandardDoorX[2];
            float floor04Gap = BuildingSpec.Floor04DoorX[3] - BuildingSpec.Floor04DoorX[2];

            Assert.AreEqual(BuildingSpec.HiddenWallExtra, floor04Gap - standardGap, 0.001f,
                            "GDD 17.5.4: the wall between 403 and 405 is 1.2m wider");

            // And that one stretch is the only thing in the plan that breaks the grid.
            Assert.IsFalse(BuildingSpec.IsOnGrid(BuildingSpec.Floor04DoorX[3]),
                           "the hidden room is meant to be the part that does not fit the module grid");
        }

        [Test]
        public void UnitDoorsFitUnderTheCeiling()
        {
            Assert.Less(BuildingSpec.UnitDoorHeight, BuildingSpec.WallHeight);
            Assert.Less(BuildingSpec.FireDoorHeight, WorldBuilder.HeightOf(ZoneIds.ServicePassage),
                        "a 2.1m fire door has to fit in the service passage");
        }

        [Test]
        public void EveryCameraIsMountedInsideItsOwnZone()
        {
            var content = new ContentData.ContentDatabase();
            content.Load();

            var channels = content.CctvChannels;
            for (int i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                var half = WorldBuilder.SizeOf(channel.zoneId) * 0.5f;
                float ceiling = WorldBuilder.HeightOf(channel.zoneId);

                Assert.LessOrEqual(Mathf.Abs(channel.localPosition.x), half.x,
                                   channel.cameraId + " is outside its zone on x");
                Assert.LessOrEqual(Mathf.Abs(channel.localPosition.z), half.y,
                                   channel.cameraId + " is outside its zone on z");

                if (channel.zoneId == ZoneIds.Rooftop) continue;   // open sky
                Assert.Less(channel.localPosition.y, ceiling,
                            channel.cameraId + " is mounted through the ceiling of " + channel.zoneId);
            }
        }

        [Test]
        public void TheCorridorIsWideEnoughToWalkDown()
        {
            // CharacterController radius 0.3 -> 0.6m of body, plus 0.2m of wall thickness.
            float clear = BuildingSpec.CorridorWidth - BuildingSpec.WallThickness;

            // Not "fits" but "reads as a corridor": at 1.55m it fit and a playtester still
            // could not tell one from a duct, because a 68 degree camera saw two walls and
            // nothing else. Twice the body width is the floor for that.
            Assert.Greater(clear, 1.2f, "the corridor is too tight to read as a corridor");
        }

        /// <summary>
        /// GDD 17.5.8. The stairwell only works if its stairs can actually be climbed, so the
        /// step geometry is checked against the controller that has to climb it rather than
        /// against a drawing.
        /// </summary>
        [Test]
        public void TheStairsCanBeClimbed()
        {
            Assert.Less(BuildingSpec.StepRise, 0.35f,
                        "a step taller than PlayerController's step offset cannot be walked up");
            Assert.Greater(BuildingSpec.StepRun, 0.25f, "the treads are too shallow to stand on");

            // A flight has to climb exactly one landing gap, or the landings drift off the
            // flights that are supposed to reach them.
            Assert.AreEqual(BuildingSpec.FlightRise,
                            BuildingSpec.StepRise * BuildingSpec.StepsPerFlight, 0.001f);

            float slope = BuildingSpec.StepRise / BuildingSpec.StepRun;
            Assert.Less(Mathf.Atan(slope) * Mathf.Rad2Deg, 45f,
                        "the flight is steeper than the controller's slope limit");
        }

        /// <summary>
        /// Every floor the stairwell claims to serve has a landing at a different height, and
        /// the shaft is tall enough to contain all of them. A sign that points at a landing
        /// which is not there is worse than the unlabelled plates it replaced.
        /// </summary>
        [Test]
        public void TheStairShaftContainsEveryLandingItSigns()
        {
            // v2.1 spec 27: one landing per floor, B2 to the roof, and no gaps between them.
            // Taken from FloorPlan rather than listed, so a floor added later is checked here
            // whether or not anyone remembers to add it.
            var order = FloorPlan.Order;
            for (int i = 1; i < order.Length; i++)
            {
                float below = WorldBuilder.StairShaft.HeightOf(order[i - 1]);
                float here = WorldBuilder.StairShaft.HeightOf(order[i]);
                Assert.AreEqual(BuildingSpec.FlightRise, here - below, 0.001f,
                                order[i] + " is not one flight above " + order[i - 1]);
            }

            // Landings alternate ends, which is what makes the flights a switchback.
            for (int i = 1; i < order.Length; i++)
                Assert.AreNotEqual(WorldBuilder.StairShaft.IsNorth(order[i - 1]),
                                   WorldBuilder.StairShaft.IsNorth(order[i]),
                                   order[i] + " is at the same end of the shaft as " + order[i - 1]);

            float lowest = WorldBuilder.StairShaft.HeightOf(FloorPlan.B2);
            Assert.AreEqual(WorldBuilder.StairShaft.Drop, -lowest, 0.001f,
                            "the slab has to reach the lowest landing");
            Assert.AreEqual(WorldBuilder.StairShaft.Drop,
                            WorldBuilder.FloorDropOf(ZoneIds.Stairwell), 0.001f,
                            "PlayerController reads the drop from the zone, so they must agree");

            float ceiling = WorldBuilder.HeightOf(ZoneIds.Stairwell);
            float top = WorldBuilder.StairShaft.HeightOf(FloorPlan.Roof);
            Assert.GreaterOrEqual(ceiling, top + BuildingSpec.UnitDoorHeight,
                                  "the top landing's door does not fit under the shaft ceiling");
        }
    }
}
