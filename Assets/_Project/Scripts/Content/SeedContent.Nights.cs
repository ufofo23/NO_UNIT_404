using System.Collections.Generic;
using UnityEngine;
using NO404.Cases;
using NO404.CCTV;
using NO404.Core;
using NO404.Dialogue;
using NO404.Endings;
using NO404.Evidence;
using NO404.Phone;
using NO404.Residents;
using NO404.Visitors;

namespace NO404.ContentData
{
    /// <summary>
    /// Nights 2 to 6 (GDD 9.3-9.7), the endings (GDD 10) and everything they need:
    /// cases C03-C14, evidence E04-E21, the conversations, the incoming calls and the
    /// CCTV anomalies those nights are built around.
    ///
    /// Evidence ids follow the GDD's own naming so the design doc and the data line up.
    /// </summary>
    public static partial class SeedContent
    {
        /// <summary>Game second for a shift time. Hours before noon belong to the next day.</summary>
        public static int At(int hour, int minute)
        {
            int normalized = hour < 12 ? hour + 24 : hour;
            return normalized * 3600 + minute * 60;
        }

        // =====================================================================
        // People introduced after night 1
        // =====================================================================

        public static ResidentDefinition[] BuildLateResidents()
        {
            var taeho = Resident("res_taeho", "B02", "resident.taeho.name", "4417", "STAFF-07",
                ResidentStatus.Staff, null, null,
                Notes(Note("resident.taeho.note.temp_guard", false)));

            var junho = Resident("res_junho", "EXT-01", "resident.junho.name", "9902", "",
                ResidentStatus.MovedOut, Vehicles(Vehicle("서울 82바 4417", true)), null,
                Notes(Note("resident.junho.note.courier", false)));

            return new[] { taeho, junho };
        }

        // =====================================================================
        // Evidence E04-E21 (GDD 9.3-9.7)
        // =====================================================================

        public static EvidenceDefinition[] BuildLateEvidence()
        {
            // Night 2
            var oldLabel = Evidence_("E04_OLD_DELIVERY_LABEL", EvidenceType.PhysicalObject, "C03", true);
            var fireTape = Evidence_("E05_FIRE_CALL_TAPE_A", EvidenceType.AudioRecording, "C04", true);
            // Owned by N2-M01 now that C03 is gone, and one of that quest's four invariants.
            var weather = Evidence_("E06_CCTV_WEATHER_MISMATCH", EvidenceType.CctvSnapshot, "N2-M01", false);
            weather.tags = new[] { EvidenceTags.Invariant };

            // Night 3
            var floorLog = Evidence_("E07_HIDDEN_MAINTENANCE_FLOOR_LOG", EvidenceType.Document, "C05", true);
            var marks = Evidence_("E08_CHILD_HEIGHT_MARKS", EvidenceType.Photo, "C05", true);
            var extinguisher = Evidence_("E09_OLD_FIRE_EXTINGUISHER_SERIAL", EvidenceType.PhysicalObject, "C05", false);
            var radio = Evidence_("E10_DONGSIK_RADIO_SIGNAL", EvidenceType.AudioRecording, "C05", false);

            // Night 4
            var db404 = Evidence_("E11_404_RESIDENT_DATABASE", EvidenceType.Document, "C07", true);
            var photo = Evidence_("E12_FAMILY_PHOTO", EvidenceType.Photo, "C08", true);
            var testimony = Evidence_("E13_SUNJA_TESTIMONY", EvidenceType.Testimony, "C08", true);
            var lockLog = Evidence_("E14_FIRE_DOOR_LOCK_LOG", EvidenceType.AccessLog, "C09", true);

            // Night 5
            var smoke = Evidence_("E15_SMOKE_DEVICE", EvidenceType.PhysicalObject, "C11", false);
            var chairmanLog = Evidence_("E16_CHAIRMAN_ACCESS_LOG", EvidenceType.AccessLog, "C11", true);
            var replay = Evidence_("E17_2009_CCTV_REPLAY", EvidenceType.CctvSnapshot, "C11", true);
            var dongsikLoc = Evidence_("E18_DONGSIK_LOCATION", EvidenceType.Document, "C12", false);

            // Night 6
            var ledger = Evidence_("E19_ORIGINAL_LEDGER", EvidenceType.Document, "C14", true);
            var bill = Evidence_("E20_2009_MAINTENANCE_BILL", EvidenceType.Document, "C13", true);
            var cutter = Evidence_("E21_HYDRAULIC_CUTTER", EvidenceType.PhysicalObject, "C14", false);

            // What the three M events that pay out in evidence actually hand over (spec 22
            // M16 / M04 / M10). Numbered from 22 because night 6 had already taken 19 to 21 -
            // the anomaly rewards were authored against the spec's own section numbering and
            // collided with it, which meant three correct answers granted nothing at all.
            var knockNote = Evidence_("E22_KNOCK_NOTE", EvidenceType.Document, "C12", false);
            var cleaningRequest = Evidence_("E23_404_CLEANING_REQUEST", EvidenceType.Document, "C07", false);
            var pipeAudio = Evidence_("E24_PIPE_AUDIO", EvidenceType.AudioRecording, "C12", false);

            // The links the final report is built from (GDD 9.7 "필수 연결").
            Relate(db404, bill, EvidenceRelation.SamePerson);
            Relate(db404, lockLog, EvidenceRelation.Cause);
            Relate(lockLog, testimony, EvidenceRelation.TestimonySupport);
            Relate(lockLog, fireTape, EvidenceRelation.TestimonySupport);
            Relate(chairmanLog, lockLog, EvidenceRelation.SamePerson);
            Relate(ledger, db404, EvidenceRelation.Cause);
            Relate(ledger, bill, EvidenceRelation.SameTime);

            // Night-by-night links.
            Relate(oldLabel, weather, EvidenceRelation.SameTime);
            Relate(oldLabel, fireTape, EvidenceRelation.Cause);
            Relate(weather, replay, EvidenceRelation.SameTime);
            Relate(floorLog, marks, EvidenceRelation.LocationContradiction);
            Relate(marks, photo, EvidenceRelation.SamePerson);
            Relate(extinguisher, floorLog, EvidenceRelation.SameTime);
            Relate(radio, dongsikLoc, EvidenceRelation.SamePerson);
            Relate(smoke, chairmanLog, EvidenceRelation.Cause);
            Relate(replay, testimony, EvidenceRelation.TestimonySupport);
            Relate(photo, db404, EvidenceRelation.SamePerson);

            // The anomaly rewards are only worth having if they reach the board.
            Relate(knockNote, dongsikLoc, EvidenceRelation.SamePerson);
            Relate(pipeAudio, radio, EvidenceRelation.SamePerson);
            Relate(cleaningRequest, db404, EvidenceRelation.LocationContradiction);

            return new[]
            {
                oldLabel, fireTape, weather,
                floorLog, marks, extinguisher, radio,
                db404, photo, testimony, lockLog,
                smoke, chairmanLog, replay, dongsikLoc,
                ledger, bill, cutter,
                knockNote, cleaningRequest, pipeAudio
            };
        }

        // =====================================================================
        // Visitors (night 2: the two Junhos, GDD 9.3)
        // =====================================================================

