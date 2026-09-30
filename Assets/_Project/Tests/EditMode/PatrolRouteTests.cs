using NUnit.Framework;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;
using NO404.Threat;

namespace NO404.Tests
{
    /// <summary>
    /// The night-5 patrol used to walk a rectangle, which put the chairman inside the parked
    /// cars. These tests hold the authored routes (GDD 9.6 / 15.2) against the same layout
    /// table WorldBuilder builds from, so a route node and a prop can never drift apart
    /// again - moving either one without the other fails here rather than in a playthrough.
    /// </summary>
    public sealed class PatrolRouteTests
    {
        /// <summary>Half the stalker capsule's width, from the 0.6 scale WorldBuilder gives it.</summary>
        const float StalkerRadius = 0.3f;

        [Test]
        public void EveryThreatZoneHasAnAuthoredRoute()
        {
            // If a zone can host the patrol it needs a route; a stalker with no route stands
            // still, which is safe but is not a patrol.
            foreach (var zoneId in PatrolRoutes.RoutedZones)
            {
                var route = PatrolRoutes.LocalRoute(zoneId);
                Assert.IsNotNull(route, zoneId + " has no authored route");
                Assert.GreaterOrEqual(route.Length, 3, zoneId + " needs a circuit, not a line");
            }
        }

        [Test]
        public void UnroutedZonesReturnNothingRatherThanAGuess()
        {
            Assert.IsNull(PatrolRoutes.LocalRoute(ZoneIds.Floor04));
            CollectionAssert.IsEmpty(
                new System.Collections.Generic.List<PatrolWaypoint>(
                    PatrolRoutes.For(ZoneIds.Floor04, Vector3.zero)));
        }

        [Test]
        public void EveryNodeStandsClearOfTheZoneWalls()
        {
            foreach (var zoneId in PatrolRoutes.RoutedZones)
            {
                var half = WorldBuilder.SizeOf(zoneId) * 0.5f;
                var route = PatrolRoutes.LocalRoute(zoneId);

                for (int i = 0; i < route.Length; i++)
                {
                    float limitX = half.x - PatrolRoutes.WallClearance;
                    float limitZ = half.y - PatrolRoutes.WallClearance;

                    Assert.LessOrEqual(Mathf.Abs(route[i].Local.x), limitX,
                        zoneId + " node " + i + " is in the east/west wall");
                    Assert.LessOrEqual(Mathf.Abs(route[i].Local.y), limitZ,
                        zoneId + " node " + i + " is in the north/south wall");
                }
            }
        }

        [Test]
        public void NoNodeStandsInsideAProp()
        {
            foreach (var zoneId in PatrolRoutes.RoutedZones)
            {
                var blocks = BasementLayout.For(zoneId, WorldBuilder.HeightOf(zoneId));
                var route = PatrolRoutes.LocalRoute(zoneId);

                for (int i = 0; i < route.Length; i++)
                    for (int b = 0; b < blocks.Length; b++)
                        Assert.IsFalse(blocks[b].Contains(route[i].Local, StalkerRadius),
                            zoneId + " node " + i + " stands inside " + blocks[b].Name);
            }
        }

        [Test]
        public void NoLegWalksThroughAProp()
        {
            foreach (var zoneId in PatrolRoutes.RoutedZones)
            {
                var blocks = BasementLayout.For(zoneId, WorldBuilder.HeightOf(zoneId));
                var route = PatrolRoutes.LocalRoute(zoneId);

                // The route is a closed circuit, so the wrap from the last node back to the
                // first is a leg like any other and is checked here too.
                for (int i = 0; i < route.Length; i++)
                {
                    var from = route[i].Local;
                    var to = route[(i + 1) % route.Length].Local;

                    for (int b = 0; b < blocks.Length; b++)
                        Assert.IsFalse(SegmentHitsBox(from, to, blocks[b], StalkerRadius),
                            zoneId + " leg " + i + "->" + ((i + 1) % route.Length) +
                            " walks through " + blocks[b].Name);
                }
            }
        }

        [Test]
        public void LegsAreLongEnoughToRead()
        {
            // Two nodes on top of each other make the stalker stutter in place.
            foreach (var zoneId in PatrolRoutes.RoutedZones)
            {
                var route = PatrolRoutes.LocalRoute(zoneId);
                for (int i = 0; i < route.Length; i++)
                {
                    float length = Vector2.Distance(route[i].Local, route[(i + 1) % route.Length].Local);
                    Assert.Greater(length, 1f,
                        zoneId + " leg " + i + " is only " + length.ToString("0.00") + "m");
                }
            }
        }

