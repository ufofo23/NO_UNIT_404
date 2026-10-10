using System.Collections.Generic;
using UnityEngine;
using NO404.Cases;
using NO404.CCTV;
using NO404.Core;
using NO404.Dialogue;
using NO404.Evidence;
using NO404.Facility;
using NO404.Residents;
using NO404.Visitors;

namespace NO404.ContentData
{
    /// <summary>
    /// Authored content for night 1 - the cold open (C00) and the shift it opens
    /// (C01, C02, T01) from GDD 9.1 / 9.2 and 11.3 / 11.4.
    ///
    /// It is written in code rather than as .asset files so a fresh clone is playable with
    /// zero manual wiring. "Tools > NO404 > Data > Bake Seed Content To Assets" turns this
    /// into inspector-editable ScriptableObjects when the designer wants to take over.
    /// Nights 2-6 extend this file (or the baked assets) - no system code changes required.
    /// </summary>
    public static partial class SeedContent
    {
        // Game seconds. The shift runs 22:00 (79200) -> 06:00 (108000).
        public const int T2200 = 22 * 3600;
        public const int T2210 = 22 * 3600 + 10 * 60;
        public const int T2220 = 22 * 3600 + 20 * 60;
        public const int T2240 = 22 * 3600 + 40 * 60;
        public const int T2250 = 22 * 3600 + 50 * 60;
        public const int T2300 = 23 * 3600;
        public const int T2330 = 23 * 3600 + 30 * 60;
        public const int T0000 = 24 * 3600;
        public const int T0100 = 25 * 3600;
        public const int T0300 = 27 * 3600;
        public const int T0600 = 30 * 3600;

        public sealed class Bundle
        {
            public CaseDefinition[] Cases;
            public EvidenceDefinition[] Evidence;
            public ResidentDefinition[] Residents;
            public DialogueDefinition[] Dialogues;
            public VisitorDefinition[] Visitors;
            public CctvChannelDefinition[] Channels;
            public AnomalyDefinition[] Anomalies;
            public MeterSeries[] MeterSeries;
            public Phone.PhoneCallDefinition[] PhoneCalls;
            public Endings.EndingDefinition[] Endings;
            public Manual.ManualPage[] ManualPages;
            public Anomalies.ManualEventDefinition[] ManualEvents;
            public Anomalies.AnomalyToolDefinition[] AnomalyTools;
        }

        /// <summary>Joins the night 0-1 seed with the nights 2-6 seed.</summary>
        static T[] Concat<T>(T[] a, T[] b)
        {
            var list = new List<T>(a ?? new T[0]);
            list.AddRange(b ?? new T[0]);
            return list.ToArray();
        }

        public static Bundle Build()
        {
            return new Bundle
            {
                Residents = Concat(BuildResidents(), BuildLateResidents()),
                Evidence = Concat(Concat(Concat(BuildEvidence(), BuildLateEvidence()), BuildV5Evidence()),
                                  BuildV51Evidence()),
                Visitors = Concat(Concat(BuildVisitors(), BuildLateVisitors()), BuildDoorstepVisitors()),
                Dialogues = Concat(Concat(BuildDialogues(), BuildLateDialogues()), BuildDoorstepDialogues()),
                // v5.0 10-15. The per-night cases C00-C14 and the routine tasks T01-T18 were
                // removed with this change: v5.0 replaces the fixed nightly roster with a
                // fifteen-quest pool per night, and the old cases were authored against a
                // structure that no longer exists. The manual events M01-M18 and the anomalous
                // tools A01-A05 were kept - they are candidates for the pool v5.0 4 wants
                // filled next, not casualties of it.
                Cases = Concat(BuildV5Mains(), BuildV51Subquests()),
                Channels = BuildChannels(),
                Anomalies = BuildAnomalies(),
                MeterSeries = BuildMeterSeries(),
                PhoneCalls = BuildPhoneCalls(),
                Endings = BuildEndings(),
                ManualPages = BuildManualPages(),
                ManualEvents = BuildManualEvents(),
                AnomalyTools = BuildAnomalyTools()
            };
        }

        // =====================================================================
        // Residents (GDD 5, 16.10)
        // =====================================================================

        public static ResidentDefinition[] BuildResidents()
        {
            return new[]
            {
                Resident("res_101", "101", "resident.101.name", "0114", "RES-101",
                    ResidentStatus.Resident, Vehicles(Vehicle("12가 3401", true)), null,
                    Notes(Note("resident.101.note.night_shift", false))),

                Resident("res_403", "403", "resident.403.name", "0000", "",
                    ResidentStatus.Vacant, null, null,
                    Notes(Note("resident.403.note.vacant_since_2009", false))),

                Resident("res_405", "405", "resident.405.name", "7712", "RES-405",
                    ResidentStatus.Resident, Vehicles(Vehicle("31나 7712", true)), null, null),

                Resident("res_303", "303", "resident.303.name", "2208", "RES-303",
                    ResidentStatus.Resident, null,
                    Visitors_(Visitor("vis_caregiver", "visitor.caregiver.name", "visitor.purpose.care", true)),
                    Notes(Note("resident.303.note.heart_condition", true),
                          Note("resident.303.note.hard_of_hearing", false))),

                Resident("res_304", "304", "resident.304.name", "0000", "",
                    ResidentStatus.Vacant, null, null,
                    Notes(Note("resident.304.note.vacant_since_march", false))),

                Resident("res_202", "202", "resident.202.name", "3391", "RES-202",
                    ResidentStatus.Resident, Vehicles(Vehicle("48다 3391", true)),
                    Visitors_(Visitor("vis_minseo", "resident.202.name", "visitor.purpose.night_shift", true)),
                    Notes(Note("resident.202.note.nurse_schedule", false))),

                Resident("res_602", "602", "resident.602.name", "5540", "RES-602",
                    ResidentStatus.Resident, null, null,
                    Notes(Note("resident.602.note.rooftop_warning", true))),

                Resident("res_1501", "1501", "resident.1501.name", "0001", "RES-1501",
                    ResidentStatus.Resident, Vehicles(Vehicle("01가 0001", true)), null,
                    Notes(Note("resident.1501.note.chairman", false))),

                Resident("res_dongsik", "B01", "resident.dongsik.name", "5812", "STAFF-02",
                    ResidentStatus.Staff, null, null,
                    Notes(Note("resident.dongsik.note.missing", true))),

                // GDD 16.10: the 404 row is styled exactly like every other record.
                // Only lastSyncDate is abnormal, and it stays out of search until night 4.
                Hidden404()
            };
        }

