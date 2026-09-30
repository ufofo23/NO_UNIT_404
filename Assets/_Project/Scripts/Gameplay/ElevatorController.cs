using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using NO404.Interaction;

namespace NO404.Gameplay
{
    /// <summary>
    /// The elevator car (GDD 3.1 "엘리베이터 내부", 9.4 "16층").
    ///
    /// The car is a real zone the player rides in. Its indicator is driven by this component
    /// rather than by the floor it is actually at, which is what makes the night-3 hook work:
    /// the building runs B2 to 6F, the panel has no 13 button, and the display can still read
    /// 13. The hidden maintenance button only exists once the player has reason to look for it.
    /// </summary>
    public sealed class ElevatorController : MonoBehaviour
    {
        /// <summary>One button in the car.</summary>
        public struct FloorButton
        {
            public string FloorId;
            public string ZoneId;
            public string LabelKey;
            public FloorButton(string floorId, string zoneId, string labelKey)
            {
                FloorId = floorId; ZoneId = zoneId; LabelKey = labelKey;
            }
        }

        /// <summary>GDD 12.3 catalogue entry #4, and the floor it claims to have stopped at.</summary>
        public const int FalseFloorType = 4;
        /// <summary>
        /// What the indicator reads when it lies (v2.1 spec 22 M01).
        ///
        /// It was 16 while the building had fifteen storeys. The compressed building has six,
        /// and the number the display invents is 13 - which is not simply a smaller lie: the
        /// thirteenth floor is the anomaly the whole of night 3 is about.
        /// </summary>
        public const int FalseFloor = 13;

        /// <summary>
        /// The panel, generated from FloorPlan (spec 27).
        ///
        /// The car cannot be sent anywhere that is not a floor the lift serves, because there
        /// is no way to write a button that names one. That is the whole guarantee spec 32
        /// asks for - the thirteenth floor is never a destination the player can select - and
        /// it holds by construction rather than by nobody adding the button.
        /// </summary>
        public static readonly FloorButton[] Panel = BuildPanel();

        static FloorButton[] BuildPanel()
        {
            var buttons = new List<FloorButton>(8);
            foreach (var floor in FloorPlan.ElevatorStops())
                buttons.Add(new FloorButton(floor.FloorId, floor.PrimaryZoneId,
                                            "ui.elevator." + floor.FloorId.ToLowerInvariant()));
            return buttons.ToArray();
        }

        Text _display;
        GameObject _maintenanceButton;
        string _currentFloorId = FloorPlan.F1;
        int _spoofedFloor;
        float _spoofUntilRealtime;

        public string CurrentFloorId { get { return _currentFloorId; } }

        /// <summary>The floor number on the indicator when it is telling the truth.</summary>
        public int CurrentFloor
        {
            get { return NumberOf(_currentFloorId); }
        }

        /// <summary>
        /// The number a floor shows on the indicator. Basements are negative, so B2 reads B2
        /// and the roof reads the top storey rather than a letter the display cannot render.
        /// </summary>
        public static int NumberOf(string floorId)
        {
            if (floorId == FloorPlan.B2) return -2;
            if (floorId == FloorPlan.B1) return -1;
            if (floorId == FloorPlan.Roof) return 6;
            int index = FloorPlan.IndexOf(floorId);
            return index < 0 ? 1 : index - FloorPlan.IndexOf(FloorPlan.B2) - 1;
        }

        /// <summary>What the indicator actually shows - not necessarily where the car is.</summary>
        public int DisplayedFloor
        {
            get { return Time.realtimeSinceStartup < _spoofUntilRealtime ? _spoofedFloor : CurrentFloor; }
        }

        public void Bind(Text display, GameObject maintenanceButton)
        {
            _display = display;
            _maintenanceButton = maintenanceButton;
        }

        void OnEnable() { EventBus.Subscribe<CctvAnomalyEvent>(OnAnomaly); }
        void OnDisable() { EventBus.Unsubscribe<CctvAnomalyEvent>(OnAnomaly); }

        void OnAnomaly(CctvAnomalyEvent evt)
        {
            // GDD 12.3 #4: the indicator reads a floor the building does not have - marketing
            // hook #3 in GDD 2.3 and the whole premise of night 3, now M01.
            //
            // Match on the catalogue type, not on the instance id. Instance ids carry the
            // night and an optional suffix ("ANOMALY_04_N3", "ANOMALY_04_N1_TEASE"), so an
            // equality test against "ANOMALY_04" never once matched and the display never lied.
            if (evt.CameraId != "CAM-08") return;

            var anomaly = ServiceHub.Content.FindAnomaly(evt.AnomalyId);
            if (anomaly == null || anomaly.typeNumber != FalseFloorType) return;

            if (evt.Started) ShowFalseFloor(FalseFloor, 12f);
            else _spoofUntilRealtime = 0f;
        }

