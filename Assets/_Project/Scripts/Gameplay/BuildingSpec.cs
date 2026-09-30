using UnityEngine;

namespace NO404.Gameplay
{
    /// <summary>
    /// The building's module specification (GDD 17.4). Every greybox dimension is derived
    /// from these constants rather than typed in, so the blockout and the eventual art
    /// modules cannot drift apart.
    ///
    /// Plan dimensions snap to <see cref="Grid"/>. Heights do not: 2.6m is a fixed spec value
    /// and the grid governs floor-plan modules, not storey height.
    /// </summary>
    public static class BuildingSpec
    {
        /// <summary>Unity 1 unit = 1 metre.</summary>
        public const float Grid = 0.5f;

        public const float WallHeight = 2.6f;
        public const float WallThickness = 0.2f;

        /// <summary>
        /// Floor slabs are built thicker than the walls. The walking surface stays at the
        /// zone's own y, so no measured dimension changes - the extra depth is all below it,
        /// purely so a fast downward sweep cannot pass through a slab in a single step.
        /// </summary>
        public const float FloorThickness = 0.5f;

        /// <summary>
        /// Corridor width in a 1998 복도식 block.
        ///
        /// 1.55m until a playtest walked it. The measurement is defensible - a real outdoor
        /// access corridor of that period runs 1.2m to 1.8m - but a first-person camera at
        /// 68 degrees inside 1.55m of grey box shows two walls and no room, and the player
        /// could not tell a corridor from a service duct. 2.4m is still a corridor, still on
        /// the grid, and it is the width at which the doors down one side read as doors.
        ///
        /// The service passage stays deliberately tighter (1.0m) so that GDD 17.5.4's "cramped
        /// and between floors" still lands as a contrast rather than as more of the same.
        /// </summary>
        public const float CorridorWidth = 2.4f;

        public const float StairWidth = 1.25f;

        // ---- stairs (GDD 17.5.8) --------------------------------------------
        //
        // A stairwell is the one place in this building where the player has to know which
        // way is up. Floor plates on a wall cannot say that, so the shaft is built as real
        // flights: a rise per step the CharacterController can climb, a run long enough to
        // walk, and a landing at each floor the stairwell serves.

        /// <summary>Rise per step. Under PlayerController's 0.35m step offset with room to spare.</summary>
        public const float StepRise = 0.175f;
        /// <summary>Steps in one flight, so a flight climbs exactly one landing gap.</summary>
        public const int StepsPerFlight = 16;
        /// <summary>Height gained by one flight - and therefore the gap between landings.</summary>
        public const float FlightRise = StepRise * StepsPerFlight;     // 2.8m

        /// <summary>Clear depth of a landing at the top or bottom of a flight.</summary>
        public const float LandingDepth = 1.8f;
        /// <summary>Distance between the two landing centres, along the shaft.</summary>
        public const float LandingSpan = 7f;

        /// <summary>
        /// Depth a flight occupies along the shaft.
        ///
        /// Derived from where the landings actually end rather than picked, because the two
        /// have to meet exactly: a flight 0.28m short of the landing leaves a lip the player
        /// drops off on the way down and stumbles on on the way up, which is precisely the
        /// kind of greybox detail that reads as "the stairs are broken".
        /// </summary>
        public const float FlightRun = LandingSpan - LandingDepth;     // 5.2m
        /// <summary>Going per step. Falls out of the run, so treads always fill the flight.</summary>
        public const float StepRun = FlightRun / StepsPerFlight;       // 0.325m

        public const float UnitDoorWidth = 0.9f;
        public const float UnitDoorHeight = 2.05f;
        public const float FireDoorWidth = 1.0f;
        public const float FireDoorHeight = 2.1f;

        /// <summary>Corridor length shared by every residential floor.</summary>
        public const float CorridorLength = 20f;

        /// <summary>Door centres along a standard corridor, on the 0.5m grid.</summary>
        public static readonly float[] StandardDoorX = { -7.5f, -4f, -0.5f, 3f, 6.5f };

        /// <summary>
        /// GDD 17.5.4: on the fourth floor the wall between 403 and 405 is 1.2m wider than
        /// anywhere else. That single off-grid stretch is the room that was built there.
        /// </summary>
        public const float HiddenWallExtra = 1.2f;

        public static readonly float[] Floor04DoorX =
        {
            -7.5f, -4f, -0.5f,
            -0.5f + 3.5f + HiddenWallExtra,          // 405, pushed out by the hidden room
            -0.5f + 3.5f + HiddenWallExtra + 3.5f    // 406
        };

        /// <summary>Centre of the blank stretch of wall that hides unit 404.</summary>
        public static float HiddenWallCentreX
        {
            get { return (Floor04DoorX[2] + Floor04DoorX[3]) * 0.5f; }
        }

        /// <summary>Mounting height for a corridor camera under a 2.6m ceiling.</summary>
        public const float CameraHeight = 2.35f;

        public static float Snap(float value)
        {
            return Mathf.Round(value / Grid) * Grid;
        }

        public static bool IsOnGrid(float value, float tolerance = 0.001f)
        {
            return Mathf.Abs(value - Snap(value)) <= tolerance;
        }
    }
}
