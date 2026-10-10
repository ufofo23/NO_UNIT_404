using System.Collections.Generic;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Which zones travel together in one additive scene (GDD 20.5).
    ///
    /// The office and the lobby are always resident because the player can return to the PC
    /// at any moment; everything else streams. Grouping follows the compressed building
    /// (v2.1 spec 0.7.1): one group per floor, because a floor is what the player moves
    /// between and the stairwell needs at most two of them resident at once (spec 0.8.3).
    ///
    /// Zones that are only reachable from one place share that place's group so a transition
    /// never needs two loads.
    /// </summary>
    public static class ZoneGroups
    {
        /// <summary>1F plus the vertical circulation. Never unloaded.</summary>
        public const string Core = "SCN_Core";
        public const string B1 = "SCN_B1";
        public const string Floor02 = "SCN_Floor02";
        public const string Floor03 = "SCN_Floor03";
        public const string Floor04 = "SCN_Floor04";
        public const string Floor05 = "SCN_Floor05";
        public const string Floor06 = "SCN_Floor06";

        static readonly Dictionary<string, string[]> Groups = new Dictionary<string, string[]>
        {
            {
                Core, new[]
                {
                    ZoneIds.Office, ZoneIds.Lobby, ZoneIds.Laundry, ZoneIds.ConvenienceStore,
                    ZoneIds.Playground, ZoneIds.Elevator, ZoneIds.Stairwell
                }
            },
            // v5.1 3.1: one basement holds parking, plant and records.
            {
                B1, new[]
                {
                    ZoneIds.Parking, ZoneIds.RecyclingYard, ZoneIds.Machinery, ZoneIds.PumpRoom,
                    ZoneIds.PipeRoom, ZoneIds.Toolroom, ZoneIds.Archive
                }
            },
            { Floor02, new[] { ZoneIds.Floor02, ZoneIds.Lounge, ZoneIds.FitnessRoom, ZoneIds.Terrace } },
            { Floor03, new[] { ZoneIds.Floor03 } },
            // The service passage and 404 are only reachable through the fourth floor.
            { Floor04, new[] { ZoneIds.Floor04, ZoneIds.ServicePassage, ZoneIds.Unit404 } },
            { Floor05, new[] { ZoneIds.Floor05 } },
            { Floor06, new[] { ZoneIds.Floor06 } }
        };

        public static readonly string[] All =
        {
            Core, B1, Floor02, Floor03, Floor04, Floor05, Floor06
        };

        /// <summary>Groups that stream in and out. Core is never unloaded.</summary>
        public static readonly string[] Streamed =
        {
            B1, Floor02, Floor03, Floor04, Floor05, Floor06
        };

        public static string[] ZonesIn(string group)
        {
            string[] zones;
            return Groups.TryGetValue(group, out zones) ? zones : new string[0];
        }

        public static string GroupOf(string zoneId)
        {
            foreach (var pair in Groups)
            {
                var zones = pair.Value;
                for (int i = 0; i < zones.Length; i++) if (zones[i] == zoneId) return pair.Key;
            }
            return null;
        }

        public static bool IsCore(string zoneId) { return GroupOf(zoneId) == Core; }

        /// <summary>
        /// The scene group that holds a floor's zones, or null for a floor with none.
        ///
        /// The stairwell uses this to keep the floors on either side of the player resident
        /// while they climb (spec 0.8.3), which is the only place a *floor* rather than a
        /// zone is the unit of streaming.
        /// </summary>
        public static string GroupOfFloor(string floorId)
        {
            string primary = FloorPlan.PrimaryZoneOf(floorId);
            return primary == null ? null : GroupOf(primary);
        }
    }
}