        /// <summary>
        /// The household that is not on any list (v5.1 13, scenario "NIGHT 4 - 이름").
        ///
        /// Three names and a move-in date, on a row the sync made tonight and whose fields
        /// were last touched in 2009. This is the one screen in the game where the younger
        /// daughter's full name is written out (v5.1 0.16): before the row is revealed the
        /// list shows the masked surname the night-1 bill used and nothing else.
        ///
        /// What is done about the row is done here, on the row. Print, export and delete are
        /// each offered once, while the case is open and nothing has been done yet; leaving
        /// all three alone is the fourth answer.
        /// </summary>
        static ResidentDefinition Hidden404()
        {
            var resident = Resident("res_404", "404", "resident.404.name", "1107", "RES-404",
                ResidentStatus.Unregistered, null, null,
                Notes(Note("resident.404.note.members", false),
                      Note("resident.404.note.move_in", false),
                      Note("resident.404.note.modified", false),
                      Note("resident.404.note.overdue", false)));

            resident.lastSyncDate = "2009-11-07";
            resident.hiddenUntilSync = true;
            resident.revealFlagId = FlagIds.Knows404;
            resident.glimpseNameKey = "resident.404.name_masked";
            resident.hideFlagId = FlagIds.Db404Deleted;
            resident.cardStatus = AccessCardStatus.None;
            // Opening the row is how the player takes a copy of it (case C07).
            resident.evidenceOnView = "E11_404_RESIDENT_DATABASE";

            var undecided = new[]
            {
                ConditionDefinition.CaseActive("N4-M01"),
                ConditionDefinition.ChoiceUnset(ChoiceIds.Db404Action)
            };

            resident.actions = new[]
            {
                new ResidentRecordAction
                {
                    actionId = "print", labelKey = "ui.residents.action.print", availability = undecided,
                    consequences = new[]
                    {
                        ConsequenceDefinition.Choice(ChoiceIds.Db404Action, "PRINT"),
                        ConsequenceDefinition.Evidence("EV_DB404_PRINT")
                    }
                },
                new ResidentRecordAction
                {
                    actionId = "export", labelKey = "ui.residents.action.export", availability = undecided,
                    consequences = new[]
                    {
                        ConsequenceDefinition.Choice(ChoiceIds.Db404Action, "EXPORT"),
                        ConsequenceDefinition.Flag(FlagIds.Db404Exported, true)
                    }
                },
                // Cannot be taken back, and takes effect now rather than when it is reported:
                // the row is gone from the screen the moment the button is pressed.
                new ResidentRecordAction
                {
                    actionId = "delete", labelKey = "ui.residents.action.delete", availability = undecided,
                    consequences = new[]
                    {
                        ConsequenceDefinition.Choice(ChoiceIds.Db404Action, "DELETE"),
                        ConsequenceDefinition.Flag(FlagIds.HarinRecordPreserved, false),
                        ConsequenceDefinition.Flag(FlagIds.Db404Deleted, true)
                    }
                }
            };

            return resident;
        }

        static ResidentDefinition Resident(string id, string unit, string nameKey, string phone, string card,
                                           ResidentStatus status, VehicleRecord[] vehicles,
                                           VisitorRecord[] visitors, ResidentNote[] notes)
        {
            var resident = ScriptableObject.CreateInstance<ResidentDefinition>();
            resident.name = "RES_" + unit;
            resident.residentId = id;
            resident.unitNumber = unit;
            resident.nameKey = nameKey;
            resident.householdKey = "resident." + unit + ".household";
            resident.phoneLast4 = phone;
            resident.cardId = card;
            resident.initialStatus = status;
            resident.cardStatus = string.IsNullOrEmpty(card) ? AccessCardStatus.None : AccessCardStatus.Active;
            resident.vehicles = vehicles ?? new VehicleRecord[0];
            resident.recurringVisitors = visitors ?? new VisitorRecord[0];
            resident.notes = notes ?? new ResidentNote[0];
            return resident;
        }

        static VehicleRecord[] Vehicles(params VehicleRecord[] items) { return items; }
        static VehicleRecord Vehicle(string plate, bool registered)
        {
            return new VehicleRecord { plate = plate, modelKey = "vehicle.model.sedan", registered = registered };
        }

        static VisitorRecord[] Visitors_(params VisitorRecord[] items) { return items; }
        static VisitorRecord Visitor(string id, string nameKey, string purposeKey, bool preRegistered)
        {
            return new VisitorRecord
            {
                visitorId = id, nameKey = nameKey, purposeKey = purposeKey, preRegistered = preRegistered
            };
        }

        static ResidentNote[] Notes(params ResidentNote[] items) { return items; }
        static ResidentNote Note(string key, bool safetyCritical)
        {
            return new ResidentNote { noteKey = key, safetyCritical = safetyCritical };
        }

        // =====================================================================
        // Evidence (GDD 14.1)
        // =====================================================================

        public static EvidenceDefinition[] BuildEvidence()
        {
            var key404 = Evidence_("EV_KEY404", EvidenceType.PhysicalObject, "C00", true);
            var contract = Evidence_("EV_CONTRACT", EvidenceType.Document, "C00", false);
            var handover = Evidence_("EV_HANDOVER", EvidenceType.Document, "C00", false);

            var waterGraph = Evidence_("EV_304_WATER", EvidenceType.MeterGraph, "C01", true);
            var soundClip = Evidence_("EV_304_SOUND", EvidenceType.AudioRecording, "C01", false);
            var camSnap = Evidence_("EV_CAM06_SNAP", EvidenceType.CctvSnapshot, "C01", false);
            var doorPhoto = Evidence_("EV_404_DOOR", EvidenceType.Photo, "C01", true);

            var minseoId = Evidence_("EV_MINSEO_ID", EvidenceType.Document, "C02", false);
            var visitLog = Evidence_("EV_VISIT_LOG", EvidenceType.AccessLog, "C02", false);
            var parkingShot = Evidence_("EV_PARKING_SHOT", EvidenceType.CctvSnapshot, "T01", false);

            // Left on the office floor after the prologue's first anomaly (GDD 9.1).
            var redSlipper = Evidence_("EV_RED_SLIPPER", EvidenceType.PhysicalObject, "C00", false);

            // Meaningful pairs for the board (GDD 16.13). Wrong links are still allowed.
            Relate(waterGraph, soundClip, EvidenceRelation.Cause);
            Relate(soundClip, camSnap, EvidenceRelation.SameTime);
            Relate(waterGraph, camSnap, EvidenceRelation.SameTime);
            Relate(doorPhoto, key404, EvidenceRelation.LocationContradiction);
            Relate(minseoId, visitLog, EvidenceRelation.SamePerson);
            Relate(handover, key404, EvidenceRelation.Cause);

            return new[]
            {
                key404, contract, handover, waterGraph, soundClip, camSnap, doorPhoto,
                minseoId, visitLog, parkingShot, redSlipper
            };
        }

        static EvidenceDefinition Evidence_(string id, EvidenceType type, string caseId, bool archiveCritical)
        {
            var definition = ScriptableObject.CreateInstance<EvidenceDefinition>();
            definition.name = id;
            definition.evidenceId = id;
            definition.displayNameKey = "evidence." + id.ToLowerInvariant() + ".name";
            definition.descriptionKey = "evidence." + id.ToLowerInvariant() + ".desc";
            definition.type = type;
            definition.ownerCaseId = caseId;
            definition.archiveCritical = archiveCritical;
            definition.tags = new string[0];
            return definition;
        }

