using NO404.Core;
using NO404.Gameplay;

namespace NO404.Anomalies
{
    /// <summary>
    /// M07's hold on the fifth floor (spec 22 M07, 0.8.4).
    ///
    /// The corridor repeating is one half of the event; the other is that the stairwell
    /// repeats with it, and spec 0.8.4 is specific about how: a player who entered the shaft
    /// at 5F and walks down arrives back at 5F. Not at a generic "wrong floor" - at the floor
    /// they started from, because knowing which floor that was is the only thing that lets
    /// them notice they are looping at all.
    ///
    /// So this bends the shaft through <see cref="StairNavigator"/>'s override rather than by
    /// moving the player: adjacency stays exactly what FloorPlan says, and the loop is a thing
    /// laid on top of it that can be lifted in one call.
    ///
    /// It subscribes to the event lifecycle rather than being ticked, so the loop exists for
    /// precisely as long as the event does - including after a load, where the event restores
    /// Active and this puts the override straight back.
    /// </summary>
    public sealed class CorridorLoopDirector
    {
        /// <summary>
        /// How many flights the loop swallows before the shaft behaves.
        ///
        /// Spec 30.3 chains this to night 4: getting M17 wrong raises FloorRisk_5F, and a
        /// worse fifth floor means a longer loop. Three is the floor rather than the value -
        /// the risk is added on top.
        /// </summary>
        public const int BaseLoopFlights = 3;

        readonly StairNavigator _stairs;
        readonly RiskService _risk;
        readonly DistortionDirector _distortion;

        bool _installed;

        public CorridorLoopDirector(StairNavigator stairs, RiskService risk,
                                    DistortionDirector distortion)
        {
            _stairs = stairs;
            _risk = risk;
            _distortion = distortion;
        }

        public void Enable()
        {
            EventBus.Subscribe<ManualEventStateChangedEvent>(OnEventStateChanged);
        }

        public void Disable()
        {
            EventBus.Unsubscribe<ManualEventStateChangedEvent>(OnEventStateChanged);
            Release();
        }

        /// <summary>
        /// Flights the shaft swallows this run.
        ///
        /// Two things lengthen it, and they are different kinds of thing. FloorRisk_5F is what
        /// the fifth floor is like - spec 30.3 chains it to getting M17 wrong the night
        /// before. Exposure is what the caretaker is like: spec 0.10.4 says the stairs repeat
        /// more often for someone carrying enough of the building, wherever they are.
        /// </summary>
        public int LoopFlights
        {
            get
            {
                int flights = BaseLoopFlights + _risk.FloorRisk(FloorPlan.F5);
                if (_distortion != null) flights += _distortion.ExtraLoopFlights;
                return flights;
            }
        }

        void OnEventStateChanged(ManualEventStateChangedEvent evt)
        {
            if (evt.EventId != ManualEventIds.M07_CorridorLoop) return;

            if (evt.Current == ManualEventState.Active) Hold();
            else if (evt.Current.IsResolved()) Release();
        }

        void Hold()
        {
            if (_installed) return;
            _stairs.SetOverride(ManualEventIds.M07_CorridorLoop, Bend);
            _installed = true;
        }

        void Release()
        {
            if (!_installed) return;
            _stairs.ClearOverride(ManualEventIds.M07_CorridorLoop);
            _installed = false;
        }

        /// <summary>
        /// Return the floor the player just left, until they have walked enough flights.
        ///
        /// Two things it deliberately does not do. It does not send them somewhere arbitrary -
        /// spec 0.8.4 wants the *entry* floor repeated, and returning <paramref name="from"/>
        /// gives exactly that whichever way they walk. And it does not hold them for ever:
        /// past the count it yields to normal adjacency, so the loop always ends even if
        /// nothing about the event does (spec 0.5).
        /// </summary>
        string Bend(string from, FloorPlan.Direction direction, string normalTarget, int flightsWalked)
        {
            if (flightsWalked >= LoopFlights) return null;

            // Never trap the player against an end of the shaft: if adjacency has nowhere to
            // send them anyway, holding them here would be a wall rather than a loop.
            return normalTarget == null ? null : from;
        }
    }
}
