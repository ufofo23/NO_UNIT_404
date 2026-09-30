using UnityEngine;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// The solid geometry of the two basement zones, in one table.
    ///
    /// It lives here rather than inside <see cref="WorldBuilder"/>'s build methods because two
    /// systems have to agree about it: WorldBuilder places these boxes, and the night-5 patrol
    /// routes in <c>NO404.Threat.PatrolRoutes</c> have to walk between them. When those two had
    /// their own copies the chairman walked through the parked cars, so there is now exactly
    /// one copy and a test that holds every patrol leg against it.
    ///
    /// Only blockers are listed. Pickups lying on the floor and the hiding spots are not
    /// obstacles - the player crouches into a hiding spot and the stalker walks past it.
    ///
    /// Positions are zone-local metres; <see cref="Block.Size"/> is the full footprint on XZ,
    /// matching the <c>localScale</c> WorldBuilder gives the cube.
    /// </summary>
    public static class BasementLayout
    {
        /// <summary>One solid box on the basement floor.</summary>
        public struct Block
        {
            public readonly string Name;
            public readonly Vector2 Centre;   // zone-local XZ
            public readonly Vector2 Size;     // full footprint on XZ
            public readonly float Height;
            public readonly Color Colour;

            public Block(string name, Vector2 centre, Vector2 size, float height, Color colour)
            {
                Name = name; Centre = centre; Size = size; Height = height; Colour = colour;
            }

            /// <summary>True when a point on the floor is inside this box, with a margin.</summary>
            public bool Contains(Vector2 point, float margin)
            {
                return Mathf.Abs(point.x - Centre.x) <= Size.x * 0.5f + margin
                    && Mathf.Abs(point.y - Centre.y) <= Size.y * 0.5f + margin;
            }
        }

        /// <summary>GDD 17.5.3: six pillars and no more than eight cars.</summary>
        public const int PillarCount = 6;
        public const int CarCount = 6;

        static readonly Color PillarColour = new Color(0.24f, 0.24f, 0.25f);
        static readonly Color ShelfColour = new Color(0.25f, 0.25f, 0.26f);
        static readonly Color ServerColour = new Color(0.16f, 0.19f, 0.21f);

        /// <summary>A parked saloon: 1.8m across, 4.2m long.</summary>
        public static readonly Vector2 CarFootprint = new Vector2(1.8f, 4.2f);

        /// <summary>
        /// Parking blockers. Six pillars on two rows at z = +/-4, six cars nose-in against the
        /// north bays at z = 5.5, and the unregistered car parked in the south fire lane, which
        /// is what T01 is about. The 4m car pitch leaves gaps at x = -8, -4, 0, 4 and 8; the
        /// patrol uses x = -4, the only one that has no pillar behind it.
        /// </summary>
        public static Block[] ParkingBlocks(float storeyHeight)
        {
            var blocks = new Block[PillarCount + CarCount + 1];
            int n = 0;

            for (int i = 0; i < PillarCount; i++)
                blocks[n++] = new Block("Pillar" + i,
                    new Vector2(-9f + (i % 3) * 9f, (i / 3) * 8f - 4f),
                    new Vector2(0.6f, 0.6f), storeyHeight, PillarColour);

            for (int i = 0; i < CarCount; i++)
                blocks[n++] = new Block("Car" + i,
                    new Vector2(-10f + i * 4f, 5.5f), CarFootprint, 1.4f,
                    new Color(0.26f + i * 0.02f, 0.26f, 0.28f));

            blocks[n] = new Block("IllegalCar", new Vector2(8f, -5.5f), CarFootprint, 1.4f,
                                  new Color(0.35f, 0.22f, 0.20f));
            return blocks;
        }

        /// <summary>
        /// Records-room blockers: four shelf runs along the north wall and the server rack in
        /// the south-east corner. Everything else in there is paper.
        /// </summary>
        public static Block[] ArchiveBlocks(Vector2 half)
        {
            var blocks = new Block[5];
            for (int i = 0; i < 4; i++)
                blocks[i] = new Block("Shelf" + i, new Vector2(-2.2f + i * 1.5f, half.y - 0.35f),
                                      new Vector2(1.2f, 0.5f), 2f, ShelfColour);

            blocks[4] = new Block("Server", new Vector2(half.x - 0.4f, -1f), new Vector2(0.6f, 0.9f),
                                  2f, ServerColour);
            return blocks;
        }

        /// <summary>The blockers for a zone, or an empty array where the zone has none.</summary>
        public static Block[] For(string zoneId, float storeyHeight)
        {
            if (zoneId == ZoneIds.Parking) return ParkingBlocks(storeyHeight);
            if (zoneId == ZoneIds.Archive) return ArchiveBlocks(WorldBuilder.SizeOf(zoneId) * 0.5f);
            return new Block[0];
        }
    }
}
