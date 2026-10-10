using NO404.Cases;
using NO404.Core;
using NO404.Evidence;

namespace NO404.ContentData
{
    /// <summary>
    /// The six fixed main quests, one per night (v5.0 10-15).
    ///
    /// These are the campaign's spine and the one part of it v5.0 0.2 forbids randomising:
    /// 404, the 2009 fire, the Yoon family, Park Dong-sik and Choi Kyung-tae happen in this
    /// order every run. Everything the pool draws around them is what changes.
    ///
    /// Each one is <c>isFixedMain</c>, so <see cref="NightPoolService"/> places it before it
    /// draws anything else and a night whose fourteen candidates are all ineligible is still
    /// a playable night.
    ///
    /// What is deliberately not here yet: the fourteen random candidates per night. v5.0 4.1
    /// wants fifteen per night and this file provides one - the campaign is playable end to
    /// end and every night is thin. That is the agreed order of work, not an oversight, and
    /// <see cref="NightPoolService.PoolPerNight"/> is the number to make true next.
    ///
    /// The decisions below follow v5.0's own tables rather than being invented: where a
    /// section says "폐기 → RECORD_DEBT +1", that is the consequence written here.
    /// </summary>
    public static partial class SeedContent
    {
        /// <summary>
        /// The evidence the six mains are made of (v5.0 19.2).
        ///
        /// Each main carries at least three channels, because 19.2 forbids a main whose answer
        /// can be settled on one screen: the database, the meter and the corridor have to be
        /// able to disagree. The ones marked archive-critical are the ones the ending's
        /// epilogue reads.
        /// </summary>
        public static EvidenceDefinition[] BuildV5Evidence()
        {
            return new[]
            {
                // N1: the bill is the object the whole campaign turns on.
                Evidence_("EV_404_BILL", EvidenceType.Document, "N1-M01", false),
                Evidence_("EV_404_BILL_PHOTO", EvidenceType.Photo, "N1-M01", false),

                // N2: two channels that disagree about one man.
                Evidence_("EV_CAM02_DOUBLE", EvidenceType.CctvSnapshot, "N2-M01", false),
                Evidence_("EV_WAYBILL_ORDER", EvidenceType.Document, "N2-M01", false),

                // N3: a number stamped on a seal, and two heights scratched on a wall.
                Evidence_("EV_SEAL_NUMBER", EvidenceType.Photo, "N3-M01", false),
                Evidence_("EV_HEIGHT_MARKS", EvidenceType.Photo, "N3-M01", false),

                // N4: the row that should not exist, and the paper that says it should.
                Evidence_("EV_DB404_ROW", EvidenceType.Document, "N4-M01", true),
                Evidence_("EV_PAPER_LEDGER", EvidenceType.Document, "N4-M01", true),

                // N5: the night of the fire, and the man still behind the wall.
                Evidence_("EV_FIRE_TAPE_2009", EvidenceType.AudioRecording, "N5-M01", true),
                Evidence_("EV_DONGSIK_ID", EvidenceType.PhysicalObject, "N5-M01", false),
                Evidence_("EV_N3_ANALOG", EvidenceType.MeterGraph, "N3-M01", false),
                Evidence_("EV_N3_LIFT_LOG", EvidenceType.Document, "N3-M01", false),
                Evidence_("EV_N5_FIRE_DOOR", EvidenceType.Photo, "N5-M01", false),
                Evidence_("EV_N5_ANALOG", EvidenceType.MeterGraph, "N5-M01", false),
                Evidence_("EV_N5_WORK_LOG", EvidenceType.AccessLog, "N5-M01", false),
                Evidence_("EV_N5_LAST_POSITION", EvidenceType.Photo, "N5-M01", false),
                Evidence_("EV_CHOI_APPROVAL", EvidenceType.Document, "N5-M01", false),
                Evidence_("EV_404_DELETION", EvidenceType.Document, "N5-M01", false),

                // N6: what 404 was actually for.
                Evidence_("EV_ORIGINAL_LEDGER", EvidenceType.Document, "N6-M01", true)
            };
        }

        public static CaseDefinition[] BuildV5Mains()
        {
            return new[]
            {
                BuildN1Main(), BuildN2Main(), BuildN3Main(),
                BuildN4Main(), BuildN5Main(), BuildN6Main()
            };
        }