        static void Relate(EvidenceDefinition a, EvidenceDefinition b, EvidenceRelation relation)
        {
            a.validRelations = Append(a.validRelations,
                new EvidenceRelationRule { otherEvidenceId = b.evidenceId, relation = relation, meaningful = true });
            b.validRelations = Append(b.validRelations,
                new EvidenceRelationRule { otherEvidenceId = a.evidenceId, relation = relation, meaningful = true });
        }

        static T[] Append<T>(T[] source, T item)
        {
            var list = new List<T>(source ?? new T[0]) { item };
            return list.ToArray();
        }

        // =====================================================================
        // Visitors (GDD 13)
        // =====================================================================

        public static VisitorDefinition[] BuildVisitors()
        {
            var minseo = ScriptableObject.CreateInstance<VisitorDefinition>();
            minseo.name = "VIS_MINSEO";
            minseo.visitorId = "vis_minseo";
            minseo.nameKey = "visitor.minseo.name";
            minseo.purposeKey = "visitor.minseo.purpose";
            minseo.idCardNameKey = "visitor.minseo.id_name";
            // v5.1 N1-R01: she is here for 303, Lee Seon-ja. Not on tonight's booking list,
            // but eight weeks of visits say she is exactly who she says she is.
            minseo.targetUnit = "303";
            minseo.cameraId = "CAM-01";
            minseo.conversationId = "D_N1_MINSEO_ENTRY";
            minseo.nightIndex = 1;
            minseo.arrivalGameSecond = At(23, 5);
            minseo.correctAccess = VisitorAccessLevel.Escorted;
            minseo.destinationZone = ZoneIds.Floor03;
            minseo.expectedRoute = new[] { ZoneIds.Lobby, ZoneIds.Elevator, ZoneIds.Floor03 };
            minseo.checks = new[]
            {
                Check("visitor.check.badge_date", "visitor.check.value.badge_valid", false, AppIds.Residents),
                Check("visitor.check.id_name", "visitor.check.value.kim_minseo", false, AppIds.Residents),
                Check("visitor.check.recent_visits", "visitor.check.value.eight_weeks_303", false, AppIds.Access),
                Check("visitor.check.resident_call", "visitor.check.value.303_no_answer", false, AppIds.Residents)
            };
            minseo.onCorrect = new[]
            {
                ConsequenceDefinition.Stat(StatIds.CommunityTrust, 4, "reason.correct_visitor"),
                ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.correct_visitor"),
                ConsequenceDefinition.Flag(FlagIds.MinseoTrusted, true),
                ConsequenceDefinition.Evidence("EV_MINSEO_ID")
            };
            minseo.onWrong = new[]
            {
                ConsequenceDefinition.Stat(StatIds.CommunityTrust, -6, "reason.wrong_visitor"),
                ConsequenceDefinition.Stat(StatIds.Performance, -2, "reason.wrong_visitor")
            };
            minseo.onHold = new[]
            {
                ConsequenceDefinition.Stat(StatIds.CommunityTrust, -1, "reason.held_visitor")
            };

            // She is the first person the game asks the caretaker to read, and she is
            // deliberately readable: composed, straightforward, nothing behind the answers.
            // Whatever the player does to her here is what they will believe a normal caller
            // looks like for the rest of the week, so it has to be a fair sample.
            minseo.truth = VisitorTruth.Legitimate;
            minseo.breakingPoint = 85;
            minseo.tells = Tells(
                Tell("obs_annoyed", ReadChannel.Voice, TellWeight.Noise),
                Tell("obs_checks_street", ReadChannel.Camera, TellWeight.Noise),
                Tell("obs_corrects_you", ReadChannel.Voice, TellWeight.Innocent),
                Tell("obs_full_name", ReadChannel.Glass, TellWeight.Innocent));
            minseo.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.minseo.reply.ask_again", 10),
                Reply(PressureTactic.Silence, "visitor.minseo.reply.silence", 12),
                Reply(PressureTactic.ShowMe, "visitor.minseo.reply.show_me", 6, "obs_corrects_you"),
                Reply(PressureTactic.Reassure, "visitor.minseo.reply.reassure", -10));