        public static VisitorDefinition[] BuildLateVisitors()
        {
            // The first call. Seo Jun-ho at the front door as he once stood at it - not the
            // man, a replay of him (v5.1 ECHO), and the interphone cannot tell the difference.
            //
            // He reads completely clean, which is the trap: the player has just been taught
            // that agitation is worth watching, and here is a caller with none of it. What
            // gives him away is not him. It is that it is raining tonight and not behind him,
            // that his waybills are on a form nobody has printed since 2009, and that the
            // caretaker who walks down to the glass finds nobody standing there.
            //
            // The informed answer is to let nobody in, because there is nobody. Letting him in
            // is not a disaster either: the door releases, the log records an entry, the lobby
            // camera shows him at the parcel shelf - and the lobby is empty.
            var echo = ScriptableObject.CreateInstance<VisitorDefinition>();
            echo.name = "VIS_JUNHO_ECHO";
            echo.visitorId = "vis_junho_echo";
            echo.nameKey = "visitor.junho.name";
            echo.purposeKey = "visitor.junho.purpose";
            echo.idCardNameKey = "visitor.junho.id_name";
            echo.targetUnit = "202";
            echo.cameraId = "CAM-01";
            echo.conversationId = "D_N2_JUNHO_ENTRY";
            echo.nightIndex = 2;
            echo.arrivalGameSecond = At(22, 10);
            echo.correctAccess = VisitorAccessLevel.Reject;
            echo.isHistoricalReplay = true;
            // Walking somebody in means walking beside them, and there is no beside.
            echo.canBeEscorted = false;
            echo.destinationZone = ZoneIds.Lobby;
            echo.expectedRoute = new[] { ZoneIds.Lobby };
            WandersToTheFourthFloor(echo);

            // v5.1 11: SAN -8 for following the wrong one up. The fourth floor is where the
            // screen says he went, and it is empty when the caretaker gets there.
            echo.onFoundOffRoute = new[]
            {
                ConsequenceDefinition.Sanity(-8, "reason.followed_the_screen_upstairs")
            };
            echo.foundOffRouteNoticeKey = "ui.memo.n2_nobody_upstairs";

            echo.checks = new[]
            {
                Check("visitor.check.id_name", "visitor.check.value.seo_junho", false, AppIds.Residents),
                Check("visitor.check.invoice", "visitor.check.value.invoice_ok", false, AppIds.Access),
                Check("visitor.check.vehicle", "visitor.check.value.no_van_logged", true, AppIds.Cctv),
                Check("visitor.check.weather", "visitor.check.value.rain_outside_only", true, AppIds.Cctv)
            };
            echo.onCorrect = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.correct_visitor")
            };
            echo.onWrong = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, -2, "reason.wrong_visitor")
            };
            echo.onHold = new[] { ConsequenceDefinition.Stat(StatIds.Performance, -1, "reason.held_visitor") };

            echo.truth = VisitorTruth.Legitimate;
            echo.breakingPoint = 88;
            echo.tells = Tells(
                Tell("obs_annoyed", ReadChannel.Voice, TellWeight.Noise),
                Tell("obs_corrects_you", ReadChannel.Voice, TellWeight.Innocent),
                Tell("obs_full_name", ReadChannel.Voice, TellWeight.Innocent));
            echo.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.junho.reply.ask_again", 8),
                Reply(PressureTactic.Silence, "visitor.junho.reply.silence", 10),
                Reply(PressureTactic.ShowMe, "visitor.junho.reply.show_me", 6, "obs_corrects_you"),
                Reply(PressureTactic.Reassure, "visitor.junho.reply.reassure", -8));

            // The second call. The real Seo Jun-ho, who has been standing at the door in the
            // rain while the interphone showed somebody else - and who is now being asked by
            // a stranger whether he did not just go in. Every signal says liar. He is the
            // most honest person on the night, and the lobby is as far as a courier goes.
            var real = ScriptableObject.CreateInstance<VisitorDefinition>();
            real.name = "VIS_JUNHO_REAL";
            real.visitorId = "vis_junho_real";
            real.nameKey = "visitor.junho.name";
            real.purposeKey = "visitor.junho.purpose_again";
            real.idCardNameKey = "visitor.junho.id_name";
            real.targetUnit = "202";
            real.cameraId = "CAM-01";
            real.conversationId = "D_N2_JUNHO_SECOND";
            real.nightIndex = 2;
            real.arrivalGameSecond = At(22, 25);
            real.correctAccess = VisitorAccessLevel.LobbyOnly;
            real.destinationZone = ZoneIds.Lobby;
            real.expectedRoute = new[] { ZoneIds.Lobby };
            WandersToTheFourthFloor(real);

            // v5.1 11: SAN -5 the first time the contradiction is seen - which is now. The
            // same man is at the door again, whatever was decided about him a minute ago.
            real.onArrive = new[]
            {
                ConsequenceDefinition.Sanity(-5, "reason.saw_the_same_man_twice")
            };

            real.checks = new[]
            {
                Check("visitor.check.id_name", "visitor.check.value.seo_junho", false, AppIds.Residents),
                Check("visitor.check.invoice", "visitor.check.value.invoice_ok", false, AppIds.Access),
                Check("visitor.check.vehicle", "visitor.check.value.van_logged", false, AppIds.Cctv),
                Check("visitor.check.camera_route", "visitor.check.value.from_street", false, AppIds.Cctv)
            };
            real.onCorrect = new[]
            {
                ConsequenceDefinition.Stat(StatIds.CommunityTrust, 4, "reason.correct_visitor"),
                ConsequenceDefinition.Stat(StatIds.Performance, 4, "reason.correct_visitor"),
                ConsequenceDefinition.Achievement(AchievementIds.TwoCouriers)
            };
            real.onWrong = new[]
            {
                ConsequenceDefinition.Stat(StatIds.CommunityTrust, -6, "reason.wrong_visitor"),
                ConsequenceDefinition.Stat(StatIds.Performance, -3, "reason.wrong_visitor")
            };
            real.onHold = new[] { ConsequenceDefinition.Stat(StatIds.CommunityTrust, -2, "reason.held_visitor") };

            real.truth = VisitorTruth.Shaken;
            real.breakingPoint = 55;
            real.hiddenReasonKey = "visitor.junho.hidden_second";
            real.tells = Tells(
                Tell("obs_breath", ReadChannel.Voice, TellWeight.Noise),
                Tell("obs_overtalks", ReadChannel.Voice, TellWeight.Noise),
                Tell("obs_checks_street", ReadChannel.Camera, TellWeight.Noise),
                Tell("obs_hands_shake", ReadChannel.Glass, TellWeight.Noise),
                // The van outside is his van, logged, still running. Nothing else arrived in
                // one tonight.
                Tell("obs_engine_running", ReadChannel.Glass, TellWeight.Innocent),
                Tell("obs_corrects_you", ReadChannel.Voice, TellWeight.Innocent, PressureTactic.Reassure));
            real.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.junho.reply.ask_again_second", 22),
                Reply(PressureTactic.Confront, "visitor.junho.reply.confront_second", 30, null, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.junho.reply.silence_second", 26),
                Reply(PressureTactic.ShowMe, "visitor.junho.reply.show_me_second", 14),
                Reply(PressureTactic.Reassure, "visitor.junho.reply.reassure_second", -16, "obs_corrects_you"));

            return new[] { echo, real };
        }

        /// <summary>
        /// What a pass past the lobby turns into (v5.1 11: a wrong grant is not game over, the
        /// visitor has to be found again on 4F or B1).
        ///
        /// Either of them given the floors goes up to the fourth and stops at the stretch of
        /// wall between 401 and 405, and stays until the caretaker walks up there. For the
        /// real courier that is a man to be found and sent home - the thing he is still
        /// talking about on night 4. For the replay it is a figure on CAM-04 and an empty
        /// corridor. Held to the lobby neither has anywhere to go.
        ///
        /// ASSUMPTION: v5.1 names "the 4F service area". The service passage is sealed until
        /// night 3, so on night 2 this is the fourth-floor corridor in front of that wall.
        /// </summary>
        static void WandersToTheFourthFloor(VisitorDefinition junho)
        {
            junho.canDeviate = true;
            junho.deviationZone = ZoneIds.Floor04;
            junho.leavesWhenFound = true;
        }

        /// <summary>
        /// v5.1 16: N1_404_BILL_PRESERVED gives night 2 a hint for the ECHO call - the 2009
        /// paper can be compared. One line, when the first caller's waybills are held up, and
        /// only for a caretaker who kept the bill: it is the same stock.
        /// </summary>
        static ConsequenceDefinition PaperFormatMemo()
        {
            return ConsequenceDefinition.Notify("ui.memo.n2_paper_format")
                                        .When(ConditionDefinition.Flag(FlagIds.BillPreserved404));
        }

        // =====================================================================
        // Conversations
        // =====================================================================

        public static DialogueDefinition[] BuildLateDialogues()
        {
            return new[]
            {
                Noise305(), JunhoEntry(), JunhoSecond(),
                ChairmanElevator(), JiwooElevator(), TaehoPassage(),
                ChairmanDelete(), SunjaTestimony(), Call404(),
                ChairmanPressure(), Evacuation(), DongsikRescue(), ChairmanConfront()
            };
        }

        /// <summary>
        /// The 305 voice message (GDD 9.2). Four lines, no choices, no one to answer: the
        /// neighbour recorded it and hung up. Time scale 1.0 - the shift does not slow down
        /// to let anyone listen to their messages.
        /// </summary>
        static DialogueDefinition Noise305()
        {
            return Conversation("D_N1_305_NOISE", DialogueChannel.Phone, 1f, 0f,
                Node("start", "speaker.unit305", "dlg.n1.305.001", "line2"),
                Node("line2", "speaker.unit305", "dlg.n1.305.002", "line3"),
                Node("line3", "speaker.unit305", "dlg.n1.305.003", "line4"),
                Node("line4", "speaker.unit305", "dlg.n1.305.004", null));
        }

        static DialogueDefinition Conversation(string id, DialogueChannel channel, float timeScale,
                                               float choiceTimer, params DialogueNode[] nodes)
        {
            var definition = ScriptableObject.CreateInstance<DialogueDefinition>();
            definition.name = id;
            definition.conversationId = id;
            definition.channel = channel;
            definition.timeScale = timeScale;
            definition.choiceTimeLimit = choiceTimer;
            definition.startNodeId = "start";
            definition.nodes = nodes;
            return definition;
        }

        /// <summary>
        /// The first call. Two of the three questions that matter can be asked from the desk:
        /// what is on his waybills, and whether it is raining where he is standing.
        /// </summary>
        static DialogueDefinition JunhoEntry()
        {
            return Conversation("D_N2_JUNHO_ENTRY", DialogueChannel.Interphone, 0.25f, 12f,
                Node("start", "speaker.junho", "dlg.n2.junho.start", null,
                    Choices(
                        Choice("check_invoice", "dlg.n2.junho.choice.invoice", "invoice"),
                        Choice("ask_weather", "dlg.n2.junho.choice.weather", "weather"),
                        Choice("check_company", "dlg.n2.junho.choice.company", "company"),
                        Choice("ask_units", "dlg.n2.junho.choice.units", "units"))),
                Node("invoice", "speaker.junho", "dlg.n2.junho.invoice", "await",
                    null, ConsequenceDefinition.Evidence("EV_WAYBILL_ORDER"), PaperFormatMemo()),
                Node("weather", "speaker.junho", "dlg.n2.junho.weather", "await",
                    null, ConsequenceDefinition.Evidence("E06_CCTV_WEATHER_MISMATCH")),
                Node("company", "speaker.junho", "dlg.n2.junho.company", "await"),
                Node("units", "speaker.junho", "dlg.n2.junho.units", "await"),
                Node("await", "speaker.junho", "dlg.n2.junho.await", null));
        }

        /// <summary>
        /// The second call. His answers are the ones tonight would give - it is raining, the
        /// waybill is dated today - and they are not evidence of anything except that he is
        /// here, which is the point.
        ///
        /// What the caretaker can put to him depends on what they did a minute ago. "Did you
        /// not just go in?" is only a question for somebody who opened the door the first
        /// time; one who turned the first call away saw him leave, and asks about that.
        /// </summary>
        static DialogueDefinition JunhoSecond()
        {
            var firstCallLetIn = ConditionDefinition.VisitorAccess(
                "vis_junho_echo", VisitorAccessLevel.Vestibule, VisitorAccessLevel.FullTemporary);
            var firstCallTurnedAway = ConditionDefinition.VisitorAccess(
                "vis_junho_echo", VisitorAccessLevel.Reject, VisitorAccessLevel.Reject);

            return Conversation("D_N2_JUNHO_SECOND", DialogueChannel.Interphone, 0.25f, 12f,
                Node("start", "speaker.junho", "dlg.n2.junho2.start", null,
                    Choices(
                        Choice("already_inside", "dlg.n2.junho2.choice.already_inside", "denies")
                            .Only(firstCallLetIn),
                        Choice("just_left", "dlg.n2.junho2.choice.just_left", "denies")
                            .Only(firstCallTurnedAway),
                        Choice("ask_invoice_number", "dlg.n2.junho2.choice.invoice_number", "number"),
                        Choice("ask_weather", "dlg.n2.junho2.choice.weather", "weather"))),
                Node("denies", "speaker.junho", "dlg.n2.junho2.denies", "await"),
                Node("number", "speaker.junho", "dlg.n2.junho2.number", "await",
                    null, ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.noticed_contradiction")),
                Node("weather", "speaker.junho", "dlg.n2.junho2.weather", "await"),
                Node("await", "speaker.junho", "dlg.n2.junho2.await", null));
        }

        static DialogueDefinition ChairmanElevator()
        {
            return Conversation("D_N3_CHAIRMAN_ELEVATOR", DialogueChannel.Phone, 0.25f, 0f,
                Node("start", "speaker.chairman", "dlg.n3.chairman.start", "order"),
                Node("order", "speaker.chairman", "dlg.n3.chairman.order", null,
                    Choices(
                        Choice("obey", "dlg.n3.chairman.choice.obey", "obey_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, -1, "reason.obeyed_chairman"),
                            ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.obeyed_chairman")),
                        Choice("cite_manual", "dlg.n3.chairman.choice.manual", "manual_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.refused_chairman")),
                        Choice("stall", "dlg.n3.chairman.choice.stall", "stall_reply"))),
                Node("obey_reply", "speaker.chairman", "dlg.n3.chairman.obey_reply", null),
                Node("manual_reply", "speaker.chairman", "dlg.n3.chairman.manual_reply", null),
                Node("stall_reply", "speaker.chairman", "dlg.n3.chairman.stall_reply", null));
        }

        static DialogueDefinition JiwooElevator()
        {
            return Conversation("D_N3_JIWOO_ELEVATOR", DialogueChannel.FaceToFace, 0.25f, 0f,
                Node("start", "speaker.jiwoo", "dlg.n3.jiwoo.start", "detail"),
                Node("detail", "speaker.jiwoo", "dlg.n3.jiwoo.detail", null,
                    Choices(
                        // Spec 21.3: with the notice up she never looked at the display, so
                        // there is no recording to ask for - only a remark in the corridor.
                        // The trust is the same either way; the exposure is not.
                        Choice("ask_recording", "dlg.n3.jiwoo.choice.recording", "recording", false,
                            ConsequenceDefinition.Flag(FlagIds.JiwooTrusted, true),
                            ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.listened_to_jiwoo"))
                            .Only(ConditionDefinition.Flag(FlagIds.JiwooExposurePrevented, false)),
                        Choice("ask_corridor", "dlg.n3.jiwoo.choice.corridor", "corridor", false,
                            ConsequenceDefinition.Flag(FlagIds.JiwooTrusted, true),
                            ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.listened_to_jiwoo"))
                            .Only(ConditionDefinition.Flag(FlagIds.JiwooExposurePrevented, true)),
                        Choice("warn_rooftop", "dlg.n3.jiwoo.choice.rooftop", "rooftop", false,
                            ConsequenceDefinition.Stat(StatIds.CommunityTrust, 2, "reason.protected_resident")),
                        Choice("dismiss", "dlg.n3.jiwoo.choice.dismiss", "dismissed", false,
                            ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3, "reason.dismissed_resident")))),
                Node("recording", "speaker.jiwoo", "dlg.n3.jiwoo.recording", null),
                Node("corridor", "speaker.jiwoo", "dlg.n3.jiwoo.corridor", null),
                Node("rooftop", "speaker.jiwoo", "dlg.n3.jiwoo.rooftop", null),
                Node("dismissed", "speaker.jiwoo", "dlg.n3.jiwoo.dismissed", null));
        }

        static DialogueDefinition TaehoPassage()
        {
            return Conversation("D_N3_TAEHO_PASSAGE", DialogueChannel.FaceToFace, 0.25f, 0f,
                Node("start", "speaker.taeho", "dlg.n3.taeho.start", "block"),
                Node("block", "speaker.taeho", "dlg.n3.taeho.block", null,
                    Choices(
                        Choice("show_contract", "dlg.n3.taeho.choice.contract", "contract_reply", false,
                            ConsequenceDefinition.Flag(FlagIds.TaehoCooperates, true),
                            ConsequenceDefinition.Flag(FlagIds.HasBlueprint, true),
                            ConsequenceDefinition.Stat(StatIds.CommunityTrust, 3, "reason.earned_trust")),
                        Choice("ask_who_sent", "dlg.n3.taeho.choice.who_sent", "who_sent_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.pressed_taeho")),
                        Choice("back_off", "dlg.n3.taeho.choice.back_off", "back_off_reply"))),
                Node("contract_reply", "speaker.taeho", "dlg.n3.taeho.contract_reply", null),
                Node("who_sent_reply", "speaker.taeho", "dlg.n3.taeho.who_sent_reply", null),
                Node("back_off_reply", "speaker.taeho", "dlg.n3.taeho.back_off_reply", null));
        }

        static DialogueDefinition ChairmanDelete()
        {
            return Conversation("D_N4_CHAIRMAN_DELETE", DialogueChannel.Phone, 0.25f, 0f,
                Node("start", "speaker.chairman", "dlg.n4.chairman.start", "code"),
                Node("code", "speaker.chairman", "dlg.n4.chairman.code", null,
                    Choices(
                        Choice("accept_code", "dlg.n4.chairman.choice.accept", "accept_reply"),
                        Choice("ask_why", "dlg.n4.chairman.choice.why", "why_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.questioned_chairman")),
                        Choice("refuse", "dlg.n4.chairman.choice.refuse", "refuse_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 2, "reason.refused_chairman")))),
                Node("accept_reply", "speaker.chairman", "dlg.n4.chairman.accept_reply", null),
                Node("why_reply", "speaker.chairman", "dlg.n4.chairman.why_reply", null),
                Node("refuse_reply", "speaker.chairman", "dlg.n4.chairman.refuse_reply", null));
        }

        static DialogueDefinition SunjaTestimony()
        {
            return Conversation("D_N4_SUNJA_TESTIMONY", DialogueChannel.FaceToFace, 0.25f, 0f,
                Node("start", "speaker.sunja", "dlg.n4.sunja.start", "photo",
                    null, ConsequenceDefinition.Evidence("E12_FAMILY_PHOTO")),
                Node("photo", "speaker.sunja", "dlg.n4.sunja.photo", null,
                    Choices(
                        Choice("ask_fire_night", "dlg.n4.sunja.choice.fire_night", "fire_night", false,
                            ConsequenceDefinition.Evidence("E13_SUNJA_TESTIMONY"),
                            ConsequenceDefinition.Flag(FlagIds.SunjaTestimony, true),
                            ConsequenceDefinition.Stat(StatIds.HarinResonance, 2, "reason.heard_testimony"),
                            ConsequenceDefinition.Achievement(AchievementIds.Witness)),
                        Choice("ask_mother", "dlg.n4.sunja.choice.mother", "mother", false,
                            ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.heard_testimony")),
                        Choice("leave", "dlg.n4.sunja.choice.leave", "left"))),
                Node("fire_night", "speaker.sunja", "dlg.n4.sunja.fire_night", "closing"),
                Node("mother", "speaker.sunja", "dlg.n4.sunja.mother", "closing"),
                Node("closing", "speaker.sunja", "dlg.n4.sunja.closing", null),
                Node("left", "speaker.sunja", "dlg.n4.sunja.left", null));
        }

        static DialogueDefinition Call404()
        {
            return Conversation("D_N4_CALL_404", DialogueChannel.Phone, 0.25f, 0f,
                Node("start", "speaker.unknown", "dlg.n4.call404.start", "echo"),
                Node("echo", "speaker.unknown", "dlg.n4.call404.echo", "child"),
                Node("child", "speaker.child", "dlg.n4.call404.child", null,
                    Choices(
                        Choice("answer_gently", "dlg.n4.call404.choice.gentle", "gentle", false,
                            ConsequenceDefinition.Stat(StatIds.HarinResonance, 2, "reason.answered_gently"),
                            ConsequenceDefinition.Flag(FlagIds.Knows404, true)),
                        Choice("ask_who", "dlg.n4.call404.choice.who", "who", false,
                            ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.answered_gently")),
                        Choice("hang_up", "dlg.n4.call404.choice.hang_up", "hung_up", true,
                            ConsequenceDefinition.Stat(StatIds.HarinResonance, -1, "reason.hung_up")))),
                Node("gentle", "speaker.child", "dlg.n4.call404.gentle", "end"),
                Node("who", "speaker.child", "dlg.n4.call404.who", "end"),
                Node("end", "speaker.child", "dlg.n4.call404.end", null),
                Node("hung_up", "speaker.unknown", "dlg.n4.call404.hung_up", null));
        }

        static DialogueDefinition ChairmanPressure()
        {
            return Conversation("D_N5_CHAIRMAN_PRESSURE", DialogueChannel.Phone, 0.25f, 0f,
                Node("start", "speaker.chairman", "dlg.n5.chairman.start", "offer"),
                Node("offer", "speaker.chairman", "dlg.n5.chairman.offer", null,
                    Choices(
                        Choice("take_deal", "dlg.n5.chairman.choice.deal", "deal_reply", false,
                            ConsequenceDefinition.Flag(FlagIds.ChairmanDeal, true),
                            ConsequenceDefinition.Stat(StatIds.Performance, 6, "reason.took_deal"),
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -2, "reason.took_deal")),
                        Choice("refuse_deal", "dlg.n5.chairman.choice.refuse", "refuse_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 2, "reason.refused_chairman")),
                        Choice("record_call", "dlg.n5.chairman.choice.record", "record_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.recorded_call"),
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.recorded_call")))),
                Node("deal_reply", "speaker.chairman", "dlg.n5.chairman.deal_reply", null),
                Node("refuse_reply", "speaker.chairman", "dlg.n5.chairman.refuse_reply", null),
                Node("record_reply", "speaker.chairman", "dlg.n5.chairman.record_reply", null));
        }

        /// <summary>Night 6 step 2: who gets told to leave the building (GDD 9.7).</summary>
        static DialogueDefinition Evacuation()
        {
            return Conversation("D_N6_EVACUATION", DialogueChannel.FaceToFace, 0f, 0f,
                Node("start", "speaker.self", "dlg.n6.evac.start", null,
                    Choices(
                        Choice("full", "dlg.n6.evac.choice.full", "full_reply", false,
                            ConsequenceDefinition.Flag(FlagIds.EvacuationOrdered, true),
                            ConsequenceDefinition.Stat(StatIds.BuildingSafety, 12, "reason.full_evacuation"),
                            ConsequenceDefinition.Stat(StatIds.CommunityTrust, 5, "reason.full_evacuation"),
                            ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.full_evacuation")),
                        Choice("partial", "dlg.n6.evac.choice.partial", "partial_reply", false,
                            ConsequenceDefinition.Flag(FlagIds.EvacuationOrdered, true),
                            ConsequenceDefinition.Stat(StatIds.BuildingSafety, 6, "reason.partial_evacuation"),
                            ConsequenceDefinition.Stat(StatIds.CommunityTrust, 2, "reason.partial_evacuation")),
                        Choice("quiet", "dlg.n6.evac.choice.quiet", "quiet_reply", false,
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.quiet_rescue"),
                            ConsequenceDefinition.Stat(StatIds.BuildingSafety, -8, "reason.quiet_rescue")))),
                Node("full_reply", "speaker.self", "dlg.n6.evac.full_reply", null),
                Node("partial_reply", "speaker.self", "dlg.n6.evac.partial_reply", null),
                Node("quiet_reply", "speaker.self", "dlg.n6.evac.quiet_reply", null));
        }

        static DialogueDefinition DongsikRescue()
        {
            return Conversation("D_N6_DONGSIK_RESCUE", DialogueChannel.FaceToFace, 0.25f, 0f,
                Node("start", "speaker.dongsik", "dlg.n6.dongsik.start", "choice_point"),
                Node("choice_point", "speaker.dongsik", "dlg.n6.dongsik.choice_point", null,
                    Choices(
                        Choice("pull_him_out", "dlg.n6.dongsik.choice.pull", "pulled", false,
                            ConsequenceDefinition.Flag(FlagIds.DongsikRescued, true),
                            ConsequenceDefinition.Flag(FlagIds.DongsikFound, true),
                            ConsequenceDefinition.Stat(StatIds.CommunityTrust, 8, "reason.rescued_dongsik"),
                            ConsequenceDefinition.Achievement(AchievementIds.FoundDongsik)),
                        Choice("ledger_first", "dlg.n6.dongsik.choice.ledger", "ledger_first", false,
                            ConsequenceDefinition.Flag(FlagIds.DongsikFound, true),
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.records_first"),
                            ConsequenceDefinition.Stat(StatIds.CommunityTrust, -5, "reason.records_first")))),
                Node("pulled", "speaker.dongsik", "dlg.n6.dongsik.pulled", null),
                Node("ledger_first", "speaker.dongsik", "dlg.n6.dongsik.ledger_first", null));
        }

        static DialogueDefinition ChairmanConfront()
        {
            return Conversation("D_N6_CHAIRMAN_CONFRONT", DialogueChannel.FaceToFace, 0.25f, 0f,
                Node("start", "speaker.chairman", "dlg.n6.confront.start", "argument"),
                Node("argument", "speaker.chairman", "dlg.n6.confront.argument", "ninety"),
                Node("ninety", "speaker.chairman", "dlg.n6.confront.ninety", null,
                    Choices(
                        Choice("hand_over_ledger", "dlg.n6.confront.choice.hand_over", "handed", false,
                            ConsequenceDefinition.Flag(FlagIds.ChairmanDeal, true),
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -3, "reason.handed_over")),
                        Choice("upload_now", "dlg.n6.confront.choice.upload", "uploaded", false,
                            ConsequenceDefinition.Flag(FlagIds.EvidencePublished, true),
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.uploaded")),
                        Choice("lock_him_in", "dlg.n6.confront.choice.lock_in", "locked", false,
                            ConsequenceDefinition.Flag(FlagIds.ChairmanTrapped, true),
                            ConsequenceDefinition.Stat(StatIds.BuildingSafety, -6, "reason.locked_chairman")),
                        Choice("burn_records", "dlg.n6.confront.choice.burn", "burned", false,
                            ConsequenceDefinition.Flag(FlagIds.RecordsBurned, true),
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -5, "reason.burned_records")))),
                Node("handed", "speaker.chairman", "dlg.n6.confront.handed", null),
                Node("uploaded", "speaker.chairman", "dlg.n6.confront.uploaded", null),
                Node("locked", "speaker.chairman", "dlg.n6.confront.locked", null),
                Node("burned", "speaker.chairman", "dlg.n6.confront.burned", null));
        }

        // =====================================================================
        // Incoming calls (GDD 20.16 priority queue)
        // =====================================================================

        public static PhoneCallDefinition[] BuildPhoneCalls()
        {
            return new[]
            {
                // GDD 9.2. 22:07, on top of the blackout. The old night 1 asked its question
                // at 23:05, an hour into a calm shift, which gave the player a quiet hour to
                // tidy the desk in first. This arrives while the camera wall is still dead and
                // the third caller is still standing at the door - the player has to decide
                // what to drop, and dropping something is the point.
                //
                // It is a message rather than a conversation: 305 left it and hung up. Nobody
                // in this building is available to be asked a follow-up question.
                Call("CALL_N1_305_NOISE", "caller.unit305", "305", "D_N1_305_NOISE",
                     CallPriority.Resident, 1, At(22, 7), "N1-M01",
                     new[] { ConsequenceDefinition.Stat(StatIds.CommunityTrust, -2, "reason.missed_call") }, 900),

                Call("CALL_N3_CHAIRMAN", "caller.chairman", "1501", "D_N3_CHAIRMAN_ELEVATOR",
                     CallPriority.Management, 3, At(22, 35), "N3-M01",
                     new[] { ConsequenceDefinition.Stat(StatIds.Performance, -2, "reason.missed_call") }, 600),

                Call("CALL_N3_JIWOO", "caller.jiwoo", "602", "D_N3_JIWOO_ELEVATOR",
                     CallPriority.Resident, 3, At(23, 0), "N3-M01",
                     new[] { ConsequenceDefinition.Stat(StatIds.CommunityTrust, -2, "reason.missed_call") }, 900),

                Call("CALL_N4_CHAIRMAN", "caller.chairman", "1501", "D_N4_CHAIRMAN_DELETE",
                     CallPriority.Management, 4, At(22, 20), "N4-M01",
                     new[] { ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 10, "reason.missed_call") }, 600),

                // The call that cannot exist. Highest priority: it rings over everything.
                Call404Definition(),

                Call("CALL_N5_CHAIRMAN", "caller.chairman", "1501", "D_N5_CHAIRMAN_PRESSURE",
                     CallPriority.Management, 5, At(23, 50), "N5-M01",
                     new[] { ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 10, "reason.missed_call") }, 0)
            };
        }

        static PhoneCallDefinition Call404Definition()
        {
            var call = Call("CALL_N4_404", "caller.unit404", "404", "D_N4_CALL_404",
                            CallPriority.Impossible, 4, At(0, 30), "N4-M01",
                            new[] { ConsequenceDefinition.Stat(StatIds.HarinResonance, -10, "reason.missed_call") },
                            1200);

            call.ringSeconds = 300;

            // C07 and C09 were two separate night-4 beats and both now hang off N4-M01. Without
            // this the 404 call rings in the same second as the chairman's and outranks it,
            // being Impossible priority - so the scene where the caretaker is told to delete
            // the record would never be heard. Twenty game minutes into the job instead.
            call.releaseDelaySeconds = 20 * 60;
            call.onAnswered = new[]
            {
                ConsequenceDefinition.Flag(FlagIds.Knows404, true),
                ConsequenceDefinition.Achievement(AchievementIds.NoUnit404)
            };
            return call;
        }

        static PhoneCallDefinition Call(string id, string callerNameKey, string number, string conversationId,
                                        CallPriority priority, int night, int gameSecond, string caseId,
                                        ConsequenceDefinition[] onMissed, int retryAfterSeconds)
        {
            var call = ScriptableObject.CreateInstance<PhoneCallDefinition>();
            call.name = id;
            call.callId = id;
            call.callerNameKey = callerNameKey;
            call.callerNumber = number;
            call.conversationId = conversationId;
            call.priority = priority;
            call.nightIndex = night;
            call.gameSecond = gameSecond;
            call.ringSeconds = 240;
            call.caseId = caseId;
            call.onMissed = onMissed ?? new ConsequenceDefinition[0];
            call.retryAfterSeconds = retryAfterSeconds;
            return call;
        }

        // =====================================================================
        // Cases C03 - C14 and the routine tasks nights 2-6 need
        // =====================================================================

        public static CaseDefinition[] BuildLateCases()
        {
            var list = new List<CaseDefinition>
            {
                BuildC03(), BuildC04(),
                BuildC05(), BuildC06(),
                BuildC07(), BuildC08(), BuildC09(),
                BuildC10(), BuildC11(), BuildC12(),
                BuildC13(), BuildC14()
            };

            list.AddRange(BuildLateTasks());
            list.AddRange(BuildRemainingTasks());
            return list.ToArray();
        }

        /// <summary>C03 - two couriers (GDD 9.3). The night-2 headline case.</summary>
        static CaseDefinition BuildC03()
        {
            var c = NewCase("C03", 2, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(22, 8);
            c.evidenceIds = new[] { "E04_OLD_DELIVERY_LABEL", "E06_CCTV_WEATHER_MISMATCH" };
            c.objectives = new[]
            {
                Objective("obj_first_courier", "case.c03.objective.first_courier", ObjectiveType.JudgeVisitor, "vis_junho_real"),
                Objective("obj_see_contradiction", "case.c03.objective.see_contradiction", ObjectiveType.ViewCctvChannel, "CAM-02"),
                Objective("obj_second_courier", "case.c03.objective.second_courier", ObjectiveType.JudgeVisitor, "vis_junho_second"),
                Objective("obj_check_lobby", "case.c03.objective.check_lobby", ObjectiveType.EnterZone, ZoneIds.Lobby),
                Objective("obj_inspect_box", "case.c03.objective.inspect_box", ObjectiveType.AcquireEvidence, "E04_OLD_DELIVERY_LABEL")
            };
            c.decisions = new[]
            {
                Decision("dec_past_footage", "case.c03.decision.past_footage", "case.c03.result.past_footage",
                    DecisionQuality.Correct,
                    new[] { "E04_OLD_DELIVERY_LABEL", "E06_CCTV_WEATHER_MISMATCH" }, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.Performance, 8, "reason.correct_report"),
                    ConsequenceDefinition.Flag(FlagIds.KnowsPastFootage, true)),

                Decision("dec_system_error", "case.c03.decision.system_error", "case.c03.result.system_error",
                    DecisionQuality.Partial, null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.partial_report")),

                Decision("dec_report_intruder", "case.c03.decision.intruder", "case.c03.result.intruder",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -6, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.Performance, -3, "reason.wrong_report"))
            };
            c.failSafe = FailSafe(At(1, 0), "E06_CCTV_WEATHER_MISMATCH", "case.c03.failsafe.notify");
            c.analyticsName = "case_c03_two_couriers";
            return c;
        }

        /// <summary>C04 - the parcel from the past (GDD 9.3 box investigation).</summary>
        static CaseDefinition BuildC04()
        {
            var c = NewCase("C04", 2, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.CaseResolved;
            c.triggerTarget = "C03";
            c.startWindowBegin = At(22, 30);
            c.startConditions = new[] { ConditionDefinition.Evidence("E04_OLD_DELIVERY_LABEL") };
            c.evidenceIds = new[] { "E05_FIRE_CALL_TAPE_A" };
            c.objectives = new[]
            {
                Objective("obj_play_tape", "case.c04.objective.play_tape", ObjectiveType.AcquireEvidence, "E05_FIRE_CALL_TAPE_A")
            };
            c.decisions = new[]
            {
                Decision("dec_archive_tape", "case.c04.decision.archive", "case.c04.result.archive",
                    DecisionQuality.Correct, new[] { "E05_FIRE_CALL_TAPE_A" }, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.correct_report")),

                Decision("dec_discard_tape", "case.c04.decision.discard", "case.c04.result.discard",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -1, "reason.wrong_report"))
            };
            // GDD 14.4 / 11.2: a P0 case may not lose its own answer to a missed deadline.
            // The tape is the only thing C04 asks for and the only thing its correct decision
            // will accept, and the deck it plays on is locked behind C03 being filed correctly
            // (WorldBuilder gates it on KnowsPastFootage). File C03 any other way and the
            // fail-safe used to fire with no alternate evidence at all: it ticked the
            // objective, left the report with nothing on it but "throw it out", and took
            // E05 out of the run. The notify line already says the lost-property register
            // logged the contents of the box, so the register is what hands the tape over.
            // Credit is still withheld - SubmitDecision downgrades a rescued Correct to
            // Partial - which is the cost of having let the deadline go.
            c.failSafe = FailSafe(At(2, 0), "E05_FIRE_CALL_TAPE_A", "case.c04.failsafe.notify");
            c.analyticsName = "case_c04_past_parcel";
            return c;
        }

        /// <summary>C05 - the 16th floor (GDD 9.4). The night-3 headline case.</summary>
        static CaseDefinition BuildC05()
        {
            var c = NewCase("C05", 3, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(22, 28);
            c.evidenceIds = new[]
            {
                "E07_HIDDEN_MAINTENANCE_FLOOR_LOG", "E08_CHILD_HEIGHT_MARKS",
                "E09_OLD_FIRE_EXTINGUISHER_SERIAL", "E10_DONGSIK_RADIO_SIGNAL"
            };
            c.objectives = new[]
            {
                Objective("obj_elevator_alert", "case.c05.objective.elevator_alert", ObjectiveType.ViewCctvChannel, "CAM-08"),
                Objective("obj_talk_jiwoo", "case.c05.objective.talk_jiwoo", ObjectiveType.CallCharacter, "D_N3_JIWOO_ELEVATOR"),
                Objective("obj_ride_elevator", "case.c05.objective.ride_elevator", ObjectiveType.EnterZone, ZoneIds.Elevator),
                Objective("obj_enter_passage", "case.c05.objective.enter_passage", ObjectiveType.EnterZone, ZoneIds.ServicePassage),
                Objective("obj_floor_log", "case.c05.objective.floor_log", ObjectiveType.AcquireEvidence, "E07_HIDDEN_MAINTENANCE_FLOOR_LOG"),
                Objective("obj_child_marks", "case.c05.objective.child_marks", ObjectiveType.AcquireEvidence, "E08_CHILD_HEIGHT_MARKS"),
                Objective("obj_radio", "case.c05.objective.radio", ObjectiveType.AcquireEvidence, "E10_DONGSIK_RADIO_SIGNAL", true)
            };
            c.decisions = new[]
            {
                Decision("dec_delete_log", "case.c05.decision.delete_log", "case.c05.result.delete_log",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Flag(FlagIds.ElevatorLogDeleted, true),
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.obeyed_chairman"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, -1, "reason.obeyed_chairman"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -2, "reason.wrong_report")),

                Decision("dec_report_floor", "case.c05.decision.report_floor", "case.c05.result.report_floor",
                    DecisionQuality.Correct, new[] { "E07_HIDDEN_MAINTENANCE_FLOOR_LOG" }, null,
                    ConsequenceDefinition.Flag(FlagIds.Floor16Found, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 2, "reason.correct_report"),
                    ConsequenceDefinition.Achievement(AchievementIds.Floor16)),

                Decision("dec_copy_evidence", "case.c05.decision.copy_evidence", "case.c05.result.copy_evidence",
                    DecisionQuality.Correct,
                    new[] { "E07_HIDDEN_MAINTENANCE_FLOOR_LOG", "E08_CHILD_HEIGHT_MARKS" }, null,
                    ConsequenceDefinition.Flag(FlagIds.Floor16Found, true),
                    ConsequenceDefinition.Flag(FlagIds.ArchiveCopied, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 3, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 2, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 3, "reason.correct_report"),
                    ConsequenceDefinition.Achievement(AchievementIds.Floor16))
            };
            c.failSafe = FailSafe(At(2, 30), "E07_HIDDEN_MAINTENANCE_FLOOR_LOG", "case.c05.failsafe.notify");
            c.analyticsName = "case_c05_floor_16";
            return c;
        }

        /// <summary>C06 - the guard's warning (GDD 9.4 Taeho).</summary>
        static CaseDefinition BuildC06()
        {
            var c = NewCase("C06", 3, CaseKind.MainCase, Priority.P1);
            c.trigger = CaseTrigger.ZoneEntered;
            c.triggerTarget = ZoneIds.ServicePassage;
            c.startWindowBegin = At(22, 30);
            c.objectives = new[]
            {
                Objective("obj_meet_taeho", "case.c06.objective.meet_taeho", ObjectiveType.CallCharacter, "D_N3_TAEHO_PASSAGE")
            };
            c.decisions = new[]
            {
                Decision("dec_keep_quiet", "case.c06.decision.keep_quiet", "case.c06.result.keep_quiet",
                    DecisionQuality.Correct, null, null,
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 3, "reason.earned_trust")),

                Decision("dec_report_taeho", "case.c06.decision.report_taeho", "case.c06.result.report_taeho",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Flag(FlagIds.TaehoCooperates, false),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -4, "reason.wrong_report"))
            };
            c.failSafe = FailSafe(At(3, 0), null, "case.c06.failsafe.notify");
            c.analyticsName = "case_c06_guard_warning";
            return c;
        }

        /// <summary>C07 - the 404 resident record (GDD 9.5). The night-4 headline case.</summary>
        static CaseDefinition BuildC07()
        {
            var c = NewCase("C07", 4, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(22, 0);
            c.evidenceIds = new[] { "E11_404_RESIDENT_DATABASE" };
            c.objectives = new[]
            {
                Objective("obj_open_db", "case.c07.objective.open_db", ObjectiveType.OpenApp, AppIds.Residents),
                Objective("obj_view_404", "case.c07.objective.view_404", ObjectiveType.ViewRecord, "res_404"),
                Objective("obj_chairman_call", "case.c07.objective.chairman_call", ObjectiveType.CallCharacter, "D_N4_CHAIRMAN_DELETE")
            };
            c.decisions = new[]
            {
                Decision("dec_delete_record", "case.c07.decision.delete", "case.c07.result.delete",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Flag(FlagIds.Db404Deleted, true),
                    ConsequenceDefinition.Stat(StatIds.Performance, 4, "reason.obeyed_chairman"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, -2, "reason.obeyed_chairman"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -3, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, -2, "reason.wrong_report")),

                Decision("dec_export_then_delete", "case.c07.decision.export", "case.c07.result.export",
                    DecisionQuality.Correct, new[] { "E11_404_RESIDENT_DATABASE" }, null,
                    ConsequenceDefinition.Flag(FlagIds.Db404Exported, true),
                    ConsequenceDefinition.Flag(FlagIds.Db404Deleted, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 3, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.correct_report"),
                    ConsequenceDefinition.Achievement(AchievementIds.Backup)),

                Decision("dec_refuse_delete", "case.c07.decision.refuse", "case.c07.result.refuse",
                    DecisionQuality.Correct, new[] { "E11_404_RESIDENT_DATABASE" }, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 4, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 3, "reason.refused_chairman"),
                    ConsequenceDefinition.Stat(StatIds.Performance, -4, "reason.refused_chairman"),
                    ConsequenceDefinition.Achievement(AchievementIds.Backup))
            };
            c.failSafe = FailSafe(At(2, 0), "E11_404_RESIDENT_DATABASE", "case.c07.failsafe.notify");
            c.analyticsName = "case_c07_404_database";
            return c;
        }

        /// <summary>C08 - Sunja's testimony (GDD 9.5). Depends on how night 1 treated her.</summary>
        static CaseDefinition BuildC08()
        {
            var c = NewCase("C08", 4, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(23, 10);
            // GDD 9.5: she only invites the player up if she is well.
            c.startConditions = new[] { ConditionDefinition.Flag(FlagIds.SunjaHealthy, true) };
            c.evidenceIds = new[] { "E13_SUNJA_TESTIMONY", "E12_FAMILY_PHOTO" };
            c.objectives = new[]
            {
                Objective("obj_visit_303", "case.c08.objective.visit_303", ObjectiveType.EnterZone, ZoneIds.Floor03),
                Objective("obj_talk_sunja", "case.c08.objective.talk_sunja", ObjectiveType.CallCharacter, "D_N4_SUNJA_TESTIMONY")
            };
            c.decisions = new[]
            {
                Decision("dec_record_testimony", "case.c08.decision.record", "case.c08.result.record",
                    DecisionQuality.Correct, new[] { "E13_SUNJA_TESTIMONY" }, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 4, "reason.correct_report")),

                Decision("dec_keep_private", "case.c08.decision.private", "case.c08.result.private",
                    DecisionQuality.Partial, null, null,
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 2, "reason.partial_report"))
            };
            c.failSafe = FailSafe(At(3, 0), "E13_SUNJA_TESTIMONY", "case.c08.failsafe.notify");
            c.analyticsName = "case_c08_sunja";
            return c;
        }

        /// <summary>C09 - the call from 404 (GDD 9.5).</summary>
        static CaseDefinition BuildC09()
        {
            var c = NewCase("C09", 4, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.PhoneCall;
            c.triggerTarget = "D_N4_CALL_404";
            c.startWindowBegin = At(0, 25);
            c.evidenceIds = new[] { "E14_FIRE_DOOR_LOCK_LOG" };
            c.objectives = new[]
            {
                Objective("obj_answer_404", "case.c09.objective.answer", ObjectiveType.CallCharacter, "D_N4_CALL_404"),
                Objective("obj_open_drawer", "case.c09.objective.open_drawer", ObjectiveType.AcquireEvidence, "E14_FIRE_DOOR_LOCK_LOG")
            };
            c.decisions = new[]
            {
                Decision("dec_log_call", "case.c09.decision.log", "case.c09.result.log",
                    DecisionQuality.Correct, new[] { "E14_FIRE_DOOR_LOCK_LOG" }, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 2, "reason.correct_report")),

                Decision("dec_ignore_call", "case.c09.decision.ignore", "case.c09.result.ignore",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, -2, "reason.wrong_report"))
            };
            c.failSafe = FailSafe(At(3, 0), "E14_FIRE_DOOR_LOCK_LOG", "case.c09.failsafe.notify");
            c.analyticsName = "case_c09_call_from_404";
            return c;
        }

        /// <summary>C10 - power distribution (GDD 9.6). Three of five circuits may stay on.</summary>
        static CaseDefinition BuildC10()
        {
            var c = NewCase("C10", 5, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(22, 40);
            c.objectives = new[]
            {
                Objective("obj_open_power", "case.c10.objective.open_power", ObjectiveType.OpenApp, AppIds.Facility)
            };
            c.decisions = new[]
            {
                Decision("dec_life_first", "case.c10.decision.life_first", "case.c10.result.life_first",
                    DecisionQuality.Correct, null,
                    new[]
                    {
                        ConditionDefinition.Circuit(CircuitIds.Heating),
                        ConditionDefinition.Circuit(CircuitIds.Elevator)
                    },
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, 6, "reason.life_first"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 6, "reason.life_first"),
                    ConsequenceDefinition.Flag(FlagIds.SunjaHealthy, true),
                    ConsequenceDefinition.Achievement(AchievementIds.PowerManager)),

                Decision("dec_records_first", "case.c10.decision.records_first", "case.c10.result.records_first",
                    DecisionQuality.Partial, null,
                    new[] { ConditionDefinition.Circuit(CircuitIds.RecordServer) },
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.records_first"),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -5, "reason.records_first"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3, "reason.records_first")),

                Decision("dec_watch_first", "case.c10.decision.watch_first", "case.c10.result.watch_first",
                    DecisionQuality.Partial, null,
                    new[] { ConditionDefinition.Circuit(CircuitIds.Cctv) },
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -2, "reason.watch_first"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.watch_first")),

                Decision("dec_no_plan", "case.c10.decision.no_plan", "case.c10.result.no_plan",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -10, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -5, "reason.wrong_report"),
                    ConsequenceDefinition.Flag(FlagIds.SunjaHealthy, false))
            };
            c.failSafe = FailSafe(At(1, 0), null, "case.c10.failsafe.notify");
            c.analyticsName = "case_c10_power";
            return c;
        }

        /// <summary>C11 - the break-in at the records room (GDD 9.6).</summary>
        static CaseDefinition BuildC11()
        {
            var c = NewCase("C11", 5, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(23, 20);
            c.evidenceIds = new[] { "E15_SMOKE_DEVICE", "E16_CHAIRMAN_ACCESS_LOG", "E17_2009_CCTV_REPLAY" };
            c.objectives = new[]
            {
                Objective("obj_check_cam10", "case.c11.objective.check_cam10", ObjectiveType.ViewCctvChannel, "CAM-10"),
                Objective("obj_enter_archive", "case.c11.objective.enter_archive", ObjectiveType.EnterZone, ZoneIds.Archive),
                Objective("obj_smoke_device", "case.c11.objective.smoke_device", ObjectiveType.AcquireEvidence, "E15_SMOKE_DEVICE"),
                Objective("obj_chairman_log", "case.c11.objective.chairman_log", ObjectiveType.AcquireEvidence, "E16_CHAIRMAN_ACCESS_LOG"),
                Objective("obj_witness_replay", "case.c11.objective.witness_replay", ObjectiveType.ViewCctvChannel, "CAM-04", true)
            };
            c.decisions = new[]
            {
                Decision("dec_document_breakin", "case.c11.decision.document", "case.c11.result.document",
                    DecisionQuality.Correct, new[] { "E15_SMOKE_DEVICE", "E16_CHAIRMAN_ACCESS_LOG" }, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 3, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 3, "reason.correct_report")),

                Decision("dec_lock_doors", "case.c11.decision.lock_doors", "case.c11.result.lock_doors",
                    DecisionQuality.Partial, null, null,
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, 3, "reason.partial_report"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.partial_report")),

                Decision("dec_treat_as_alarm", "case.c11.decision.false_alarm", "case.c11.result.false_alarm",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -2, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.Performance, -3, "reason.wrong_report"))
            };
            c.failSafe = FailSafe(At(3, 0), "E16_CHAIRMAN_ACCESS_LOG", "case.c11.failsafe.notify");
            c.analyticsName = "case_c11_archive_breakin";
            return c;
        }

        /// <summary>C12 - where Dongsik is (GDD 9.6).</summary>
        static CaseDefinition BuildC12()
        {
            var c = NewCase("C12", 5, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(0, 50);
            c.evidenceIds = new[] { "E18_DONGSIK_LOCATION" };
            c.objectives = new[]
            {
                Objective("obj_follow_signal", "case.c12.objective.follow_signal", ObjectiveType.EnterZone, ZoneIds.ServicePassage),
                Objective("obj_locate_dongsik", "case.c12.objective.locate", ObjectiveType.AcquireEvidence, "E18_DONGSIK_LOCATION")
            };
            c.decisions = new[]
            {
                Decision("dec_call_fire_service", "case.c12.decision.call_fire", "case.c12.result.call_fire",
                    DecisionQuality.Correct, new[] { "E18_DONGSIK_LOCATION" }, null,
                    ConsequenceDefinition.Flag(FlagIds.DongsikLocated, true),
                    ConsequenceDefinition.Flag(FlagIds.FireDepartmentCalled, true),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, 5, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 5, "reason.correct_report")),

                Decision("dec_log_malfunction", "case.c12.decision.malfunction", "case.c12.result.malfunction",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, -1, "reason.obeyed_chairman"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -6, "reason.wrong_report"))
            };
            c.failSafe = FailSafe(At(3, 30), "E18_DONGSIK_LOCATION", "case.c12.failsafe.notify");
            c.analyticsName = "case_c12_dongsik_location";
            return c;
        }

        /// <summary>C13 - the final report (GDD 9.7 step 1).</summary>
        static CaseDefinition BuildC13()
        {
            var c = NewCase("C13", 6, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(22, 0);
            c.objectives = new[]
            {
                Objective("obj_open_board", "case.c13.objective.open_board", ObjectiveType.OpenApp, AppIds.Evidence)
            };

            // GDD 9.7 requires 404 DB + 2009 bill + fire-door log + chairman log, plus EITHER
            // Sunja's testimony OR the fire-call tape. Two Correct variants express the "or".
            var core = new[]
            {
                "E11_404_RESIDENT_DATABASE", "E20_2009_MAINTENANCE_BILL",
                "E14_FIRE_DOOR_LOCK_LOG", "E16_CHAIRMAN_ACCESS_LOG"
            };

            c.decisions = new[]
            {
                Decision("dec_full_report_testimony", "case.c13.decision.full_testimony", "case.c13.result.full",
                    DecisionQuality.Correct,
                    Concat(core, new[] { "E13_SUNJA_TESTIMONY" }), null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 3, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.Performance, 10, "reason.correct_report")),

                Decision("dec_full_report_tape", "case.c13.decision.full_tape", "case.c13.result.full",
                    DecisionQuality.Correct,
                    Concat(core, new[] { "E05_FIRE_CALL_TAPE_A" }), null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 3, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.Performance, 10, "reason.correct_report")),

                Decision("dec_suspicion_report", "case.c13.decision.suspicion", "case.c13.result.suspicion",
                    DecisionQuality.Partial, null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.partial_report"))
            };
            c.failSafe = FailSafe(At(3, 0), null, "case.c13.failsafe.notify");
            c.analyticsName = "case_c13_final_report";
            return c;
        }

        /// <summary>C14 - rescue at 404 and the final submission (GDD 9.7 steps 2-6).</summary>
        static CaseDefinition BuildC14()
        {
            var c = NewCase("C14", 6, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(22, 20);
            c.evidenceIds = new[] { "E19_ORIGINAL_LEDGER", "E21_HYDRAULIC_CUTTER" };
            c.objectives = new[]
            {
                Objective("obj_evacuation", "case.c14.objective.evacuation", ObjectiveType.CallCharacter, "D_N6_EVACUATION"),
                Objective("obj_get_cutter", "case.c14.objective.get_cutter", ObjectiveType.AcquireEvidence, "E21_HYDRAULIC_CUTTER"),
                Objective("obj_enter_404", "case.c14.objective.enter_404", ObjectiveType.EnterZone, ZoneIds.Unit404),
                Objective("obj_rescue", "case.c14.objective.rescue", ObjectiveType.CallCharacter, "D_N6_DONGSIK_RESCUE"),
                Objective("obj_confront", "case.c14.objective.confront", ObjectiveType.CallCharacter, "D_N6_CHAIRMAN_CONFRONT"),
                Objective("obj_ledger", "case.c14.objective.ledger", ObjectiveType.AcquireEvidence, "E19_ORIGINAL_LEDGER", true)
            };
            c.decisions = new[]
            {
                Decision("dec_submit_police", "case.c14.decision.police", "case.c14.result.police",
                    DecisionQuality.Correct, null, null,
                    ConsequenceDefinition.Flag(FlagIds.SubmittedToPolice, true),
                    ConsequenceDefinition.Flag(FlagIds.EvidencePublished, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.published")),

                Decision("dec_submit_media", "case.c14.decision.media", "case.c14.result.media",
                    DecisionQuality.Correct, null, null,
                    ConsequenceDefinition.Flag(FlagIds.SubmittedToMedia, true),
                    ConsequenceDefinition.Flag(FlagIds.EvidencePublished, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 2, "reason.published"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3, "reason.published")),

                Decision("dec_submit_residents", "case.c14.decision.residents", "case.c14.result.residents",
                    DecisionQuality.Partial, null, null,
                    ConsequenceDefinition.Flag(FlagIds.SubmittedToResidents, true),
                    ConsequenceDefinition.Flag(FlagIds.EvidencePublished, true),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 4, "reason.published")),

                Decision("dec_submit_union", "case.c14.decision.union", "case.c14.result.union",
                    DecisionQuality.Partial, null, null,
                    ConsequenceDefinition.Flag(FlagIds.SubmittedToUnion, true),
                    ConsequenceDefinition.Stat(StatIds.Performance, 5, "reason.internal_only")),

                Decision("dec_delete_everything", "case.c14.decision.delete", "case.c14.result.delete",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Flag(FlagIds.SubmissionDeleted, true),
                    ConsequenceDefinition.Flag(FlagIds.RecordsBurned, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -10, "reason.deleted_everything"))
            };
            c.failSafe = FailSafe(At(4, 30), null, "case.c14.failsafe.notify");
            c.analyticsName = "case_c14_rescue";
            return c;
        }

        // ---- routine tasks for nights 2-6 (GDD 11.4) -------------------------

        /// <summary>
        /// Routine tasks are timed to land on top of the night's main beats, not between them.
        ///
        /// They used to be spaced out politely - one thing every five minutes, each waiting for
        /// the last to finish - and a shift with no two demands at once is a queue, not a job.
        /// Since routine tasks have no dependencies of their own, moving them onto the case
        /// beats is the cheapest pressure in the game: the player has to decide what waits.
        /// The interphone at the front door and the evidence on the eighth floor are in
        /// different rooms, and there is one of the player.
        /// </summary>
        static CaseDefinition[] BuildLateTasks()
        {
            var tasks = new[]
            {
                // Night 2: onto the CAM-02 contradiction window (22:18-22:40) and C04.
                //
                // T03 used to be scheduled here as well as on night 1, which made it the only
                // duplicated case id in the game. Nothing broke visibly, and that is the
                // interesting part: ContentDatabase keys cases by id, so the second
                // registration overwrote the first and one of the two nights quietly lost its
                // water leak. Which night lost it depended on the order the seed built in -
                // BuildRemainingTasks runs after BuildLateTasks, so night 1 won and night 2's
                // copy never existed at runtime.
                //
                // Night 1 is the one the design asks for (GDD 9.2 has listed it there since
                // v1.0, and it is night 1's relief valve), so the dead copy goes. The loader
                // now records id collisions and Validate Content reports them.
                Task("T05", 2, At(22, 34), "access", AppIds.Access,
                     StatIds.BuildingSafety, 3, -4),

                // Night 3: onto the chairman's call (22:35) and Jiwoo's (23:00).
                Task("T07", 3, At(22, 35), "cctv", "CAM-11",
                     StatIds.BuildingSafety, 4, -5),
                Task("T06", 3, At(23, 5), "cctv", "CAM-08",
                     StatIds.BuildingSafety, 4, -8),

                // Night 4: onto the chairman's deletion call (22:20) and Sunja's window (23:10).
                Task("T10", 4, At(22, 22), "facility", AppIds.Facility,
                     StatIds.CommunityTrust, 4, -5),
                Task("T11", 4, At(23, 15), "cctv", "CAM-12",
                     StatIds.CommunityTrust, 3, -4),

                // Night 5: onto the search of the records room (C11, 23:20).
                Task("T12", 5, At(23, 26), "facility", AppIds.Facility,
                     StatIds.BuildingSafety, 4, -4),

                // Night 6: onto the evacuation decision (C14, 22:20).
                Task("T16", 6, At(22, 25), "residents", AppIds.Residents,
                     StatIds.CommunityTrust, 5, -6)
            };

            // Spec 32: at least eight routine wrong answers have to become something the
            // caretaker walks into on a later night. What follows is the four of them that
            // live in the table above; T01, T03, T08 and T18 are given theirs where they are
            // built. Every one is deferred, because the point is not tonight's penalty.
            foreach (var definition in tasks)
            {
                switch (definition.caseId)
                {
                    // T05: the entrance stays on AUTO, so the lobby keeps letting people in.
                    case "T05":
                        AddToWrong(definition,
                            Later(ConsequenceDefinition.Stat(StatIds.UnauthorizedEntryRisk, 1,
                                                             "reason.entrance_left_open")),
                            Later(ConsequenceDefinition.Notify("notify.t05.door_still_auto")));
                        break;

                    // T06: spec 16 T06 prices the forced reboot at eight points of the
                    // building, and the building is what the last night is made of.
                    case "T06":
                        AddToWrong(definition,
                            Later(ConsequenceDefinition.Stat(StatIds.BuildingSafety, -8,
                                                             "reason.forced_lift_reboot")),
                            Later(ConsequenceDefinition.Notify("notify.t06.lift_still_out")));
                        break;

                    // T10: the heating never came back. Spec 16 T10 only counts if she was
                    // walking unaided to begin with, which is how the flag is read rather
                    // than how it is set.
                    case "T10":
                        AddToWrong(definition,
                            Later(ConsequenceDefinition.Flag(FlagIds.SunjaCritical)),
                            Later(ConsequenceDefinition.Notify("notify.t10.still_cold")));
                        break;

                    // T11: the roof door was left to itself and Ji-woo will not be knocking
                    // on the sixth floor when it matters.
                    case "T11":
                        AddToWrong(definition,
                            Later(ConsequenceDefinition.Flag(FlagIds.JiwooRisk)),
                            Later(ConsequenceDefinition.Notify("notify.t11.roof_door_again")));
                        break;
                }
            }

            return tasks;
        }

        /// <summary>
        /// The rest of the GDD 11.4 table. Four of these carry a real payload rather than a
        /// stat nudge: mishandling them is how the player loses a thread instead of a number.
        /// </summary>
        static CaseDefinition[] BuildRemainingTasks()
        {
            // T04 noise complaint - a warning on the record, or a feud between two units.
            // Lands with the late courier at the door (23:20).
            var t04 = Task("T04", 1, At(23, 22), "app", AppIds.Phone,
                           StatIds.CommunityTrust, 3, -4);

            // T03 water leak on night 1. GDD 9.2 has listed this among night 1's required
            // tasks since v1.0 and the string table has carried its text just as long, but no
            // case ever scheduled it.
            //
            // It matters beyond closing that gap. Taking T02 off night 1 (v1.5) took a
            // case-closing relief with it, and Simulate Nights caught what that did: an
            // average night 1 finished at 64 pressure, harder than nights 2 through 5,
            // because night 1 has the fewest cases and so the fewest ways to push the night
            // back. A teaching night may not be the second-hardest night in the game. T03
            // gives the relief back, and gives it back as the task the design already asked
            // for rather than as a number.
            //
            // 00:10 puts it after the eighth-floor patrol, so the back half of the shift has
            // something in it besides waiting for the reserve to fall.
            var t03 = Task("T03", 1, At(0, 10), "app", AppIds.Facility,
                           StatIds.BuildingSafety, 3, -3);
            // Spec 16 T03: left alone the line keeps running, and the fifth floor stays wet
            // for the rest of the game.
            AddToWrong(t03, Later(ConsequenceDefinition.Flag(FlagIds.LeakUnrepaired)),
                            Later(ConsequenceDefinition.FloorRisk(Gameplay.FloorPlan.F5, 1)),
                            Later(ConsequenceDefinition.Notify("notify.t03.ceiling_still_wet")));

            // T08 missing pet - closing the basement door is what actually finds it.
            var t08 = Task("T08", 2, At(23, 10), "cctv", "CAM-09",
                           StatIds.CommunityTrust, 4, -4);
            // Spec 16 T08: the basement door was never shut, so it got out.
            AddToWrong(t08, Later(ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3,
                                                             "reason.pet_lost_outside")),
                            Later(ConsequenceDefinition.Notify("notify.t08.notice_on_the_door")));

            // T09 taxi at the gate - a temporary vehicle pass, or a stranger waved through.
            var t09 = Task("T09", 3, At(0, 30), "app", AppIds.Residents,
                           StatIds.Performance, 3, -3);
            AddToWrong(t09, ConsequenceDefinition.Stat(StatIds.BuildingSafety, -4, "reason.let_stranger_in"));

            // T18 lost key - a temporary card issued properly, or one the chair can borrow.
            var t18 = Task("T18", 3, At(23, 40), "app", AppIds.Access,
                           StatIds.Performance, 3, -2);
            AddToWrong(t18, ConsequenceDefinition.Stat(StatIds.BuildingSafety, -4, "reason.key_uncontrolled"),
                            ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -1, "reason.key_uncontrolled"),
                            // Spec 16 T18: the old card was never killed and it still opens
                            // doors. Night 5 is when somebody else uses it.
                            Later(ConsequenceDefinition.Stat(StatIds.ChairmanAccessEase, 1,
                                                             "reason.key_uncontrolled")),
                            Later(ConsequenceDefinition.Notify("notify.t18.old_key_used")));

            // T13 misdelivered mail - GDD 11.4: handling it wrong is how the 404 clue is missed.
            // Lands on Sunja's testimony window, so the player has to choose what to miss.
            var t13 = Task("T13", 4, At(23, 12), "app", AppIds.Access,
                           StatIds.CommunityTrust, 2, -2);
            AddToCorrect(t13, ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.found_404_clue"),
                              ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.found_404_clue"));

            // T15 water meter anomaly - the same table that exposes 404's consumption.
            // Lands on the call from 404 (00:30), which is the one call worth answering.
            var t15 = Task("T15", 4, At(0, 32), "app", AppIds.Facility,
                           StatIds.BuildingSafety, 3, -3);
            AddToCorrect(t15, ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.found_404_clue"),
                              ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.found_404_clue"));

            // T14 parking alarm - an unchecked zone is how someone gets in on night 5.
            // Lands on the power-budget decision (C10, 22:40).
            var t14 = Task("T14", 5, At(22, 44), "cctv", "CAM-09",
                           StatIds.BuildingSafety, 4, -5);
            AddToWrong(t14, ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.opened_a_route"));

            // T17 camera fault - leaving it down is how the archive break-in goes unseen.
            // Lands on the search for Dongsik (C12, 00:50).
            var t17 = Task("T17", 5, At(0, 54), "app", AppIds.Cctv,
                           StatIds.BuildingSafety, 3, -3);
            AddToWrong(t17, ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -1, "reason.blind_spot"));

            return new[] { t03, t04, t08, t09, t18, t13, t15, t14, t17 };
        }

        static void AddToCorrect(CaseDefinition definition, params ConsequenceDefinition[] extra)
        {
            AppendConsequences(definition, DecisionQuality.Correct, extra);
        }

        static void AddToWrong(CaseDefinition definition, params ConsequenceDefinition[] extra)
        {
            AppendConsequences(definition, DecisionQuality.Wrong, extra);
        }

        static void AppendConsequences(CaseDefinition definition, DecisionQuality quality,
                                       ConsequenceDefinition[] extra)
        {
            for (int i = 0; i < definition.decisions.Length; i++)
            {
                if (definition.decisions[i].quality != quality) continue;
                definition.decisions[i].consequences = Concat(definition.decisions[i].consequences, extra);
            }
        }

        /// <summary>
        /// Routine tasks share one shape: look something up, then choose to handle it or not.
        /// They exist to make the shift feel like a job, so they stay deliberately small.
        /// </summary>
        static CaseDefinition Task(string id, int night, int startSecond, string objectiveKind,
                                   string targetId, string statId, int correctDelta, int wrongDelta)
        {
            var c = NewCase(id, night, CaseKind.RoutineTask, Priority.P1);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = startSecond;

            var lower = id.ToLowerInvariant();
            var type = objectiveKind == "cctv" ? ObjectiveType.ViewCctvChannel : ObjectiveType.OpenApp;

            c.objectives = new[]
            {
                Objective("obj_check", "task." + lower + ".objective.check", type, targetId)
            };
            c.decisions = new[]
            {
                Decision("dec_handle", "task." + lower + ".decision.handle", "task." + lower + ".result.handle",
                    DecisionQuality.Correct, null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.routine_correct"),
                    ConsequenceDefinition.Stat(statId, correctDelta, "reason.routine_correct")),

                Decision("dec_defer", "task." + lower + ".decision.defer", "task." + lower + ".result.defer",
                    DecisionQuality.Wrong, null, null,
                    ConsequenceDefinition.Stat(statId, wrongDelta, "reason.routine_wrong"))
            };
            c.failSafe = FailSafe(At(4, 0), null, "task." + lower + ".failsafe.notify");
            c.analyticsName = "task_" + lower;
            return c;
        }

        // =====================================================================
        // Endings (GDD 10)
        // =====================================================================

        /// <summary>
        /// The four grades and the failure result (v5.0 8.3).
        ///
        /// These no longer carry conditions. v5.0 8.2 grades the campaign arithmetically on
        /// the caretaker's own HP and SAN, so the choosing happens in EndingService and these
        /// definitions supply what the screen shows - which is the honest division: an
        /// algorithm the document writes out in full should be code, not a pile of conditions
        /// that approximates it.
        ///
        /// What the player did still matters. v5.0 8.3 is explicit that ArchiveIntegrity,
        /// CommunityTrust, rescuing Dong-sik and preserving Harin's record change the epilogue
        /// and how much of the truth becomes public, rather than which of the four they get.
        /// EndingService.EpilogueModifiers is where that is read.
        ///
        /// The evaluation order is kept only so the gallery lists them best-first.
        /// </summary>
        public static EndingDefinition[] BuildEndings()
        {
            return new[]
            {
                Ending(EndingIds.A_RecordedPeople, 10, AchievementIds.TrueRecord, EndingAftermath.None),
                Ending(EndingIds.B_SafeSilence,    20, AchievementIds.Silence,    EndingAftermath.None),
                Ending(EndingIds.C_Erased404,      30, AchievementIds.Erased,     EndingAftermath.None),
                Ending(EndingIds.D_CommunityCollapse, 40, AchievementIds.Collapse, EndingAftermath.CorruptTitle),

                // Not a grade and not in the gallery: the campaign reached its last scene in a
                // state that could not carry it (v5.0 8.1).
                Ending(EndingIds.NoEnding, 90, null, EndingAftermath.None)
            };
        }

        static EndingDefinition Ending(string id, int order, string achievementId, EndingAftermath aftermath)
        {
            var ending = ScriptableObject.CreateInstance<EndingDefinition>();
            ending.name = id;
            ending.endingId = id;
            ending.titleKey = "ending." + id.ToLowerInvariant() + ".title";
            ending.summaryKey = "ending." + id.ToLowerInvariant() + ".summary";
            ending.bodyKey = "ending." + id.ToLowerInvariant() + ".body";
            ending.lockedHintKey = "ending.locked";
            ending.achievementId = achievementId;
            ending.evaluationOrder = order;
            ending.aftermath = aftermath;
            return ending;
        }
    }
}
