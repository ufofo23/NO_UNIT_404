using System.Collections.Generic;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// The compressed building (v2.1 spec 0.7.1 and 27).
    ///
    /// Nine spaces, bottom to top: B2, B1, 1F..6F, ROOF. Nothing else is a floor. The
    /// pre-v2.1 layout had normal residents on 8, 12, 13 and 15; those floors are gone and
    /// their content was relocated by the table in spec 0.11.
    ///
    /// The single most important property of this table is what it does *not* contain: there
    /// is no thirteenth floor. 13 is a number the elevator can display and an anomaly the
    /// player can be standing in (C05 / M01), never a destination the movement code can
    /// resolve. Because every stair door and every lift button computes its target through
    /// <see cref="Neighbour"/> or <see cref="IsElevatorDestination"/>, a phantom floor cannot
    /// become reachable by someone adding a button and forgetting the rule.
    /// </summary>
    public static class FloorPlan
    {
        // ---- floor ids (spec 27 uses exactly these strings) -----------------

        public const string B2 = "B2";
        public const string B1 = "B1";
        public const string F1 = "F1";
        public const string F2 = "F2";
        public const string F3 = "F3";
        public const string F4 = "F4";
        public const string F5 = "F5";
        public const string F6 = "F6";
        public const string Roof = "ROOF";

        /// <summary>
        /// The floor number the building does not have (spec 0.7.1). Present as a constant so
        /// anomaly content can name it; deliberately absent from <see cref="Order"/> so no
        /// navigation query can ever return it.
        /// </summary>
        public const string PhantomF13 = "F13";

        /// <summary>Every real floor, bottom to top. Index order is the adjacency order.</summary>
        public static readonly string[] Order = { B2, B1, F1, F2, F3, F4, F5, F6, Roof };

        public enum Direction { Down = -1, Up = 1 }

        // ---- definitions ----------------------------------------------------

        /// <summary>One row of the spec 27 floor definition table.</summary>
        public sealed class FloorDefinition
        {
            public readonly string FloorId;
            public readonly string DisplayNameKey;
            /// <summary>Sign text on a stair landing, e.g. "4F". Not localized: it is a number.</summary>
            public readonly string SignText;
            public readonly bool ElevatorAccessible;
            /// <summary>The zone the player arrives in when they leave the stairwell here.</summary>
            public readonly string PrimaryZoneId;
            /// <summary>Zones that belong to this floor and are always reachable from it.</summary>
            public readonly string[] NormalZones;
            /// <summary>Zones on this floor that need a flag, a key or an anomaly to enter.</summary>
            public readonly string[] ConditionalZones;

            public FloorDefinition(string floorId, string displayNameKey, string signText,
                                   bool elevatorAccessible, string primaryZoneId,
                                   string[] normalZones, string[] conditionalZones)
            {
                FloorId = floorId;
                DisplayNameKey = displayNameKey;
                SignText = signText;
                ElevatorAccessible = elevatorAccessible;
                PrimaryZoneId = primaryZoneId;
                NormalZones = normalZones ?? new string[0];
                ConditionalZones = conditionalZones ?? new string[0];
            }

            /// <summary>Floor above on the stairs, or null at the roof (spec 27).</summary>
            public string StairUp { get { return Neighbour(FloorId, Direction.Up); } }
            /// <summary>Floor below on the stairs, or null at B2 (spec 27).</summary>
            public string StairDown { get { return Neighbour(FloorId, Direction.Down); } }
        }

        static readonly FloorDefinition[] Definitions =
        {
            new FloorDefinition(B2, "world.floor.b2", "B2", true, ZoneIds.Machinery,
                new[] { ZoneIds.Machinery, ZoneIds.PumpRoom, ZoneIds.PipeRoom, ZoneIds.Toolroom },
                new[] { ZoneIds.Archive }),

            new FloorDefinition(B1, "world.floor.b1", "B1", true, ZoneIds.Parking,
                new[] { ZoneIds.Parking, ZoneIds.RecyclingYard },
                new string[0]),

            new FloorDefinition(F1, "world.floor.f1", "1F", true, ZoneIds.Lobby,
                new[] { ZoneIds.Lobby, ZoneIds.Office, ZoneIds.Laundry, ZoneIds.ConvenienceStore },
                new[] { ZoneIds.Playground }),

            new FloorDefinition(F2, "world.floor.f2", "2F", true, ZoneIds.Floor02,
                new[] { ZoneIds.Floor02, ZoneIds.Lounge, ZoneIds.FitnessRoom, ZoneIds.Terrace },
                new string[0]),

            new FloorDefinition(F3, "world.floor.f3", "3F", true, ZoneIds.Floor03,
                new[] { ZoneIds.Floor03 },
                new string[0]),

            // 4F is the only floor whose conditional space is the point of the game.
            new FloorDefinition(F4, "world.floor.f4", "4F", true, ZoneIds.Floor04,
                new[] { ZoneIds.Floor04 },
                new[] { ZoneIds.ServicePassage, ZoneIds.Unit404 }),

            new FloorDefinition(F5, "world.floor.f5", "5F", true, ZoneIds.Floor05,
                new[] { ZoneIds.Floor05 },
                new string[0]),

            new FloorDefinition(F6, "world.floor.f6", "6F", true, ZoneIds.Floor06,
                new[] { ZoneIds.Floor06 },
                new string[0]),

            // Spec 27: the roof has no lift. It is reached by one more stair run above 6F.
            new FloorDefinition(Roof, "world.floor.roof", "R", false, ZoneIds.Rooftop,
                new[] { ZoneIds.Rooftop },
                new string[0])
        };

        static readonly Dictionary<string, FloorDefinition> ById = BuildById();
        static readonly Dictionary<string, string> FloorByZone = BuildFloorByZone();

        static Dictionary<string, FloorDefinition> BuildById()
        {
            var map = new Dictionary<string, FloorDefinition>(Definitions.Length);
            for (int i = 0; i < Definitions.Length; i++) map[Definitions[i].FloorId] = Definitions[i];
            return map;
        }

        static Dictionary<string, string> BuildFloorByZone()
        {
            var map = new Dictionary<string, string>(32);
            for (int i = 0; i < Definitions.Length; i++)
            {
                var def = Definitions[i];
                for (int z = 0; z < def.NormalZones.Length; z++) map[def.NormalZones[z]] = def.FloorId;
                for (int z = 0; z < def.ConditionalZones.Length; z++) map[def.ConditionalZones[z]] = def.FloorId;
            }
            return map;
        }

        public static IReadOnlyList<FloorDefinition> All { get { return Definitions; } }

        public static FloorDefinition Find(string floorId)
        {
            if (string.IsNullOrEmpty(floorId)) return null;
            FloorDefinition def;
            return ById.TryGetValue(floorId, out def) ? def : null;
        }

        public static bool Exists(string floorId) { return Find(floorId) != null; }

        /// <summary>Position in <see cref="Order"/>, or -1 for anything that is not a real floor.</summary>
        public static int IndexOf(string floorId)
        {
            for (int i = 0; i < Order.Length; i++) if (Order[i] == floorId) return i;
            return -1;
        }

        // ---- movement -------------------------------------------------------

        /// <summary>
        /// The floor one step from <paramref name="floorId"/>, or null when the shaft ends.
        ///
        /// Spec 27: a stair door prefab must not carry a DestinationFloor. Everything that
        /// moves the player between floors resolves its target here, from where the player
        /// currently is plus which way they walked - which is what structurally prevents the
        /// bug spec 0.8.1 bans, where every stair door in the building came out in the lobby.
        /// </summary>
        public static string Neighbour(string floorId, Direction direction)
        {
            int index = IndexOf(floorId);
            if (index < 0) return null;
            int next = index + (int)direction;
            return next >= 0 && next < Order.Length ? Order[next] : null;
        }

        public static string Up(string floorId) { return Neighbour(floorId, Direction.Up); }
        public static string Down(string floorId) { return Neighbour(floorId, Direction.Down); }

        /// <summary>True when the two floors are one flight apart.</summary>
        public static bool AreAdjacent(string a, string b)
        {
            int ia = IndexOf(a), ib = IndexOf(b);
            return ia >= 0 && ib >= 0 && (ia - ib == 1 || ib - ia == 1);
        }

        /// <summary>
        /// Whether the lift may stop here. Guards the phantom floor at the one place a
        /// player-facing control could otherwise reach it (spec 32: the thirteenth floor can
        /// never be chosen as a normal destination).
        /// </summary>
        public static bool IsElevatorDestination(string floorId)
        {
            var def = Find(floorId);
            return def != null && def.ElevatorAccessible;
        }

        /// <summary>Every floor the lift serves, bottom to top.</summary>
        public static IEnumerable<FloorDefinition> ElevatorStops()
        {
            for (int i = 0; i < Definitions.Length; i++)
                if (Definitions[i].ElevatorAccessible) yield return Definitions[i];
        }

        // ---- zones ----------------------------------------------------------

        /// <summary>The floor a zone sits on, or null for the transit zones (lift car, shaft).</summary>
        public static string FloorOfZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            string floorId;
            return FloorByZone.TryGetValue(zoneId, out floorId) ? floorId : null;
        }

        /// <summary>Where the player lands when they step off the stairs onto this floor.</summary>
        public static string PrimaryZoneOf(string floorId)
        {
            var def = Find(floorId);
            return def == null ? null : def.PrimaryZoneId;
        }

        /// <summary>Sign text for a landing, e.g. "B2", "4F", "R".</summary>
        public static string SignOf(string floorId)
        {
            var def = Find(floorId);
            return def == null ? "?" : def.SignText;
        }

        public static string DisplayKeyOf(string floorId)
        {
            var def = Find(floorId);
            return def == null ? "world.floor.unknown" : def.DisplayNameKey;
        }
    }
}
