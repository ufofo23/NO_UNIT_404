using UnityEngine;
using NO404.Core;
using NO404.Interaction;
using SC = NO404.ContentData.SeedContent;

namespace NO404.Gameplay
{
    /// <summary>
    /// Where the night 1/3/5 subquests happen (v5.1 10, 12, 14), placed in the B1~6F
    /// building of v5.1 3.1.
    ///
    /// Each prop is a small kit of primitives - a panel with a door and a handle, a dial with
    /// a needle, a ceiling sensor with its LED - under one root that carries the only
    /// collider. That root is what the interaction ray finds and what <see cref="PropArt"/>
    /// can replace with a model of the same name. Props never share a volume, even when
    /// their quests are on different nights: the ray stops at the first collider it meets,
    /// and an inert prop in front of a live one would hide it.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        enum Kit { Panel, Gauge, Paper, Sensor, Lamp, Hatch, Valve, Breaker, Radio, Package, Device, Camera,
                   Extinguisher, Seal, Clock, Plate, Tape, Drain, Opening, Door }

        void BuildSubquestProps()
        {
            var office = HalfOf(ZoneIds.Office);
            var lobby = HalfOf(ZoneIds.Lobby);
            var parking = HalfOf(ZoneIds.Parking);
            float corridor = BuildingSpec.CorridorWidth * 0.5f;      // 1.2
            float north = corridor - 0.1f, south = -corridor + 0.1f;

            // ---------------- night 1 ----------------
            Ev("N1-R02", ZoneIds.PumpRoom, "PumpPressureGauge", new Vector3(0.4f, 1.4f, 2.88f), Kit.Gauge, "EV_N1R02_PRESSURE");
            Act("N1-R02", ZoneIds.PumpRoom, "PumpControlPanel", new Vector3(2.4f, 1.2f, 2.85f), Kit.Panel, "n1r02_panel");
            Act("N1-R02", ZoneIds.PumpRoom, "PumpBreaker", new Vector3(3.85f, 1.4f, 0.5f), Kit.Breaker, "n1r02_breaker", yaw: 90f);
            Ev("N1-R03", ZoneIds.Lobby, "EntranceDoorSensor", new Vector3(-3.65f, 2.36f, 2.28f), Kit.Sensor, "EV_N1R03_SENSOR");
            Ev("N1-R04", ZoneIds.Parking, "IllegalCarPlate", new Vector3(8f, 0.55f, -7.66f), Kit.Plate, "EV_N1R04_PLATE");
            Ev("N1-R04", ZoneIds.Office, "ParkingLedger", new Vector3(-0.6f, 0.83f, office.y - 0.85f), Kit.Paper, "EV_N1R04_DB");
            Ev("N1-R05", ZoneIds.Floor06, "VentDamperHatch", new Vector3(1.5f, HeightOf(ZoneIds.Floor06) - 0.06f, -0.3f), Kit.Hatch, "EV_N1R05_DAMPER", pitch: -90f);
            Ev("N1-R06", ZoneIds.Laundry, "LaundryDrainTrap", new Vector3(0.5f, 0.05f, 0.5f), Kit.Drain, "EV_N1R06_TRAP");
            Ev("N1-R06", ZoneIds.PumpRoom, "DrainPumpGauge", new Vector3(-0.6f, 1.0f, 2.88f), Kit.Gauge, "EV_N1R06_SEPARATE");
            Ev("N1-R07", ZoneIds.Floor03, "SmokeSensor3F", new Vector3(-6f, HeightOf(ZoneIds.Floor03) - 0.05f, 0f), Kit.Sensor, "EV_N1R07_SENSOR");
            Ev("N1-R08", ZoneIds.Lobby, "Parcel403Slot", new Vector3(lobby.x - 0.55f, 1.05f, 2.15f), Kit.Package, "EV_N1R08_SHELF");
            Ev("N1-R09", ZoneIds.Lobby, "Mailbox403Envelope", new Vector3(-lobby.x + 0.45f, 1.6f, -0.3f), Kit.Plate, "EV_N1R09_ENVELOPE", yaw: -90f);
            Ev("N1-R10", ZoneIds.Office, "FacilityLog304", new Vector3(office.x - 0.12f, 1.4f, -1.0f), Kit.Panel, "EV_N1R10_LOG", yaw: 90f);
            Ev("N1-R10", ZoneIds.Floor03, "WaterMeter304", new Vector3(2.4f, 0.75f, south), Kit.Panel, "EV_N1R10_METER", yaw: 180f);
            Ev("N1-R11", ZoneIds.Elevator, "CarOperationLog", new Vector3(0.68f, 1.2f, -0.35f), Kit.Panel, "EV_N1R11_LIFT_LOG", yaw: 90f);
            Ev("N1-R11", ZoneIds.Lobby, "LiftCallButton", new Vector3(2.75f, 1.1f, lobby.y - 0.1f), Kit.Breaker, "EV_N1R11_GHOST_PRESS");
            Ev("N1-R12", ZoneIds.Office, "AnalogWallClock", new Vector3(1.6f, 2.0f, -office.y + 0.12f), Kit.Clock, "EV_N1R12_CLOCK", yaw: 180f);
            Ev("N1-R14", ZoneIds.RecyclingYard, "DiscardedWardrobeSticker", new Vector3(-3f, 1.2f, 2.52f), Kit.Seal, "EV_N1R14_STICKER");

            // ---------------- night 3 ----------------
            Ev("N3-R01", ZoneIds.Office, "LiftInspectionOrder", new Vector3(0.55f, 0.83f, office.y - 0.85f), Kit.Paper, "EV_N3R01_WORK_ORDER");
            Ev("N3-R01", ZoneIds.Lobby, "InspectorBadge", new Vector3(0.0f, 0.93f, 1.8f), Kit.Paper, "EV_N3R01_BADGE");
            Ev("N3-R02", ZoneIds.Lobby, "ContractorWorkOrder", new Vector3(0.6f, 0.93f, 1.8f), Kit.Paper, "EV_N3R02_WORK_ORDER");
            Ev("N3-R02", ZoneIds.Lobby, "ContractorToolboxSeal", new Vector3(-0.7f, 0.2f, 1.3f), Kit.Package, "EV_N3R02_SEAL");
            Ev("N3-R03", ZoneIds.Floor04, "StairDoorCloser", new Vector3(9.85f, 2.25f, 0.4f), Kit.Device, "EV_N3R03_CLOSER", yaw: 90f);
            Act("N3-R03", ZoneIds.Floor04, "StairDoorTension", new Vector3(9.85f, 2.25f, -0.4f), Kit.Valve, "n3r03_tension", yaw: 90f);
            Ev("N3-R04", ZoneIds.Floor05, "PatchedWall504Measure", new Vector3(3f, 0.9f, north - 0.06f), Kit.Tape, "EV_N3R04_WALL_LENGTH");
            Act("N3-R04", ZoneIds.Floor05, "PatchedWall504Knock", new Vector3(3f, 1.55f, north - 0.06f), Kit.Plate, "n3r04_knock");
            Ev("N3-R05", ZoneIds.Stairwell, "EmergencyLamp4F", Landing(FloorPlan.F4, 1.6f, 2.2f), Kit.Lamp, "EV_N3R05_LAMP", yaw: LandingYaw(FloorPlan.F4));
            Ev("N3-R06", ZoneIds.Archive, "RecordBoxSeal0417", new Vector3(0.8f, 1.3f, HalfOf(ZoneIds.Archive).y - 0.72f), Kit.Package, "EV_N3R06_SEAL");
            Ev("N3-R06", ZoneIds.Archive, "RecordHandoverSheet", new Vector3(-1.6f, 0.83f, -1.2f), Kit.Paper, "EV_N3R06_HANDOVER");
            Ev("N3-R07", ZoneIds.Floor04, "FloorPlanFrame4F", new Vector3(0f, 1.5f, south), Kit.Plate, "EV_N3R07_PLAN", yaw: 180f);
            Ev("N3-R07", ZoneIds.Floor04, "WallKnockSpot404", new Vector3(BuildingSpec.HiddenWallCentreX, 1.0f, north), Kit.Plate, "EV_N3R07_COUNT");
            Ev("N3-R08", ZoneIds.Floor05, "LoopExtinguisherWest", new Vector3(-8.6f, 0.45f, south + 0.1f), Kit.Extinguisher, "EV_N3R08_EXTINGUISHER", yaw: 180f);
            Ev("N3-R08", ZoneIds.Floor05, "LoopWindowDrops", new Vector3(-2f, 1.75f, south), Kit.Tape, "EV_N3R08_WINDOW", yaw: 180f);
            Act("N3-R08", ZoneIds.Floor05, "LoopFollowNumber", new Vector3(BuildingSpec.StandardDoorX[4], 1.5f, north), Kit.Plate, "n3r08_follow_number");
            Act("N3-R08", ZoneIds.Floor05, "LoopRunThrough", new Vector3(9.85f, 1.3f, -0.8f), Kit.Plate, "n3r08_run", yaw: 90f);
            Act("N3-R08", ZoneIds.Floor05, "LoopMarkerExit", new Vector3(-8.0f, 1.5f, south), Kit.Plate, "n3r08_marker", yaw: 180f);
            Ev("N3-R09", ZoneIds.Machinery, "LiftControllerLog", new Vector3(HalfOf(ZoneIds.Machinery).x - 0.15f, 1.3f, 1.0f), Kit.Panel, "EV_N3R09_CONTROL_LOG", yaw: 90f);
            Ev("N3-R09", ZoneIds.Elevator, "CarIndicatorCheck", new Vector3(0.45f, 1.85f, 0.6f), Kit.Plate, "EV_N3R09_POSITION");
            Ev("N3-R10", ZoneIds.Floor04, "CorridorMeasureTape", new Vector3(3.1f, 0.35f, north), Kit.Tape, "EV_N3R10_MEASURE");
            Ev("N3-R11", ZoneIds.Stairwell, "LandingPaint4F", Landing(FloorPlan.F4, -1.4f, 0.04f, onFloor: true), Kit.Tape, "EV_N3R11_LANDING_PAINT", pitch: 90f);
            Ev("N3-R11", ZoneIds.Floor04, "StairDoorSealNorth", new Vector3(9.85f, 1.2f, 0.65f), Kit.Seal, "EV_N3R11_SEAL", yaw: 90f);
            Ev("N3-R12", ZoneIds.Floor04, "ServiceWallHatch", new Vector3(6.0f, 0.55f, north), Kit.Hatch, "EV_N3R12_CHILD_ITEMS");
            Ev("N3-R13", ZoneIds.Stairwell, "StairLampNumber", Landing(FloorPlan.F4, -1.6f, 2.2f), Kit.Lamp, "EV_N3R13_LAMP_NO", yaw: LandingYaw(FloorPlan.F4));
            Ev("N3-R13", ZoneIds.Stairwell, "StairDoorSealNumber", Landing(FloorPlan.F4, 0.75f, 1.2f), Kit.Seal, "EV_N3R13_SEAL_NO", yaw: LandingYaw(FloorPlan.F4));
            Act("N3-R13", ZoneIds.Stairwell, "KeepDescending", StairWall(FloorPlan.F4), Kit.Plate, "n3r13_descend", yaw: 90f);
            Ev("N3-R14", ZoneIds.Floor04, "ServiceDoorVoice", new Vector3(BuildingSpec.Floor04DoorX[4], 1.5f, north - 0.02f), Kit.Device, "EV_N3R14_FUTURE_VOICE");
            Act("N3-R14", ZoneIds.Floor04, "ServiceDoorForce", new Vector3(BuildingSpec.Floor04DoorX[4] - 0.38f, 1.0f, north - 0.02f), Kit.Breaker, "n3r14_open");

            // ---------------- night 5 ----------------
            Ev("N5-R01", ZoneIds.Machinery, "LoadVoltageGauge", new Vector3(-1.5f, 1.5f, HalfOf(ZoneIds.Machinery).y - 0.55f), Kit.Gauge, "EV_N5R01_LOAD");
            Act("N5-R01", ZoneIds.Machinery, "OverloadPanel", new Vector3(0f, 1.2f, HalfOf(ZoneIds.Machinery).y - 0.55f), Kit.Breaker, "n5r01_panel");
            Ev("N5-R02", ZoneIds.Floor03, "Door303Knock", new Vector3(BuildingSpec.StandardDoorX[2], 1.5f, north - 0.04f), Kit.Plate, "EV_N5R02_STATE");
            Ev("N5-R03", ZoneIds.PumpRoom, "PumpOverloadGauge", new Vector3(1.3f, 1.5f, 2.88f), Kit.Gauge, "EV_N5R03_GAUGE");
            Act("N5-R03", ZoneIds.PumpRoom, "PumpBypassValve", new Vector3(2.9f, 0.9f, 1.0f), Kit.Valve, "n5r03_valve", yaw: 90f);
            Ev("N5-R04", ZoneIds.Parking, "ArchiveDoorSeal", new Vector3(2.65f, 1.3f, -parking.y + 0.12f), Kit.Seal, "EV_N5R04_SEAL", yaw: 180f);
            Ev("N5-R04", ZoneIds.Office, "ArchiveAccessLog", new Vector3(office.x - 0.12f, 1.4f, -2.2f), Kit.Panel, "EV_N5R04_LOG", yaw: 90f);
            Ev("N5-R05", ZoneIds.Floor04, "ReturnedPackage", new Vector3(6.8f, 0.2f, 0.82f), Kit.Package, "EV_N5R05_PACKAGE");
            Ev("N5-R06", ZoneIds.Floor04, "ServiceRecorderSlot", new Vector3(6.6f, 2.2f, north), Kit.Device, "EV_N5R06_SLOT");
            Ev("N5-R06", ZoneIds.Floor04, "ServiceRecorderPlayback", new Vector3(6.0f, 2.2f, north), Kit.Radio, "EV_N5R06_2009_VOICE",
               requiredFlag: Cases.SubquestRules.N5RecorderSafe);
            Ev("N5-R07", ZoneIds.Floor03, "AnalogThermometer3F", new Vector3(-4f, 1.5f, south), Kit.Gauge, "EV_N5R07_TEMP", yaw: 180f);
            Ev("N5-R07", ZoneIds.Office, "FireAlarmPanel", new Vector3(-office.x + 0.12f, 1.5f, 2.3f), Kit.Panel, "EV_N5R07_PANEL", yaw: -90f);
            Ev("N5-R08", ZoneIds.Floor04, "Cam04Housing", new Vector3(9.6f, 2.3f, -0.75f), Kit.Camera, "EV_N5R08_LED", yaw: 90f);
            Ev("N5-R09", ZoneIds.Floor04, "StairDoorSealSouth", new Vector3(9.85f, 1.2f, -0.65f), Kit.Seal, "EV_N5R09_SEAL", yaw: 90f);
            Ev("N5-R10", ZoneIds.Floor05, "LoopExtinguisherEast", new Vector3(8.6f, 0.45f, south + 0.1f), Kit.Extinguisher, "EV_N5R10_MARKER", yaw: 180f);
            Act("N5-R10", ZoneIds.Floor05, "LostRandomDoor", new Vector3(BuildingSpec.StandardDoorX[0], 1.5f, north), Kit.Plate, "n5r10_door");
            Act("N5-R10", ZoneIds.Floor05, "LostMarkerExit", new Vector3(8.0f, 1.5f, south), Kit.Plate, "n5r10_marker", yaw: 180f);
            Ev("N5-R11", ZoneIds.Stairwell, "StairStatus1F", Landing(FloorPlan.F1, 1.6f, 1.5f), Kit.Plate, "EV_N5R11_STAIRS", yaw: LandingYaw(FloorPlan.F1));
            Ev("N5-R11", ZoneIds.Office, "EvacuationBoard", new Vector3(2.8f, 1.4f, -office.y + 0.12f), Kit.Plate, "EV_N5R11_RISK", yaw: 180f);
            Ev("N5-R12", ZoneIds.Parking, "SprinklerTemp", new Vector3(3.4f, 1.6f, -parking.y + 0.12f), Kit.Gauge, "EV_N5R12_TEMP", yaw: 180f);
            Act("N5-R12", ZoneIds.Parking, "SprinklerValve", new Vector3(4.2f, 1.2f, -parking.y + 0.14f), Kit.Valve, "n5r12_valve", yaw: 180f);
            Act("N5-R12", ZoneIds.Parking, "SprinklerPower", new Vector3(5.1f, 1.4f, -parking.y + 0.12f), Kit.Breaker, "n5r12_power", yaw: 180f);
            Ev("N5-R13", ZoneIds.Office, "OldBandScanner", new Vector3(-office.x + 0.25f, 1.1f, -0.2f), Kit.Radio, "EV_N5R13_PATTERN", yaw: -90f);
            Ev("N5-R13", ZoneIds.Floor04, "BroadcastPosition", new Vector3(BuildingSpec.HiddenWallCentreX, 1.7f, north), Kit.Plate, "EV_N5R13_POSITION");
            Ev("N5-R14", ZoneIds.Floor04, "OverlapAnalogTemp", new Vector3(4.0f, 1.4f, south), Kit.Gauge, "EV_N5R14_OVERLAP", yaw: 180f);
            Act("N5-R14", ZoneIds.Floor04, "OverlapVisiblePath", new Vector3(-4.0f, 1.0f, south), Kit.Opening, "n5r14_follow", yaw: 180f);
        }

        // ---- placement helpers ----------------------------------------------

        /// <summary>A spot on a stairwell landing's back wall, at a height above that landing.</summary>
        static Vector3 Landing(string floorId, float x, float height, bool onFloor = false)
        {
            float z = StairShaft.LandingZOf(floorId);
            float wall = HalfOf(ZoneIds.Stairwell).y - 0.12f;
            return new Vector3(x, StairShaft.HeightOf(floorId) + height, onFloor ? z : (z > 0f ? wall : -wall));
        }

        static float LandingYaw(string floorId) { return StairShaft.LandingZOf(floorId) > 0f ? 0f : 180f; }

        /// <summary>The east wall of the shaft, beside the flight that leaves this landing.</summary>
        static Vector3 StairWall(string floorId)
        {
            float z = StairShaft.LandingZOf(floorId);
            return new Vector3(HalfOf(ZoneIds.Stairwell).x - 0.12f, StairShaft.HeightOf(floorId) + 1.2f,
                               z > 0f ? z - 1.3f : z + 1.3f);
        }

        /// <summary>
        /// Kits face local -z. <paramref name="yaw"/> turns that face off a wall: 0 on a north
        /// wall, 180 on a south wall, 90 on an east wall, -90 on a west wall.
        /// <paramref name="pitch"/> lays it flat: -90 faces the floor (a ceiling fitting), 90 the
        /// ceiling (paint on the ground).
        /// </summary>
        void Ev(string caseId, string zone, string name, Vector3 position, Kit kit, string evidenceId,
                float yaw = 0f, float pitch = 0f, string requiredFlag = null)
        {
            Prop(caseId, zone, name, position, kit, evidenceId, null, SC.PropKey(evidenceId), yaw, pitch, requiredFlag);
        }

        void Act(string caseId, string zone, string name, Vector3 position, Kit kit, string action, float yaw = 0f)
        {
            Prop(caseId, zone, name, position, kit, null, action, "quest.act." + action, yaw, 0f, null);
        }

        void Prop(string caseId, string zone, string name, Vector3 position, Kit kit, string evidenceId,
                  string action, string labelKey, float yaw, float pitch, string requiredFlag)
        {
            var root = OwnedRoot(zone);
            if (root == null) return;

            var size = SizeOf(kit);
            GameObject go = PropArt.TryBuild(root, name, position, size);
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                var collider = go.AddComponent<BoxCollider>();
                collider.size = size;
                BuildKit(go.transform, kit);
            }

            go.AddComponent<SubquestInteractable>().Setup(caseId, evidenceId, action, labelKey, requiredFlag);
        }

        static Vector3 SizeOf(Kit kit)
        {
            switch (kit)
            {
                case Kit.Panel: return new Vector3(0.42f, 0.55f, 0.14f);
                case Kit.Gauge: return new Vector3(0.28f, 0.28f, 0.12f);
                case Kit.Paper: return new Vector3(0.3f, 0.06f, 0.36f);
                case Kit.Sensor: return new Vector3(0.24f, 0.08f, 0.24f);
                case Kit.Lamp: return new Vector3(0.42f, 0.2f, 0.14f);
                case Kit.Hatch: return new Vector3(0.6f, 0.6f, 0.08f);
                case Kit.Valve: return new Vector3(0.36f, 0.36f, 0.2f);
                case Kit.Breaker: return new Vector3(0.24f, 0.32f, 0.1f);
                case Kit.Radio: return new Vector3(0.38f, 0.2f, 0.22f);
                case Kit.Package: return new Vector3(0.45f, 0.32f, 0.36f);
                case Kit.Device: return new Vector3(0.36f, 0.2f, 0.14f);
                case Kit.Camera: return new Vector3(0.18f, 0.16f, 0.32f);
                case Kit.Extinguisher: return new Vector3(0.22f, 0.62f, 0.22f);
                case Kit.Seal: return new Vector3(0.16f, 0.12f, 0.06f);
                case Kit.Clock: return new Vector3(0.36f, 0.36f, 0.08f);
                case Kit.Plate: return new Vector3(0.34f, 0.24f, 0.06f);
                case Kit.Tape: return new Vector3(0.5f, 0.08f, 0.06f);
                case Kit.Drain: return new Vector3(0.36f, 0.1f, 0.36f);
                case Kit.Opening: return new Vector3(0.9f, 1.9f, 0.06f);
                default: return new Vector3(0.4f, 0.4f, 0.1f);
            }
        }

        // ---- the kits ----------------------------------------------------------

        void BuildKit(Transform root, Kit kit)
        {
            var steel = new Color(0.46f, 0.47f, 0.46f);
            var dark = new Color(0.12f, 0.12f, 0.13f);
            var paper = new Color(0.80f, 0.78f, 0.70f);
            var red = new Color(0.62f, 0.12f, 0.10f);
            var size = SizeOf(kit);

            switch (kit)
            {
                case Kit.Panel:
                    Piece(root, "Body", Vector3.zero, size, new Color(0.42f, 0.44f, 0.42f));
                    Piece(root, "Face", new Vector3(0f, 0.05f, -size.z * 0.5f - 0.005f), new Vector3(size.x - 0.08f, size.y - 0.2f, 0.01f), dark);
                    Glow(root, "Screen", new Vector3(0f, 0.08f, -size.z * 0.5f - 0.012f), new Vector3(size.x - 0.14f, size.y - 0.32f, 0.004f), new Color(0.25f, 0.55f, 0.45f));
                    Piece(root, "Handle", new Vector3(size.x * 0.32f, -0.18f, -size.z * 0.5f - 0.02f), new Vector3(0.03f, 0.1f, 0.03f), steel);
                    break;

                case Kit.Gauge:
                    Round(root, "Dial", Vector3.zero, new Vector3(size.x, size.z, size.y), new Color(0.82f, 0.82f, 0.78f), 90f);
                    Round(root, "Bezel", new Vector3(0f, 0f, 0.01f), new Vector3(size.x + 0.03f, size.z * 0.6f, size.y + 0.03f), steel, 90f);
                    Piece(root, "Needle", new Vector3(0.03f, 0.03f, -size.z * 0.5f - 0.01f), new Vector3(0.012f, 0.11f, 0.006f), red)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
                    break;

                case Kit.Paper:
                    Piece(root, "Sheet", Vector3.zero, new Vector3(size.x * 0.9f, 0.012f, size.z * 0.9f), paper);
                    Piece(root, "Clip", new Vector3(0f, 0.012f, size.z * 0.4f), new Vector3(0.08f, 0.02f, 0.03f), steel);
                    for (int i = 0; i < 4; i++)
                        Piece(root, "Line" + i, new Vector3(-0.02f, 0.008f, 0.1f - i * 0.06f), new Vector3(0.2f, 0.004f, 0.012f), new Color(0.3f, 0.3f, 0.32f));
                    break;

                case Kit.Sensor:
                    Round(root, "Disc", Vector3.zero, new Vector3(0.22f, 0.06f, 0.22f), new Color(0.86f, 0.86f, 0.84f), 0f);
                    Glow(root, "Led", new Vector3(0.06f, -0.035f, 0f), new Vector3(0.02f, 0.01f, 0.02f), new Color(0.95f, 0.15f, 0.1f));
                    break;

                case Kit.Lamp:
                    Piece(root, "Box", Vector3.zero, size, new Color(0.78f, 0.78f, 0.74f));
                    Glow(root, "HeadL", new Vector3(-0.12f, -0.05f, -0.08f), new Vector3(0.1f, 0.06f, 0.04f), new Color(0.35f, 0.33f, 0.28f));
                    Glow(root, "HeadR", new Vector3(0.12f, -0.05f, -0.08f), new Vector3(0.1f, 0.06f, 0.04f), new Color(0.35f, 0.33f, 0.28f));
                    break;

                case Kit.Hatch:
                    Piece(root, "Frame", Vector3.zero, size, new Color(0.40f, 0.40f, 0.38f));
                    Piece(root, "Gap", new Vector3(0f, 0f, -0.035f), new Vector3(size.x - 0.06f, size.y - 0.06f, 0.012f), dark);
                    Piece(root, "Latch", new Vector3(size.x * 0.35f, 0f, -0.05f), new Vector3(0.05f, 0.08f, 0.02f), steel);
                    break;

                case Kit.Valve:
                    Round(root, "Stem", new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 0.14f, 0.05f), steel, 90f);
                    Round(root, "Wheel", new Vector3(0f, 0f, -0.05f), new Vector3(0.32f, 0.03f, 0.32f), red, 90f);
                    break;

                case Kit.Breaker:
                    Piece(root, "Box", Vector3.zero, size, new Color(0.34f, 0.36f, 0.35f));
                    Piece(root, "Lever", new Vector3(0f, 0.04f, -0.07f), new Vector3(0.05f, 0.12f, 0.04f), new Color(0.15f, 0.15f, 0.15f));
                    Piece(root, "Warning", new Vector3(0f, -0.11f, -0.052f), new Vector3(0.18f, 0.05f, 0.004f), new Color(0.80f, 0.65f, 0.12f));
                    break;

                case Kit.Radio:
                    Piece(root, "Case", Vector3.zero, size, new Color(0.22f, 0.24f, 0.22f));
                    Glow(root, "Band", new Vector3(-0.06f, 0.03f, -size.z * 0.5f - 0.004f), new Vector3(0.18f, 0.05f, 0.004f), new Color(0.85f, 0.55f, 0.20f));
                    Round(root, "Knob", new Vector3(0.12f, 0f, -size.z * 0.5f - 0.015f), new Vector3(0.06f, 0.02f, 0.06f), steel, 90f);
                    Piece(root, "Aerial", new Vector3(0.14f, 0.22f, 0f), new Vector3(0.01f, 0.26f, 0.01f), steel);
                    break;

                case Kit.Package:
                    Piece(root, "Carton", Vector3.zero, size, new Color(0.56f, 0.44f, 0.30f));
                    Piece(root, "TapeTop", new Vector3(0f, size.y * 0.5f + 0.002f, 0f), new Vector3(0.06f, 0.004f, size.z + 0.004f), new Color(0.72f, 0.66f, 0.50f));
                    Piece(root, "Label", new Vector3(0.08f, 0.04f, -size.z * 0.5f - 0.003f), new Vector3(0.16f, 0.1f, 0.004f), paper);
                    break;

                case Kit.Device:
                    Piece(root, "Housing", Vector3.zero, size, new Color(0.26f, 0.27f, 0.28f));
                    Glow(root, "Led", new Vector3(0.12f, 0.05f, -size.z * 0.5f - 0.004f), new Vector3(0.02f, 0.02f, 0.004f), new Color(0.2f, 0.9f, 0.3f));
                    Piece(root, "Cable", new Vector3(-0.1f, -0.25f, 0f), new Vector3(0.02f, 0.3f, 0.02f), dark);
                    break;

                case Kit.Camera:
                    Piece(root, "Body", Vector3.zero, size, new Color(0.80f, 0.80f, 0.78f));
                    Round(root, "Lens", new Vector3(0f, 0f, -size.z * 0.5f - 0.02f), new Vector3(0.09f, 0.04f, 0.09f), dark, 90f);
                    Glow(root, "Tally", new Vector3(0.06f, 0.06f, -size.z * 0.5f), new Vector3(0.015f, 0.015f, 0.006f), new Color(0.95f, 0.15f, 0.1f));
                    break;

                case Kit.Extinguisher:
                    Round(root, "Bottle", Vector3.zero, new Vector3(0.18f, 0.56f, 0.18f), red, 0f);
                    Piece(root, "Head", new Vector3(0f, 0.31f, 0f), new Vector3(0.08f, 0.06f, 0.12f), dark);
                    Piece(root, "Tag", new Vector3(0f, 0.12f, -0.095f), new Vector3(0.07f, 0.1f, 0.004f), new Color(0.85f, 0.75f, 0.2f));
                    break;

                case Kit.Seal:
                    Piece(root, "Tag", Vector3.zero, size, new Color(0.82f, 0.68f, 0.16f));
                    Piece(root, "Number", new Vector3(0f, 0f, -size.z * 0.5f - 0.002f), new Vector3(0.1f, 0.03f, 0.004f), dark);
                    break;

                case Kit.Clock:
                    Round(root, "Face", Vector3.zero, new Vector3(0.34f, 0.04f, 0.34f), new Color(0.88f, 0.87f, 0.82f), 90f);
                    Round(root, "Rim", new Vector3(0f, 0f, 0.012f), new Vector3(0.37f, 0.03f, 0.37f), dark, 90f);
                    Piece(root, "Hour", new Vector3(0f, 0.04f, -0.03f), new Vector3(0.015f, 0.09f, 0.006f), dark);
                    Piece(root, "Minute", new Vector3(0.05f, 0f, -0.032f), new Vector3(0.12f, 0.012f, 0.006f), dark);
                    break;

                case Kit.Plate:
                    Piece(root, "Plate", Vector3.zero, size, new Color(0.55f, 0.55f, 0.52f));
                    Piece(root, "Marks", new Vector3(0f, 0f, -size.z * 0.5f - 0.002f), new Vector3(size.x * 0.7f, size.y * 0.3f, 0.004f), dark);
                    break;

                case Kit.Tape:
                    Piece(root, "Strip", Vector3.zero, size, new Color(0.85f, 0.72f, 0.18f));
                    for (int i = 0; i < 5; i++)
                        Piece(root, "Tick" + i, new Vector3(-0.2f + i * 0.1f, 0f, -size.z * 0.5f - 0.002f), new Vector3(0.006f, 0.05f, 0.004f), dark);
                    break;

                case Kit.Drain:
                    Piece(root, "Grate", Vector3.zero, new Vector3(size.x, 0.02f, size.z), new Color(0.30f, 0.30f, 0.30f));
                    for (int i = 0; i < 5; i++)
                        Piece(root, "Slot" + i, new Vector3(0f, 0.012f, -0.12f + i * 0.06f), new Vector3(size.x - 0.06f, 0.004f, 0.02f), dark);
                    Glow(root, "Water", new Vector3(0f, 0.015f, 0f), new Vector3(size.x + 0.4f, 0.002f, size.z + 0.3f), new Color(0.06f, 0.08f, 0.09f));
                    break;

                case Kit.Opening:
                    // A gap in the wall that should not be there, in 2009's smoke.
                    Glow(root, "Void", Vector3.zero, size, new Color(0.03f, 0.025f, 0.02f));
                    Piece(root, "Scorch", new Vector3(0f, 0.8f, -0.02f), new Vector3(size.x + 0.2f, 0.3f, 0.01f), new Color(0.10f, 0.08f, 0.07f));
                    break;

                default:
                    Piece(root, "Box", Vector3.zero, size, steel);
                    break;
            }
        }

        GameObject Piece(Transform root, string name, Vector3 position, Vector3 size, Color colour)
        {
            var go = Box(root, name, position, size, colour);
            StripCollider(go);
            return go;
        }

        GameObject Round(Transform root, string name, Vector3 position, Vector3 size, Color colour, float pitch)
        {
            var go = Part(root, PrimitiveType.Cylinder, name, position, size, colour, false);
            go.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            return go;
        }

        GameObject Glow(Transform root, string name, Vector3 position, Vector3 size, Color colour)
        {
            var go = Piece(root, name, position, size, colour);
            SurfaceArt.ApplyGlow(go, colour);
            return go;
        }
    }
}
