using UnityEngine;
using NO404.Anomalies;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Where the five anomalous tools physically stand (v2.1 spec 23, 29).
    ///
    /// Each is an ordinary fixture in an ordinary room, built once with its zone and never
    /// switched off. That is the point of them and the reason none of these use ManualProp:
    /// an M prop hides until its event starts because the anomaly is what puts it there, while
    /// a convenience store till has always been on the counter. What changes on the night the
    /// spec gives it is not whether the caretaker can see the machine - it is what happens
    /// when they use it.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        static readonly Color MachineColour = new Color(0.34f, 0.33f, 0.36f);
        static readonly Color TokenColour = new Color(0.46f, 0.44f, 0.22f);

        void StageAnomalyTools()
        {
            StageConvenienceStore();
            StageWishLocker();
            StageLounge();
            StageToolroom();
            StageRoofMachine();
        }

        /// <summary>A01: the till at the counter, and the roll of paper behind it.</summary>
        void StageConvenienceStore()
        {
            var root = OwnedRoot(ZoneIds.ConvenienceStore);
            if (root == null) return;

            var half = HalfOf(ZoneIds.ConvenienceStore);

            Prop(root, "StoreCounter", new Vector3(0f, 0.5f, -half.y + 1.2f),
                 new Vector3(3.0f, 1.0f, 0.7f), PropColour);
            Prop(root, "StoreShelves", new Vector3(half.x - 0.5f, 1.0f, 0f),
                 new Vector3(0.6f, 2.0f, half.y * 1.4f), PropColour);

            var pos = Prop(root, "A01_Pos", new Vector3(0.6f, 1.15f, -half.y + 1.2f),
                           new Vector3(0.35f, 0.3f, 0.3f), MachineColour);
            pos.AddComponent<AnomalyToolTerminal>()
               .Setup(ManualEventIds.A01_ReceiptPrinter, "ui.prompt.a01.use");
        }

        /// <summary>
        /// A02: the bottom locker with its number worn off, and the door that has to be shut.
        ///
        /// The release is a separate object beside the locker rather than the locker itself,
        /// because a caretaker who walked away with the door open has to have something to
        /// come back to - and because spec 23 A02 wants shutting it to be one deliberate act
        /// rather than the same key they just pressed to open the menu.
        /// </summary>
        void StageWishLocker()
        {
            var root = OwnedRoot(ZoneIds.Lobby);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Lobby);

            var locker = Prop(root, "A02_UnnumberedLocker",
                              new Vector3(half.x - 0.6f, 0.3f, -half.y + 1.4f),
                              new Vector3(0.5f, 0.6f, 0.6f), MachineColour);
            locker.AddComponent<AnomalyToolTerminal>()
                  .Setup(ManualEventIds.A02_WishParcelLocker, "ui.prompt.a02.use");

            var door = Prop(root, "A02_LockerDoor",
                            new Vector3(half.x - 0.6f, 0.3f, -half.y + 1.05f),
                            new Vector3(0.5f, 0.6f, 0.06f), MachineColour);
            door.AddComponent<AnomalyToolHoldRelease>().Setup(ManualEventIds.A02_WishParcelLocker);
        }

        /// <summary>A03: the set in the residents lounge, and the chairs nobody sits in.</summary>
        void StageLounge()
        {
            var root = OwnedRoot(ZoneIds.Lounge);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Lounge);

            Prop(root, "LoungeSofa", new Vector3(0f, 0.35f, -1.2f),
                 new Vector3(2.4f, 0.7f, 0.8f), PropColour);
            Prop(root, "LoungeTable", new Vector3(0f, 0.25f, 0.2f),
                 new Vector3(1.2f, 0.5f, 0.6f), PropColour);

            var set = Prop(root, "A03_Vcr", new Vector3(0f, 0.9f, half.y - 0.7f),
                           new Vector3(0.8f, 0.7f, 0.6f), MachineColour);
            set.AddComponent<AnomalyToolTerminal>()
               .Setup(ManualEventIds.A03_LoungeVcr, "ui.prompt.a03.use");
        }

        /// <summary>
        /// A04: the red box against the far wall, its return slot, and one of the two tokens.
        ///
        /// The token is here rather than anywhere prettier because B2 is the floor the
        /// caretaker has least reason to linger on, and a token they have to have already been
        /// down for is a token they earned by working the building.
        /// </summary>
        void StageToolroom()
        {
            var root = OwnedRoot(ZoneIds.Toolroom);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Toolroom);

            Prop(root, "ToolBench", new Vector3(0f, 0.45f, half.y - 0.8f),
                 new Vector3(2.4f, 0.9f, 0.6f), PropColour);

            var box = Prop(root, "A04_Toolbox", new Vector3(-half.x + 0.8f, 0.5f, 0f),
                           new Vector3(0.8f, 1.0f, 0.5f), HazardColour);
            box.AddComponent<AnomalyToolTerminal>()
               .Setup(ManualEventIds.A04_EndlessToolbox, "ui.prompt.a04.use");

            var slot = Prop(root, "A04_ReturnSlot", new Vector3(-half.x + 0.8f, 1.15f, 0f),
                            new Vector3(0.5f, 0.12f, 0.4f), MachineColour);
            slot.AddComponent<AnomalyToolHoldRelease>().Setup(ManualEventIds.A04_EndlessToolbox);

            var token = Prop(root, "A05_Token_B2", new Vector3(0.6f, 0.95f, half.y - 0.8f),
                             new Vector3(0.1f, 0.06f, 0.1f), TokenColour);
            token.AddComponent<AnomalyTokenPickup>().Setup("ui.prompt.a05.take_token");
        }

        /// <summary>
        /// A05: the machine on the last landing, the parapet it pulls toward, and the second
        /// token.
        ///
        /// The drift anchor sits at the parapet rather than on the machine because that is
        /// where the pull is - spec 23 A05 has the caretaker leaning toward the edge, not
        /// toward the thing that sold them the photograph.
        /// </summary>
        void StageRoofMachine()
        {
            // v5.1 3.1: there is no roof. The machine stands at the east end of the top floor,
            // against the windows, which is the nearest thing the building still has to an edge.
            var root = OwnedRoot(ZoneIds.Floor06);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Floor06);

            var machine = Prop(root, "A05_Vending", new Vector3(half.x - 1.4f, 0.9f, -half.y + 0.4f),
                               new Vector3(0.7f, 1.8f, 0.6f), MachineColour);
            machine.AddComponent<AnomalyToolTerminal>()
                   .Setup(ManualEventIds.A05_LostAndFoundMachine, "ui.prompt.a05.use");

            var token = Prop(root, "A05_Token_Roof", new Vector3(half.x - 2.2f, 0.06f, -half.y + 0.4f),
                             new Vector3(0.1f, 0.06f, 0.1f), TokenColour);
            token.AddComponent<AnomalyTokenPickup>().Setup("ui.prompt.a05.take_token");

            // The window M18 spends its whole procedure keeping the caretaker away from.
            Marker(root, "A05_Parapet", new Vector3(6f, 1f, -half.y + 0.3f))
                .AddComponent<AnomalyDriftAnchor>()
                .Setup(ManualEventIds.A05_LostAndFoundMachine);
        }
    }
}
