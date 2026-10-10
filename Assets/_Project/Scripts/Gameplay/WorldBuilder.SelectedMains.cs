using UnityEngine;
using NO404.Core;
using NO404.Interaction;

namespace NO404.Gameplay
{
    public sealed partial class WorldBuilder
    {
        /// <summary>
        /// A main-quest prop, built from the same kits as the subquest props so the two read
        /// as one building. <paramref name="yaw"/> follows the kit convention (0 on a north wall).
        /// </summary>
        void MainProp(string zone, string name, Vector3 position, string quest, string evidence, string action = null,
                      string label = "quest.inspect", Kit kit = Kit.Plate, float yaw = 0f)
        {
            var root = OwnedRoot(zone);
            if (root == null) return;

            var size = SizeOf(kit);
            var go = PropArt.TryBuild(root, name, position, size);
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.AddComponent<BoxCollider>().size = size;
                BuildKit(go.transform, kit);
            }
            go.AddComponent<MainQuestInteractable>().Setup(quest, evidence, action, label);
        }

        void BuildSelectedMainProps()
        {
            var office = HalfOf(ZoneIds.Office);
            // On the filing cabinet and on the east wall, instead of in mid-air in the office.
            MainProp(ZoneIds.Office, "PersonalHealthRecord", new Vector3(office.x - 0.4f, 1.33f, office.y - 0.5f), null, null, "health_record", "quest.read_health", Kit.Paper);
            MainProp(ZoneIds.Office, "Water304Last20Minutes", new Vector3(office.x - 0.12f, 1.4f, 0.6f), "N1-M01", "EV_304_WATER", label: "quest.read_water", kit: Kit.Panel, yaw: 90f);
            MainProp(ZoneIds.Lobby, "Mailbox404Bill", new Vector3(-HalfOf(ZoneIds.Lobby).x + 0.45f, 1.2f, 1.4f), "N1-M01", "EV_404_BILL", label: "quest.read_bill", yaw: -90f);
            MainProp(ZoneIds.Floor03, "ForceDoor304", new Vector3(BuildingSpec.StandardDoorX[3] - 0.45f, 1.2f, HalfOf(ZoneIds.Floor03).y - 0.18f), "N1-M01", null, "force_304", "quest.force_304", Kit.Breaker);
            MainProp(ZoneIds.Floor04, "LandingSeal4F", new Vector3(-4.7f, 1.3f, HalfOf(ZoneIds.Floor04).y - 0.18f), "N3-M01", "EV_SEAL_NUMBER", kit: Kit.Seal);
            MainProp(ZoneIds.Floor04, "AnalogHeightPressure", new Vector3(-4.1f, 1.3f, HalfOf(ZoneIds.Floor04).y - 0.18f), "N3-M01", "EV_N3_ANALOG", kit: Kit.Gauge);
            // Inside the car, on its west wall. It was placed beyond the wall, where no ray
            // from inside the car could reach it.
            MainProp(ZoneIds.Elevator, "LiftServiceIndexLog", new Vector3(-0.68f, 1.2f, 0.3f), "N3-M01", "EV_N3_LIFT_LOG", kit: Kit.Panel, yaw: -90f);
            float wall = HalfOf(ZoneIds.ServicePassage).y - 0.12f;
            MainProp(ZoneIds.ServicePassage, "ComparePersonalHeight", new Vector3(-1.5f, 1.2f, wall), "N3-M01", null, "compare_height", "quest.compare_height");
            MainProp(ZoneIds.ServicePassage, "UnstableDeepHatch", new Vector3(2.8f, 1.2f, wall), "N3-M01", null, "open_hatch", "quest.open_hatch", Kit.Hatch);
            MainProp(ZoneIds.ServicePassage, "MisleadingServiceDoor", new Vector3(4f, 1.2f, wall), "N3-M01", null, "wrong_door", "quest.follow_sign");
            MainProp(ZoneIds.ServicePassage, "FixedSealReturnMarker", new Vector3(-5f, 1.2f, wall), "N3-M01", null, "return_marker", "quest.return_marker", Kit.Seal);
            MainProp(ZoneIds.Floor04, "CurrentFireDoorSeal", new Vector3(-3.5f, 1.3f, HalfOf(ZoneIds.Floor04).y - 0.18f), "N5-M01", "EV_N5_FIRE_DOOR", kit: Kit.Seal);
            MainProp(ZoneIds.Floor04, "CurrentTemperatureVoltage", new Vector3(-2.9f, 1.3f, HalfOf(ZoneIds.Floor04).y - 0.18f), "N5-M01", "EV_N5_ANALOG", kit: Kit.Gauge);
            // Clear of the hatch beside it; the two used to share a volume.
            MainProp(ZoneIds.ServicePassage, "OldStaffId", new Vector3(3.45f, 1.45f, wall), "N5-M01", "EV_DONGSIK_ID");
            MainProp(ZoneIds.ServicePassage, "LastEntryTrace", new Vector3(4.7f, 1.2f, wall), "N5-M01", "EV_N5_LAST_POSITION");
            MainProp(ZoneIds.Parking, "FireCallTape2009", new Vector3(-1.9f, 1.3f, HalfOf(ZoneIds.Parking).y - 0.2f), "N5-M01", "EV_FIRE_TAPE_2009", kit: Kit.Radio);
            MainProp(ZoneIds.Parking, "DutyAccessLog2009", new Vector3(-1.3f, 1.3f, HalfOf(ZoneIds.Parking).y - 0.2f), "N5-M01", "EV_N5_WORK_LOG");
            MainProp(ZoneIds.Parking, "ChoiSignedApproval", new Vector3(-0.7f, 1.3f, HalfOf(ZoneIds.Parking).y - 0.2f), "N5-M01", "EV_CHOI_APPROVAL");
            MainProp(ZoneIds.Parking, "Unit404DeletionHistory", new Vector3(-0.1f, 1.3f, HalfOf(ZoneIds.Parking).y - 0.2f), "N5-M01", "EV_404_DELETION");
        }
    }
}
