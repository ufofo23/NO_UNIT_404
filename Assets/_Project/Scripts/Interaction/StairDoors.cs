using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Interaction
{
    /// <summary>
    /// The stair door on a floor: the way into the shaft.
    ///
    /// Spec 0.8.1 bans what this replaces. Every floor used to carry a plain ZoneTransition to
    /// the stairwell, and the stairwell had one spawn point, on the ground landing - so a
    /// player who opened the stair door on the fourth floor arrived in the lobby stairwell,
    /// every time, from every floor.
    ///
    /// The fix is not a table of correct spawn points. It is that this component has no
    /// destination at all: it tells <see cref="StairNavigator"/> which floor the player left,
    /// and the navigator says which landing that is. A stair door that has been placed on the
    /// wrong floor now puts the player on the wrong landing *consistently with its own floor*,
    /// which is a placement bug someone can see, rather than a teleport to the lobby.
    /// </summary>
    public sealed class StairEntryDoor : ZoneTransition
    {
        [SerializeField] string _fromFloorId;

        public void SetupStairEntry(string fromFloorId, AccessLevel access, string labelKey)
        {
            _fromFloorId = fromFloorId;
            Setup(ZoneIds.Stairwell, access, labelKey);
        }

        public override string TargetZoneId { get { return ZoneIds.Stairwell; } }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            if (!base.CanInteract(context, out reasonKey)) return false;

            if (!FloorPlan.Exists(_fromFloorId))
            {
                // A stair door on something that is not a floor is a build error, not a locked
                // door, so say so rather than silently dropping the player somewhere.
                Log.Error("Stairs", "stair door has no valid floor: " + _fromFloorId);
                reasonKey = "ui.prompt.nothing_here";
                return false;
            }

            return true;
        }

        protected override Transform ResolveDestination()
        {
            ServiceHub.Stairs.EnterFrom(_fromFloorId, CurrentVariant());
            return WorldBuilder.LandingAnchor(_fromFloorId);
        }

        /// <summary>
        /// Night 5 dresses the shaft as an emergency stairwell (spec 0.8.2 StairVariant).
        /// Adjacency is untouched - spec 30.1 checks that an outage does not resurrect the
        /// teleport-to-lobby behaviour.
        /// </summary>
        static StairVariant CurrentVariant()
        {
            var power = ServiceHub.Power;
            bool outage = power != null && !power.CorridorLightsOn;
            return outage ? StairVariant.Emergency : StairVariant.Normal;
        }
    }

    /// <summary>
    /// The fire door on a stairwell landing: the way out of the shaft.
    ///
    /// Like <see cref="StairEntryDoor"/> it stores no destination. There is one of these per
    /// landing in the greybox, but the one the player can reach is whichever landing they are
    /// standing on, and where it leads is that floor - so an anomaly that has bent the shaft
    /// (M07) changes where the doors go without touching any door.
    /// </summary>
    public sealed class StairLandingDoor : ZoneTransition
    {
        [SerializeField] string _landingFloorId;

        public void SetupLanding(string landingFloorId, AccessLevel access, string labelKey)
        {
            _landingFloorId = landingFloorId;
            Setup(FloorPlan.PrimaryZoneOf(landingFloorId), access, labelKey);
        }

        public string LandingFloorId { get { return _landingFloorId; } }

        /// <summary>
        /// Where this door leads.
        ///
        /// Normally the floor whose landing it sits on. While an anomaly has hold of the shaft
        /// it is whatever that landing currently *reads* as, which is how M07 works without
        /// moving the player: they walk down from the fifth floor, arrive at the landing below,
        /// and the door there puts them back on the fifth (spec 0.8.4).
        /// </summary>
        public override string TargetZoneId
        {
            get
            {
                var stairs = ServiceHub.Stairs;
                string floorId = stairs != null && stairs.InStairwell &&
                                 stairs.PhysicalLanding == _landingFloorId
                    ? stairs.CurrentLanding
                    : _landingFloorId;

                return FloorPlan.PrimaryZoneOf(floorId) ?? ZoneIds.Lobby;
            }
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            if (!base.CanInteract(context, out reasonKey)) return false;

            // Only the door on the landing the player is standing on. The others are built and
            // present, several metres above or below - the raycast will not reach them anyway,
            // and this makes that explicit rather than incidental.
            //
            // It is keyed to the physical landing, not the apparent one: an anomaly changes
            // where a door goes, never which door is in front of you.
            var stairs = ServiceHub.Stairs;
            if (stairs != null && stairs.InStairwell && stairs.PhysicalLanding != _landingFloorId)
            {
                reasonKey = "ui.prompt.nothing_here";
                return false;
            }

            return true;
        }

        protected override void OnTransitioned(string zoneId)
        {
            ServiceHub.Stairs.Exit();
        }
    }
}