        /// <summary>
        /// Shared shape of a main quest: it is the night's spine, it always runs, and it is
        /// never competed for by the draw.
        /// </summary>
        static CaseDefinition Main(string id, int night, AnomalyFamily family, float minutes)
        {
            var c = NewCase(id, night, CaseKind.MainCase, Priority.P0);
            c.questType = QuestType.Main;
            c.family = family;
            c.isFixedMain = true;
            c.estimatedMinutes = minutes;
            c.baseWeight = 0;              // placed, never drawn
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = T2200;
            return c;
        }

        // =================================================================
        // N1-M01 - 304 noise / a bill for a unit that does not exist (v5.0 10)
        // =================================================================

        /// <summary>
        /// The night the game still looks like a job.
        ///
        /// 305 reports a child bouncing a ball in 304. The database says 304 has been empty
        /// for years and drawing no power, and the water meter says somebody has used water
        /// in the last twenty minutes. Both are true. That contradiction, and not a figure in
        /// a corridor, is the first thing this game asks the player to hold.
        /// </summary>
        static CaseDefinition BuildN1Main()
        {
            var c = Main("N1-M01", 1, AnomalyFamily.Record, 13f);
            c.startWindowBegin = At(22, 7);
            c.evidenceIds = new[] { "EV_304_WATER", "EV_304_SOUND", "EV_404_BILL" };

            c.objectives = new[]
            {
                Objective("obj_view_304", "case.n1m01.objective.view_304",
                          ObjectiveType.ViewRecord, "res_304"),
                Objective("obj_check_water", "case.n1m01.objective.check_water",
                          ObjectiveType.AcquireEvidence, "EV_304_WATER"),
                Objective("obj_go_floor03", "case.n1m01.objective.go_floor03",
                          ObjectiveType.EnterZone, ZoneIds.Floor03),
                Objective("obj_record_sound", "case.n1m01.objective.record_sound",
                          ObjectiveType.AcquireEvidence, "EV_304_SOUND"),
                Objective("obj_read_bill", "case.n1m01.objective.bill", ObjectiveType.AcquireEvidence, "EV_404_BILL")
            };

            c.decisions = new[]
            {
                // v5.0 10: preserving the bill is the branch every later night reads.
                Decision("dec_preserve_bill", "case.n1m01.decision.preserve",
                    "case.n1m01.result.preserve", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 8, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 5, "reason.noticed_contradiction"),
                    ConsequenceDefinition.Flag(FlagIds.BillPreserved404, true),
                    ConsequenceDefinition.Evidence("EV_404_BILL_PHOTO"),

                    ConsequenceDefinition.Achievement(AchievementIds.FirstShift)),

                // Filed as an office error. The clue survives; the connection does not.
                Decision("dec_file_as_error", "case.n1m01.decision.file_error",
                    "case.n1m01.result.file_error", DecisionQuality.Partial,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.partial_report"),
                    ConsequenceDefinition.Achievement(AchievementIds.FirstShift)),

                // v5.0 10: discarding it costs a record debt and makes night 2 harder.
                Decision("dec_discard_bill", "case.n1m01.decision.discard",
                    "case.n1m01.result.discard", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -8, "reason.wrong_report"),
                    ConsequenceDefinition.Debt(DebtIds.Record, 1, "reason.discarded_record"),
                    ConsequenceDefinition.Flag(FlagIds.BillPreserved404, false),
                    ConsequenceDefinition.Achievement(AchievementIds.FirstShift))
            };