        public void ShowFalseFloor(int floor, float seconds)
        {
            _spoofedFloor = floor;
            _spoofUntilRealtime = Time.realtimeSinceStartup + seconds;
            Log.Info("Elevator", "indicator forced to " + floor);
        }

        /// <summary>
        /// Move the car. Refuses anything that is not a floor the lift serves, so the one
        /// place a caller could put the car on the thirteenth floor is closed (spec 32).
        /// </summary>
        public void SetFloor(string floorId)
        {
            if (!FloorPlan.IsElevatorDestination(floorId))
            {
                Log.Error("Elevator", "refused to move the car to " + floorId);
                return;
            }
            _currentFloorId = floorId;
        }

        void Update()
        {
            if (_display == null) return;

            int shown = DisplayedFloor;
            _display.text = shown < 0 ? "B" + (-shown) : shown.ToString();
            // Red whenever the number is one the building does not have. Six storeys, so
            // anything above the sixth is the lie.
            _display.color = shown > 6 ? UI.UiFactory.Danger : UI.UiFactory.Accent;

            // The maintenance button is only reachable once the player holds Maintenance access,
            // which case C05 grants. Before that the panel looks completely ordinary.
            if (_maintenanceButton != null)
            {
                bool available = ServiceHub.State.HasAccess(AccessLevel.Maintenance);
                if (_maintenanceButton.activeSelf != available) _maintenanceButton.SetActive(available);
            }
        }
    }

    /// <summary>A single floor button inside the car.</summary>
    public sealed class ElevatorButton : InteractableBase
    {
        ElevatorController _car;
        string _floorId;
        string _targetZoneId;
        AccessLevel _required = AccessLevel.Staff1;

        public void Setup(ElevatorController car, string floorId, string targetZoneId, string labelKey,
                          AccessLevel required)
        {
            _car = car;
            _floorId = floorId;
            _targetZoneId = targetZoneId;
            _required = required;
            _labelKey = labelKey;
            _range = 1.8f;
            _repeatable = true;
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            if (!base.CanInteract(context, out reasonKey)) return false;

            if (!ServiceHub.State.HasAccess(_required))
            {
                reasonKey = "ui.prompt.no_access";
                return false;
            }

            // Night 5 only: the elevator runs on the power budget (GDD 9.6). Asking whether
            // the circuit is registered keeps every other night unaffected.
            if (ServiceHub.Facility.HasCircuit(CircuitIds.Elevator) &&
                !ServiceHub.Facility.IsCircuitOn(CircuitIds.Elevator))
            {
                reasonKey = "ui.prompt.elevator_no_power";
                return false;
            }

            // The car will not move to a floor that has not finished streaming in (GDD 20.5).
            var streamer = ServiceHub.Zones;
            if (streamer != null && !streamer.IsZoneReady(_targetZoneId))
            {
                streamer.RequestZone(_targetZoneId);
                reasonKey = streamer.HasFailed(_targetZoneId) ? "ui.prompt.zone_load_failed"
                                                              : "ui.prompt.zone_loading";
                return false;
            }

            return true;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            var player = context.Transform != null
                ? context.Transform.GetComponentInParent<PlayerController>()
                : null;

            var destination = ZoneRegistry.FindSpawn(_targetZoneId);
            if (player == null || destination == null) return;

            if (_car != null) _car.SetFloor(_floorId);

            // Leaving the car is leaving the shaft as far as the stair state is concerned:
            // a player who rides to 5F and then opens the stair door there must start at the
            // 5F landing, not wherever they last were in the stairwell (spec 0.8.2).
            ServiceHub.Stairs.Exit();

            // GDD 15.5: the car is the single most expensive thing on the night circuit, which
            // is what makes the stairs a choice rather than a longer version of the same trip.
            Net.NetShift.Request(Net.NetShift.ShiftAct.ElevatorRide);

            player.Teleport(destination.position, destination.rotation);
            ServiceHub.Player.EnterZone(_targetZoneId);
            Net.NetShift.Request(Net.NetShift.ShiftAct.EnterZone, _targetZoneId);
            ServiceHub.Save.RequestAutosave(Save.SaveReason.ZoneTransition);
        }
    }
}
