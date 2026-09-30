using System;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>How a stairwell run is dressed. Spec 0.8.2 StairVariant.</summary>
    public enum StairVariant
    {
        /// <summary>Lit, signed, ordinary.</summary>
        Normal = 0,
        /// <summary>An anomaly is acting on the shaft: signs, lighting and geometry may lie.</summary>
        Distorted = 1,
        /// <summary>Night 5 outage. Emergency lighting only - but the same adjacency.</summary>
        Emergency = 2
    }

    /// <summary>
    /// Where the player is inside the stairwell, and which landing one more flight reaches.
    ///
    /// Spec 0.8.1 bans the shape this replaces: a stair door that loads a stairwell scene and
    /// drops the player at a fixed lobby exit, from every floor in the building. The fix is
    /// structural rather than a table of correct destinations - nothing here or in
    /// <see cref="FloorPlan"/> stores a destination at all. A landing is only ever
    /// "where I entered, plus the flights I have walked", so a stair door physically cannot
    /// name the wrong floor.
    ///
    /// A plain service rather than a MonoBehaviour: the whole spec 30.1 adjacency matrix is
    /// then an EditMode test that needs no scene.
    /// </summary>
    public sealed class StairNavigator
    {
        /// <summary>
        /// Lets an anomaly bend the shaft without any of this class knowing about it.
        ///
        /// Returns the landing the player actually arrives at, or null to accept the normal
        /// adjacent floor. M07's corridor loop is exactly this: return the floor they left.
        /// </summary>
        /// <param name="fromFloor">The landing the player stepped off.</param>
        /// <param name="direction">Which way they walked.</param>
        /// <param name="normalTarget">What plain adjacency says, possibly null at the shaft ends.</param>
        /// <param name="flightsWalked">Flights walked since this stairwell entry, before this one.</param>
        public delegate string LandingOverride(string fromFloor, FloorPlan.Direction direction,
                                               string normalTarget, int flightsWalked);

        LandingOverride _override;
        string _overrideOwner;

        // ---- spec 0.8.2 state ------------------------------------------------

        /// <summary>The floor whose door the player opened to get into the shaft.</summary>
        public string EntryFloor { get; private set; }
        /// <summary>
        /// The landing the player is standing on now, as the building presents it.
        ///
        /// Usually the same as <see cref="PhysicalLanding"/>. They part company when an
        /// anomaly has hold of the shaft: M07 sends someone who walked down from the fifth
        /// floor back onto the fifth floor, and the way that reads to the player is that the
        /// landing they arrived at is the one they left (spec 0.8.4).
        /// </summary>
        public string CurrentLanding { get; private set; }
        /// <summary>
        /// Which landing the player is actually standing on, by height in the shaft.
        ///
        /// Doors and props are placed against this; signs and destinations follow
        /// <see cref="CurrentLanding"/>. Keeping the two apart is what lets an anomaly bend
        /// the stairwell without anything having to move the player.
        /// </summary>
        public string PhysicalLanding { get; private set; }
        /// <summary>Which way the player was last travelling, for sign and audio dressing.</summary>
        public FloorPlan.Direction DirectionHint { get; private set; }
        public StairVariant Variant { get; private set; }
        /// <summary>True between opening a stair door and leaving through one.</summary>
        public bool InStairwell { get; private set; }
        /// <summary>Flights walked since entering. Anomalies count loops with this.</summary>
        public int FlightsWalked { get; private set; }

        public StairNavigator()
        {
            Reset();
        }

        public void Reset()
        {
            _override = null;
            _overrideOwner = null;
            EntryFloor = FloorPlan.F1;
            CurrentLanding = FloorPlan.F1;
            PhysicalLanding = FloorPlan.F1;
            DirectionHint = FloorPlan.Direction.Up;
            Variant = StairVariant.Normal;
            InStairwell = false;
            FlightsWalked = 0;
        }

        // ---- entering and leaving --------------------------------------------

        /// <summary>
        /// Open a stair door on <paramref name="fromFloor"/>.
        ///
        /// Spec 0.8.2: the first landing sign always reads the floor the player came from.
        /// Entering from 4F shows "4F", from B1 shows "B1"; there is no such thing as a
        /// default entry landing.
        /// </summary>
        public bool EnterFrom(string fromFloor, StairVariant variant = StairVariant.Normal)
        {
            if (!FloorPlan.Exists(fromFloor))
            {
                Log.Error("Stairs", "refused stair entry from non-floor '" + fromFloor + "'");
                return false;
            }

            EntryFloor = fromFloor;
            CurrentLanding = fromFloor;
            PhysicalLanding = fromFloor;
            Variant = variant;
            InStairwell = true;
            FlightsWalked = 0;

            EventBus.Publish(new StairLandingChangedEvent(fromFloor, fromFloor, true));
            Log.Info("Stairs", "entered at " + fromFloor + " (" + variant + ")");
            return true;
        }

        /// <summary>Leave the shaft through the door on the current landing.</summary>
        public void Exit()
        {
            if (!InStairwell) return;
            Log.Info("Stairs", "left at " + CurrentLanding + " after " + FlightsWalked + " flights");
            InStairwell = false;
            ClearOverride(_overrideOwner);
        }

        // ---- walking ----------------------------------------------------------

        /// <summary>
        /// What one more flight in <paramref name="direction"/> reaches, without moving.
        ///
        /// Null means the shaft ends here: B2 has no floor below it and the roof none above
        /// (spec 27), so the door on that side of the landing simply is not there.
        /// </summary>
        public string PeekLanding(FloorPlan.Direction direction)
        {
            string normal = FloorPlan.Neighbour(CurrentLanding, direction);
            if (_override == null) return normal;

            string bent = _override(CurrentLanding, direction, normal, FlightsWalked);
            return bent ?? normal;
        }

        /// <summary>
        /// Walk one flight. Returns the landing arrived at, or null when there is no flight
        /// that way.
        /// </summary>
        public string Walk(FloorPlan.Direction direction)
        {
            if (!InStairwell)
            {
                Log.Error("Stairs", "Walk called while not in the stairwell");
                return null;
            }

            string from = CurrentLanding;
            string target = PeekLanding(direction);
            if (target == null) return null;

            DirectionHint = direction;
            FlightsWalked++;
            // Off the landing they are physically on, not the one the shaft claims: while an
            // anomaly is bending the signs those are different floors, and the body follows
            // the geometry.
            PhysicalLanding = FloorPlan.Neighbour(PhysicalLanding, direction) ?? PhysicalLanding;
            CurrentLanding = target;

            EventBus.Publish(new StairLandingChangedEvent(from, target, false));
            Log.Info("Stairs", from + " -> " + target + " (" + direction + ")");
            return target;
        }

        /// <summary>
        /// The player has physically reached a landing.
        ///
        /// This is the one the game actually uses. <see cref="Walk"/> is the same idea stated
        /// as an instruction and is what the tests drive; in play nobody instructs the
        /// stairwell - the player climbs, and StairLandingTracker reports where they got to.
        ///
        /// Returns the landing as the building presents it, which an anomaly may bend.
        /// </summary>
        public string ArriveAt(string physicalFloor)
        {
            if (!FloorPlan.Exists(physicalFloor)) return CurrentLanding;
            if (physicalFloor == PhysicalLanding) return CurrentLanding;

            // Somebody can enter the shaft mid-climb after a load, so the first report is an
            // arrival rather than a flight walked.
            bool known = FloorPlan.Exists(PhysicalLanding);
            var direction = !known || FloorPlan.IndexOf(physicalFloor) > FloorPlan.IndexOf(PhysicalLanding)
                ? FloorPlan.Direction.Up
                : FloorPlan.Direction.Down;

            string from = CurrentLanding;
            string apparent = physicalFloor;

            if (_override != null)
                apparent = _override(from, direction, physicalFloor, FlightsWalked) ?? physicalFloor;

            PhysicalLanding = physicalFloor;
            CurrentLanding = apparent;
            DirectionHint = direction;
            FlightsWalked++;
            InStairwell = true;

            EventBus.Publish(new StairLandingChangedEvent(from, apparent, false));
            Log.Info("Stairs", "arrived at " + physicalFloor +
                               (apparent == physicalFloor ? "" : " (reads as " + apparent + ")"));
            return apparent;
        }

        public string Ascend() { return Walk(FloorPlan.Direction.Up); }
        public string Descend() { return Walk(FloorPlan.Direction.Down); }

        /// <summary>The zone the landing door opens into.</summary>
        public string ExitZone { get { return FloorPlan.PrimaryZoneOf(CurrentLanding); } }

        /// <summary>Sign text on the current landing. Anomalies may show something else.</summary>
        public string LandingSign { get { return FloorPlan.SignOf(CurrentLanding); } }

        // ---- streaming (spec 0.8.3) -------------------------------------------

        /// <summary>
        /// The scene groups that must stay resident for the shaft to look continuous: the
        /// landing the player is on and the one flight either side of it.
        ///
        /// Spec 0.8.3 keeps two segments alive rather than the whole shaft. Returning groups
        /// rather than loading them keeps this class free of ZoneStreamer, so the adjacency
        /// tests never need a scene.
        /// </summary>
        public string[] ResidentGroups()
        {
            string below = FloorPlan.Down(CurrentLanding);
            string above = FloorPlan.Up(CurrentLanding);

            var groups = new System.Collections.Generic.List<string>(3);
            AddGroup(groups, CurrentLanding);
            AddGroup(groups, below);
            AddGroup(groups, above);
            return groups.ToArray();
        }

        static void AddGroup(System.Collections.Generic.List<string> into, string floorId)
        {
            if (floorId == null) return;
            string group = ZoneGroups.GroupOfFloor(floorId);
            if (group != null && group != ZoneGroups.Core && !into.Contains(group)) into.Add(group);
        }

        // ---- anomaly hooks -----------------------------------------------------

        /// <summary>
        /// Install a landing override. One at a time, keyed by owner so an event can only
        /// remove its own - two anomalies bending the same shaft would make neither
        /// debuggable.
        /// </summary>
        public void SetOverride(string ownerId, LandingOverride landingOverride)
        {
            if (string.IsNullOrEmpty(ownerId) || landingOverride == null) return;
            if (_override != null && _overrideOwner != ownerId)
            {
                Log.Error("Stairs", "override from " + ownerId + " refused; " +
                                    _overrideOwner + " already holds the shaft");
                return;
            }
            _override = landingOverride;
            _overrideOwner = ownerId;
            Variant = StairVariant.Distorted;
            Log.Info("Stairs", "landing override installed by " + ownerId);
        }

        public void ClearOverride(string ownerId)
        {
            if (_override == null || _overrideOwner != ownerId) return;
            _override = null;
            _overrideOwner = null;
            if (Variant == StairVariant.Distorted) Variant = StairVariant.Normal;
            Log.Info("Stairs", "landing override cleared by " + ownerId);
        }

        public bool HasOverride { get { return _override != null; } }
        public string OverrideOwner { get { return _overrideOwner; } }

        /// <summary>Night 5 dresses the shaft for the outage without touching adjacency.</summary>
        public void SetVariant(StairVariant variant)
        {
            if (Variant == variant) return;
            Variant = variant;
            Log.Info("Stairs", "variant = " + variant);
        }

        // ---- save --------------------------------------------------------------

        /// <summary>
        /// Spec 30.1: a save taken in the shaft has to come back on the same landing, having
        /// entered from the same floor. Restoring only the landing would let a reload turn a
        /// loop the player was halfway through solving into a fresh one.
        /// </summary>
        public void LoadFrom(string entryFloor, string currentLanding, int direction,
                             int variant, bool inStairwell, int flightsWalked)
        {
            EntryFloor = FloorPlan.Exists(entryFloor) ? entryFloor : FloorPlan.F1;
            CurrentLanding = FloorPlan.Exists(currentLanding) ? currentLanding : EntryFloor;
            DirectionHint = direction < 0 ? FloorPlan.Direction.Down : FloorPlan.Direction.Up;
            Variant = Enum.IsDefined(typeof(StairVariant), variant) ? (StairVariant)variant : StairVariant.Normal;
            PhysicalLanding = CurrentLanding;
            InStairwell = inStairwell;
            FlightsWalked = flightsWalked < 0 ? 0 : flightsWalked;

            // An override belongs to a running anomaly, which re-installs it when the event
            // restores. Carrying a stale delegate across a load would bend the shaft with
            // nothing left to unbend it.
            _override = null;
            _overrideOwner = null;
        }
    }
}