            c.failSafe = FailSafe(T0300, null, "case.n1m01.failsafe.notify");
            c.analyticsName = "n1_main_304_noise";
            return c;
        }

        // =================================================================
        // N2-M01 - two Seo Jun-ho (v5.0 11)
        // =================================================================

        /// <summary>
        /// The courier is at the entrance on the interphone. He is also already in the lobby
        /// on CAM-02, sorting boxes.
        ///
        /// v5.0 11 wants the answer to come from invariants rather than from the face: it is
        /// raining outside, and the weather, the wall clock and the order of the waybills are
        /// what disagree. Getting it right off two of those is what sets ECHO_RULE_CONFIRMED.
        /// </summary>
        static CaseDefinition BuildN2Main()
        {
            var c = Main("N2-M01", 2, AnomalyFamily.Echo, 16f);

            // Time-triggered, not Interphone-triggered, even though it opens with somebody at
            // the door. The night chain is built from the night's timed cases, so a main that
            // waited on a visitor was a main the chain could not see - night 2 had nothing
            // pacing it, nothing to hang its callers on, and no last case to close. The
            // courier still arrives with the quest: GameLoop files him against this beat.
            c.startWindowBegin = At(22, 20);
            c.evidenceIds = new[] { "EV_CAM02_DOUBLE", "EV_WAYBILL_ORDER" };

            c.objectives = new[]
            {
                Objective("obj_watch_cam02", "case.n2m01.objective.watch_cam02",
                          ObjectiveType.ViewCctvChannel, "CAM-02"),
                Objective("obj_check_lobby", "case.n2m01.objective.check_lobby",
                          ObjectiveType.EnterZone, ZoneIds.Lobby),
                Objective("obj_judge_junho", "case.n2m01.objective.judge",
                          ObjectiveType.JudgeVisitor, "vis_junho_real")
            };

            c.decisions = new[]
            {
                Decision("dec_verified_lobby_only", "case.n2m01.decision.lobby_only",
                    "case.n2m01.result.lobby_only", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Flag(FlagIds.EchoRuleConfirmed, true),
                    ConsequenceDefinition.Choice(ChoiceIds.JunhoStatus, "TRUSTED"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 5, "reason.correct_report"),
                    ConsequenceDefinition.Sanity(-5, "reason.saw_the_same_man_twice"),
                    ConsequenceDefinition.Sanity(3, "reason.read_correctly")),

                Decision("dec_wait_outside", "case.n2m01.decision.wait_outside",
                    "case.n2m01.result.wait_outside", DecisionQuality.Partial,
                    null, null,
                    ConsequenceDefinition.Choice(ChoiceIds.JunhoStatus, "NEUTRAL"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3, "reason.delivery_delayed"),
                    ConsequenceDefinition.Sanity(-5, "reason.saw_the_same_man_twice")),

                // v5.0 11: an unverified full pass is the access debt this campaign bills for.
                Decision("dec_full_pass", "case.n2m01.decision.full_pass",
                    "case.n2m01.result.full_pass", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Debt(DebtIds.Access, 1, "reason.unverified_pass"),
                    ConsequenceDefinition.Choice(ChoiceIds.JunhoStatus, "NEUTRAL"),
                    ConsequenceDefinition.Sanity(-8, "reason.followed_the_wrong_one")),

                Decision("dec_reject", "case.n2m01.decision.reject",
                    "case.n2m01.result.reject", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Choice(ChoiceIds.JunhoStatus, "REJECTED"),
                    ConsequenceDefinition.Debt(DebtIds.Trust, 1, "reason.turned_away_a_real_caller"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -5, "reason.wrong_report"))
            };

            c.failSafe = FailSafe(T0300, null, "case.n2m01.failsafe.notify");
            c.analyticsName = "n2_main_two_junho";
            return c;
        }

        // =================================================================
        // N3-M01 - the sealed 4F service area (v5.1 12)
        // =================================================================

        /// <summary>
        /// The lift reports 4F-SERVICE DOOR OPEN inside the existing fourth floor.
        ///
        /// v5.0 12's lesson is that the number on the wall is not the floor. The seal on the
        /// stair landing, the analogue gauge and the flights actually walked are what say
        /// where the caretaker is - and the two children's height marks scratched into the
        /// service passage are what this night is really for.
        /// </summary>
        static CaseDefinition BuildN3Main()
        {
            var c = Main("N3-M01", 3, AnomalyFamily.Space, 17f);
            c.evidenceIds = new[] { "EV_SEAL_NUMBER", "EV_HEIGHT_MARKS", "EV_N3_ANALOG", "EV_N3_LIFT_LOG" };

            c.objectives = new[]
            {
                Objective("obj_check_seal", "case.n3m01.objective.check_seal",
                          ObjectiveType.AcquireEvidence, "EV_SEAL_NUMBER"),
                new ObjectiveDefinition { objectiveId = "obj_analog", titleKey = "case.n3m01.objective.analog", type = ObjectiveType.AcquireEvidence, targetId = "EV_N3_ANALOG", optional = true },
                new ObjectiveDefinition { objectiveId = "obj_lift_log", titleKey = "case.n3m01.objective.lift", type = ObjectiveType.AcquireEvidence, targetId = "EV_N3_LIFT_LOG", optional = true },
                Objective("obj_enter_service", "case.n3m01.objective.enter_service",
                          ObjectiveType.EnterZone, ZoneIds.ServicePassage),
                Objective("obj_find_marks", "case.n3m01.objective.find_marks",
                          ObjectiveType.AcquireEvidence, "EV_HEIGHT_MARKS")
            };

            c.decisions = new[]
            {
                Decision("dec_verified_physically", "case.n3m01.decision.verified",
                    "case.n3m01.result.verified", DecisionQuality.Correct,
                    new[] { "EV_SEAL_NUMBER", "EV_N3_ANALOG", "EV_N3_LIFT_LOG", "EV_HEIGHT_MARKS" }, null,
                    ConsequenceDefinition.Flag(FlagIds.SpaceRuleConfirmed, true),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 10, "reason.noticed_contradiction"),


                    ConsequenceDefinition.Sanity(3, "reason.read_correctly")),

                Decision("dec_withdrew", "case.n3m01.decision.withdrew",
                    "case.n3m01.result.withdrew", DecisionQuality.Partial,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 5, "reason.partial_report"),
                    ConsequenceDefinition.Notify("case.n3m01.result.withdrew")),

                // v5.0 12: trusting the sign twice is what the distortion debt is for.
                Decision("dec_trusted_the_sign", "case.n3m01.decision.trusted_sign",
                    "case.n3m01.result.trusted_sign", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Debt(DebtIds.Distortion, 2, "reason.walked_the_wrong_door"),
                    ConsequenceDefinition.Sanity(-10, "reason.lost_in_the_building"))
            };

            c.failSafe = FailSafe(T0300, null, "case.n3m01.failsafe.notify");
            c.analyticsName = "n3_main_4f_service";
            return c;
        }

        // =================================================================
        // N4-M01 - 404 enters the resident database (v5.0 13)
        // =================================================================

        /// <summary>
        /// A household that does not exist appears in the system, with three names and a
        /// move-in date of 2008-12-19. The chairman calls it a sync error and says delete it.
        ///
        /// This is the night the campaign asks what the player is actually for. Every branch
        /// stays playable - v5.0 13 is explicit that deleting does not close the truth route -
        /// but only one of them leaves Harin's name where somebody else can find it.
        /// </summary>
        static CaseDefinition BuildN4Main()
        {
            var c = Main("N4-M01", 4, AnomalyFamily.Record, 18f);
            c.evidenceIds = new[] { "EV_DB404_ROW", "EV_PAPER_LEDGER" };

            c.objectives = new[]
            {
                Objective("obj_open_residents", "case.n4m01.objective.open_residents",
                          ObjectiveType.OpenApp, AppIds.Residents),
                Objective("obj_view_404", "case.n4m01.objective.view_404",
                          ObjectiveType.ViewRecord, "res_404"),
                Objective("obj_paper_ledger", "case.n4m01.objective.paper_ledger",
                          ObjectiveType.AcquireEvidence, "EV_PAPER_LEDGER")
            };

            c.decisions = new[]
            {
                // v5.0 13: paper plus keeping the row is the strongest archive outcome.
                Decision("dec_print_and_keep", "case.n4m01.decision.print",
                    "case.n4m01.result.print", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Choice(ChoiceIds.Db404Action, "PRINT"),
                    ConsequenceDefinition.Flag(FlagIds.HarinRecordPreserved, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 15, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 15, "reason.defied_the_chairman"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 10, "reason.noticed_contradiction"),
                    ConsequenceDefinition.Sanity(-8, "reason.read_her_brothers_name")),

                Decision("dec_export", "case.n4m01.decision.export",
                    "case.n4m01.result.export", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Choice(ChoiceIds.Db404Action, "EXPORT"),
                    ConsequenceDefinition.Flag(FlagIds.HarinRecordPreserved, true),
                    ConsequenceDefinition.Flag(FlagIds.Db404Exported, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 12, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.ChairmanAlert, 25, "reason.defied_the_chairman"),
                    ConsequenceDefinition.Sanity(-8, "reason.read_her_brothers_name")),

                Decision("dec_keep_quiet", "case.n4m01.decision.keep",
                    "case.n4m01.result.keep", DecisionQuality.Partial,
                    null, null,
                    ConsequenceDefinition.Choice(ChoiceIds.Db404Action, "KEEP"),
                    ConsequenceDefinition.Flag(FlagIds.HarinRecordPreserved, true),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 10, "reason.noticed_contradiction"),
                    ConsequenceDefinition.Sanity(-4, "reason.read_her_brothers_name")),

                // v5.0 13: deleting costs two record debts and the name itself.
                Decision("dec_delete", "case.n4m01.decision.delete",
                    "case.n4m01.result.delete", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Choice(ChoiceIds.Db404Action, "DELETE"),
                    ConsequenceDefinition.Flag(FlagIds.HarinRecordPreserved, false),
                    ConsequenceDefinition.Flag(FlagIds.Db404Deleted, true),
                    ConsequenceDefinition.Debt(DebtIds.Record, 2, "reason.deleted_the_record"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -15, "reason.wrong_report"),
                    // v5.0 13 fail-safe: the truth route survives on paper.
                    new ConsequenceDefinition
                    {
                        type = ConsequenceType.GrantEvidence, targetId = "EV_PAPER_LEDGER", nextNight = true
                    })
            };

            c.failSafe = FailSafe(T0300, "EV_DB404_ROW", "case.n4m01.failsafe.notify");
            c.analyticsName = "n4_main_db404";
            return c;
        }

        // =================================================================
        // N5-M01 - the 2009 fire, replayed (v5.0 14)
        // =================================================================

        /// <summary>
        /// Every timestamp on the wall turns to 2009-11-07 02:08, and something behind the
        /// fourth-floor wall starts knocking in a pattern.
        ///
        /// v5.0 14 makes this the night the earlier bills come due: what the player can hold
        /// together depends on the fire door, the pump and the reserve they left behind.
        /// </summary>
        static CaseDefinition BuildN5Main()
        {
            var c = Main("N5-M01", 5, AnomalyFamily.Echo, 20f);
            c.evidenceIds = new[] { "EV_FIRE_TAPE_2009", "EV_DONGSIK_ID", "EV_N5_FIRE_DOOR", "EV_N5_ANALOG", "EV_N5_WORK_LOG", "EV_N5_LAST_POSITION", "EV_CHOI_APPROVAL", "EV_404_DELETION" };

            c.objectives = new[]
            {
                Objective("obj_watch_timestamps", "case.n5m01.objective.timestamps",
                          ObjectiveType.ViewCctvChannel, "CAM-06"),
                Objective("obj_reach_floor04", "case.n5m01.objective.reach_floor04",
                          ObjectiveType.EnterZone, ZoneIds.Floor04),
                Objective("obj_verify_signal", "case.n5m01.objective.verify_signal",
                          ObjectiveType.AcquireEvidence, "EV_DONGSIK_ID"),
                Objective("obj_current_door", "case.n5m01.objective.door", ObjectiveType.AcquireEvidence, "EV_N5_FIRE_DOOR"),
                Objective("obj_current_meter", "case.n5m01.objective.analog", ObjectiveType.AcquireEvidence, "EV_N5_ANALOG"),
                Objective("obj_fire_tape", "case.n5m01.objective.tape", ObjectiveType.AcquireEvidence, "EV_FIRE_TAPE_2009"),
                Objective("obj_work_log", "case.n5m01.objective.work", ObjectiveType.AcquireEvidence, "EV_N5_WORK_LOG"),
                new ObjectiveDefinition { objectiveId = "obj_choi", titleKey = "case.n5m01.objective.choi", type = ObjectiveType.AcquireEvidence, targetId = "EV_CHOI_APPROVAL", optional = true },
                new ObjectiveDefinition { objectiveId = "obj_deletion", titleKey = "case.n5m01.objective.deletion", type = ObjectiveType.AcquireEvidence, targetId = "EV_404_DELETION", optional = true },
                new ObjectiveDefinition { objectiveId = "obj_last_position", titleKey = "case.n5m01.objective.position", type = ObjectiveType.AcquireEvidence, targetId = "EV_N5_LAST_POSITION", optional = true }
            };

            c.decisions = new[]
            {
                Decision("dec_residents_first", "case.n5m01.decision.residents",
                    "case.n5m01.result.residents", DecisionQuality.Correct,
                    null, null,

                    ConsequenceDefinition.Choice(ChoiceIds.FireDoorStatus, "SAFE"),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, 8, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 8, "reason.correct_report"),
                    ConsequenceDefinition.Notify("case.n5m01.result.residents")),

                Decision("dec_archive_first", "case.n5m01.decision.archive",
                    "case.n5m01.result.archive", DecisionQuality.Correct,
                    null, null,

                    ConsequenceDefinition.Choice(ChoiceIds.FireDoorStatus, "JAMMED"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 12, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -5, "reason.evacuation_delayed"),
                    ConsequenceDefinition.Notify("case.n5m01.result.archive")),

                // v5.0 14: chasing the signal first finds him fastest and costs the most.
                Decision("dec_chase_signal", "case.n5m01.decision.chase",
                    "case.n5m01.result.chase", DecisionQuality.Partial,
                    null, null,


                    ConsequenceDefinition.Health(-10, "reason.smoke"),
                    ConsequenceDefinition.Sanity(-5, "reason.last_rescue_echo")),

                // Trying to hold everything up at once overloads the ring.
                Decision("dec_hold_everything", "case.n5m01.decision.hold_all",
                    "case.n5m01.result.hold_all", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Debt(DebtIds.Safety, 2, "reason.power_overload"),
                    ConsequenceDefinition.Choice(ChoiceIds.FireDoorStatus, "OPEN"),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -10, "reason.wrong_report"),
                    ConsequenceDefinition.Health(-8, "reason.smoke"))
            };

            c.failSafe = FailSafe(T0300, null, "case.n5m01.failsafe.notify");
            c.analyticsName = "n5_main_fire_echo";
            return c;
        }

        // =================================================================
        // N6-M01 - there was a unit 404 (v5.0 15)
        // =================================================================

        /// <summary>
        /// The last shift before the building is sealed. The player opens 404, decides between
        /// a man and a ledger, and leaves.
        ///
        /// The ending is not chosen here. v5.0 8.1 gates it on HP and SAN at this quest's
        /// final stage, and what is decided in these four options only changes the epilogue -
        /// which is the whole reason the grade could be simplified to two numbers without
        /// making the campaign's choices weightless.
        /// </summary>
        static CaseDefinition BuildN6Main()
        {
            var c = Main("N6-M01", 6, AnomalyFamily.Identity, 26f);
            c.questType = QuestType.Main;
            c.evidenceIds = new[] { "EV_ORIGINAL_LEDGER" };

            c.objectives = new[]
            {
                Objective("obj_enter_404", "case.n6m01.objective.enter_404",
                          ObjectiveType.EnterZone, ZoneIds.Unit404),
                Objective("obj_take_ledger", "case.n6m01.objective.take_ledger",
                          ObjectiveType.AcquireEvidence, "EV_ORIGINAL_LEDGER")
            };

            c.decisions = new[]
            {
                Decision("dec_people_first", "case.n6m01.decision.people",
                    "case.n6m01.result.people", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Flag(FlagIds.DongsikRescued, true),
                    ConsequenceDefinition.Flag(FlagIds.DongsikFound, true),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 10, "reason.correct_report"),
                    ConsequenceDefinition.Sanity(-5, "reason.entered_404")),

                Decision("dec_ledger_first", "case.n6m01.decision.ledger",
                    "case.n6m01.result.ledger", DecisionQuality.Correct,
                    null, null,
                    ConsequenceDefinition.Flag(FlagIds.LedgerSecured, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 15, "reason.correct_report"),
                    ConsequenceDefinition.Sanity(-5, "reason.entered_404")),

                // v5.0 15: both, but only for somebody whose earlier nights bought the time.
                Decision("dec_both", "case.n6m01.decision.both",
                    "case.n6m01.result.both", DecisionQuality.Correct,
                    null,
                    new[]
                    {
                        ConditionDefinition.Stat(StatIds.BuildingSafety, 60),
                        ConditionDefinition.Flag(FlagIds.DongsikSignalFound, true)
                    },
                    ConsequenceDefinition.Flag(FlagIds.DongsikRescued, true),
                    ConsequenceDefinition.Flag(FlagIds.DongsikFound, true),
                    ConsequenceDefinition.Flag(FlagIds.LedgerSecured, true),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 15, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 10, "reason.correct_report"),
                    ConsequenceDefinition.Health(-8, "reason.structure_gave_way"),
                    ConsequenceDefinition.Sanity(-5, "reason.entered_404")),

                Decision("dec_leave", "case.n6m01.decision.leave",
                    "case.n6m01.result.leave", DecisionQuality.Wrong,
                    null, null,
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, -20, "reason.wrong_report"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -10, "reason.wrong_report"),
                    ConsequenceDefinition.Sanity(-5, "reason.entered_404"))
            };

            c.failSafe = FailSafe(T0300, null, "case.n6m01.failsafe.notify");
            c.analyticsName = "n6_main_unit_404";
            return c;
        }
    }
}
