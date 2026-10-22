using UnityEngine;
using NO404.Core;
using R = NO404.Cases.SubquestRules;

namespace NO404.Gameplay
{
    /// <summary>
    /// Where the night 2/4/6 subquests happen (v5.1 11, 13, 15), in the same kits and under
    /// the same rule as <see cref="BuildSubquestProps"/>: each prop is its own collider, is
    /// silent outside its quest, and never shares a volume with another prop on any night.
    ///
    /// The office walls carry a lot of these, so they are laid out as fixed slots - west,
    /// east and south wall, three desk positions - and each slot belongs to one prop.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        void BuildEvenSubquestProps()
        {
            var office = HalfOf(ZoneIds.Office);
            var lobby = HalfOf(ZoneIds.Lobby);
            var parking = HalfOf(ZoneIds.Parking);
            var machinery = HalfOf(ZoneIds.Machinery);
            var archive = HalfOf(ZoneIds.Archive);
            var passage = HalfOf(ZoneIds.ServicePassage);
            var unit = HalfOf(ZoneIds.Unit404);
            float corridor = BuildingSpec.CorridorWidth * 0.5f;      // 1.2
            float north = corridor - 0.1f, south = -corridor + 0.1f;

            // Office slots. West faces -90, east 90, south 180; the desk is the front edge.
            // The key cabinet holds the west wall from z 0 to 1.6, the radio z -0.8, and the
            // 304 water chart (N1-M01) the east wall at z 0.6. The desk is full - the memo,
            // the contract and the cassette player are on it - so these are all wall slots.
            // The door to the lobby is the middle of the south wall.
            float westX = -office.x + 0.12f, eastX = office.x - 0.12f, southZ = -office.y + 0.12f;
            float deskZ = office.y - 0.85f;
            // Lobby: the mailbox bank's face, the parcel shelf's top, the south wall.
            float mailboxX = -lobby.x + 0.45f, lobbySouth = -lobby.y + 0.1f;
            // Service passage: 1m wide, so a prop sits on one wall or the other - and since a
            // look from the far wall crosses the whole passage, no two props (or the night-3/5
            // main fixtures between -5 and 4.7, or Taeho at -3.2) share an x. Free here:
            // -4.0, -2.6, -1.0, -0.4, 0.2, 0.8, 1.4, 2.4.
            float passageNorth = passage.y - 0.08f, passageSouth = -passage.y + 0.08f;

            // ---------------- night 2 ----------------
            Ev("N2-R01", ZoneIds.Lobby, "ReturnInvoice", new Vector3(lobby.x - 0.35f, 1.63f, 0.9f), Kit.Paper, "EV_N2R01_INVOICE");
            Ev("N2-R01", ZoneIds.Lobby, "ReturnParcel", new Vector3(4.4f, 0.17f, 2.6f), Kit.Package, "EV_N2R01_ITEM");
            Ev("N2-R02", ZoneIds.Office, "TempCardRequest", new Vector3(westX, 0.85f, -1.9f), Kit.Plate, "EV_N2R02_REQUEST", yaw: -90f);
            Ev("N2-R02", ZoneIds.Lobby, "ResidentPhotoId", new Vector3(1.3f, 0.93f, 1.8f), Kit.Paper, "EV_N2R02_PHOTO_ID");
            Ev("N2-R03", ZoneIds.Machinery, "ArchiveFeederGauge", new Vector3(3.0f, 1.5f, machinery.y - 0.55f), Kit.Gauge, "EV_N2R03_LOAD");
            Act("N2-R03", ZoneIds.Machinery, "ArchiveFeederIsolator", new Vector3(4.1f, 1.4f, machinery.y - 0.55f), Kit.Breaker, "n2r03_isolate");
            Act("N2-R03", ZoneIds.Machinery, "ArchiveFeederBusbar", new Vector3(-3.2f, 1.2f, machinery.y - 0.55f), Kit.Panel, "n2r03_rebalance");
            Ev("N2-R04", ZoneIds.Parking, "MisreadPlate", new Vector3(-8f, 0.55f, -7.66f), Kit.Plate, "EV_N2R04_PLATE");
            Ev("N2-R04", ZoneIds.Office, "VehicleRegistry", new Vector3(eastX, 1.4f, -0.25f), Kit.Panel, "EV_N2R04_REGISTRY", yaw: 90f);
            Ev("N2-R05", ZoneIds.Lobby, "OldReturnCarton", new Vector3(3.3f, 0.17f, 2.6f), Kit.Package, "EV_N2R05_CARTON");
            Ev("N2-R05", ZoneIds.Lobby, "ReturnSlipBook", new Vector3(-1.3f, 0.93f, 1.8f), Kit.Paper, "EV_N2R05_SLIP");
            Ev("N2-R06", ZoneIds.Floor03, "WetUmbrella", new Vector3(-7.2f, 0.32f, south + 0.12f), Kit.Extinguisher, "EV_N2R06_UMBRELLA", yaw: 180f);
            Ev("N2-R07", ZoneIds.Office, "CloneTokenLog", new Vector3(eastX, 1.4f, 1.4f), Kit.Panel, "EV_N2R07_TOKEN", yaw: 90f);
            Ev("N2-R07", ZoneIds.Lobby, "DoorstepIntercom", new Vector3(-1.0f, 1.4f, lobby.y - 0.1f), Kit.Plate, "EV_N2R07_DOORSTEP");
            ActAt("N2-R08", ZoneIds.Lobby, "FeedTestMark", new Vector3(1.5f, 0.04f, -1.5f), Kit.Tape, "n2r08_deviate", 0f, 90f);
            Ev("N2-R09", ZoneIds.Office, "ParkingDbPrintout", new Vector3(0f, 0.83f, deskZ), Kit.Paper, "EV_N2R09_DB");
            Ev("N2-R09", ZoneIds.Parking, "EmptyBayFloor", new Vector3(-3f, 0.03f, 6.5f), Kit.Tape, "EV_N2R09_FLOOR", pitch: 90f);
            Ev("N2-R09", ZoneIds.Parking, "EmptyBayTyreMarks", new Vector3(-4.2f, 0.03f, 6.5f), Kit.Tape, "EV_N2R09_TYRES", pitch: 90f);
            Ev("N2-R10", ZoneIds.Office, "AccessLogScreen", new Vector3(westX, 1.5f, 2.75f), Kit.Panel, "EV_N2R10_LOG", yaw: -90f);
            Ev("N2-R10", ZoneIds.Floor04, "Door406Seal", new Vector3(BuildingSpec.Floor04DoorX[4] + 0.6f, 1.2f, north), Kit.Seal, "EV_N2R10_SEAL");
            Ev("N2-R11", ZoneIds.Elevator, "CarFloorIndicator", new Vector3(-LiftWall, 1.9f, 0.3f), Kit.Plate, "EV_N2R11_INDICATOR", yaw: -90f);
            Ev("N2-R11", ZoneIds.Lobby, "LiftFootprints", new Vector3(2.7f, 0.03f, 3.0f), Kit.Tape, "EV_N2R11_PRINTS", pitch: 90f);
            Ev("N2-R12", ZoneIds.Floor04, "SmokeSensor4F", new Vector3(-2.5f, HeightOf(ZoneIds.Floor04) - 0.05f, 0f), Kit.Sensor, "EV_N2R12_SENSOR");
            Ev("N2-R13", ZoneIds.Lobby, "MailboxMemo", new Vector3(mailboxX, 1.25f, 2.2f), Kit.Plate, "EV_N2R13_MEMO", yaw: -90f);
            Ev("N2-R13", ZoneIds.Office, "WorkLogbook", new Vector3(westX, 0.85f, -2.4f), Kit.Plate, "EV_N2R13_LOGBOOK", yaw: -90f);
            Ev("N2-R13", ZoneIds.Parking, "B1StairDoorCheck", new Vector3(-2.0f, 1.3f, -parking.y + 0.12f), Kit.Plate, "EV_N2R13_B1_DOOR", yaw: 180f);
            Ev("N2-R14", ZoneIds.Lobby, "Parcel404", new Vector3(2.0f, 0.17f, 2.6f), Kit.Package, "EV_N2R14_INVOICE");
            Ev("N2-R14", ZoneIds.Office, "OldShopDirectory", new Vector3(1.0f, 1.5f, southZ), Kit.Plate, "EV_N2R14_SENDER", yaw: 180f);

            // ---------------- night 4 ----------------
            Ev("N4-R01", ZoneIds.Floor03, "Door303Intercom", new Vector3(BuildingSpec.StandardDoorX[2] + 0.65f, 1.35f, north - 0.04f), Kit.Plate, "EV_N4R01_STATE");
            Ev("N4-R02", ZoneIds.Archive, "OriginalResidentFiles", new Vector3(-0.6f, 1.3f, archive.y - 0.72f), Kit.Package, "EV_N4R02_ORIGINALS");
            Ev("N4-R02", ZoneIds.Office, "BackupListScreen", new Vector3(westX, 1.5f, -1.4f), Kit.Panel, "EV_N4R02_BACKUP", yaw: -90f);
            Ev("N4-R04", ZoneIds.Office, "PaperManualBinder", new Vector3(westX, 1.5f, -1.9f), Kit.Plate, "EV_N4R04_PAPER", yaw: -90f);
            Ev("N4-R04", ZoneIds.Office, "PcManualScreen", new Vector3(-2.4f, 1.4f, southZ), Kit.Panel, "EV_N4R04_PC", yaw: 180f);
            Ev("N4-R05", ZoneIds.Floor03, "FamilyPhotoPrint", new Vector3(5.0f, 1.3f, south), Kit.Plate, "EV_N4R05_PRINT", yaw: 180f);
            Ev("N4-R05", ZoneIds.Office, "PhotoScanner", new Vector3(westX, 1.5f, 1.85f), Kit.Device, "EV_N4R05_SCAN", yaw: -90f);
            Ev("N4-R06", ZoneIds.Office, "LoginWarning", new Vector3(westX, 1.5f, -2.4f), Kit.Panel, "EV_N4R06_WARNING", yaw: -90f);
            Ev("N4-R06", ZoneIds.Machinery, "B1Terminal", new Vector3(-machinery.x + 0.15f, 1.3f, 1.0f), Kit.Panel, "EV_N4R06_TERMINAL", yaw: -90f);
            Ev("N4-R07", ZoneIds.Archive, "KnockingRecordBox", new Vector3(1.8f, 1.3f, archive.y - 0.72f), Kit.Package, "EV_N4R07_VIBRATION");
            Ev("N4-R08", ZoneIds.Machinery, "OldCassetteRecorder", new Vector3(-machinery.x + 0.15f, 1.3f, -1.0f), Kit.Radio, "EV_N4R08_TAPE", yaw: -90f);
            Ev("N4-R09", ZoneIds.Office, "AccessLog2009", new Vector3(eastX, 1.4f, 2.4f), Kit.Panel, "EV_N4R09_LOG2009", yaw: 90f);
            Act("N4-R10", ZoneIds.Floor03, "Door303Sunja", new Vector3(BuildingSpec.StandardDoorX[2], 1.0f, north - 0.04f), Kit.Plate, "n4r10_door");
            Ev("N4-R11", ZoneIds.Floor04, "PencilDrawing", new Vector3(-2.2f, 0.55f, north), Kit.Plate, "EV_N4R11_DRAWING");
            Act("N4-R11", ZoneIds.Floor04, "PencilDrawingScrape", new Vector3(-1.5f, 0.55f, north), Kit.Tape, "n4r11_scrape");
            Ev("N4-R12", ZoneIds.Lobby, "MailboxMeasure", new Vector3(mailboxX, 0.7f, 0.3f), Kit.Tape, "EV_N4R12_MEASURE", yaw: -90f);
            Act("N4-R12", ZoneIds.Lobby, "Mailbox404Slot", new Vector3(mailboxX, 0.75f, 1.65f), Kit.Plate, "n4r12_open", yaw: -90f);
            Ev("N4-R13", ZoneIds.Elevator, "CarMirror", new Vector3(-LiftWall, 1.2f, -0.3f), Kit.Plate, "EV_N4R13_MIRROR", yaw: -90f);
            Act("N4-R13", ZoneIds.Elevator, "CarMirrorLookAgain", new Vector3(-0.3f, 1.2f, LiftWall), Kit.Plate, "n4r13_watch");
            Ev("N4-R14", ZoneIds.Archive, "StampedEnvelope", new Vector3(archive.x - 0.15f, 1.3f, 0f), Kit.Plate, "EV_N4R14_STAMP", yaw: 90f);

            // ---------------- night 6 ----------------
            Ev("N6-R01", ZoneIds.Lobby, "ResidentRequestSlip", new Vector3(2.0f, 1.3f, lobbySouth), Kit.Plate, "EV_N6R01_SLIP", yaw: 180f);
            Ev("N6-R02", ZoneIds.Office, "ArchiveIndexScreen", new Vector3(2.1f, 1.0f, southZ), Kit.Panel, "EV_N6R02_INDEX", yaw: 180f);
            Ev("N6-R03", ZoneIds.ServicePassage, "PassageReferenceMarker", new Vector3(-4.0f, 1.0f, passageNorth), Kit.Tape, "EV_N6R03_REFERENCE",
               requiredFlag: R.F4ReferenceMarker);
            Ev("N6-R03", ZoneIds.ServicePassage, "PassageDoorSeal", new Vector3(-1.0f, 1.2f, passageNorth), Kit.Seal, "EV_N6R03_REFERENCE");
            Ev("N6-R04", ZoneIds.Office, "OfficeSpeaker", new Vector3(1.0f, 2.25f, southZ), Kit.Device, "EV_N6R04_BROADCAST", yaw: 180f);
            Ev("N6-R05", ZoneIds.Unit404, "UnplannedDoorPlate", new Vector3(-unit.x + 0.1f, 1.3f, 0.3f), Kit.Plate, "EV_N6R05_DOOR", yaw: -90f);
            Act("N6-R05", ZoneIds.Unit404, "UnplannedDoorHandle", new Vector3(-unit.x + 0.1f, 0.95f, 0.3f), Kit.Breaker, "n6r05_open", yaw: -90f);
            Ev("N6-R05", ZoneIds.Unit404, "RedSlipperAtDoor", new Vector3(-unit.x + 0.45f, 0.08f, 0.75f), Kit.Seal, "EV_N6R05_SLIPPER", pitch: 90f);
            Ev("N6-R06", ZoneIds.ServicePassage, "FallenNameplate", new Vector3(0.8f, 0.4f, passageSouth), Kit.Seal, "EV_N6R06_NAMEPLATE", yaw: 180f);
            Act("N6-R06", ZoneIds.ServicePassage, "PassageShoring", new Vector3(1.4f, 1.2f, passageNorth), Kit.Device, "n6r06_stabilise");
            Act("N6-R06", ZoneIds.ServicePassage, "PassageDebris", new Vector3(2.4f, 0.6f, passageSouth), Kit.Package, "n6r06_clear", yaw: 180f);
            Ev("N6-R07", ZoneIds.Unit404, "CopyLedger", new Vector3(-1.6f, 1.3f, -unit.y + 0.1f), Kit.Plate, "EV_N6R07_COPY", yaw: 180f);
            Ev("N6-R07", ZoneIds.Unit404, "LedgerPaperTest", new Vector3(1.0f, 1.3f, -unit.y + 0.1f), Kit.Plate, "EV_N6R07_PAPER_TEST", yaw: 180f);
            Ev("N6-R08", ZoneIds.Floor04, "SmokeThermometer", new Vector3(-6.0f, 1.5f, south), Kit.Gauge, "EV_N6R08_TEMP", yaw: 180f);
            Act("N6-R08", ZoneIds.Floor04, "FireDoorLatch", new Vector3(9.3f, 1.65f, north), Kit.Breaker, "n6r08_firedoor");
            Act("N6-R08", ZoneIds.Floor04, "CorridorVent", new Vector3(-8.6f, 2.0f, north), Kit.Panel, "n6r08_vent");
            Ev("N6-R09", ZoneIds.Lobby, "LockedDoorStatus", new Vector3(0.8f, 1.3f, lobbySouth), Kit.Plate, "EV_N6R09_LOCK", yaw: 180f);
            Act("N6-R09", ZoneIds.Machinery, "RemoteLockCircuit", new Vector3(-machinery.x + 0.15f, 1.3f, 2.4f), Kit.Breaker, "n6r09_cut", yaw: -90f);
            Act("N6-R09", ZoneIds.Lobby, "ForceLockedDoor", new Vector3(-0.4f, 1.0f, lobbySouth), Kit.Breaker, "n6r09_force", yaw: 180f);
            Ev("N6-R10", ZoneIds.Office, "ResidentChat", new Vector3(westX, 0.85f, -1.4f), Kit.Plate, "EV_N6R10_CHAT", yaw: -90f);
            Ev("N6-R11", ZoneIds.ServicePassage, "Passage404Seal", new Vector3(-0.4f, 1.2f, passageSouth), Kit.Seal, "EV_N6R11_SEAL", yaw: 180f);
            Ev("N6-R12", ZoneIds.Lobby, "Cam01Housing", new Vector3(0.6f, 2.3f, lobby.y - 0.25f), Kit.Camera, "EV_N6R12_DIRECTION");
            Ev("N6-R13", ZoneIds.ServicePassage, "RadioCrackle", new Vector3(0.2f, 1.0f, passageNorth), Kit.Radio, "EV_N6R13_RADIO");
            Ev("N6-R13", ZoneIds.ServicePassage, "ChildVoice", new Vector3(-2.6f, 1.0f, passageNorth), Kit.Device, "EV_N6R13_CHILD");
            Ev("N6-R14", ZoneIds.Stairwell, "GroundFloorAirVent", Landing(FloorPlan.F1, -1.6f, 1.3f), Kit.Plate, "EV_N6R14_AIR", yaw: LandingYaw(FloorPlan.F1));
        }

        /// <summary>
        /// The lift car's walls are 0.75 out with their inner face at about 0.65; a thin plate
        /// has to stand proud of that or a look at it lands on the wall.
        /// </summary>
        const float LiftWall = 0.6f;

        /// <summary><see cref="Act"/> for a fitting that lies flat (a mark on the floor).</summary>
        void ActAt(string caseId, string zone, string name, Vector3 position, Kit kit, string action, float yaw, float pitch)
        {
            Prop(caseId, zone, name, position, kit, null, action, "quest.act." + action, yaw, pitch, null);
        }
    }
}
