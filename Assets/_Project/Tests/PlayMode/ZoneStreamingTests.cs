using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// Exercises the real additive streaming path (GDD 20.5). These have to be PlayMode tests
    /// because they load and unload actual scenes.
    /// </summary>
    public sealed class ZoneStreamingTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Bootstrap installs itself before the first scene loads, so the services exist.
            while (!ServiceHub.Ready) yield return null;

            ServiceHub.Zones.UnloadAllStreamed();

            // One frame is not enough: UnloadSceneAsync takes as long as it takes, and starting
            // a test while a group is still going away is how a corridor ends up with two
            // floors in it.
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;
        }

        [UnityTest]
        public IEnumerator CoreSceneRegistersTheAlwaysResidentZones()
        {
            ServiceHub.Zones.LoadCoreImmediate();
            yield return null;

            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Office), "the office must always be resident");
            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Lobby));
            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Elevator));
            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Stairwell));

            Assert.IsNotNull(WorldBuilder.OfficeSpawn, "the player needs a spawn in the core scene");
            Assert.IsNotNull(WorldBuilder.OfficeTerminal, "the facility PC lives in the core scene");
        }

        [UnityTest]
        public IEnumerator AStreamedFloorIsNotResidentUntilItIsAskedFor()
        {
            ServiceHub.Zones.LoadCoreImmediate();
            yield return null;

            Assert.IsFalse(ZoneRegistry.IsLoaded(ZoneIds.Floor03),
                           "the eighth floor must not be paying for itself before anyone asks");

            ServiceHub.Zones.RequestZone(ZoneIds.Floor03);
            yield return WaitForZone(ZoneIds.Floor03);

            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Floor03));
            Assert.IsTrue(SceneManager.GetSceneByName(ZoneGroups.Floor03).isLoaded);
        }

        [UnityTest]
        public IEnumerator LoadingAFloorBringsEveryZoneInItsGroup()
        {
            ServiceHub.Zones.LoadCoreImmediate();
            ServiceHub.Zones.RequestZone(ZoneIds.Floor04);
            yield return WaitForZone(ZoneIds.Floor04);

            // The service passage and unit 404 are only reachable through the fourth floor,
            // so they travel in the same scene and never need a second load.
            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.ServicePassage));
            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Unit404));
        }

        [UnityTest]
        public IEnumerator AnUnusedFloorIsEvictedAndItsZonesDeregistered()
        {
            ServiceHub.Zones.LoadCoreImmediate();
            ServiceHub.Zones.RequestZone(ZoneIds.Floor06);
            yield return WaitForZone(ZoneIds.Floor06);

            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Floor06));

            // Nothing renews the request and the player is still in the office.
            ServiceHub.Player.EnterZone(ZoneIds.Office);
            float deadline = Time.realtimeSinceStartup + ZoneStreamer.KeepAliveSeconds + 5f;

            while (ZoneRegistry.IsLoaded(ZoneIds.Floor06) && Time.realtimeSinceStartup < deadline)
            {
                ServiceHub.Zones.Tick();
                yield return null;
            }

            Assert.IsFalse(ZoneRegistry.IsLoaded(ZoneIds.Floor06),
                           "an unused floor must be unloaded and unregistered");
        }

        [UnityTest]
        public IEnumerator TheFloorThePlayerIsStandingOnIsNeverEvicted()
        {
            ServiceHub.Zones.LoadCoreImmediate();
            ServiceHub.Zones.RequestZone(ZoneIds.Floor03);
            yield return WaitForZone(ZoneIds.Floor03);

            ServiceHub.Player.EnterZone(ZoneIds.Floor03);

            float deadline = Time.realtimeSinceStartup + ZoneStreamer.KeepAliveSeconds + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                ServiceHub.Zones.Tick();
                yield return null;
            }

            Assert.IsTrue(ZoneRegistry.IsLoaded(ZoneIds.Floor03),
                          "unloading the floor under the player would drop them out of the world");

            ServiceHub.Player.EnterZone(ZoneIds.Office);
        }

        /// <summary>
        /// The corridor is only 1.55m wide now, so a prop placed a little too far from the
        /// wall would block the whole floor. This walks a player-sized capsule down the centre
        /// line of every corridor and fails if anything is in the way.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryCorridorHasAClearWalkableCentreLine()
        {
            ServiceHub.Zones.LoadCoreImmediate();

            string[] corridors = { ZoneIds.Floor02, ZoneIds.Floor03, ZoneIds.Floor04, ZoneIds.Floor05, ZoneIds.Floor06 };
            for (int c = 0; c < corridors.Length; c++)
            {
                ServiceHub.Zones.RequestZone(corridors[c]);
                yield return WaitForZone(corridors[c]);

                var root = ZoneRegistry.Find(corridors[c]);
                float halfLength = WorldBuilder.SizeOf(corridors[c]).x * 0.5f;

                // Colliders built this frame are not in the physics scene until it syncs, and
                // Unity does not sync on its own by default. Without this the overlap queries
                // below read stale transforms - which is why this test passed for months with
                // a capsule that reaches 0.15m into the floor slab, and then started failing
                // intermittently the moment another test loaded a zone before it.
                Physics.SyncTransforms();

                const float radius = 0.3f;      // CharacterController radius
                // Above the radius, so the lower cap never dips into the floor being stood on.
                const float bottom = 0.35f;
                const float top = PlayerController.StandHeight - 0.15f;

                for (float x = -halfLength + 1f; x <= halfLength - 1f; x += 0.5f)
                {
                    var centre = root.position + new Vector3(x, 0f, 0f);
                    var hits = Physics.OverlapCapsule(centre + Vector3.up * bottom,
                                                      centre + Vector3.up * top,
                                                      radius, ~0, QueryTriggerInteraction.Ignore);

                    for (int i = 0; i < hits.Length; i++)
                        Assert.Fail(corridors[c] + " is blocked at x=" + x + " by " + hits[i].name);
                }
            }
        }

        /// <summary>
        /// GDD 17.5.8. The stairwell is the one room whose job is to say which way is up, and
        /// a playtest said it did not: "the stairs only go to the roof and the first floor".
        /// It is now a shaft with five landings and real flights between them.
        ///
        /// Geometry that cannot be walked is worse than the plates it replaced, so this stands
        /// on every landing and checks two things: something solid is underfoot, and nothing is
        /// through the player's body. The landing heights come from the same table the world is
        /// built from, so this fails if a landing is ever signed but not built.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryStairLandingCanBeStoodOn()
        {
            ServiceHub.Zones.LoadCoreImmediate();
            ServiceHub.Zones.RequestZone(ZoneIds.Stairwell);
            yield return WaitForZone(ZoneIds.Stairwell);

            var root = ZoneRegistry.Find(ZoneIds.Stairwell);
            Assert.IsNotNull(root);
            Physics.SyncTransforms();

            // Every landing the compressed building has (v2.1 spec 27), from FloorPlan rather
            // than listed here, so a new floor is covered without anyone remembering to.
            var order = FloorPlan.Order;
            var heights = new float[order.Length];
            var landingZ = new float[order.Length];
            for (int f = 0; f < order.Length; f++)
            {
                heights[f] = WorldBuilder.StairShaft.HeightOf(order[f]);
                landingZ[f] = WorldBuilder.StairShaft.LandingZOf(order[f]);
            }

            const float radius = 0.3f;
            // Above the radius, so the capsule's lower cap never dips into the surface the
            // player is standing on - the floor under your feet is not an obstruction.
            const float bottom = 0.35f;
            const float top = PlayerController.StandHeight - 0.15f;

            for (int i = 0; i < heights.Length; i++)
            {
                var stand = root.position + new Vector3(0f, heights[i], landingZ[i]);

                RaycastHit hit;
                Assert.IsTrue(Physics.Raycast(stand + Vector3.up * 0.25f, Vector3.down, out hit, 0.6f,
                                              ~0, QueryTriggerInteraction.Ignore),
                              "nothing to stand on at landing " + i + " (y=" + heights[i] + ")");

                var hits = Physics.OverlapCapsule(stand + Vector3.up * bottom, stand + Vector3.up * top,
                                                  radius, ~0, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                    Assert.Fail("landing " + i + " is blocked by " + hits[h].name);
            }
        }

        /// <summary>
        /// And the flights between those landings have to be walkable too - both lanes, along
        /// their whole run. A rail across the mouth of a flight is exactly the sort of thing
        /// that looks right in the editor and cannot be walked into.
        /// </summary>
        [UnityTest]
        public IEnumerator BothStairLanesAreClearAlongTheirWholeRun()
        {
            ServiceHub.Zones.LoadCoreImmediate();
            ServiceHub.Zones.RequestZone(ZoneIds.Stairwell);
            yield return WaitForZone(ZoneIds.Stairwell);

            var root = ZoneRegistry.Find(ZoneIds.Stairwell);
            Physics.SyncTransforms();

            // All six flights, B1 to 6F, in the lanes WorldBuilder alternates them
            // between. Walking every one of them is the point: spec 0.8 makes the stairs a
            // continuous space, and a single flight nobody can pass turns it back into a
            // teleport menu with extra steps.
            var order = FloorPlan.Order;
            for (int i = 0; i + 1 < order.Length; i++)
            {
                string lower = order[i], upper = order[i + 1];
                float lane = (i % 2 == 0) ? WorldBuilder.StairShaft.LaneBX : WorldBuilder.StairShaft.LaneAX;

                CheckLane(root, lane, WorldBuilder.StairShaft.FlightEndOf(lower),
                          WorldBuilder.StairShaft.HeightOf(lower),
                          WorldBuilder.StairShaft.HeightOf(upper),
                          lower + "->" + upper);
            }
        }

        /// <summary>
        /// Walks the treads of one flight and checks the player can pass above each of them.
        ///
        /// What this can and cannot measure took two tries to get right. A capsule standing on
        /// a staircase always intersects the next riser up - that is what a staircase is, and
        /// the CharacterController climbs it with its step offset. So overlapping the whole
        /// body reports every correctly built flight as blocked.
        ///
        /// What a capsule can say is whether the player's torso and head get through, which is
        /// the failure this exists to catch: a handrail across the mouth of a flight, a landing
        /// slab at head height, one flight built through the one above it. So the sample starts
        /// at 0.95m - clear of any riser, well under the 2.05m doors - and runs to standing
        /// height.
        /// </summary>
        static void CheckLane(Transform root, float laneX, float halfRun,
                              float fromY, float toY, string what)
        {
            const float radius = 0.28f;
            const float bottom = 0.95f;
            float top = PlayerController.StandHeight - 0.2f;

            int steps = BuildingSpec.StepsPerFlight;
            float rise = (toY - fromY) / steps;
            float run = (-halfRun - halfRun) / steps;

            for (int i = 0; i < steps; i++)
            {
                // Standing on the top of tread i, in the middle of it.
                float tread = fromY + rise * (i + 1);
                float z = halfRun + run * (i + 0.5f);

                var stand = root.position + new Vector3(laneX, tread, z);
                var hits = Physics.OverlapCapsule(stand + Vector3.up * bottom,
                                                  stand + Vector3.up * top, radius,
                                                  ~0, QueryTriggerInteraction.Ignore);

                for (int h = 0; h < hits.Length; h++)
                    Assert.Fail(what + " is blocked on tread " + i + " (z=" + z.ToString("0.0") +
                                ") by " + hits[h].name);
            }
        }

        static IEnumerator WaitForZone(string zoneId)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!ZoneRegistry.IsLoaded(zoneId) && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(ZoneRegistry.IsLoaded(zoneId), "timed out waiting for " + zoneId);
        }
    }
}
