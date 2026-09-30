using UnityEngine;
using NO404.Anomalies;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Where the M01..M18 props physically sit (v2.1 spec 22, 29).
    ///
    /// Every event is assembled from the small set of components in ManualProps rather than
    /// from a script of its own. That is not only economy: the components are where the rules
    /// spec 0.4 and 0.10.5 impose actually live - a prohibition is an ordinary unmarked
    /// interactable, a wrong answer is recoverable, a watcher only watches inside its window.
    /// Eighteen bespoke scripts would each be a fresh chance to break one of those quietly.
    ///
    /// Props are built with their zone and are inert until their event is Active, so a floor
    /// keeps the same shape on every night (spec 0.7.2) and a streamed scene never has to be
    /// rebuilt when an anomaly starts.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        static readonly Color PropColour = new Color(0.40f, 0.38f, 0.34f);
        static readonly Color HazardColour = new Color(0.52f, 0.24f, 0.22f);
        static readonly Color FigureColour = new Color(0.16f, 0.16f, 0.18f);

        void BuildManualStages()
        {
            StageLobby();
            StageLaundry();
            StagePlayground();
            StageFitnessRoom();
            StageTerrace();
            StageParking();
            StageRecyclingYard();
            StagePumpRoom();
            StagePipeRoom();
            StageOffice();
            StageFloor04();
            StageFloor05();
            StageFloor06();
            StageRooftop();

            // v2.1 spec 23. Built with their zones and inert until their night, exactly like
            // the M props above: a POS that only answers on night 4 is still a POS on night 3,
            // and it has to be standing there for that to read as a change.
            StageAnomalyTools();
        }

        // ---- helpers ---------------------------------------------------------

        /// <summary>A greybox stand-in for one anomaly prop.</summary>
        GameObject Prop(Transform root, string name, Vector3 position, Vector3 size, Color colour)
        {
            return Box(root, name, position, size, colour);
        }

        /// <summary>A prop with no body: a watcher that only needs a position in the room.</summary>
        GameObject Marker(Transform root, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            return go;
        }

        static readonly Vector3 PanelSize = new Vector3(0.5f, 0.35f, 0.1f);
        static readonly Vector3 SmallSize = new Vector3(0.3f, 0.3f, 0.2f);
        static readonly Vector3 BagSize = new Vector3(0.55f, 0.4f, 1.1f);

        // =====================================================================
        // 1F
        // =====================================================================

        /// <summary>M06 (parcel for a name not on the roll) and M13 (the postcards).</summary>
        void StageLobby()
        {
            var root = OwnedRoot(ZoneIds.Lobby);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Lobby);
            const string m06 = ManualEventIds.M06_LostParcel;

            // The locker bank is ordinary furniture; only P-04 belongs to the event.
            Prop(root, "ParcelLockers", new Vector3(-half.x + 0.5f, 1.0f, 2.5f),
                 new Vector3(0.6f, 2.0f, 3.0f), PropColour);

            // Checking the recipient happens at the desk, in the apps that already exist.
            Marker(root, "M06_RecordWatcher", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualAppWatcher>()
                .Setup(m06, "recordChecks",
                       new[] { AppIds.Residents, AppIds.Access, AppIds.Facility },
                       "obj_verify", 2);

            var parcel = Prop(root, "M06_Parcel", new Vector3(-half.x + 0.95f, 1.15f, 2.5f),
                              new Vector3(0.35f, 0.3f, 0.4f), PropColour);
            parcel.AddComponent<ManualActionProp>()
                  .Setup(m06, "ui.prompt.m06.stamp", "stamped", "obj_stamp")
                  .RequiresCounter("recordChecks", "ui.prompt.m06.unverified", 2)
                  .AlwaysVisible();

            // Both prohibitions are one press, on the box the player is already holding.
            var lid = Prop(root, "M06_Lid", new Vector3(-half.x + 0.95f, 1.38f, 2.5f),
                           new Vector3(0.35f, 0.06f, 0.4f), PropColour);
            lid.AddComponent<ManualForbiddenProp>().Setup(m06, "ui.prompt.m06.open", "opened");

            var shake = Prop(root, "M06_Shake", new Vector3(-half.x + 0.95f, 0.92f, 2.5f),
                             new Vector3(0.35f, 0.15f, 0.4f), PropColour);
            shake.AddComponent<ManualForbiddenProp>().Setup(m06, "ui.prompt.m06.shake", "shaken");

            var shelf = Prop(root, "M06_ReturnShelf", new Vector3(half.x - 0.4f, 1.1f, -half.y + 1.2f),
                             new Vector3(0.5f, 0.1f, 1.4f), PropColour);
            shelf.AddComponent<ManualActionProp>()
                 .Setup(m06, "ui.prompt.m06.shelve", "shelved", "obj_shelve")
                 .RequiresCounter("stamped", "ui.prompt.m06.unstamped")
                 .AlwaysVisible();

            // Looking back at the shelf, and only between leaving it and reaching the office.
            Marker(root, "M06_LookBack", new Vector3(half.x - 0.4f, 1.4f, -half.y + 1.2f))
                .AddComponent<ManualGazeWatcher>()
                .Setup(m06, "lookedBack", 0.8f, 20f, 14f, "notify.m06.looked_back")
                .ArmedBetween("shelved", "returned");

            Marker(root, "M06_Return", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualZoneWatcher>()
                .Setup(m06, "returned", ZoneIds.Office, "obj_return_to_office");

            // ---- M13, the postcards --------------------------------------
            const string m13 = ManualEventIds.M13_Postcards;

            Prop(root, "Mailboxes", new Vector3(-half.x + 0.5f, 1.2f, -2.5f),
                 new Vector3(0.4f, 1.6f, 2.4f), PropColour);

            var shred = Prop(root, "M13_Shredder", new Vector3(-half.x + 1.1f, 0.5f, -3.4f),
                             new Vector3(0.4f, 0.9f, 0.4f), PropColour);
            shred.AddComponent<ManualActionProp>()
                 .Setup(m13, "ui.prompt.m13.shred", "shredded", "obj_decide")
                 .AlwaysVisible();

            var read = Prop(root, "M13_Postcard", new Vector3(-half.x + 1.0f, 1.25f, -2.5f),
                            new Vector3(0.25f, 0.02f, 0.18f), PropColour);
            read.AddComponent<ManualActionProp>().Setup(m13, "ui.prompt.m13.read", "read");

            // The log has two columns, and the temptation is to file a claim as a fact.
            var logFact = Prop(root, "M13_LogFact", new Vector3(-half.x + 1.6f, 1.05f, -2.2f),
                               new Vector3(0.3f, 0.05f, 0.25f), PropColour);
            logFact.AddComponent<ManualActionProp>()
                   .Setup(m13, "ui.prompt.m13.log_separated", "logged", "obj_decide")
                   .RequiresCounter("read", "ui.prompt.m13.unread");

            var logGuess = Prop(root, "M13_LogGuess", new Vector3(-half.x + 1.6f, 1.05f, -2.8f),
                                new Vector3(0.3f, 0.05f, 0.25f), PropColour);
            logGuess.AddComponent<ManualForbiddenProp>()
                    .Setup(m13, "ui.prompt.m13.log_guess", "guessedAsFact", "obj_decide");
        }

        /// <summary>M12: the machine that has to be allowed to finish.</summary>
        void StageLaundry()
        {
            var root = OwnedRoot(ZoneIds.Laundry);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Laundry);
            const string m12 = ManualEventIds.M12_NightLaundry;

            for (int i = 0; i < 4; i++)
                Prop(root, "Washer0" + (i + 1), new Vector3(-half.x + 0.5f, 0.5f, -1.8f + i * 1.2f),
                     new Vector3(0.7f, 1.0f, 0.9f), PropColour);

            // 02:14 on the display. Five game minutes of standing there, which at the shift
            // clock is about twenty seconds of not doing something else.
            Marker(root, "M12_Cycle", new Vector3(-half.x + 0.5f, 1.2f, 1.8f))
                .AddComponent<ManualTimerProp>()
                .Setup(m12, "cycleEnded", 300, "obj_wait", null, "notify.m12.cycle_done")
                .Also("drumOpen");

            var stop = Prop(root, "M12_Stop", new Vector3(-half.x + 0.92f, 1.15f, 1.8f),
                            SmallSize, HazardColour);
            stop.AddComponent<ManualForbiddenProp>()
                .Setup(m12, "ui.prompt.m12.force_stop", "forcedStop", "obj_wait")
                .Also("drumOpen");

            var door = Prop(root, "M12_Door", new Vector3(-half.x + 0.92f, 0.6f, 1.8f),
                            new Vector3(0.12f, 0.5f, 0.5f), PropColour);
            door.AddComponent<ManualForbiddenProp>().Setup(m12, "ui.prompt.m12.force_door", "forcedDoor");

            var doll = Prop(root, "M12_Doll", new Vector3(-half.x + 1.2f, 0.6f, 1.8f),
                            new Vector3(0.25f, 0.3f, 0.2f), PropColour);
            doll.AddComponent<ManualActionProp>()
                .Setup(m12, "ui.prompt.m12.retrieve", "dollRetrieved", "obj_retrieve")
                .RequiresCounter("drumOpen", "ui.prompt.m12.still_running");

            // The incinerator is two floors down, so the last step is a walk, not a click.
            var bin = Prop(root, "M12_SealedBox", new Vector3(half.x - 0.5f, 0.4f, 0f),
                           new Vector3(0.5f, 0.7f, 0.5f), PropColour);
            bin.AddComponent<ManualActionProp>()
               .Setup(m12, "ui.prompt.m12.incinerate", "dollIncinerated", "obj_incinerate")
               .RequiresCounter("dollRetrieved", "ui.prompt.m12.nothing_to_burn")
               .AlwaysVisible();
        }

        /// <summary>M08: the shadows waiting for something that was never collected.</summary>
        void StagePlayground()
        {
            var root = OwnedRoot(ZoneIds.Playground);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Playground);
            const string m08 = ManualEventIds.M08_ShadowChildren;

            Prop(root, "Swings", new Vector3(-2f, 1.1f, 2f), new Vector3(3.2f, 2.2f, 0.3f), PropColour);
            Prop(root, "Sandpit", new Vector3(1.5f, 0.05f, -1.5f), new Vector3(4f, 0.1f, 4f), PropColour);

            var board = Prop(root, "M08_FacilityLog", new Vector3(-half.x + 0.4f, 1.4f, 0f),
                             new Vector3(0.1f, 0.7f, 1.0f), PropColour);
            board.AddComponent<ManualActionProp>()
                 .Setup(m08, "ui.prompt.m08.check_log", "identified", "obj_identify")
                 .AlwaysVisible();

            var ball = Prop(root, "M08_RedBall", new Vector3(half.x - 1.0f, 0.2f, -half.y + 1.0f),
                            new Vector3(0.22f, 0.22f, 0.22f), HazardColour);
            ball.AddComponent<ManualActionProp>()
                .Setup(m08, "ui.prompt.m08.take_ball", "ballObtained", "obj_obtain_ball")
                .RequiresCounter("identified", "ui.prompt.m08.unknown_item");

            var centre = Prop(root, "M08_PlaceHere", new Vector3(1.5f, 0.03f, -1.5f),
                              new Vector3(0.6f, 0.05f, 0.6f), PropColour);
            centre.AddComponent<ManualActionProp>()
                  .Setup(m08, "ui.prompt.m08.place_ball", "ballPlaced", "obj_place")
                  .RequiresCounter("ballObtained", "ui.prompt.m08.nothing_to_place");

            // Anything else placed there is the wrong answer, and it is right there to try.
            var wrongToy = Prop(root, "M08_OtherToy", new Vector3(2.6f, 0.15f, -2.4f),
                                new Vector3(0.2f, 0.2f, 0.2f), PropColour);
            wrongToy.AddComponent<ManualForbiddenProp>()
                    .Setup(m08, "ui.prompt.m08.place_other", "wrongToyPlaced", "obj_place");

            Marker(root, "M08_Shadows", new Vector3(0.5f, 0.1f, -0.5f))
                .AddComponent<ManualProximityWatcher>()
                .Setup(m08, "approachedShadows", 1.6f, "notify.m08.pushed_back");
        }

        // =====================================================================
        // 2F
        // =====================================================================

        /// <summary>M14: the machines whose obvious control is the wrong one.</summary>
        void StageFitnessRoom()
        {
            var root = OwnedRoot(ZoneIds.FitnessRoom);
            if (root == null) return;

            var half = HalfOf(ZoneIds.FitnessRoom);
            const string m14 = ManualEventIds.M14_Treadmills;

            for (int i = 0; i < 3; i++)
            {
                float z = -2.2f + i * 2.2f;
                Prop(root, "Treadmill0" + (i + 1), new Vector3(-1.5f, 0.5f, z),
                     new Vector3(0.8f, 1.0f, 2.0f), PropColour);

                // Standing 1.5m in front of it, which is what the manual asks for.
                var announce = Prop(root, "M14_Announce0" + (i + 1), new Vector3(0.2f, 1.1f, z),
                                    SmallSize, PropColour);
                announce.AddComponent<ManualActionProp>()
                        .Setup(m14, "ui.prompt.m14.announce", "announced", "obj_announce",
                               1, 3)
                        .WithRange(2.0f);

                var stop = Prop(root, "M14_Stop0" + (i + 1), new Vector3(-1.5f, 1.1f, z + 0.9f),
                                SmallSize, PropColour);
                stop.AddComponent<ManualActionProp>()
                    .Setup(m14, "ui.prompt.m14.stop", "machinesStopped", "obj_stop", 1, 3)
                    .RequiresCounter("tapCount", "ui.prompt.m14.too_fast", 6);
            }

            // One board, knocked twice per machine.
            var board = Prop(root, "M14_RulesBoard", new Vector3(half.x - 0.2f, 1.6f, 0f),
                             new Vector3(0.1f, 0.8f, 1.2f), PropColour);
            board.AddComponent<ManualSequenceProp>()
                 .Setup(m14, "ui.prompt.m14.tap_board", "tapCount", "obj_tap_sign", 0.5f, 6);

            // The most visible control in the room.
            var breaker = Prop(root, "M14_MainBreaker", new Vector3(-half.x + 0.2f, 1.5f, 0f),
                               new Vector3(0.12f, 0.5f, 0.4f), HazardColour);
            breaker.AddComponent<ManualForbiddenProp>()
                   .Setup(m14, "ui.prompt.m14.main_power", "mainPowerCut", "obj_stop")
                   .EndsEvent();
        }

        /// <summary>M15: prune the malformations, and only those.</summary>
        void StageTerrace()
        {
            var root = OwnedRoot(ZoneIds.Terrace);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Terrace);
            const string m15 = ManualEventIds.M15_PlanterGrowth;

            for (int i = 0; i < 6; i++)
            {
                float x = -4.5f + i * 1.8f;
                Prop(root, "Planter0" + (i + 1), new Vector3(x, 0.4f, 2f),
                     new Vector3(1.2f, 0.8f, 1.0f), PropColour);
            }

            // Eight malformations across the planters; six is the bar (spec 22 M15).
            for (int i = 0; i < 8; i++)
            {
                float x = -4.5f + (i % 6) * 1.8f;
                float y = 1.0f + (i / 6) * 0.25f;
                var cut = Prop(root, "M15_Mutation" + (i + 1), new Vector3(x, y, 2f),
                               new Vector3(0.14f, 0.3f, 0.14f), HazardColour);
                cut.AddComponent<ManualActionProp>()
                   .Setup(m15, "ui.prompt.m15.cut_mutation", "mutantCuts", "obj_cut", 1, 8)
                   .ObjectiveAt(6);
            }

            // Cutting healthy growth is its own failure, so "cut everything" is not a safe
            // fallback (spec 22 M15).
            for (int i = 0; i < 4; i++)
            {
                float x = -3.6f + i * 1.8f;
                var normal = Prop(root, "M15_Branch" + (i + 1), new Vector3(x, 0.95f, 2.4f),
                                  new Vector3(0.12f, 0.28f, 0.12f), PropColour);
                normal.AddComponent<ManualActionProp>()
                      .Setup(m15, "ui.prompt.m15.cut_branch", "normalCuts", null, 1, 4);
            }

            var root_pull = Prop(root, "M15_RootPull", new Vector3(-4.5f, 0.85f, 1.6f),
                                 new Vector3(0.3f, 0.2f, 0.3f), HazardColour);
            root_pull.AddComponent<ManualForbiddenProp>()
                     .Setup(m15, "ui.prompt.m15.uproot", "rootPulled", "obj_cut")
                     .EndsEvent();

            var bag = Prop(root, "M15_WasteBag", new Vector3(half.x - 0.8f, 0.3f, -2f),
                           new Vector3(0.4f, 0.6f, 0.4f), PropColour);
            bag.AddComponent<ManualActionProp>()
               .Setup(m15, "ui.prompt.m15.bag", "bagged", "obj_bag")
               .RequiresCounter("mutantCuts", "ui.prompt.m15.nothing_cut", 6)
               .AlwaysVisible();
        }

        // =====================================================================
        // B1
        // =====================================================================

        /// <summary>M02 (the backwards walker) and M11 (the bay that reads occupied).</summary>
        void StageParking()
        {
            var root = OwnedRoot(ZoneIds.Parking);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Parking);
            const string m02 = ManualEventIds.M02_BackwardsWalker;

            var figure = Prop(root, "M02_Figure", new Vector3(-3f, 0.9f, 1.5f),
                              new Vector3(0.4f, 1.8f, 0.3f), FigureColour);

            // Head height, so a glance across the car park is not a breach.
            Marker(root, "M02_Face", new Vector3(-3f, 1.65f, 1.35f))
                .AddComponent<ManualGazeWatcher>()
                .Setup(m02, "lookedAtFace", 0.8f, 16f, 12f, "notify.m02.saw_face");

            Marker(root, "M02_Distance", new Vector3(-3f, 0.9f, 1.5f))
                .AddComponent<ManualProximityWatcher>()
                .Setup(m02, "closedWithinFourMetres", 4f, "notify.m02.too_close");

            Marker(root, "M02_Zoom", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualCameraWatcher>()
                .Setup(m02, "usedZoom", "CAM-09", "notify.m02.zoomed");

            // Callable from well outside four metres, which is the whole point.
            var announce = Prop(root, "M02_BayLine", new Vector3(-3f, 0.03f, -2.5f),
                                new Vector3(2.4f, 0.05f, 5f), PropColour);
            announce.AddComponent<ManualActionProp>()
                    .Setup(m02, "ui.prompt.m02.announce", "announced", "obj_announce")
                    .WithRange(9f);

            Marker(root, "M02_Exit", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualZoneWatcher>()
                .Setup(m02, "exitReached", ZoneIds.Parking, "obj_reach_exit", true);

            // ---- M11, bay B1-17 ------------------------------------------
            const string m11 = ManualEventIds.M11_GhostParking;
            var bay = new Vector3(6f, 0f, -3f);

            Prop(root, "Bay_B1_17", bay + new Vector3(0f, 0.02f, 0f),
                 new Vector3(2.4f, 0.04f, 5f), PropColour);

            Marker(root, "M11_Visit", bay + new Vector3(0f, 1f, 0f))
                .AddComponent<ManualProximityWatcher>()
                .Setup(m11, "visited", 2.5f)
                .Completes("obj_visit");

            var barrier = Prop(root, "M11_Barrier", bay + new Vector3(0f, 0.5f, 2.6f),
                               new Vector3(2.2f, 0.1f, 0.1f), PropColour);
            barrier.AddComponent<ManualActionProp>()
                   .Setup(m11, "ui.prompt.m11.barrier", "barrierPlaced", "obj_barrier")
                   .RequiresCounter("visited", "ui.prompt.m11.not_there");

            // Head height over nothing at all: the windscreen the sensor insists is there.
            var sticker = Prop(root, "M11_Windscreen", bay + new Vector3(0f, 1.2f, 1.2f),
                               new Vector3(1.4f, 0.5f, 0.05f), PropColour);
            sticker.AddComponent<ManualActionProp>()
                   .Setup(m11, "ui.prompt.m11.sticker", "stickerPlaced", "obj_sticker")
                   .RequiresCounter("barrierPlaced", "ui.prompt.m11.no_barrier");

            Marker(root, "M11_ExitLog", bay)
                .AddComponent<ManualTimerProp>()
                .Setup(m11, "exitLogged", 240, "obj_exit_log", "stickerPlaced",
                       "notify.m11.exit_logged");

            Marker(root, "M11_BayInterior", bay + new Vector3(0f, 0.9f, 0f))
                .AddComponent<ManualProximityWatcher>()
                .Setup(m11, "enteredBay", 1.1f, "notify.m11.stepped_in")
                .ArmedBetween("barrierPlaced", "exitLogged");

            var console = Prop(root, "M11_SensorConsole", new Vector3(half.x - 0.4f, 1.2f, -half.y + 1f),
                               PanelSize, HazardColour);
            console.AddComponent<ManualForbiddenProp>()
                   .Setup(m11, "ui.prompt.m11.delete_record", "sensorRecordDeleted", "obj_exit_log")
                   .EndsEvent();
        }

        /// <summary>M03 (tomorrow's patrol log) and M05 (the bags that are not recyclable).</summary>
        void StageRecyclingYard()
        {
            var root = OwnedRoot(ZoneIds.RecyclingYard);
            if (root == null) return;

            var half = HalfOf(ZoneIds.RecyclingYard);
            const string m03 = ManualEventIds.M03_TomorrowsLog;

            Prop(root, "ScrapCrt", new Vector3(-half.x + 1.2f, 0.4f, half.y - 1.2f),
                 new Vector3(0.7f, 0.6f, 0.6f), PropColour);

            var radio = Prop(root, "M03_Radio", new Vector3(-half.x + 1.2f, 0.85f, half.y - 1.8f),
                             new Vector3(0.4f, 0.2f, 0.25f), PropColour);
            radio.AddComponent<ManualActionProp>()
                 .Setup(m03, "ui.prompt.m03.record", "recorded", "obj_record")
                 .AlwaysVisible();

            // Three instructions in the broadcast; two of them is enough (spec 22 M03).
            for (int i = 0; i < 3; i++)
            {
                var note = Prop(root, "M03_Memo" + (i + 1),
                                new Vector3(-half.x + 2.2f, 1.1f - i * 0.25f, half.y - 1.8f),
                                new Vector3(0.3f, 0.18f, 0.05f), PropColour);
                note.AddComponent<ManualActionProp>()
                    .Setup(m03, "ui.prompt.m03.memo", "warningsApplied", "obj_memo", 1, 3)
                    .ObjectiveAt(2)
                    .RequiresCounter("recorded", "ui.prompt.m03.nothing_recorded");
            }

            // ---- M05, the four bags --------------------------------------
            const string m05 = ManualEventIds.M05_NotRecyclable;

            for (int i = 0; i < 4; i++)
            {
                var bag = Prop(root, "M05_Bag" + (i + 1),
                               new Vector3(-1.5f + i * 1.1f, 0.2f, -half.y + 1.6f),
                               BagSize, FigureColour);
                bag.AddComponent<ManualActionProp>()
                   .Setup(m05, "ui.prompt.m05.seal", "sealCount", "obj_seal", 1, 4);
            }

            var cart = Prop(root, "M05_Cart", new Vector3(2.6f, 0.35f, -half.y + 1.6f),
                            new Vector3(0.8f, 0.7f, 1.2f), PropColour);
            cart.AddComponent<ManualActionProp>()
                .Setup(m05, "ui.prompt.m05.load_two", "cartLoaded", "obj_cart")
                .RequiresCounter("sealCount", "ui.prompt.m05.unsealed", 4)
                .AlwaysVisible();

            var overload = Prop(root, "M05_CartOverload", new Vector3(2.6f, 0.95f, -half.y + 1.6f),
                                new Vector3(0.8f, 0.3f, 1.2f), PropColour);
            overload.AddComponent<ManualForbiddenProp>()
                    .Setup(m05, "ui.prompt.m05.load_all", "cartOverloaded", "obj_cart");

            var general = Prop(root, "M05_GeneralWaste", new Vector3(half.x - 1f, 0.5f, 0f),
                               new Vector3(1f, 1.0f, 1f), PropColour);
            general.AddComponent<ManualForbiddenProp>()
                   .Setup(m05, "ui.prompt.m05.general_waste", "treatedAsGeneralWaste", "obj_incinerate")
                   .EndsEvent();

            // The incinerator itself is on B2, so the last step is a trip down the stairs.
            var incinerator = OwnedRoot(ZoneIds.Machinery);
            if (incinerator != null)
            {
                var furnace = Prop(incinerator, "M05_Incinerator",
                                   new Vector3(HalfOf(ZoneIds.Machinery).x - 0.8f, 0.9f, 0f),
                                   new Vector3(1.0f, 1.8f, 1.4f), PropColour);
                furnace.AddComponent<ManualActionProp>()
                       .Setup(m05, "ui.prompt.m05.incinerate", "incinerated", "obj_incinerate", 1, 4)
                       .RequiresCounter("cartLoaded", "ui.prompt.m05.nothing_loaded")
                       .AlwaysVisible();
            }
        }

        // =====================================================================
        // B2
        // =====================================================================

        /// <summary>M09: the red button that works.</summary>
        void StagePumpRoom()
        {
            var root = OwnedRoot(ZoneIds.PumpRoom);
            if (root == null) return;

            var half = HalfOf(ZoneIds.PumpRoom);
            const string m09 = ManualEventIds.M09_BlackWater;

            Prop(root, "PumpTank", new Vector3(-1.5f, 1.0f, 0f), new Vector3(2.0f, 2.0f, 2.0f), PropColour);

            var sample = Prop(root, "M09_SampleValve", new Vector3(-0.4f, 1.0f, 0f),
                              SmallSize, PropColour);
            sample.AddComponent<ManualActionProp>()
                  .Setup(m09, "ui.prompt.m09.sample", "sampleTaken", "obj_sample")
                  .AlwaysVisible();

            var cartA = Prop(root, "M09_CartridgeA", new Vector3(1.2f, 0.9f, -1.2f),
                             new Vector3(0.3f, 0.5f, 0.3f), PropColour);
            cartA.AddComponent<ManualActionProp>()
                 .Setup(m09, "ui.prompt.m09.cartridge_a", "cartridgeA")
                 .RequiresCounter("sampleTaken", "ui.prompt.m09.no_sample")
                 .AlwaysVisible();

            // C only goes in after A. Out of order it does not seat - which is what the note
            // on the page says happens (spec 22 M09).
            var cartC = Prop(root, "M09_CartridgeC", new Vector3(1.2f, 0.9f, 0f),
                             new Vector3(0.3f, 0.5f, 0.3f), PropColour);
            cartC.AddComponent<ManualActionProp>()
                 .Setup(m09, "ui.prompt.m09.cartridge_c", "cartridgeC", "obj_cartridges")
                 .RequiresCounter("cartridgeA", "ui.prompt.m09.wrong_order")
                 .Also("cartridgeOrderCorrect")
                 .AlwaysVisible();

            var residue = Prop(root, "M09_ServiceRod", new Vector3(-1.5f, 2.1f, 0f),
                               new Vector3(1.6f, 0.1f, 1.6f), PropColour);
            residue.AddComponent<ManualSequenceProp>()
                   .Setup(m09, "ui.prompt.m09.push_residue", "residueHandled", "obj_residue", 1.0f, 3);

            // 2.4 bar, carried as decibars so the counter stays an integer.
            Marker(root, "M09_Pressure", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualTimerProp>()
                .Setup(m09, "pressureDecibar", 180, "obj_normal", "residueHandled",
                       "notify.m09.pressure_restored")
                .StartsAt(3)
                .Sets(24);

            var stop = Prop(root, "M09_EmergencyStop", new Vector3(half.x - 0.2f, 1.3f, 0f),
                            new Vector3(0.12f, 0.4f, 0.4f), HazardColour);
            stop.AddComponent<ManualForbiddenProp>()
                .Setup(m09, "ui.prompt.m09.emergency_stop", "emergencyStop", "obj_normal")
                .EndsEvent();
        }

        /// <summary>M10: five valves and one breathing pulse.</summary>
        void StagePipeRoom()
        {
            var root = OwnedRoot(ZoneIds.PipeRoom);
            if (root == null) return;

            var half = HalfOf(ZoneIds.PipeRoom);
            const string m10 = ManualEventIds.M10_PipeGrowth;

            Prop(root, "M10_NewPipework", new Vector3(0f, 1.6f, half.y - 0.15f),
                 new Vector3(8f, 0.12f, 0.12f), HazardColour);

            var meter = Prop(root, "M10_Listener", new Vector3(-half.x + 0.6f, 1.1f, 0f),
                             SmallSize, PropColour);
            meter.AddComponent<ManualActionProp>()
                 .Setup(m10, "ui.prompt.m10.measure", "measured", "obj_measure")
                 .AlwaysVisible();

            // V4 carries the 0.8Hz pulse. The valves are identical to look at; the meter is
            // the only thing that tells them apart, which is why measuring is a step.
            for (int i = 1; i <= 5; i++)
            {
                var valve = Prop(root, "M10_Valve" + i, new Vector3(-3.2f + (i - 1) * 1.6f, 1.2f, half.y - 0.4f),
                                 new Vector3(0.24f, 0.24f, 0.24f), PropColour);
                valve.AddComponent<ManualChoiceProp>()
                     .Setup(m10, "ui.prompt.m10.quarter_turn", "valveChosen", i, "obj_quarter_turn")
                     .RequiresCounter("measured", "ui.prompt.m10.unmeasured")
                     .Also("quarterTurnOnly");
            }

            Marker(root, "M10_Settle", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualTimerProp>()
                .Setup(m10, "settled", 150, "obj_wait", "valveChosen", "notify.m10.growth_stopped");

            var cutter = Prop(root, "M10_CutPipe", new Vector3(2.8f, 1.6f, half.y - 0.35f),
                              new Vector3(0.3f, 0.2f, 0.2f), HazardColour);
            cutter.AddComponent<ManualForbiddenProp>()
                  .Setup(m10, "ui.prompt.m10.cut_pipe", "pipeCut", "obj_quarter_turn")
                  .EndsEvent();

            var closeAll = Prop(root, "M10_CloseAll", new Vector3(half.x - 0.4f, 1.2f, 0f),
                                PanelSize, HazardColour);
            closeAll.AddComponent<ManualForbiddenProp>()
                    .Setup(m10, "ui.prompt.m10.close_all", "allValvesClosed", "obj_quarter_turn")
                    .EndsEvent();
        }

        // =====================================================================
        // The office, and 4F to the roof
        // =====================================================================

        /// <summary>M01: three broadcasts and the patience not to look.</summary>
        void StageOffice()
        {
            var root = OwnedRoot(ZoneIds.Office);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Office);
            const string m01 = ManualEventIds.M01_PhantomFloor;

            var panel = Prop(root, "M01_BroadcastPanel", new Vector3(half.x - 0.15f, 1.5f, 1.2f),
                             new Vector3(0.1f, 0.45f, 0.6f), PropColour);
            panel.AddComponent<ManualSequenceProp>()
                 .Setup(m01, "ui.prompt.m01.broadcast", "broadcastCount", "obj_broadcast", 1.5f, 3);

            // The sensor goes out five game minutes after the third broadcast.
            Marker(root, "M01_Sensor", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualTimerProp>()
                .Setup(m01, "sensorOff", 300, "obj_wait_sensor", "broadcastCount",
                       "notify.m01.sensor_off")
                .StartsAt(3);

            // Forbidden only while the sensor is lit: afterwards the manual sends the player
            // to the log, and watching then is the job (spec 22 M01 step 4).
            Marker(root, "M01_CameraWatch", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualCameraWatcher>()
                .Setup(m01, "cctvZoomed", "CAM-08", "notify.m01.zoomed", "sensorOff");

            var log = Prop(root, "M01_TravelLog", new Vector3(-half.x + 0.4f, 1.1f, -1.5f),
                           new Vector3(0.1f, 0.35f, 0.5f), PropColour);
            log.AddComponent<ManualActionProp>()
               .Setup(m01, "ui.prompt.m01.check_log", "logChecked", "obj_check_log")
               .RequiresCounter("sensorOff", "ui.prompt.m01.sensor_still_on")
               .AlwaysVisible();

            Marker(root, "M01_ServiceLevel", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualZoneWatcher>()
                .Setup(m01, "serviceLevelEntered", ZoneIds.ServicePassage, "obj_service_level");
        }

        /// <summary>M16 (the wall that knocks) and M04 (the ceiling that drips).</summary>
        void StageFloor04()
        {
            var root = OwnedRoot(ZoneIds.Floor04);
            if (root == null) return;

            float wallX = BuildingSpec.HiddenWallCentreX;
            float corridorZ = HalfOf(ZoneIds.Floor04).y - 0.2f;
            const string m16 = ManualEventIds.M16_KnockEcho;

            Marker(root, "M16_Wall", new Vector3(wallX, 1.4f, corridorZ))
                .AddComponent<ManualKnockWall>()
                .Setup(m16, "obj_wait_sequence", "obj_count");

            // The reply is available before the set has finished, and taking it early is
            // recorded rather than blocked (spec 0.4).
            var reply = Prop(root, "M16_ReplyWall", new Vector3(wallX, 1.4f, -corridorZ + 0.1f),
                             new Vector3(1.0f, 0.8f, 0.08f), PropColour);
            reply.AddComponent<ManualSequenceProp>()
                 .Setup(m16, "ui.prompt.m16.knock", "replyCount", null, 0.6f)
                 .EarlyUseSets("playerInitiatedEarly", "sequenceHeard")
                 .AlwaysVisible();

            var endReply = Prop(root, "M16_EndReply", new Vector3(wallX + 0.8f, 1.1f, -corridorZ + 0.1f),
                                SmallSize, PropColour);
            endReply.AddComponent<ManualActionProp>()
                    .Setup(m16, "ui.prompt.m16.end_reply", "replyEnded", "obj_reply")
                    .RequiresCounter("replyCount", "ui.prompt.m16.no_reply");

            Marker(root, "M16_Silence", new Vector3(wallX, 1.4f, corridorZ))
                .AddComponent<ManualTimerProp>()
                .Setup(m16, "silenceHeld", 150, "obj_wait_ten", "replyEnded", "notify.m16.answered");

            // ---- M04, the door that is not a door -------------------------
            const string m04 = ManualEventIds.M04_CeilingStain;

            // Spec 31: deleting the 404 record outright puts ten marks on the ceiling instead
            // of seven, and the judgement compares what was cleaned against what was there.
            Marker(root, "M04_Target", new Vector3(wallX, 1f, corridorZ))
                .AddComponent<ManualInitProp>()
                .Setup(m04, "stainTarget", 7, FlagIds.Db404Deleted, 10);

            var neutraliser = Prop(root, "M04_Neutraliser", new Vector3(wallX - 3f, 0.3f, corridorZ - 0.3f),
                                   new Vector3(0.2f, 0.4f, 0.2f), PropColour);
            neutraliser.AddComponent<ManualActionProp>()
                       .Setup(m04, "ui.prompt.m04.take_neutraliser", "neutraliser", "obj_neutralizer");

            // A separate prefab from the real 404 door, which is behind the service passage
            // (spec 32): this one is 1.5m deep and gone by morning.
            var anomalyDoor = Prop(root, "AnomalyDoor_404", new Vector3(wallX, 1.0f, corridorZ),
                                   new Vector3(BuildingSpec.UnitDoorWidth, 2.05f, 0.1f), PropColour);
            anomalyDoor.AddComponent<ManualActionProp>()
                       .Setup(m04, "ui.prompt.m04.open_door", "doorOpen")
                       .RequiresCounter("neutraliser", "ui.prompt.m04.no_neutraliser");

            // Ten marks are built; only stainTarget of them have to be cleared, and the extra
            // three simply are not there on a night when the record survived.
            for (int i = 0; i < 10; i++)
            {
                var stain = Prop(root, "M04_Stain" + (i + 1),
                                 new Vector3(wallX - 0.5f + (i % 5) * 0.25f, 2.35f,
                                             corridorZ + 0.4f + (i / 5) * 0.3f),
                                 new Vector3(0.2f, 0.04f, 0.2f), HazardColour);
                stain.AddComponent<ManualActionProp>()
                     .Setup(m04, "ui.prompt.m04.spray", "stain" + (i + 1), null, 1, 3)
                     .RequiresCounter("doorOpen", "ui.prompt.m04.door_shut")
                     .OnCompleteAdds("stainsCleaned");
            }

            // No single mark can be the one that finishes the job, because how many there are
            // was decided when the event started. Something has to watch the count.
            Marker(root, "M04_AllClean", new Vector3(wallX, 1f, corridorZ))
                .AddComponent<ManualThresholdProp>()
                .Setup(m04, "stainsCleaned", "obj_clean", "stainTarget", 7, "notify.m04.all_clean");

            // Called from behind, while there is still cleaning to do.
            Marker(root, "M04_Voice", new Vector3(wallX, 1.5f, corridorZ - 2.5f))
                .AddComponent<ManualGazeWatcher>()
                .Setup(m04, "lookedBack", 0.7f, 25f, 10f, "notify.m04.looked_back")
                .ArmedBetween("stainsCleaned", "doorClosed");

            Marker(root, "M04_DeepRoom", new Vector3(wallX, 1.0f, corridorZ + 1.6f))
                .AddComponent<ManualProximityWatcher>()
                .Setup(m04, "enteredDeep", 0.9f, "notify.m04.pulled_back")
                .ArmedBetween("doorOpen", "doorClosed");

            var close = Prop(root, "M04_CloseDoor", new Vector3(wallX + 0.7f, 1.0f, corridorZ),
                             SmallSize, PropColour);
            close.AddComponent<ManualActionProp>()
                 .Setup(m04, "ui.prompt.m04.close_door", "doorClosed", "obj_close_door")
                 .RequiresCounter("stainsCleaned", "ui.prompt.m04.stains_remain");
        }

        /// <summary>M17 (the demolished unit) and M07 (the corridor that repeats).</summary>
        void StageFloor05()
        {
            var root = OwnedRoot(ZoneIds.Floor05);
            if (root == null) return;

            float wallX = BuildingSpec.StandardDoorX[3];
            float corridorZ = HalfOf(ZoneIds.Floor05).y - 0.2f;
            const string m17 = ManualEventIds.M17_Unit504Noise;

            // Three listening points: high, central, low.
            for (int i = 0; i < 3; i++)
            {
                var point = Prop(root, "M17_Listen" + (i + 1),
                                 new Vector3(wallX, 2.1f - i * 0.7f, corridorZ),
                                 new Vector3(0.2f, 0.2f, 0.06f), PropColour);
                point.AddComponent<ManualActionProp>()
                     .Setup(m17, "ui.prompt.m17.listen", "measuredPoints", "obj_measure", 1, 3);
            }

            Marker(root, "M17_Complaints", new Vector3(wallX, 1f, corridorZ))
                .AddComponent<ManualAppWatcher>()
                .Setup(m17, "complaintsRead", new[] { AppIds.Residents }, "obj_check_complaints");

            // 1 = the television, 2 = furniture dragging, 3 = the pipework. The old complaint
            // names the middle one; the loudest is the pipework (spec 22 M17).
            for (int i = 1; i <= 3; i++)
            {
                var notice = Prop(root, "M17_Notice" + i,
                                  new Vector3(wallX + 0.45f, 2.1f - (i - 1) * 0.7f, corridorZ),
                                  new Vector3(0.25f, 0.3f, 0.04f), PropColour);
                notice.AddComponent<ManualChoiceProp>()
                      .Setup(m17, "ui.prompt.m17.post_notice", "causeChoice", i, "obj_post_notice")
                      .RequiresCounter("complaintsRead", "ui.prompt.m17.no_records")
                      .Also("noticePosted");
            }

            var breakWall = Prop(root, "M17_BreakWall", new Vector3(wallX - 0.6f, 1.1f, corridorZ),
                                 SmallSize, HazardColour);
            breakWall.AddComponent<ManualForbiddenProp>()
                     .Setup(m17, "ui.prompt.m17.break_wall", "wallBroken", "obj_post_notice")
                     .EndsEvent();

            var cutPower = Prop(root, "M17_CutPower", new Vector3(wallX - 1.2f, 1.4f, corridorZ),
                                SmallSize, HazardColour);
            cutPower.AddComponent<ManualForbiddenProp>()
                    .Setup(m17, "ui.prompt.m17.cut_power", "powerCut", "obj_post_notice")
                    .EndsEvent();

            // ---- M07, the loop -------------------------------------------
            const string m07 = ManualEventIds.M07_CorridorLoop;

            // The one fitting whose state changes on every lap, which is what the manual says
            // to compare instead of the door numbers.
            var extinguisher = Prop(root, "M07_Extinguisher",
                                    new Vector3(HalfOf(ZoneIds.Floor05).x - 1.0f, 1.0f, corridorZ),
                                    new Vector3(0.18f, 0.5f, 0.18f), HazardColour);
            extinguisher.AddComponent<ManualActionProp>()
                        .Setup(m07, "ui.prompt.m07.compare", "compared", "obj_compare")
                        .AlwaysVisible();

            var bell504 = Prop(root, "M07_Bell504", new Vector3(wallX - 0.55f, 1.15f, corridorZ - 0.1f),
                               SmallSize, PropColour);
            bell504.AddComponent<ManualActionProp>()
                   .Setup(m07, "ui.prompt.m07.ring", "correctBellRung", "obj_ring")
                   .RequiresCounter("compared", "ui.prompt.m07.nothing_compared");

            // The five doors that are not it. Ringing them is allowed and gets nowhere.
            for (int i = 0; i < BuildingSpec.StandardDoorX.Length; i++)
            {
                if (i == 3) continue;
                var bell = Prop(root, "M07_WrongBell" + i,
                                new Vector3(BuildingSpec.StandardDoorX[i] - 0.55f, 1.15f, corridorZ - 0.1f),
                                SmallSize, PropColour);
                bell.AddComponent<ManualActionProp>()
                    .Setup(m07, "ui.prompt.m07.ring", "wrongBell", null, 1, 5);
            }

            Marker(root, "M07_Running", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualSprintWatcher>()
                .Setup(m07, "ran", ZoneIds.Floor05);
        }

        /// <summary>M18: the announcement is made from the landing, not the roof.</summary>
        void StageFloor06()
        {
            var root = OwnedRoot(ZoneIds.Floor06);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Floor06);
            const string m18 = ManualEventIds.M18_RoofFigure;

            var panel = Prop(root, "M18_BroadcastPanel", new Vector3(half.x - 0.3f, 1.5f, half.y - 0.15f),
                             new Vector3(0.45f, 0.35f, 0.1f), PropColour);
            panel.AddComponent<ManualSequenceProp>()
                 .Setup(m18, "ui.prompt.m18.broadcast", "broadcastCount", "obj_broadcast", 2f, 1);

            Marker(root, "M18_FigureGone", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualTimerProp>()
                .Setup(m18, "figureGone", 240, "obj_wait", "broadcastCount", "notify.m18.figure_left")
                .StartsAt(1);

            Marker(root, "M18_Threshold", new Vector3(0f, 1f, 0f))
                .AddComponent<ManualZoneWatcher>()
                .Setup(m18, "crossedThreshold", ZoneIds.Rooftop);
        }

        /// <summary>M18 continued: the five metres the manual will not let the player cross.</summary>
        void StageRooftop()
        {
            var root = OwnedRoot(ZoneIds.Rooftop);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Rooftop);
            const string m18 = ManualEventIds.M18_RoofFigure;

            var figure = Prop(root, "M18_Figure", new Vector3(0f, 1.4f, half.y - 0.6f),
                              new Vector3(0.4f, 1.8f, 0.3f), FigureColour);

            Marker(root, "M18_Distance", figure.transform.localPosition)
                .AddComponent<ManualProximityWatcher>()
                .Setup(m18, "approachedWithinFiveMetres", 5f, "notify.m18.too_close");
        }
    }
}