        [Test]
        public void RouteWaypointsSitOnTheFloorAtTheZoneOrigin()
        {
            var origin = new Vector3(0f, -12f, 0f);
            var world = new System.Collections.Generic.List<PatrolWaypoint>(
                PatrolRoutes.For(ZoneIds.Parking, origin));
            var local = PatrolRoutes.LocalRoute(ZoneIds.Parking);

            Assert.AreEqual(local.Length, world.Count);
            for (int i = 0; i < world.Count; i++)
            {
                Assert.AreEqual(origin.x + local[i].Local.x, world[i].Position.x, 0.0001f);
                Assert.AreEqual(origin.y + PatrolRoutes.FootHeight, world[i].Position.y, 0.0001f);
                Assert.AreEqual(origin.z + local[i].Local.y, world[i].Position.z, 0.0001f);
                Assert.AreEqual(local[i].Dwell, world[i].DwellSeconds, 0.0001f);
            }
        }


        [Test]
        public void EveryRouteStopsSomewhere()
        {
            // A circuit with no dwell is the old rectangle with more corners. Each route has
            // to stand still somewhere, or the hiding spots on it are unusable (GDD 9.6).
            foreach (var zoneId in PatrolRoutes.RoutedZones)
            {
                float dwell = 0f;
                var route = PatrolRoutes.LocalRoute(zoneId);
                for (int i = 0; i < route.Length; i++) dwell += route[i].Dwell;

                Assert.Greater(dwell, 0f, zoneId + " never stops");
            }
        }

        [Test]
        public void EveryInspectionStopIsWithinReachOfWhatItWatches()
        {
            // A node that claims to be watching a prop has to actually be beside it, or the
            // comment is the only thing tying the two together.
            foreach (var zoneId in PatrolRoutes.RoutedZones)
            {
                var blocks = BasementLayout.For(zoneId, WorldBuilder.HeightOf(zoneId));
                var route = PatrolRoutes.LocalRoute(zoneId);

                for (int i = 0; i < route.Length; i++)
                {
                    if (string.IsNullOrEmpty(route[i].Watching)) continue;

                    bool found = false;
                    for (int b = 0; b < blocks.Length; b++)
                    {
                        if (blocks[b].Name != route[i].Watching) continue;
                        found = true;

                        float gap = Vector2.Distance(route[i].Local, blocks[b].Centre);
                        Assert.LessOrEqual(gap, PatrolRoutes.InspectionReach + blocks[b].Size.magnitude * 0.5f,
                            zoneId + " node " + i + " claims to watch " + blocks[b].Name +
                            " from " + gap.ToString("0.00") + "m");
                    }

                    // BreakerPanel and the archive door are placed by WorldBuilder outside the
                    // blocker table, so an unmatched name is only a failure when it looks like
                    // a blocker that has since been renamed.
                    if (!found)
                        Assert.That(route[i].Watching,
                            Does.StartWith("Breaker").Or.StartWith("Toolbox"),
                            zoneId + " node " + i + " watches unknown prop " + route[i].Watching);
                }
            }
        }

        /// <summary>
        /// Slab method: clip the segment against the box grown by the walker's radius. Cheap,
        /// exact for axis-aligned boxes, and the boxes here are all axis-aligned.
        /// </summary>
        static bool SegmentHitsBox(Vector2 from, Vector2 to, BasementLayout.Block block, float radius)
        {
            Vector2 min = block.Centre - block.Size * 0.5f - Vector2.one * radius;
            Vector2 max = block.Centre + block.Size * 0.5f + Vector2.one * radius;
            Vector2 d = to - from;

            float enter = 0f, exit = 1f;

            for (int axis = 0; axis < 2; axis++)
            {
                float origin = axis == 0 ? from.x : from.y;
                float delta = axis == 0 ? d.x : d.y;
                float lo = axis == 0 ? min.x : min.y;
                float hi = axis == 0 ? max.x : max.y;

                if (Mathf.Abs(delta) < 1e-6f)
                {
                    if (origin < lo || origin > hi) return false;   // parallel and outside
                    continue;
                }

                float t1 = (lo - origin) / delta;
                float t2 = (hi - origin) / delta;
                if (t1 > t2) { float swap = t1; t1 = t2; t2 = swap; }

                enter = Mathf.Max(enter, t1);
                exit = Mathf.Min(exit, t2);
                if (enter > exit) return false;
            }

            return true;
        }
    }
}
