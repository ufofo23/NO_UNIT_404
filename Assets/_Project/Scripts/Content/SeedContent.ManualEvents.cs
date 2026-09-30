using System.Collections.Generic;
using UnityEngine;
using NO404.Anomalies;
using NO404.Cases;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.ContentData
{
    /// <summary>
    /// The eighteen night-anomaly events M01..M18 (v2.1 spec 22), in the common shape spec 26
    /// defines.
    ///
    /// Each one is a procedure the player carries out under a manual rule, judged on counters
    /// the world raises as they work. Two conventions hold throughout:
    ///
    /// - A counter that stands for a yes/no is 0 or 1, so spec 26's "playerInitiatedEarly ==
    ///   false" becomes IsFalse("playerInitiatedEarly") and reads the same either way.
    /// - onWrong never contains RecordViolation. ManualEventService decides whether a wrong
    ///   answer was also a *violation*, because that depends on whether the player was ever
    ///   given the page (spec 0.9.2) - which content cannot know.
    /// </summary>
    public static partial class SeedContent
    {
        // The times spec 22 gives for each event are gone from the data on purpose.
        //
        // They described a timetable, and a timetable is the wrong shape for this: an event
        // whose hour had not come could not be reached however well the caretaker was working,
        // and one whose hour passed while they were three floors down arrived out of nowhere.
        // What the spec actually wants is the order - which anomaly follows which - and that
        // survives here as the chain. The clock still matters everywhere it should: the shift
        // ends at 06:00, and every wait inside an event is measured in game seconds.

        public static ManualEventDefinition[] BuildManualEvents()
        {
            var events = new List<ManualEventDefinition>(18);

            // =================================================================
            // Night 1 - spec 28 step 6 builds these first.
            // =================================================================

            // M06 명부에 없는 수취인의 택배. The first anomaly the caretaker meets, and the one
            // that teaches the shape of all the others: the manual says what to do, and the
            // reason the parcel is addressed to a unit that does not exist is not in it.
            events.Add(Event(ManualEventIds.M06_LostParcel, 1, FloorPlan.F1, ZoneIds.Lobby, 0,
                ManualEventTrigger.NightStart, null,
                objectives: new[]
                {
                    Step("obj_verify", "manual.m06.objective.verify"),
                    Step("obj_stamp", "manual.m06.objective.stamp"),
                    Step("obj_shelve", "manual.m06.objective.shelve"),
                    Step("obj_return_to_office", "manual.m06.objective.return")
                },
                correct: new[]
                {
                    // Spec 22 M06: two of the three record checks are the minimum grounds for
                    // sending it back, which is the same two-source rule the visitor judgements
                    // run on (GDD 13.2).
                    ManualCheckDefinition.Literal("recordChecks", ManualCompare.AtLeast, 2),
                    ManualCheckDefinition.IsFalse("opened").Breaking("manual.m06.forbid.1"),
                    ManualCheckDefinition.IsFalse("shaken").Breaking("manual.m06.forbid.2"),
                    ManualCheckDefinition.IsFalse("lookedBack").Breaking("manual.m06.forbid.3")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.routine_correct"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.routine_correct")
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Distortion(10),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F1, 1),
                    // Spec 22 M06: opening it costs a locker for the rest of the run.
                    NextNight(ConsequenceDefinition.Notify("notify.m06.locker_unusable"))
                },
                failSafe: ManualFailSafe(2, "ENABLE_RETURN_STAMP", "manual.m06.failsafe")));

            // M14 무인 운동기구. Built second on purpose (spec 28): its wrong answer is the
            // one the room is designed to suggest, so it teaches that the obvious control is
            // not the procedure.
            events.Add(Event(ManualEventIds.M14_Treadmills, 1, FloorPlan.F2, ZoneIds.FitnessRoom, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M06_LostParcel,
                objectives: new[]
                {
                    Step("obj_announce", "manual.m14.objective.announce"),
                    Step("obj_tap_sign", "manual.m14.objective.tap"),
                    Step("obj_stop", "manual.m14.objective.stop")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsFalse("mainPowerCut").Breaking("manual.m14.forbid.1"),
                    ManualCheckDefinition.Literal("machinesStopped", ManualCompare.Equals, 3)
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.routine_correct"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F2, -1)
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, -5, "reason.wrong_procedure"),
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -2, "reason.wrong_procedure"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F2, 1)
                },
                failSafe: ManualFailSafe(2, "SHOW_SPEED_READOUT", "manual.m14.failsafe")));

            // M13 기억 엽서. One a night from night 1 to night 5. Not a hazard - a temptation,
            // and the only M event whose correct answer is allowed to be "do not engage".
            var postcards = Event(ManualEventIds.M13_Postcards, 1, FloorPlan.F1, ZoneIds.Lobby, 0,
                ManualEventTrigger.NightStart, null,
                objectives: new[] { Step("obj_decide", "manual.m13.objective.decide") },
                correct: new[]
                {
                    // Order matters: the prohibition is tested first so that writing a guess
                    // into the fact column is reported as the broken rule rather than as an
                    // unfinished job. Everything below it names no rule, because not shredding
                    // a postcard is a choice the manual offers, not one it forbids.
                    ManualCheckDefinition.IsFalse("guessedAsFact").Breaking("manual.m13.forbid.1"),
                    ManualCheckDefinition.IsTrue("shredded"),
                    ManualCheckDefinition.IsFalse("read")
                },
                partial: new[]
                {
                    // Spec 22 M13's second course: read it, then separate what is confirmed
                    // from what is merely claimed. It costs more than the shredder and buys
                    // something the shredder does not.
                    ManualCheckDefinition.IsFalse("guessedAsFact"),
                    ManualCheckDefinition.IsTrue("logged")
                },
                // Spec 22 M13 prices the three courses and nothing else. Shredding it unread
                // is the only act in the game that lowers exposure by itself.
                onCorrect: new[] { ConsequenceDefinition.Distortion(-2) },
                onPartial: new[]
                {
                    // +5 for having read it, -3 for the verification memo. The player pays two
                    // and keeps the clue.
                    ConsequenceDefinition.Distortion(2),
                    ConsequenceDefinition.Flag(FlagIds.OptionalClueUnlocked),
                    ConsequenceDefinition.Notify("notify.m13.logged")
                },
                onWrong: new[] { ConsequenceDefinition.Distortion(5) },
                failSafe: ManualFailSafe(0, null, null));
            postcards.repeatsNightly = true;
            postcards.lastNightIndex = 5;
            events.Add(postcards);

            // =================================================================
            // Night 2 - spec 28 step 7.
            // =================================================================

            // M02 반대로 걷는 주차장 주민. The rule the player will be tested on for the rest
            // of the game: do not look, do not zoom, keep the distance, announce, leave.
            events.Add(Event(ManualEventIds.M02_BackwardsWalker, 2, FloorPlan.B1, ZoneIds.Parking, 0,
                ManualEventTrigger.NightStart, null,
                objectives: new[]
                {
                    Step("obj_announce", "manual.m02.objective.announce"),
                    Step("obj_reach_exit", "manual.m02.objective.exit")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsFalse("lookedAtFace").Breaking("manual.m02.forbid.1"),
                    ManualCheckDefinition.IsFalse("usedZoom").Breaking("manual.m02.forbid.2"),
                    ManualCheckDefinition.IsFalse("closedWithinFourMetres").Breaking("manual.m02.forbid.3"),
                    ManualCheckDefinition.IsTrue("announced"),
                    ManualCheckDefinition.IsTrue("exitReached")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, 1, "reason.routine_correct"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.B1, -1)
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Distortion(15),
                    ConsequenceDefinition.FloorRisk(FloorPlan.B1, 1)
                },
                failSafe: ManualFailSafe(2, "MARK_EXIT_ROUTE", "manual.m02.failsafe")));

            // M03 폐가전의 내일 순찰일지. The spec 0.9.2 "broadcast forecasts tomorrow" source,
            // and the one event whose reward is a rule rather than a stat: solving it hands
            // the player M01 a night early.
            events.Add(Event(ManualEventIds.M03_TomorrowsLog, 2, FloorPlan.B1, ZoneIds.RecyclingYard, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M02_BackwardsWalker,
                objectives: new[]
                {
                    Step("obj_record", "manual.m03.objective.record"),
                    Step("obj_memo", "manual.m03.objective.memo")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsTrue("recorded"),
                    // Spec 22 M03: two of the three instructions is enough. The same counter
                    // serves the memo ticked tonight and the instruction carried out tomorrow,
                    // because the spec counts them as the same act.
                    ManualCheckDefinition.Literal("warningsApplied", ManualCompare.AtLeast, 2)
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Flag(FlagIds.JiwooExposurePrevented),
                    ConsequenceDefinition.ManualPage(ManualEventIds.PageOf(ManualEventIds.M01_PhantomFloor)),
                    // Spec 21.3 prices carrying out tomorrow's instruction: the sign goes up,
                    // the student never stands in front of the lift, and the sixth floor is
                    // one step calmer for the rest of the game.
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 2, "reason.future_warning_applied"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F6, -1)
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Flag(FlagIds.JiwooExposurePrevented, false),
                    NextNight(ConsequenceDefinition.FloorRisk(FloorPlan.F6, 1))
                },
                failSafe: ManualFailSafe(2, "REPLAY_BROADCAST", "manual.m03.failsafe")));

            // M12 예약되지 않은 세탁. Waiting is the whole procedure; the timer counts down
            // honestly, and the only way to fail is to be impatient.
            events.Add(Event(ManualEventIds.M12_NightLaundry, 2, FloorPlan.F1, ZoneIds.Laundry, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M03_TomorrowsLog,
                objectives: new[]
                {
                    Step("obj_wait", "manual.m12.objective.wait"),
                    Step("obj_retrieve", "manual.m12.objective.retrieve"),
                    Step("obj_incinerate", "manual.m12.objective.incinerate")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsFalse("forcedStop").Breaking("manual.m12.forbid.1"),
                    ManualCheckDefinition.IsFalse("forcedDoor").Breaking("manual.m12.forbid.2"),
                    ManualCheckDefinition.IsTrue("dollIncinerated")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.routine_correct")
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -3, "reason.wrong_procedure"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F1, 2),
                    NextNight(ConsequenceDefinition.Notify("notify.m12.wet_floor"))
                },
                failSafe: ManualFailSafe(2, "SHOW_TIMER_REMAINING", "manual.m12.failsafe")));

            // =================================================================
            // Night 3 - spec 28 step 8 rewrites C05 in this shape.
            // =================================================================

            // M01 존재하지 않는 13층. The building announces a floor it does not have, and the
            // procedure is three broadcasts and the patience not to look. Solving it is what
            // opens the 4F service level, so the whole main investigation runs through a
            // manual rule being obeyed.
            events.Add(Event(ManualEventIds.M01_PhantomFloor, 3, FloorPlan.F1, ZoneIds.Office, 0,
                ManualEventTrigger.NightStart, null,
                objectives: new[]
                {
                    Step("obj_broadcast", "manual.m01.objective.broadcast"),
                    Step("obj_wait_sensor", "manual.m01.objective.wait_sensor"),
                    Step("obj_check_log", "manual.m01.objective.check_log"),
                    Step("obj_service_level", "manual.m01.objective.service_level")
                },
                correct: new[]
                {
                    ManualCheckDefinition.Literal("broadcastCount", ManualCompare.Equals, 3),
                    ManualCheckDefinition.IsFalse("cctvZoomed").Breaking("manual.m01.forbid.1"),
                    ManualCheckDefinition.IsTrue("sensorOff"),
                    ManualCheckDefinition.IsTrue("logChecked"),
                    ManualCheckDefinition.IsTrue("serviceLevelEntered")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Evidence("E07_HIDDEN_MAINTENANCE_FLOOR_LOG"),
                    ConsequenceDefinition.Flag(FlagIds.MaintenanceMapOwned),
                    ConsequenceDefinition.Flag(FlagIds.Floor16Found),
                    new ConsequenceDefinition
                    {
                        type = ConsequenceType.GrantAccess, amount = (int)AccessLevel.Maintenance
                    }
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Distortion(15),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F4, 1)
                },
                failSafe: ManualFailSafe(2, "REVEAL_BROADCAST_PANEL", "manual.m01.failsafe",
                                   ManualEventIds.PageOf(ManualEventIds.M01_PhantomFloor))));

            // M05 재활용 불가 부산물. Four bags, four seals, two at a time, one at a time into
            // the incinerator. Every wrong answer here is a shortcut.
            events.Add(Event(ManualEventIds.M05_NotRecyclable, 3, FloorPlan.B1, ZoneIds.RecyclingYard, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M01_PhantomFloor,
                objectives: new[]
                {
                    Step("obj_seal", "manual.m05.objective.seal"),
                    Step("obj_cart", "manual.m05.objective.cart"),
                    Step("obj_incinerate", "manual.m05.objective.incinerate")
                },
                correct: new[]
                {
                    ManualCheckDefinition.Literal("sealCount", ManualCompare.Equals, 4),
                    ManualCheckDefinition.Literal("incinerated", ManualCompare.Equals, 4),
                    ManualCheckDefinition.IsFalse("treatedAsGeneralWaste").Breaking("manual.m05.forbid.2"),
                    ManualCheckDefinition.IsFalse("cartOverloaded").Breaking("manual.m05.forbid.3")
                },
                onCorrect: new[]
                {
                    // Spec 22 M05 states the failure costs and not the reward. +2 Performance
                    // is the value every other correctly handled routine job pays.
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.routine_correct")
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.FloorRisk(FloorPlan.B1, 1),
                    ConsequenceDefinition.Distortion(10),
                    NextNight(ConsequenceDefinition.Notify("notify.m05.bags_returned"))
                },
                failSafe: ManualFailSafe(2, "HIGHLIGHT_SEAL_STICKERS", "manual.m05.failsafe")));

            // M18 옥상 난간 인물. The one event permitted to end a shift, and only on a repeat
            // of a prohibition the manual prints as lethal (spec 0.10.5).
            var roof = Event(ManualEventIds.M18_RoofFigure, 3, FloorPlan.Roof, ZoneIds.Rooftop, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M05_NotRecyclable,
                objectives: new[]
                {
                    Step("obj_broadcast", "manual.m18.objective.broadcast"),
                    Step("obj_wait", "manual.m18.objective.wait")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsFalse("approachedWithinFiveMetres").Breaking("manual.m18.forbid.2"),
                    ManualCheckDefinition.IsFalse("crossedThreshold").Breaking("manual.m18.forbid.3"),
                    ManualCheckDefinition.Literal("broadcastCount", ManualCompare.Equals, 1),
                    ManualCheckDefinition.IsTrue("figureGone")
                },
                // The unlabelled tape left on the landing, which is what makes the lounge set
                // in spec 23 A03 more than a set. It is handed over on either outcome on
                // purpose: an anomalous tool is never the reward for having got something
                // right, and locking A03 behind a clean M18 would make one mistake on night 3
                // quietly remove a whole machine from the rest of the game.
                onCorrect: new[]
                {
                    ConsequenceDefinition.FloorRisk(FloorPlan.Roof, -1),
                    ConsequenceDefinition.Flag(ItemIds.BlackTape)
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Distortion(15),
                    ConsequenceDefinition.FloorRisk(FloorPlan.Roof, 1),
                    ConsequenceDefinition.Flag(ItemIds.BlackTape)
                },
                failSafe: ManualFailSafe(2, "MARK_BROADCAST_PANEL", "manual.m18.failsafe"));
            roof.lethalOnRepeat = true;
            events.Add(roof);

            // =================================================================
            // Night 4 - spec 28 step 9.
            // =================================================================

            // M16 404 벽 노크. Spec 26 uses this event as the worked example of the data
            // format, so its checks are written exactly as that section states them.
            events.Add(Event(ManualEventIds.M16_KnockEcho, 4, FloorPlan.F4, ZoneIds.Floor04, 0,
                ManualEventTrigger.NightStart, null,
                objectives: new[]
                {
                    Step("obj_wait_sequence", "manual.m16.objective.wait_sequence"),
                    Step("obj_count", "manual.m16.objective.count", hidden: true),
                    Step("obj_reply", "manual.m16.objective.reply"),
                    Step("obj_wait_ten", "manual.m16.objective.wait_ten")
                },
                correct: new[]
                {
                    ManualCheckDefinition.Counters("replyCount", ManualCompare.Equals, "sourceCount"),
                    ManualCheckDefinition.IsFalse("playerInitiatedEarly").Breaking("manual.m16.forbid.1")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.found_404_clue"),
                    ConsequenceDefinition.Evidence("E22_KNOCK_NOTE")
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Distortion(8),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F4, 1),
                    ConsequenceDefinition.Sequence("F4_LIGHTS_OUT")
                },
                failSafe: ManualFailSafe(2, "ENABLE_KNOCK_REPLAY", "manual.m16.failsafe"),
                startConditions: new[] { ConditionDefinition.Flag(FlagIds.Knows404) }));

            // M17 504호 흔적. Three sounds, and the loudest is not the cause - the old
            // complaint record is. A wrong answer here raises 5F risk, which is what makes
            // night 5's corridor loop worse (spec 30.3).
            events.Add(Event(ManualEventIds.M17_Unit504Noise, 4, FloorPlan.F5, ZoneIds.Floor05, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M16_KnockEcho,
                objectives: new[]
                {
                    Step("obj_measure", "manual.m17.objective.measure"),
                    Step("obj_check_complaints", "manual.m17.objective.complaints"),
                    Step("obj_post_notice", "manual.m17.objective.notice")
                },
                correct: new[]
                {
                    // 1 = TV audio, 2 = furniture being dragged, 3 = pipe friction.
                    ManualCheckDefinition.Literal("causeChoice", ManualCompare.Equals, 2),
                    ManualCheckDefinition.IsTrue("noticePosted"),
                    ManualCheckDefinition.IsFalse("wallBroken").Breaking("manual.m17.forbid.1"),
                    ManualCheckDefinition.IsFalse("powerCut").Breaking("manual.m17.forbid.2")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.routine_correct"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F5, -1)
                },
                onWrong: new[] { ConsequenceDefinition.FloorRisk(FloorPlan.F5, 1) },
                failSafe: ManualFailSafe(2, "HIGHLIGHT_OLD_COMPLAINT", "manual.m17.failsafe")));

            // M15 화단 이상 증식. A timed pruning job. Cutting too much is its own failure, so
            // "cut everything" is not a safe fallback.
            events.Add(Event(ManualEventIds.M15_PlanterGrowth, 4, FloorPlan.F2, ZoneIds.Terrace, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M17_Unit504Noise,
                objectives: new[]
                {
                    Step("obj_cut", "manual.m15.objective.cut"),
                    Step("obj_bag", "manual.m15.objective.bag")
                },
                correct: new[]
                {
                    ManualCheckDefinition.Literal("mutantCuts", ManualCompare.AtLeast, 6),
                    ManualCheckDefinition.IsFalse("rootPulled").Breaking("manual.m15.forbid.1"),
                    ManualCheckDefinition.Literal("normalCuts", ManualCompare.AtMost, 2).Breaking("manual.m15.forbid.2")
                },
                partial: new[]
                {
                    ManualCheckDefinition.Literal("mutantCuts", ManualCompare.AtLeast, 4),
                    ManualCheckDefinition.IsFalse("rootPulled")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, 2, "reason.routine_correct"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F2, -1)
                },
                onPartial: new[] { ConsequenceDefinition.Notify("notify.m15.partial") },
                onWrong: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3, "reason.wrong_procedure")
                },
                failSafe: ManualFailSafe(2, "MARK_MUTATED_BRANCHES", "manual.m15.failsafe")));

            // M04 404호 천장 오염 민원. The complaint card has no delete button: spec 22 M04
            // makes refusing the job a choice with a price rather than a way out. The number of
            // stains is set when the event starts, because night 4's C07 decision changes it
            // (spec 31), which is why the check compares two counters instead of a literal.
            events.Add(Event(ManualEventIds.M04_CeilingStain, 4, FloorPlan.F4, ZoneIds.Floor04, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M15_PlanterGrowth,
                objectives: new[]
                {
                    Step("obj_neutralizer", "manual.m04.objective.neutralizer"),
                    Step("obj_clean", "manual.m04.objective.clean"),
                    Step("obj_close_door", "manual.m04.objective.close_door")
                },
                correct: new[]
                {
                    ManualCheckDefinition.Counters("stainsCleaned", ManualCompare.Equals, "stainTarget"),
                    ManualCheckDefinition.IsFalse("lookedBack").Breaking("manual.m04.forbid.3"),
                    ManualCheckDefinition.IsFalse("enteredDeep").Breaking("manual.m04.forbid.2"),
                    ManualCheckDefinition.IsTrue("doorClosed")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, 3, "reason.correct_report"),
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.found_404_clue"),
                    ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, 1, "reason.correct_report"),
                    ConsequenceDefinition.Evidence("E23_404_CLEANING_REQUEST")
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, -5, "reason.wrong_procedure"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -1, "reason.wrong_procedure"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.F4, 1)
                },
                failSafe: ManualFailSafe(2, "MARK_REMAINING_STAINS", "manual.m04.failsafe"),
                startConditions: new[] { ConditionDefinition.Flag(FlagIds.Knows404) }));

            // =================================================================
            // Night 5 - spec 28 step 10.
            // =================================================================

            // M09 검은 물. The red emergency-stop button works, and using it is how the
            // building loses its water supply for the evacuation (spec 30.3).
            events.Add(Event(ManualEventIds.M09_BlackWater, 5, FloorPlan.B2, ZoneIds.PumpRoom, 0,
                ManualEventTrigger.NightStart, null,
                objectives: new[]
                {
                    Step("obj_sample", "manual.m09.objective.sample"),
                    Step("obj_cartridges", "manual.m09.objective.cartridges"),
                    Step("obj_residue", "manual.m09.objective.residue"),
                    Step("obj_normal", "manual.m09.objective.normal")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsFalse("emergencyStop").Breaking("manual.m09.forbid.1"),
                    ManualCheckDefinition.IsTrue("sampleTaken"),
                    ManualCheckDefinition.IsTrue("cartridgeOrderCorrect"),
                    ManualCheckDefinition.Literal("residueHandled", ManualCompare.AtLeast, 3),
                    // Pressure in decibars, so 2.4 bar is 24 and the counter stays an integer.
                    ManualCheckDefinition.Literal("pressureDecibar", ManualCompare.AtLeast, 24)
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, 3, "reason.routine_correct"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.B2, -1)
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -8, "reason.wrong_procedure"),
                    ConsequenceDefinition.Stat(StatIds.CommunityTrust, -3, "reason.wrong_procedure"),
                    ConsequenceDefinition.Flag(FlagIds.WaterOutage)
                },
                failSafe: ManualFailSafe(2, "LABEL_CARTRIDGE_ORDER", "manual.m09.failsafe")));

            // M10 비등록 배관. Five valves, one pulse at 0.8Hz. Closing everything is the
            // instinct and costs the most.
            events.Add(Event(ManualEventIds.M10_PipeGrowth, 5, FloorPlan.B2, ZoneIds.PipeRoom, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M09_BlackWater,
                objectives: new[]
                {
                    Step("obj_measure", "manual.m10.objective.measure"),
                    Step("obj_quarter_turn", "manual.m10.objective.quarter_turn"),
                    Step("obj_wait", "manual.m10.objective.wait")
                },
                correct: new[]
                {
                    ManualCheckDefinition.Literal("valveChosen", ManualCompare.Equals, 4),
                    ManualCheckDefinition.IsTrue("quarterTurnOnly"),
                    ManualCheckDefinition.IsFalse("pipeCut").Breaking("manual.m10.forbid.1"),
                    ManualCheckDefinition.IsFalse("allValvesClosed").Breaking("manual.m10.forbid.2")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, 2, "reason.routine_correct"),
                    ConsequenceDefinition.Evidence("E24_PIPE_AUDIO")
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.BuildingSafety, -2, "reason.wrong_procedure"),
                    ConsequenceDefinition.FloorRisk(FloorPlan.B2, 1)
                },
                failSafe: ManualFailSafe(2, "SHOW_PULSE_OVERLAY", "manual.m10.failsafe")));

            // M11 잔상 주차. Deleting the sensor record is the fast answer and leaves an
            // invisible obstruction in the B1 route the evacuation needs (spec 22 M11).
            events.Add(Event(ManualEventIds.M11_GhostParking, 5, FloorPlan.B1, ZoneIds.Parking, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M10_PipeGrowth,
                objectives: new[]
                {
                    Step("obj_visit", "manual.m11.objective.visit"),
                    Step("obj_barrier", "manual.m11.objective.barrier"),
                    Step("obj_sticker", "manual.m11.objective.sticker"),
                    Step("obj_exit_log", "manual.m11.objective.exit_log")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsTrue("barrierPlaced"),
                    ManualCheckDefinition.IsTrue("stickerPlaced"),
                    ManualCheckDefinition.IsFalse("sensorRecordDeleted").Breaking("manual.m11.forbid.1"),
                    ManualCheckDefinition.IsFalse("enteredBay").Breaking("manual.m11.forbid.2"),
                    ManualCheckDefinition.IsTrue("exitLogged")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, 2, "reason.routine_correct"),
                    ConsequenceDefinition.Flag(FlagIds.GhostCarResolved)
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.FloorRisk(FloorPlan.B1, 1),
                    ConsequenceDefinition.Flag(FlagIds.GhostCarResolved, false)
                },
                failSafe: ManualFailSafe(2, "SHOW_WATER_OUTLINE", "manual.m11.failsafe")));

            // M07 5층 복도 무한 루프. The corridor repeats, and so does the stairwell above it
            // (spec 0.8.4). 504 is the one fixture whose state changes every lap, which is why
            // the manual says to compare fittings rather than door numbers.
            events.Add(Event(ManualEventIds.M07_CorridorLoop, 5, FloorPlan.F5, ZoneIds.Floor05, 0,
                ManualEventTrigger.AfterEvent, ManualEventIds.M11_GhostParking,
                objectives: new[]
                {
                    Step("obj_compare", "manual.m07.objective.compare"),
                    Step("obj_ring", "manual.m07.objective.ring")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsTrue("correctBellRung"),
                    ManualCheckDefinition.IsFalse("ran").Breaking("manual.m07.forbid.1")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.Performance, 1, "reason.routine_correct")
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.FloorRisk(FloorPlan.F5, 1),
                    ConsequenceDefinition.Distortion(5)
                },
                // Spec 22 M07: three wrong bells and the game points at the extinguisher seal.
                failSafe: ManualFailSafe(3, "HINT_EXTINGUISHER_SEAL", "manual.m07.failsafe")));

            // =================================================================
            // Night 6 - spec 28 step 12.
            // =================================================================

            // M08 놀이터의 그림자 아이들. Three separate routes lead to the same object, so a
            // player who never touched the anomalous tools can still solve it (spec 32).
            events.Add(Event(ManualEventIds.M08_ShadowChildren, 6, FloorPlan.F1, ZoneIds.Playground, 0,
                ManualEventTrigger.NightStart, null,
                objectives: new[]
                {
                    Step("obj_identify", "manual.m08.objective.identify"),
                    Step("obj_obtain_ball", "manual.m08.objective.obtain"),
                    Step("obj_place", "manual.m08.objective.place")
                },
                correct: new[]
                {
                    ManualCheckDefinition.IsTrue("ballPlaced"),
                    ManualCheckDefinition.IsFalse("wrongToyPlaced").Breaking("manual.m08.forbid.2"),
                    ManualCheckDefinition.IsFalse("approachedShadows").Breaking("manual.m08.forbid.1")
                },
                onCorrect: new[]
                {
                    ConsequenceDefinition.Stat(StatIds.HarinResonance, 1, "reason.found_404_clue"),
                    ConsequenceDefinition.Flag(FlagIds.CourtyardEscapeSafe)
                },
                onWrong: new[]
                {
                    ConsequenceDefinition.FloorRisk(FloorPlan.F1, 1),
                    ConsequenceDefinition.Distortion(10),
                    ConsequenceDefinition.Flag(FlagIds.CourtyardEscapeSafe, false)
                },
                failSafe: ManualFailSafe(2, "REVEAL_LOST_PROPERTY", "manual.m08.failsafe")));

            return events.ToArray();
        }

        // ---- builders ---------------------------------------------------------

        static ManualEventDefinition Event(
            string eventId, int night, string floorId, string zoneId, int earliestSecond,
            ManualEventTrigger trigger, string triggerTarget,
            ManualObjectiveDefinition[] objectives,
            ManualCheckDefinition[] correct,
            ConsequenceDefinition[] onCorrect,
            ConsequenceDefinition[] onWrong,
            ManualFailSafeDefinition failSafe,
            ManualCheckDefinition[] partial = null,
            ConsequenceDefinition[] onPartial = null,
            ConditionDefinition[] startConditions = null)
        {
            var def = ScriptableObject.CreateInstance<ManualEventDefinition>();
            string slug = eventId.ToLowerInvariant();

            def.eventId = eventId;
            def.name = eventId;
            def.nameKey = "manual." + slug + ".title";
            def.summaryKey = "manual." + slug + ".summary";
            def.nightIndex = night;
            def.floorId = floorId;
            def.zoneId = zoneId;
            def.earliestSecond = earliestSecond;
            def.trigger = trigger;
            def.triggerTarget = triggerTarget;
            def.startConditions = startConditions ?? new ConditionDefinition[0];
            def.manualPageId = ManualEventIds.PageOf(eventId);
            def.objectives = objectives ?? new ManualObjectiveDefinition[0];
            def.correctConditions = correct ?? new ManualCheckDefinition[0];
            def.partialConditions = partial ?? new ManualCheckDefinition[0];
            def.onCorrect = onCorrect ?? new ConsequenceDefinition[0];
            def.onPartial = onPartial ?? new ConsequenceDefinition[0];
            def.onWrong = onWrong ?? new ConsequenceDefinition[0];
            def.failSafe = failSafe ?? new ManualFailSafeDefinition();
            return def;
        }

        static ManualObjectiveDefinition Step(string id, string titleKey,
                                              bool optional = false, bool hidden = false)
        {
            return new ManualObjectiveDefinition
            {
                objectiveId = id, titleKey = titleKey, optional = optional, hidden = hidden
            };
        }

        static ManualFailSafeDefinition ManualFailSafe(int afterWrongAttempts, string action,
                                                 string notifyKey, string revealPageId = null)
        {
            return new ManualFailSafeDefinition
            {
                afterWrongAttempts = afterWrongAttempts,
                action = action,
                notifyKey = notifyKey,
                revealPageId = revealPageId
            };
        }

        /// <summary>Defer a consequence to the start of the next night (spec 0.1 step 8).</summary>
        static ConsequenceDefinition NextNight(ConsequenceDefinition consequence)
        {
            consequence.nextNight = true;
            return consequence;
        }
    }
}
