using System.Collections.Generic;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Threat
{
    /// <summary>One stop on a patrol: where he stands, and how long he stands there.</summary>
    public struct PatrolWaypoint
    {
        public readonly Vector3 Position;
        public readonly float DwellSeconds;

        public PatrolWaypoint(Vector3 position, float dwellSeconds)
        {
            Position = position; DwellSeconds = dwellSeconds;
        }
    }

    /// <summary>
    /// The authored patrol routes for the night-5 threat window (GDD 9.6 / 15.2).
    ///
    /// These used to be one rectangle per zone, which walked the chairman through the parked
    /// cars and past everything he is supposed to be checking. A patrol reads as a patrol only
    /// when it stops at the things a caretaker's supervisor would stop at, so each leg below is
    /// a straight line of clear floor between two props that WorldBuilder actually places, and
    /// the nodes that sit at a prop carry a dwell.
    ///
    /// The dwell is not decoration. GDD 9.6 has the chairman in the records room to retrieve
    /// records, not to walk a beat, and a 6x5 room walked without stopping puts him back at the
    /// one hiding cabinet every eight seconds - which leaves the player nowhere to be. Standing
    /// at the shelves is both what the scene says he is doing and what makes the room playable.
    ///
    /// Coordinates are zone-local metres on the XZ plane, listed as a closed circuit: he walks
    /// the list and wraps from the last node back to the first, so the final leg has to be clear
    /// too. <c>PatrolRouteTests</c> holds every leg against <see cref="BasementLayout"/> - the
    /// same table WorldBuilder builds the props from - and against the zone walls.
    /// </summary>
    public static class PatrolRoutes
    {
        /// <summary>Where the stalker capsule's centre sits; matches its 0.9 half-height.</summary>
        public const float FootHeight = 0.9f;

        /// <summary>How far a waypoint has to stay from a wall so the capsule does not clip it.</summary>
        public const float WallClearance = 0.6f;

        /// <summary>A node with a dwell has to be inside this of the prop it is watching.</summary>
        public const float InspectionReach = 1.6f;

        public struct Node
        {
            public readonly Vector2 Local;
            public readonly float Dwell;
            public readonly string Watching;   // prop name, or null when he is only passing

            public Node(float x, float z, float dwell = 0f, string watching = null)
            {
                Local = new Vector2(x, z); Dwell = dwell; Watching = watching;
            }
        }

        /// <summary>
        /// Basement parking, 25 x 18 (GDD 17.5.3). He comes down the stairs on the east side,
        /// checks the breaker panel the player keeps returning to (GDD 15.5), sweeps the south
        /// fire lane where the unregistered car sits (T01), passes the dark utility corner and
        /// comes back along the north lane behind the parked cars, trying the archive door. The
        /// legs thread the 4m gap between the cars at x = -4 and the lane at z = 1.4, both clear
        /// of the six pillars at z = +/-4. One lap is about 106m of walking plus 10s of stops.
        /// </summary>
        static readonly Node[] ParkingRoute =
        {
            new Node( 11.2f,  0.0f),                          // stairwell door - where he comes in
            new Node(  2.0f,  1.4f),                          // central lane, between the pillar rows
            new Node(-10.8f,  2.2f, 3f, "BreakerPanel"),      // checks the night reserve
            new Node(-10.6f, -6.8f),                          // south-west corner, past the Toolbox
            new Node( -2.0f, -7.2f),                          // south fire lane
            new Node(  4.5f, -7.4f),                          // south fire lane, east end
            new Node(  6.2f, -5.5f, 4f, "IllegalCar"),        // the unregistered car - why he is down here
            new Node(  6.2f, -2.4f),                          // clear of the car again
            new Node( 11.3f, -3.2f),                          // east wall, in sight of DarkUtilityCorner
            new Node( 11.4f,  8.2f),                          // east wall, north end, past the stair door
            new Node(  4.0f,  8.1f),                          // north lane, behind the parked cars
            new Node(  0.0f,  8.2f, 3f),                      // the archive door - he tries the handle
            new Node( -4.0f,  8.1f),                          // north lane, west end
            new Node( -4.0f,  1.4f)                           // down through the gap between two cars
        };

        /// <summary>
        /// Records room, 6 x 5 (C11). Too small for a sweep, and GDD 9.6 does not ask for one:
        /// he is here to take records back. So he works the shelves, checks the server rack,
        /// looks at the smoke device that let him in, and returns down the west wall - which is
        /// where HidingCabinetB is, so the room's one hiding spot is on his path rather than
        /// off it. The stops are what turn 13m of walking into a room the player can work in.
        /// </summary>
        static readonly Node[] ArchiveRoute =
        {
            new Node(-2.3f,  1.4f, 5f, "Shelf0"),   // the west shelves - MaintenanceBill2009 is here
            new Node( 1.6f,  1.4f, 5f, "Shelf3"),   // the east shelves
            new Node( 1.9f, -0.2f, 4f, "Server"),   // the rack: the records he actually came for
            new Node( 0.4f, -1.9f, 2f),             // the smoke device on the floor
            new Node(-2.2f, -1.6f)                  // the west wall - he walks straight past the cabinet
        };

        /// <summary>Every zone that has an authored route.</summary>
        public static readonly string[] RoutedZones = { ZoneIds.Parking, ZoneIds.Archive };

        /// <summary>Zone-local nodes, or null where no route is authored. Exposed for tests.</summary>
        public static Node[] LocalRoute(string zoneId)
        {
            if (zoneId == ZoneIds.Parking) return ParkingRoute;
            if (zoneId == ZoneIds.Archive) return ArchiveRoute;
            return null;
        }

        /// <summary>
        /// The route for a zone, in world space. Zones with no authored route get none: a
        /// stalker with an empty list stands still rather than wandering through geometry
        /// nobody laid out for him, which is the safe failure for a threat window GDD 15.2
        /// restricts to two places.
        /// </summary>
        public static IEnumerable<PatrolWaypoint> For(string zoneId, Vector3 zoneOrigin)
        {
            var route = LocalRoute(zoneId);
            if (route == null) yield break;

            for (int i = 0; i < route.Length; i++)
                yield return new PatrolWaypoint(
                    zoneOrigin + new Vector3(route[i].Local.x, FootHeight, route[i].Local.y),
                    route[i].Dwell);
        }
    }
}