            // The cold open's wrong caller (GDD 9.1). 22:09, nine minutes into the game, and
            // it is the first thing in the game that can be got wrong at a price - night 1 no
            // longer suppresses the pressure ladder, because night 0 is where that suppression
            // lived and night 0 is gone.
            var courier = ScriptableObject.CreateInstance<VisitorDefinition>();
            courier.name = "VIS_COURIER_LATE";
            courier.visitorId = "vis_courier_late";
            courier.nameKey = "visitor.courier.name";
            courier.purposeKey = "visitor.courier.purpose";
            courier.idCardNameKey = "visitor.courier.id_name";
            courier.targetUnit = "304";
            courier.cameraId = "CAM-01";
            courier.conversationId = "D_N1_COURIER_ENTRY";
            courier.nightIndex = 1;
            courier.arrivalGameSecond = At(22, 9);
            // 304 is vacant and the invoice unit does not match: the informed call is to keep
            // him outside altogether. He is also the first caller in the game who will use a
            // floor pass for something other than what he said - nine minutes in, on a night
            // when the cameras are dark and nothing will see him do it.
            courier.correctAccess = VisitorAccessLevel.Reject;
            courier.destinationZone = ZoneIds.Floor03;
            courier.expectedRoute = new[] { ZoneIds.Lobby, ZoneIds.Elevator, ZoneIds.Floor03 };
            courier.canDeviate = true;
            courier.deviationZone = ZoneIds.Floor04;
            courier.canBeEscorted = false;
            courier.checks = new[]
            {
                Check("visitor.check.target_unit", "visitor.check.value.unit_304_vacant", true, AppIds.Residents),
                Check("visitor.check.invoice", "visitor.check.value.invoice_mismatch", true, AppIds.Access),
                Check("visitor.check.delivery_hours", "visitor.check.value.after_hours", true, AppIds.Access),
                Check("visitor.check.vehicle", "visitor.check.value.no_vehicle_log", true, AppIds.Cctv)
            };
            courier.onCorrect = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, 4, "reason.correct_visitor"),
                ConsequenceDefinition.Stat(StatIds.BuildingSafety, 2, "reason.correct_visitor")
            };
            courier.onWrong = new[]
            {
                ConsequenceDefinition.Stat(StatIds.BuildingSafety, -5, "reason.wrong_visitor"),
                ConsequenceDefinition.Stat(StatIds.Performance, -3, "reason.wrong_visitor"),
                ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.wrong_visitor")
            };
            courier.onHold = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, -1, "reason.held_visitor")
            };

            // The first liar, nine minutes in, and he is meant to be catchable without any of
            // the apps working - the camera wall is still dark. Everything decisive about him
            // is on the interphone or through the lobby glass, which is the game teaching its
            // second route to an answer before it has taught its first.
            courier.truth = VisitorTruth.Deceptive;
            courier.breakingPoint = 60;
            courier.tells = Tells(
                Tell("obs_invoice_hidden", ReadChannel.Camera, TellWeight.Deception),
                Tell("obs_no_vehicle", ReadChannel.Camera, TellWeight.Deception),
                Tell("obs_apologises", ReadChannel.Voice, TellWeight.Noise),
                Tell("obs_gloves", ReadChannel.Camera, TellWeight.Noise),
                Tell("obs_empty_bag", ReadChannel.Glass, TellWeight.Deception),
                Tell("obs_rehearsed", ReadChannel.Voice, TellWeight.Deception, PressureTactic.AskAgain));
            courier.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.courier.reply.ask_again", 18, "obs_rehearsed"),
                Reply(PressureTactic.Confront, "visitor.courier.reply.confront", 30, null, AppIds.Residents),
                Reply(PressureTactic.Silence, "visitor.courier.reply.silence", 26),
                Reply(PressureTactic.ShowMe, "visitor.courier.reply.show_me", 32, "obs_invoice_hidden"),
                Reply(PressureTactic.Reassure, "visitor.courier.reply.reassure", -6));

            // ---- the cold open's first two callers (GDD 9.1) ----
            //
            // These two opened the old night 0 and now open the game itself, at 22:02 and
            // 22:05, on top of a dead camera wall. Both are legitimate: the player has to be
            // shown what an ordinary caller looks like before 22:09 asks them to spot one that
            // is not. Their ids still say n0 because content ids are what saves are written
            // against, and renaming them would buy nothing but a migration.

            var guest = ScriptableObject.CreateInstance<VisitorDefinition>();
            guest.name = "VIS_N0_GUEST_JIWOO";
            guest.visitorId = "vis_n0_guest_jiwoo";
            guest.nameKey = "visitor.n0_guest.name";
            guest.purposeKey = "visitor.n0_guest.purpose";
            guest.idCardNameKey = "visitor.n0_guest.id_name";
            guest.targetUnit = "602";
            guest.cameraId = "CAM-01";
            guest.conversationId = "D_N0_JIWOO_GATE";
            guest.nightIndex = 1;
            guest.arrivalGameSecond = At(22, 2);
            guest.correctAccess = VisitorAccessLevel.FloorPass;
            guest.destinationZone = ZoneIds.Floor06;
            guest.expectedRoute = new[] { ZoneIds.Lobby, ZoneIds.Elevator, ZoneIds.Floor06 };
            guest.checks = new[]
            {
                Check("visitor.check.pre_registered", "visitor.check.value.unit_602_expected", false, AppIds.Residents),
                Check("visitor.check.vehicle", "visitor.check.value.plate_match_602", false, AppIds.Cctv)
            };
            guest.onCorrect = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.correct_visitor"),
                ConsequenceDefinition.Stat(StatIds.CommunityTrust, 1, "reason.correct_visitor")
            };
            guest.onWrong = new[] { ConsequenceDefinition.Stat(StatIds.CommunityTrust, -2, "reason.wrong_visitor") };
            guest.onHold = new[] { ConsequenceDefinition.Stat(StatIds.Performance, -1, "reason.held_visitor") };

            guest.truth = VisitorTruth.Legitimate;
            guest.breakingPoint = 90;
            guest.tells = Tells(
                Tell("obs_annoyed", ReadChannel.Voice, TellWeight.Noise),
                Tell("obs_avoids_lens", ReadChannel.Camera, TellWeight.Noise),
                Tell("obs_corrects_you", ReadChannel.Voice, TellWeight.Innocent),
                // The first thing in the game that is only visible from the lobby, and it is
                // deliberately worth nothing: he has been at a bar. The walk has to be taught
                // before it is ever needed.
                Tell("obs_smells_of_drink", ReadChannel.Glass, TellWeight.Noise));
            guest.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.n0_guest.reply.ask_again", 8),
                Reply(PressureTactic.Silence, "visitor.n0_guest.reply.silence", 10),
                Reply(PressureTactic.ShowMe, "visitor.n0_guest.reply.show_me", 6, "obs_corrects_you"),
                Reply(PressureTactic.Reassure, "visitor.n0_guest.reply.reassure", -8));

            // Seo Jun-ho's first delivery - the box that returns as C03's headline case on
            // night 2. The label already does not match the resident it is corrected to.
            var prologueCourier = ScriptableObject.CreateInstance<VisitorDefinition>();
            prologueCourier.name = "VIS_N0_COURIER";
            prologueCourier.visitorId = "vis_n0_courier";
            prologueCourier.nameKey = "visitor.junho.name";
            prologueCourier.purposeKey = "visitor.n0_courier.purpose";
            prologueCourier.idCardNameKey = "visitor.junho.id_name";
            prologueCourier.targetUnit = "303";
            prologueCourier.cameraId = "CAM-01";
            prologueCourier.conversationId = "D_N0_COURIER_JUNHO";
            prologueCourier.nightIndex = 1;
            prologueCourier.arrivalGameSecond = At(22, 5);
            prologueCourier.correctAccess = VisitorAccessLevel.LobbyOnly;
            prologueCourier.destinationZone = ZoneIds.Lobby;
            prologueCourier.expectedRoute = new[] { ZoneIds.Lobby };
            prologueCourier.canBeEscorted = false;
            prologueCourier.checks = new[]
            {
                Check("visitor.check.target_unit", "visitor.check.value.unit_303_confirmed", false, AppIds.Residents),
                Check("visitor.check.recipient_name", "visitor.check.value.harin_label", true, AppIds.Residents)
            };
            prologueCourier.onCorrect = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.correct_visitor")
            };
            prologueCourier.onWrong = new[] { ConsequenceDefinition.Stat(StatIds.Performance, -2, "reason.wrong_visitor") };
            prologueCourier.onHold = new[] { ConsequenceDefinition.Stat(StatIds.Performance, -1, "reason.held_visitor") };

            // Jun-ho on the night none of it has happened yet: bored, cold, entirely ordinary.
            // He is here so that night 4 has something to be different from.
            prologueCourier.truth = VisitorTruth.Legitimate;
            prologueCourier.breakingPoint = 85;
            prologueCourier.tells = Tells(
                Tell("obs_annoyed", ReadChannel.Voice, TellWeight.Noise),
                Tell("obs_invoice_hidden", ReadChannel.Camera, TellWeight.Noise),
                Tell("obs_engine_running", ReadChannel.Camera, TellWeight.Noise),
                Tell("obs_corrects_you", ReadChannel.Voice, TellWeight.Innocent),
                Tell("obs_slept_in_car", ReadChannel.Glass, TellWeight.Noise));
            prologueCourier.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.n0_courier.reply.ask_again", 10),
                Reply(PressureTactic.Silence, "visitor.n0_courier.reply.silence", 12),
                Reply(PressureTactic.ShowMe, "visitor.n0_courier.reply.show_me", 6, "obs_corrects_you"),
                Reply(PressureTactic.Reassure, "visitor.n0_courier.reply.reassure", -8));

            return new[] { minseo, courier, guest, prologueCourier };
        }

        static VisitorCheck Check(string labelKey, string valueKey, bool contradicts, string appId)
        {
            return new VisitorCheck
            {
                labelKey = labelKey, valueKey = valueKey, contradicts = contradicts, crossReferenceAppId = appId
            };
        }

        // =====================================================================
        // Dialogue (GDD 20.12)
        // =====================================================================

        public static DialogueDefinition[] BuildDialogues()
        {
            // The first thing in the game. It starts itself at 22:00:00 (GameLoop), because
            // nobody handed this shift over - the radio was already talking when the door was
            // unlocked, and it has been talking for three days.
            //
            // It used to be a briefing with three questions to ask, which is a conversation,
            // which needs someone on the other end. There is no one on the other end. So the
            // choices are gone, the time scale is 1.0 rather than 0.25 - the clock does not
            // slow down to let anyone listen - and the last node is the log stamp rather than
            // a sign-off. Four lines, about fifteen seconds, and the interphone is already
            // ringing underneath them.
            var prologue = ScriptableObject.CreateInstance<DialogueDefinition>();
            prologue.name = "D_PROLOGUE_RADIO";
            prologue.conversationId = "D_PROLOGUE_RADIO";
            prologue.channel = DialogueChannel.Radio;
            prologue.timeScale = 1f;
            prologue.startNodeId = "start";
            prologue.nodes = new[]
            {
                Node("start", "speaker.dongsik", "dlg.radio.dongsik.001", "line2"),
                Node("line2", "speaker.dongsik", "dlg.radio.dongsik.002", "line3"),
                Node("line3", "speaker.dongsik", "dlg.radio.dongsik.003", "line4"),
                Node("line4", "speaker.dongsik", "dlg.radio.dongsik.004", "end"),
                Node("end", "speaker.unknown", "dlg.radio.dongsik.stamp", null)
            };

            var minseo = ScriptableObject.CreateInstance<DialogueDefinition>();
            minseo.name = "D_N1_MINSEO_ENTRY";
            minseo.conversationId = "D_N1_MINSEO_ENTRY";
            minseo.channel = DialogueChannel.Interphone;
            minseo.timeScale = 0.25f;
            minseo.choiceTimeLimit = 12f;
            minseo.startNodeId = "start";
            minseo.nodes = new[]
            {
                Node("start", "speaker.minseo", "dlg.n1.minseo.start", null,
                    Choices(
                        Choice("verify_id", "dlg.n1.minseo.choice.verify_id", "id_shown"),
                        Choice("call_resident", "dlg.n1.minseo.choice.call_resident", "resident_confirms"),
                        Choice("ask_purpose", "dlg.n1.minseo.choice.ask_purpose", "purpose"))),
                Node("id_shown", "speaker.minseo", "dlg.n1.minseo.id_shown", "await_decision"),
                Node("resident_confirms", "speaker.minseo", "dlg.n1.minseo.resident_confirms", "await_decision"),
                Node("purpose", "speaker.minseo", "dlg.n1.minseo.purpose", "await_decision"),
                Node("await_decision", "speaker.minseo", "dlg.n1.minseo.await_decision", null)
            };

            var courier = ScriptableObject.CreateInstance<DialogueDefinition>();
            courier.name = "D_N1_COURIER_ENTRY";
            courier.conversationId = "D_N1_COURIER_ENTRY";
            courier.channel = DialogueChannel.Interphone;
            courier.timeScale = 0.25f;
            courier.choiceTimeLimit = 12f;
            courier.startNodeId = "start";
            courier.nodes = new[]
            {
                Node("start", "speaker.courier", "dlg.n1.courier.start", null,
                    Choices(
                        Choice("ask_unit", "dlg.n1.courier.choice.ask_unit", "unit_answer"),
                        Choice("ask_invoice", "dlg.n1.courier.choice.ask_invoice", "invoice_answer"),
                        Choice("ask_company", "dlg.n1.courier.choice.ask_company", "company_answer"))),
                Node("unit_answer", "speaker.courier", "dlg.n1.courier.unit_answer", "await_decision"),
                Node("invoice_answer", "speaker.courier", "dlg.n1.courier.invoice_answer", "await_decision"),
                Node("company_answer", "speaker.courier", "dlg.n1.courier.company_answer", "await_decision"),
                Node("await_decision", "speaker.courier", "dlg.n1.courier.await_decision", null)
            };

            var jiwooGate = ScriptableObject.CreateInstance<DialogueDefinition>();
            jiwooGate.name = "D_N0_JIWOO_GATE";
            jiwooGate.conversationId = "D_N0_JIWOO_GATE";
            jiwooGate.channel = DialogueChannel.Interphone;
            jiwooGate.timeScale = 0.25f;
            jiwooGate.choiceTimeLimit = 12f;
            jiwooGate.startNodeId = "start";
            jiwooGate.nodes = new[]
            {
                Node("start", "speaker.jiwoo", "dlg.n0.jiwoo_gate.start", null,
                    Choices(
                        Choice("check_vehicle", "dlg.n0.jiwoo_gate.choice.vehicle", "vehicle_reply"),
                        Choice("check_resident", "dlg.n0.jiwoo_gate.choice.resident", "resident_reply"),
                        Choice("ask_purpose", "dlg.n0.jiwoo_gate.choice.purpose", "purpose_reply"))),
                Node("vehicle_reply", "speaker.jiwoo", "dlg.n0.jiwoo_gate.vehicle_reply", "await"),
                Node("resident_reply", "speaker.jiwoo", "dlg.n0.jiwoo_gate.resident_reply", "await"),
                Node("purpose_reply", "speaker.jiwoo", "dlg.n0.jiwoo_gate.purpose_reply", "await"),
                Node("await", "speaker.jiwoo", "dlg.n0.jiwoo_gate.await", null)
            };

            var courierN0 = ScriptableObject.CreateInstance<DialogueDefinition>();
            courierN0.name = "D_N0_COURIER_JUNHO";
            courierN0.conversationId = "D_N0_COURIER_JUNHO";
            courierN0.channel = DialogueChannel.Interphone;
            courierN0.timeScale = 0.25f;
            courierN0.choiceTimeLimit = 12f;
            courierN0.startNodeId = "start";
            courierN0.nodes = new[]
            {
                Node("start", "speaker.junho", "dlg.n0.courier.start", null,
                    Choices(
                        Choice("ask_unit", "dlg.n0.courier.choice.ask_unit", "unit_answer"),
                        Choice("ask_invoice", "dlg.n0.courier.choice.ask_invoice", "invoice_answer"),
                        Choice("ask_recipient", "dlg.n0.courier.choice.ask_recipient", "recipient_answer"))),
                Node("unit_answer", "speaker.junho", "dlg.n0.courier.unit_answer", "await"),
                Node("invoice_answer", "speaker.junho", "dlg.n0.courier.invoice_answer", "await"),
                Node("recipient_answer", "speaker.junho", "dlg.n0.courier.recipient_answer", "await"),
                Node("await", "speaker.junho", "dlg.n0.courier.await", null)
            };

            return new[] { prologue, minseo, courier, jiwooGate, courierN0 };
        }

        static DialogueNode Node(string id, string speakerKey, string textKey, string next,
                                 DialogueChoice[] choices = null, params ConsequenceDefinition[] onEnter)
        {
            return new DialogueNode
            {
                nodeId = id,
                speakerKey = speakerKey,
                textKey = textKey,
                nextNodeId = next,
                choices = choices ?? new DialogueChoice[0],
                onEnter = onEnter ?? new ConsequenceDefinition[0]
            };
        }

        static DialogueChoice[] Choices(params DialogueChoice[] items) { return items; }

        static DialogueChoice Choice(string id, string textKey, string next, bool ends = false,
                                     params ConsequenceDefinition[] consequences)
        {
            return new DialogueChoice
            {
                choiceId = id,
                textKey = textKey,
                nextNodeId = next,
                endsConversation = ends,
                consequences = consequences ?? new ConsequenceDefinition[0]
            };
        }

        /// <summary>
        /// Put a line behind a condition, so a conversation can be about what actually
        /// happened.
        ///
        /// DialogueService already filters choices it cannot show; nothing was using it,
        /// which is how Ji-woo came to offer a recording of a floor she had been kept away
        /// from (spec 21.3).
        /// </summary>
        static DialogueChoice Only(this DialogueChoice choice, params ConditionDefinition[] conditions)
        {
            choice.conditions = conditions ?? new ConditionDefinition[0];
            return choice;
        }

        /// <summary>
        /// Defer a consequence to the start of the next night (GDD 11.2, spec 30.3).
        ///
        /// The point of the routine tasks having these at all is spec 32's requirement that a
        /// wrong answer become a physical change in the building rather than a number in a
        /// summary the player never opens.
        /// </summary>
        static ConsequenceDefinition Later(ConsequenceDefinition consequence)
        {
            consequence.nextNight = true;
            return consequence;
        }

        // =====================================================================
        // Cases (GDD 9.1, 9.2, 11.3, 11.4)
        // =====================================================================

        public static CaseDefinition[] BuildCases()
        {
            return new[] { BuildC00(), BuildC01(), BuildC02(), BuildT01(), BuildT02() };
        }

        /// <summary>
        /// C00 - the cold open (GDD 9.1). Night 1, 22:00:00, and the shift is already wrong.
        ///
        /// This case used to be the prologue: a separate night 0 that ran twenty-five minutes
        /// of handover, two callers who were both fine, and a pressure ladder switched off at
        /// the source. It taught the buttons and nothing else, and it opened the game on the
        /// one shift where nothing the player did could matter. Both of those are why the
        /// first hour read as a puzzle room rather than as a night shift.
        ///
        /// So there is no night 0 any more. The game opens here, on a building whose camera
        /// wall is dead, whose night reserve is already under the warning line, and whose
        /// radio is playing the last thing the missing caretaker said. The verbs the prologue
        /// used to teach from a checklist - walk, interact, read a screen, leave the office
        /// and pay for leaving it - are all on the only road out of that state, which is the
        /// basement board. Nothing here is labelled a tutorial because nothing here is one.
        ///
        /// Three objectives, and each of them is something the night is doing to the player
        /// rather than a box the game has asked them to tick.
        /// </summary>
        static CaseDefinition BuildC00()
        {
            var c = NewCase("C00", 1, CaseKind.Tutorial, Priority.P0);
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = T2200;
            c.evidenceIds = new[] { "EV_KEY404", "EV_RED_SLIPPER" };
            c.objectives = new[]
            {
                // The whole opening in one line. Reaching the board means finding the lift,
                // crossing the lobby and standing in a room with no interphone in it while the
                // front door rings - the lesson GDD 15.5 exists to teach, and the one the
                // prologue never once charged for.
                Objective("obj_restore_feeds", "case.c00.objective.restore_feeds",
                          ObjectiveType.PerformAction, Facility.NightPowerService.BreakerActionId),

                // The first judgement in the game that can be got wrong, nine minutes in, and
                // it costs. Both of its contradictions are on the panel from the first shift,
                // so GDD 13.2's two independent facts are satisfiable the moment it is asked.
                Objective("obj_judge_vacant_parcel", "case.c00.objective.judge_vacant_parcel",
                          ObjectiveType.JudgeVisitor, "vis_courier_late"),

                // The key is in the lobby mailbox, which is on the way to the basement. It is
                // an objective rather than a prop because it is what grants 404 access later
                // (GDD 15.3): a player who never picks it up loses a door, not a thread.
                Objective("obj_take_key", "case.c00.objective.take_key",
                          ObjectiveType.AcquireEvidence, "EV_KEY404")
            };
            c.decisions = new[]
            {
                // The honest filing. Nothing in the log explains a twelve-channel cut, and
                // saying so on the record is what starts the paper trail the endings read.
                Decision("dec_report_unexplained", "case.c00.decision.unexplained",
                    "case.c00.result.unexplained", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 4, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, 2, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.correct_report"),
                    ConsequenceDefinition.Flag(FlagIds.PrologueDone, true),
                    ConsequenceDefinition.Achievement(AchievementIds.FirstShift)),

                // The comfortable filing. Keeps the shift tidy and buys the building's version
                // of events, which is the trade this whole game is about (GDD 4.3).
                Decision("dec_report_breaker", "case.c00.decision.breaker",
                    "case.c00.result.breaker", DecisionQuality.Partial,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.partial_report"),
                    ConsequenceDefinition.Flag(FlagIds.PrologueDone, true),
                    ConsequenceDefinition.Achievement(AchievementIds.FirstShift)),

                // Filing nothing at all. The chairman notices a caretaker who does not write
                // things down, and so does the record (GDD 7.2).
                Decision("dec_report_nothing", "case.c00.decision.nothing",
                    "case.c00.result.nothing", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, -3, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 1, "reason.wrong_report"),
                    ConsequenceDefinition.Flag(FlagIds.PrologueDone, true),
                    ConsequenceDefinition.Achievement(AchievementIds.FirstShift))
            };
            c.failSafe = FailSafe(T0300, null, "case.c00.failsafe.notify");
            c.analyticsName = "case_c00_cold_open";
            return c;
        }

        /// <summary>C01 - noise from a vacant unit. Cross-reference play (GDD 9.2).</summary>
        static CaseDefinition BuildC01()
        {
            var c = NewCase("C01", 1, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Time;
            // 22:07, not 22:20. The 305 voice message lands while the camera wall is still
            // dead and the front door is still ringing, so the night's real question arrives
            // on top of the crisis instead of politely after it (GDD 9.2).
            c.startWindowBegin = At(22, 7);
            c.startWindowEnd = T2330;
            c.evidenceIds = new[] { "EV_304_WATER", "EV_304_SOUND", "EV_CAM06_SNAP" };
            c.objectives = new[]
            {
                Objective("obj_view_304", "case.c01.objective.view_304", ObjectiveType.ViewRecord, "res_304"),
                Objective("obj_check_water", "case.c01.objective.check_water", ObjectiveType.AcquireEvidence, "EV_304_WATER"),
                Objective("obj_watch_cam06", "case.c01.objective.watch_cam06", ObjectiveType.ViewCctvChannel, "CAM-06"),
                Objective("obj_patrol_floor08", "case.c01.objective.patrol_floor08", ObjectiveType.EnterZone, ZoneIds.Floor03),
                Objective("obj_record_sound", "case.c01.objective.record_sound", ObjectiveType.AcquireEvidence, "EV_304_SOUND")
            };
            c.decisions = new[]
            {
                Decision("dec_pipe_noise", "case.c01.decision.pipe", "case.c01.result.pipe", DecisionQuality.Partial,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.partial_report"),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -2, "reason.partial_report")),

                Decision("dec_unregistered_occupant", "case.c01.decision.occupant", "case.c01.result.occupant",
                    DecisionQuality.Correct,
                    new[] { "EV_304_WATER", "EV_304_SOUND" }, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 8, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 3, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.correct_report"),
                    ConsequenceDefinition.Evidence("EV_404_DOOR")),

                Decision("dec_no_issue", "case.c01.decision.nothing", "case.c01.result.nothing", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, -5, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3, "reason.wrong_report"),
                    // GDD 14.4: one wrong call never permanently blocks the truth ending.
                    new ConsequenceDefinition
                    {
                        type = ConsequenceType.GrantEvidence, targetId = "EV_CAM06_SNAP", nextNight = true
                    })
            };
            c.failSafe = FailSafe(T0100, "EV_304_WATER", "case.c01.failsafe.notify");
            c.analyticsName = "case_c01_vacant_noise";
            return c;
        }

        /// <summary>C02 - the nurse at the front door (GDD 9.2 / 13).</summary>
        static CaseDefinition BuildC02()
        {
            var c = NewCase("C02", 1, CaseKind.MainCase, Priority.P0);
            c.trigger = CaseTrigger.Interphone;
            c.triggerTarget = "vis_minseo";
            c.startWindowBegin = T2240;
            c.objectives = new[]
            {
                Objective("obj_check_202", "case.c02.objective.check_202", ObjectiveType.ViewRecord, "res_202"),
                Objective("obj_judge", "case.c02.objective.judge", ObjectiveType.JudgeVisitor, "vis_minseo")
            };
            c.decisions = new[]
            {
                Decision("dec_logged", "case.c02.decision.logged", "case.c02.result.logged", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.logged_visitor"),
                    ConsequenceDefinition.Evidence("EV_VISIT_LOG")),

                // A real second option, not a rubber stamp: filing it as a pattern worth
                // watching pays out in resonance instead of raw performance (GDD 13.3).
                Decision("dec_note_pattern", "case.c02.decision.note_pattern", "case.c02.result.note_pattern",
                    DecisionQuality.Correct, null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.logged_visitor"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.noticed_contradiction"),
                    ConsequenceDefinition.Evidence("EV_VISIT_LOG"))
            };
            c.failSafe = FailSafe(T0300, null, "case.c02.failsafe.notify");
            c.analyticsName = "case_c02_nurse";
            return c;
        }

        /// <summary>T01 - illegal parking (GDD 11.4).</summary>
        static CaseDefinition BuildT01()
        {
            var c = NewCase("T01", 1, CaseKind.RoutineTask, Priority.P1);
            c.trigger = CaseTrigger.Time;
            // Lands just as C01 opens, so the night's first two demands arrive together.
            c.startWindowBegin = T2220;
            c.objectives = new[]
            {
                Objective("obj_view_cam09", "task.t01.objective.view_cam09", ObjectiveType.ViewCctvChannel, "CAM-09"),
                Objective("obj_lookup_plate", "task.t01.objective.lookup_plate", ObjectiveType.OpenApp, AppIds.Residents)
            };
            c.decisions = new[]
            {
                Decision("dec_warn_and_tow", "task.t01.decision.warn_tow", "task.t01.result.warn_tow", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.routine_correct"),
                    ConsequenceDefinition.Evidence("EV_PARKING_SHOT")),
                Decision("dec_ignore", "task.t01.decision.ignore", "task.t01.result.ignore", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -2, "reason.routine_wrong"),
                    // Spec 30.3 opens with this exact chain: the car is still across the fire
                    // lane on every night after, and on night 6 the escape is narrower for it.
                    Later(ConsequenceDefinition.Flag(FlagIds.FireLaneBlocked)),
                    Later(ConsequenceDefinition.Notify("notify.t01.car_still_there")))
            };
            c.failSafe = FailSafe(T0300, null, "task.t01.failsafe.notify");
            c.analyticsName = "task_t01_parking";
            return c;
        }

        /// <summary>
        /// T02 - parcel storage (GDD 11.4). Scheduled on night 2, not night 1.
        ///
        /// Tutorial task B in the prologue already teaches parcel registration end to end, so
        /// asking for the same verb again one night later added length to night 1 without
        /// adding a question. Night 2 is the courier night - GDD 9.3 lists sorting parcels
        /// among its required tasks - so the task now sits on the shift its own fiction is
        /// about, and night 1 spends the room on the door instead.
        /// </summary>
        static CaseDefinition BuildT02()
        {
            var c = NewCase("T02", 2, CaseKind.RoutineTask, Priority.P1);
            c.trigger = CaseTrigger.Time;
            // Lands after the two Junhos, while the lobby is still the night's open question.
            c.startWindowBegin = T2250;
            c.objectives = new[]
            {
                Objective("obj_open_access", "task.t02.objective.open_access", ObjectiveType.OpenApp, AppIds.Access),
                Objective("obj_check_lobby", "task.t02.objective.check_lobby", ObjectiveType.ViewCctvChannel, "CAM-02")
            };
            c.decisions = new[]
            {
                Decision("dec_register", "task.t02.decision.register", "task.t02.result.register", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.routine_correct"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 1, "reason.routine_correct")),
                Decision("dec_leave", "task.t02.decision.leave", "task.t02.result.leave", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -2, "reason.routine_wrong"),
                    ConsequenceDefinition.Flag("ParcelLost", true))
            };
            c.failSafe = FailSafe(T0300, null, "task.t02.failsafe.notify");
            c.analyticsName = "task_t02_parcel";
            return c;
        }

        static CaseDefinition NewCase(string id, int night, CaseKind kind, Priority priority)
        {
            var c = ScriptableObject.CreateInstance<CaseDefinition>();
            c.name = "CASE_" + id;
            c.caseId = id;

            // Routine tasks (T01..T18) use the "task." key namespace, story cases use "case.".
            var prefix = kind == CaseKind.RoutineTask ? "task." : "case.";
            c.titleKey = prefix + id.ToLowerInvariant() + ".title";
            c.summaryKey = prefix + id.ToLowerInvariant() + ".summary";
            c.kind = kind;
            c.priority = priority;
            c.nightIndex = night;
            c.startWindowBegin = T2200;
            c.startWindowEnd = 0;
            return c;
        }

        static ObjectiveDefinition Objective(string id, string titleKey, ObjectiveType type, string target,
                                             bool optional = false, bool hidden = false)
        {
            return new ObjectiveDefinition
            {
                objectiveId = id, titleKey = titleKey, type = type, targetId = target,
                optional = optional, hidden = hidden
            };
        }

        static DecisionDefinition Decision(string id, string labelKey, string resultKey, DecisionQuality quality,
                                           string[] requiredEvidence, ConditionDefinition[] availability,
                                           params ConsequenceDefinition[] consequences)
        {
            return new DecisionDefinition
            {
                decisionId = id,
                labelKey = labelKey,
                resultKey = resultKey,
                quality = quality,
                requiredEvidenceIds = requiredEvidence ?? new string[0],
                availability = availability ?? new ConditionDefinition[0],
                consequences = consequences ?? new ConsequenceDefinition[0]
            };
        }

        static FailSafeDefinition FailSafe(int triggerSecond, string alternateEvidence, string notifyKey)
        {
            return new FailSafeDefinition
            {
                enabled = true,
                triggerGameSecond = triggerSecond,
                alternateEvidenceId = alternateEvidence,
                notifyKey = notifyKey
            };
        }

        // =====================================================================
        // CCTV (GDD 12.1, 12.3)
        // =====================================================================

        public static CctvChannelDefinition[] BuildChannels()
        {
            // Mount heights sit under the 2.6m ceiling from GDD 17.4; positions are inside the
            // zone sizes in WorldBuilder, and each camera now lives in the zone GDD 12.1 names.
            const float H = Gameplay.BuildingSpec.CameraHeight;

            return new[]
            {
                Channel("CAM-01", ZoneIds.Lobby,     new Vector3(0f, H, 3.7f),      new Vector3(15f, 180f, 0f), true),
                Channel("CAM-02", ZoneIds.Lobby,     new Vector3(-5.4f, H, -3.6f),  new Vector3(14f, 35f, 0f), false),
                Channel("CAM-03", ZoneIds.Office,    new Vector3(0f, H, -2.7f),     new Vector3(16f, 0f, 0f), true),
                Channel("CAM-04", ZoneIds.Floor04,   new Vector3(9.5f, H, 0f),      new Vector3(10f, -90f, 0f), false),
                Channel("CAM-05", ZoneIds.Stairwell, new Vector3(0f, H, -2.3f),     new Vector3(12f, 0f, 0f), false),
                Channel("CAM-06", ZoneIds.Floor03,   new Vector3(-9.5f, H, 0f),     new Vector3(10f, 90f, 0f), true),
                Channel("CAM-07", ZoneIds.Floor05,   new Vector3(-9.5f, H, 0f),     new Vector3(10f, 90f, 0f), false),
                Channel("CAM-08", ZoneIds.Elevator,  new Vector3(0f, 2.15f, 0.6f),  new Vector3(22f, 180f, 0f), true),
                Channel("CAM-09", ZoneIds.Parking,   new Vector3(0f, H, -8.6f),     new Vector3(12f, 0f, 0f), false),
                Channel("CAM-10", ZoneIds.Archive,   new Vector3(0f, H, -2.3f),     new Vector3(12f, 0f, 0f), false),
                Channel("CAM-11", ZoneIds.Parking,   new Vector3(-11.9f, H, 6f),    new Vector3(12f, 90f, 0f), false),
                Channel("CAM-12", ZoneIds.Floor06,   new Vector3(9.5f, H, 0f),      new Vector3(10f, -90f, 0f), false)
            };
        }

        static CctvChannelDefinition Channel(string id, string zone, Vector3 position, Vector3 euler, bool audio)
        {
            return new CctvChannelDefinition
            {
                cameraId = id,
                labelKey = "cctv." + id.Replace("-", "").ToLowerInvariant() + ".label",
                zoneId = zone,
                localPosition = position,
                localEuler = euler,
                hasAudio = audio,
                fieldOfView = 72f
            };
        }

        // =====================================================================
        // Facility meters (GDD 16.12)
        // =====================================================================

        public static MeterSeries[] BuildMeterSeries()
        {
            return new[]
            {
                Series(MeterKind.Water, "303", "facility.series.303", "facility.unit.water", false,
                       new[] { 0.4f, 0.3f, 0.2f, 0.2f, 0.1f, 0.1f, 0.2f, 0.5f }),

                // 304 is vacant yet consumes water all night - the C01 contradiction.
                // Reading this graph is how the player obtains EV_304_WATER.
                WithEvidence(
                    Series(MeterKind.Water, "304", "facility.series.304", "facility.unit.water", false,
                           new[] { 0.0f, 0.9f, 1.4f, 1.6f, 1.5f, 1.2f, 0.8f, 0.1f }),
                    "EV_304_WATER"),

                Series(MeterKind.Water, "202", "facility.series.202", "facility.unit.water", false,
                       new[] { 0.6f, 0.4f, 0.2f, 0.1f, 0.1f, 0.1f, 0.3f, 0.7f }),

                Series(MeterKind.Power, "common", "facility.series.common", "facility.unit.power", false,
                       new[] { 22f, 21f, 20f, 20f, 19f, 19f, 21f, 24f }),

                Series(MeterKind.Power, "303", "facility.series.303", "facility.unit.power", false,
                       new[] { 1.2f, 0.9f, 0.6f, 0.5f, 0.5f, 0.5f, 0.8f, 1.4f }),

                // GDD 2.3 hook #6: only the unit that does not exist spikes.
                Series404()
            };
        }

        static MeterSeries Series404()
        {
            var series = Series(MeterKind.Power, "404", "facility.series.404", "facility.unit.power", true,
                                new[] { 0.1f, 3.8f, 6.4f, 7.1f, 6.9f, 5.2f, 2.6f, 0.4f });
            series.RevealFlagId = FlagIds.Knows404;
            return series;
        }

        static MeterSeries WithEvidence(MeterSeries series, string evidenceId)
        {
            series.EvidenceId = evidenceId;
            return series;
        }

        static MeterSeries Series(MeterKind kind, string id, string labelKey, string unitKey,
                                  bool hidden, float[] hourlyValues)
        {
            var series = new MeterSeries
            {
                SeriesId = id, LabelKey = labelKey, Kind = kind, UnitKey = unitKey, Hidden = hidden
            };

            for (int i = 0; i < hourlyValues.Length; i++)
                series.Samples.Add(new MeterSample(T2200 + i * 3600, hourlyValues[i]));

            return series;
        }
    }
}
