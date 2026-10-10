using NO404.Core;
using NO404.Evidence;

namespace NO404.Cases
{
    /// <summary>
    /// The runtime half of the night 1 / 3 / 5 subquests (v5.1 10, 12, 14).
    ///
    /// The quests themselves are data in SeedContent.V51Pools. What lives here is what data
    /// cannot say: a panel that shocks you unless its breaker is down, a corridor that hands
    /// you back to its own start, a night-5 job whose shape depends on what was chosen on
    /// night 1. Every one of those reads and writes the saved flag/stat/choice state, so a
    /// reload lands in exactly the same building.
    /// </summary>
    public static class SubquestRules
    {
        // ---- state written here (all saved through GameStateService) -----------

        public const string N1R02BreakerOff = "N1R02_BREAKER_OFF";
        public const string N1R02PanelWarned = "N1R02_PANEL_WARNED";
        public const string N1R02Shocked = "N1R02_SHOCKED";
        public const string N1R06Slippery = "N1R06_SLIPPERY";
        public const string N1R06Slipped = "N1R06_SLIPPED";
        public const string N1R10MeterTurning = "N1R10_METER_TURNING";
        public const string N1R11Ignored = "N1R11_IGNORED";
        public const string MinseoEscorted = "N1R01_GRANT_ESCORT";
        public const string MinseoFloorPass = "N1R01_GRANT_FLOORPASS";
        public const string MinseoLobbyOnly = "N1R01_GRANT_LOBBY";
        public const string MinseoDenied = "N1R01_GRANT_DENIED";

        public const string N3R03TensionSet = "N3R03_TENSION_SET";
        public const string N3R08Looping = "N3R08_LOOPING";
        public const string N3R08Loops = "N3R08_LOOP_COUNT";
        public const string N3R08Escaped = "N3R08_ESCAPED";
        public const string N3R13Loops = "N3R13_LOOP_COUNT";
        public const string N3R14Opened = "N3R14_OPENED";
        public const string WallResponse404 = "404_WALL_RESPONSE";
        public const string StairLightFixed = "STAIR_LIGHT_FIXED";
        public const string F4ReferenceMarker = "F4_REFERENCE_MARKER";
        public const string FutureWarning404 = "FUTURE_WARNING_404";
        public const string FireDoorPrep = "FIRE_DOOR_PREP";

        public const string N5SunjaReady = "N5_SUNJA_READY";
        public const string N5SunjaWeak = "N5_SUNJA_WEAK";
        public const string N5SunjaClosed = "N5_SUNJA_CLOSED";
        public const string N5PumpOk = "N5_PUMP_OK";
        public const string N5PumpOverload = "N5_PUMP_OVERLOAD";
        public const string N5PumpLeak = "N5_PUMP_LEAK";
        public const string N5PumpHandled = "N5_PUMP_VALVE_HANDLED";
        public const string N5SensorTrue = "N5_SENSOR_TRUE";
        public const string N5SensorUnreliable = "N5_SENSOR_UNRELIABLE";
        public const string N5RecorderSafe = "N5_RECORDER_SAFE";
        public const string N5RecorderGone = "N5_RECORDER_GONE";
        public const string N5PackageReal = "N5_PACKAGE_IS_THE_LOST_ONE";
        public const string N5ArchiveDoorIntact = "N5R04_DOOR_INTACT";
        public const string N5PanelHandled = "N5R01_PANEL_HANDLED";
        public const string N5SprinklerPowerCut = "N5R12_POWER_CUT";
        public const string N5SprinklerShocked = "N5R12_SHOCKED";
        public const string N5R10Looping = "N5R10_LOOPING";
        public const string N5R10Loops = "N5R10_LOOP_COUNT";
        public const string N5R13CrossChecked = "N5R13_CROSSCHECKED";
        public const string N5R13CalledHerName = "N5R13_SEOWOO_VARIANT";
        public const string N5R14Collided = "N5R14_COLLIDED";
        public const string ElevatorShed = "N5_ELEVATOR_SHED";

        /// <summary>The facility app's 304 water chart (N1-M01), and the log N1-R10 files it as.</summary>
        const string WaterReading304 = "EV_304_WATER";
        const string WaterLog304 = "EV_N1R10_LOG";

        /// <summary>v5.1 N3-R08: three loops, then the corridor stops hiding its marker.</summary>
        public const int LoopsBeforeClearHint = 3;
        /// <summary>v5.1 N3-R08: SAN -3 a loop, at most -15.</summary>
        public const int LoopSanCost = 3;
        public const int LoopSanCap = 15;

        public static bool Handles(string caseId)
        {
            if (string.IsNullOrEmpty(caseId) || caseId.Length < 5) return false;
            return (caseId.StartsWith("N1-R") || caseId.StartsWith("N3-R") || caseId.StartsWith("N5-R"));
        }

        public static bool Active(string caseId)
        {
            var cases = ServiceHub.Cases;
            if (cases == null) return false;
            var runtime = cases.Find(caseId);
            return runtime != null && runtime.State.IsActive();
        }

        static GameStateService State { get { return ServiceHub.State; } }

        // -----------------------------------------------------------------
        // the draw
        // -----------------------------------------------------------------

        /// <summary>
        /// v5.1 4.4 step 7: a quest an earlier choice caused outranks one the night invented.
        /// Only the links v5.1 actually writes down are here.
        /// </summary>
        public static int WeightBonus(CaseDefinition def)
        {
            var state = State;
            if (def == null || state == null) return 0;

            switch (def.caseId)
            {
                case "N3-R09": return state.GetFlag(N1R11Ignored) ? 50 : 0;
                case "N5-R02": return state.ChoiceIs(ChoiceIds.SunjaCare, "GOOD") ? 0 : 40;
                case "N5-R03": return state.ChoiceIs(ChoiceIds.PumpStatus, "REPAIRED") ? 0 : 40;
                case "N5-R04": return state.GetStat(StatIds.ChairmanAlert) / 2;
                case "N5-R05":
                    return state.GetDebt(DebtIds.Access) > 0 || state.ChoiceIs(ChoiceIds.OldPackageStatus, "LOST") ? 40 : 0;
                case "N5-R06": return state.ChoiceIs(ChoiceIds.ServiceRecorder, "REMOVED") ? 40 : 0;
                case "N5-R07": return state.ChoiceIs(ChoiceIds.FireSensorStatus, "FIXED") ? 0 : 40;
                case "N5-R10":
                    return state.GetStat("PLAYER_LOST_COUNT") >= 2 || state.GetDebt(DebtIds.Distortion) >= 2 ? 50 : 0;
            }
            return 0;
        }

        // -----------------------------------------------------------------
        // lifecycle
        // -----------------------------------------------------------------

        /// <summary>
        /// Fixes a quest's variant the moment it opens. Written as flags so the report's
        /// decision availability, the props and a reload all read one answer.
        /// </summary>
        public static void Started(string caseId)
        {
            if (!Handles(caseId)) return;
            var state = State;

            switch (caseId)
            {
                case "N1-R10":
                    // v5.1 N1-R10: the meter is turning on some runs and still on others.
                    state.SetFlag(N1R10MeterTurning, Roll(caseId, 2) == 0);
                    break;

                case "N3-R02":
                    Notify("quest.n3r02.choi_pressure", NotificationSeverity.Warning);
                    break;

                case "N3-R08":
                    state.SetFlag(N3R08Looping, true);
                    state.SetStat(N3R08Loops, 0);
                    break;

                case "N5-R02":
                    state.SetFlag(N5SunjaReady, state.ChoiceIs(ChoiceIds.SunjaCare, "GOOD"));
                    state.SetFlag(N5SunjaClosed, state.ChoiceIs(ChoiceIds.SunjaCare, "DENIED"));
                    state.SetFlag(N5SunjaWeak, !state.GetFlag(N5SunjaReady) && !state.GetFlag(N5SunjaClosed));
                    break;

                case "N5-R03":
                    state.SetFlag(N5PumpOk, state.ChoiceIs(ChoiceIds.PumpStatus, "REPAIRED"));
                    state.SetFlag(N5PumpLeak, state.ChoiceIs(ChoiceIds.PumpStatus, "IGNORED"));
                    state.SetFlag(N5PumpOverload, !state.GetFlag(N5PumpOk) && !state.GetFlag(N5PumpLeak));
                    Notify(state.GetFlag(N5PumpOk) ? "quest.n5r03.variant.ok"
                         : state.GetFlag(N5PumpLeak) ? "quest.n5r03.variant.leak" : "quest.n5r03.variant.overload",
                           NotificationSeverity.Warning);
                    break;

                case "N5-R04":
                    // A chairman who has been watching closely sends somebody who can open the door.
                    state.SetFlag(N5ArchiveDoorIntact, state.GetStat(StatIds.ChairmanAlert) < 50);
                    break;

                case "N5-R05":
                    state.SetFlag(N5PackageReal, state.GetDebt(DebtIds.Access) > 0 ||
                                                 state.ChoiceIs(ChoiceIds.OldPackageStatus, "LOST"));
                    break;

                case "N5-R06":
                    state.SetFlag(N5RecorderGone, state.ChoiceIs(ChoiceIds.ServiceRecorder, "REMOVED"));
                    state.SetFlag(N5RecorderSafe, !state.GetFlag(N5RecorderGone));
                    break;

                case "N5-R07":
                    state.SetFlag(N5SensorTrue, state.ChoiceIs(ChoiceIds.FireSensorStatus, "FIXED"));
                    state.SetFlag(N5SensorUnreliable, !state.GetFlag(N5SensorTrue));
                    break;

                case "N5-R10":
                    state.SetFlag(N5R10Looping, true);
                    state.SetStat(N5R10Loops, 0);
                    if (state.GetFlag(FlagIds.SpaceRuleConfirmed))
                        Notify("quest.n5r10.space_memo", NotificationSeverity.Info);
                    break;

                case "N5-R13":
                    // Night 3's answered knock is the same pattern; it is waiting in the log.
                    if (state.GetFlag(WallResponse404))
                        ServiceHub.Evidence.Acquire("EV_N5R13_KNOCK", EvidenceSource.Dialogue);
                    break;
            }

            Persist();
        }

        /// <summary>v5.1 HP/SAN tables for the first time a piece of evidence is actually read.</summary>
        public static void EvidenceRead(string evidenceId)
        {
            var state = State;
            if (state == null || ServiceHub.Vitals == null) return;

            int strain = StrainFor(evidenceId);
            if (strain > 0 && !state.GetFlag(evidenceId + "_READ"))
            {
                state.SetFlag(evidenceId + "_READ", true);
                ServiceHub.Vitals.Strain(strain, "reason.subquest_evidence");
            }

            if (evidenceId == "EV_N5R13_PATTERN" && Roll("N5-R13", 4) == 0 && !state.GetFlag(N5R13CalledHerName))
            {
                // v5.1 N5-R13: a rare reactive variant. Shown, never explained.
                state.SetFlag(N5R13CalledHerName, true);
                Notify("quest.n5r13.seowoo", NotificationSeverity.Warning);
            }

            RefreshFacts();
        }

        static int StrainFor(string evidenceId)
        {
            var state = State;
            switch (evidenceId)
            {
                case "EV_N1R10_METER": return state.GetFlag(N1R10MeterTurning) ? 2 : 0;
                case "EV_N1R11_GHOST_PRESS": return 3;
                case "EV_N1R14_STICKER": return 3;
                case "EV_N3R12_CHILD_ITEMS": return 4;
                case "EV_N3R14_FUTURE_VOICE": return 6;
                case "EV_N5R06_2009_VOICE": return 3;
                case "EV_N5R09_SEAL": return 5;
                case "EV_N5R13_PATTERN": return 5;
                case "EV_N5R14_OVERLAP": return 4;
            }
            return 0;
        }

        /// <summary>Facts that only exist once two separate pieces agree.</summary>
        public static void RefreshFacts()
        {
            var state = State;
            var evidence = ServiceHub.Evidence;
            if (state == null || evidence == null) return;

            // v5.1 N5-R13: position, pattern and one independent witness to the pattern.
            bool witness = evidence.Has("EV_N5R13_KNOCK") || evidence.Has("EV_N5_WORK_LOG") ||
                           state.GetFlag(FlagIds.SunjaTestimony);
            if (evidence.Has("EV_N5R13_PATTERN") && evidence.Has("EV_N5R13_POSITION") && witness)
                state.SetFlag(N5R13CrossChecked, true);
        }

        public static void Tick(float deltaTime)
        {
            var state = State;
            var player = ServiceHub.Player;
            if (state == null || player == null || ServiceHub.Vitals == null) return;

            // v5.1 N1-R10 reads the facility log ("Facility Log / Field"), and the facility
            // app's 304 water chart - filed as N1-M01's evidence - is that log. A playtester
            // read the chart, saw it filed, and watched N1-R10 stay open: its log could only
            // be had from a panel on the office wall. Checked every tick rather than on the
            // two events, so the order they happen in and a save made in between both stop
            // mattering.
            var evidence = ServiceHub.Evidence;
            if (evidence != null && evidence.Has(WaterReading304) && !evidence.Has(WaterLog304) && Active("N1-R10"))
                evidence.Acquire(WaterLog304, EvidenceSource.Facility);

            // v5.1 N1-R06: a drain left overflowing is a floor that will take your feet.
            if (state.GetFlag(N1R06Slippery) && !state.GetFlag(N1R06Slipped) && player.CurrentZone == ZoneIds.Laundry)
            {
                state.SetFlag(N1R06Slipped, true);
                ServiceHub.Vitals.Damage(4, "reason.slipped");
                Notify("quest.n1r06.slipped", NotificationSeverity.Warning);
            }
        }

        /// <summary>A report that cannot be filed from where the caretaker is standing.</summary>
        public static string ReportBlockedKey(string caseId)
        {
            var state = State;
            if (state == null) return null;

            // A deadline that has come and gone hands the corridor back (GDD 14.4: no dead end).
            var runtime = ServiceHub.Cases != null ? ServiceHub.Cases.Find(caseId) : null;
            if (runtime != null && runtime.FailSafeFired) return null;

            if (caseId == "N3-R08" && state.GetFlag(N3R08Looping)) return "quest.loop.blocked";
            if (caseId == "N5-R10" && state.GetFlag(N5R10Looping)) return "quest.loop.blocked";
            return null;
        }

        public static void Resolved(string caseId, string decisionId)
        {
            if (!Handles(caseId)) return;
            var state = State;

            switch (caseId)
            {
                case "N1-R11":
                    if (decisionId == "dec_reboot") ServiceHub.Evidence.Remove("EV_N1R11_LIFT_LOG");
                    if (decisionId == "dec_ignore") state.SetFlag(N1R11Ignored, true);
                    break;

                case "N3-R09":
                    // The 4S line in the controller is the same line the night's main asks for.
                    if (decisionId == "dec_keep_log")
                        ServiceHub.Evidence.Acquire("EV_N3_LIFT_LOG", EvidenceSource.Facility);
                    break;

                case "N5-R01":
                    state.SetFlag(ElevatorShed, decisionId == "dec_cctv_lights_archive");
                    break;

                case "N3-R08": state.SetFlag(N3R08Looping, false); break;
                case "N5-R10": state.SetFlag(N5R10Looping, false); break;
            }

            Persist();
        }

        /// <summary>
        /// The door is where N1-R01 is decided; the report only files it. Each grant opens the
        /// one decision that matches it.
        /// </summary>
        public static void VisitorDecided(string visitorId, int level)
        {
            if (visitorId != "vis_minseo" || State == null) return;

            var access = (NO404.Visitors.VisitorAccessLevel)level;
            State.SetFlag(MinseoEscorted, access == NO404.Visitors.VisitorAccessLevel.Escorted);
            State.SetFlag(MinseoFloorPass, access == NO404.Visitors.VisitorAccessLevel.FloorPass ||
                                           access == NO404.Visitors.VisitorAccessLevel.FullTemporary);
            State.SetFlag(MinseoLobbyOnly, access == NO404.Visitors.VisitorAccessLevel.LobbyOnly ||
                                           access == NO404.Visitors.VisitorAccessLevel.Vestibule);
            State.SetFlag(MinseoDenied, access == NO404.Visitors.VisitorAccessLevel.Reject);
        }

        // -----------------------------------------------------------------
        // world actions
        // -----------------------------------------------------------------

        /// <summary>The case an action belongs to, so a prop is inert outside its quest.</summary>
        public static string CaseOf(string action)
        {
            if (string.IsNullOrEmpty(action) || action.Length < 4) return null;
            // "n1r02_panel" -> "N1-R02"
            return action.Substring(0, 2).ToUpperInvariant() + "-" + action.Substring(2, 3).ToUpperInvariant();
        }

        public static bool CanAct(string action)
        {
            var state = State;
            if (state == null || !Active(CaseOf(action))) return false;

            switch (action)
            {
                case "n1r02_breaker": return !state.GetFlag(N1R02BreakerOff);
                case "n1r02_panel": return !ServiceHub.Evidence.Has("EV_N1R02_FILTER");
                case "n3r03_tension": return !state.GetFlag(N3R03TensionSet);
                case "n3r04_knock": return !ServiceHub.Evidence.Has("EV_N3R04_REPLY");
                case "n3r08_follow_number":
                case "n3r08_run": return state.GetFlag(N3R08Looping);
                case "n3r08_marker": return state.GetFlag(N3R08Looping);
                case "n3r13_descend": return true;
                case "n3r14_open": return !state.GetFlag(N3R14Opened);
                case "n5r01_panel": return !state.GetFlag(N5PanelHandled);
                case "n5r03_valve": return !state.GetFlag(N5PumpHandled);
                case "n5r10_door": return state.GetFlag(N5R10Looping);
                case "n5r10_marker": return state.GetFlag(N5R10Looping);
                case "n5r12_power": return !state.GetFlag(N5SprinklerPowerCut);
                case "n5r12_valve": return !ServiceHub.Evidence.Has("EV_N5R12_VALVE");
                case "n5r14_follow": return !state.GetFlag(N5R14Collided);
            }
            return false;
        }

        /// <summary>
        /// Performs an action. Returns where the caretaker ends up when the action moves them
        /// ("zone:Floor05", "landing:F4"), or null when they stay put.
        /// </summary>
        public static string Act(string action)
        {
            if (!CanAct(action)) return null;
            var state = State;
            var vitals = ServiceHub.Vitals;
            string moveTo = null;

            switch (action)
            {
                case "n1r02_breaker":
                    state.SetFlag(N1R02BreakerOff, true);
                    Notify("quest.n1r02.breaker_off", NotificationSeverity.Info);
                    break;

                case "n1r02_panel":
                    // v5.1 N1-R02: warned first, hurt second, and repairable after either.
                    if (state.GetFlag(N1R02BreakerOff))
                        ServiceHub.Evidence.Acquire("EV_N1R02_FILTER", EvidenceSource.WorldPickup);
                    else if (!state.GetFlag(N1R02PanelWarned))
                    {
                        state.SetFlag(N1R02PanelWarned, true);
                        Notify("quest.n1r02.panel_warning", NotificationSeverity.Warning);
                    }
                    else if (!state.GetFlag(N1R02Shocked))
                    {
                        state.SetFlag(N1R02Shocked, true);
                        vitals.Damage(10, "reason.electric_shock");
                        Notify("quest.n1r02.shocked", NotificationSeverity.Warning);
                    }
                    else Notify("quest.n1r02.panel_warning", NotificationSeverity.Warning);
                    break;

                case "n3r03_tension":
                    // Read the worn closer first and the spring is set safely; guess, and it bites.
                    state.SetFlag(N3R03TensionSet, true);
                    if (!ServiceHub.Evidence.Has("EV_N3R03_CLOSER"))
                    {
                        vitals.Damage(4, "reason.hand_caught");
                        Notify("quest.n3r03.pinched", NotificationSeverity.Warning);
                    }
                    else Notify("quest.n3r03.tension_ok", NotificationSeverity.Info);
                    break;

                case "n3r04_knock":
                    ServiceHub.Evidence.Acquire("EV_N3R04_REPLY", EvidenceSource.WorldPickup);
                    vitals.Strain(4, "reason.something_answered");
                    break;

                case "n3r08_follow_number":
                case "n3r08_run":
                    moveTo = Loop(N3R08Loops, "zone:" + ZoneIds.Floor05, action == "n3r08_run");
                    break;

                case "n3r08_marker":
                    if (!ServiceHub.Evidence.Has("EV_N3R08_EXTINGUISHER"))
                    {
                        Notify("quest.loop.need_marker", NotificationSeverity.Warning);
                        break;
                    }
                    state.SetFlag(N3R08Looping, false);
                    state.SetFlag(N3R08Escaped, true);
                    vitals.NoteInvariantRead();
                    Notify("quest.loop.escaped", NotificationSeverity.Info);
                    break;

                case "n3r13_descend":
                    state.AddStat(N3R13Loops, 1);
                    vitals.Strain(6, "reason.lost_in_the_building");
                    state.AddDebt(DebtIds.Distortion, 1, "reason.lost_in_the_building");
                    Notify("quest.n3r13.same_landing", NotificationSeverity.Warning);
                    moveTo = "landing:" + Gameplay.FloorPlan.F4;
                    break;

                case "n3r14_open":
                    state.SetFlag(N3R14Opened, true);
                    vitals.Damage(8, "reason.opened_the_wrong_door");
                    vitals.Strain(8, "reason.heard_yourself");
                    break;

                case "n5r01_panel":
                    state.SetFlag(N5PanelHandled, true);
                    if (!state.ChoiceIs(ChoiceIds.PumpStatus, "REPAIRED"))
                    {
                        vitals.Damage(10, "reason.electric_shock");
                        Notify("quest.n5r01.overload_shock", NotificationSeverity.Warning);
                    }
                    ServiceHub.Evidence.Acquire("EV_N5R01_RESET", EvidenceSource.Facility);
                    break;

                case "n5r03_valve":
                    state.SetFlag(N5PumpHandled, true);
                    int burn = state.GetFlag(N5PumpLeak) ? 12 : state.GetFlag(N5PumpOverload) ? 5 : 0;
                    if (burn > 0) vitals.Damage(burn, "reason.steam");
                    ServiceHub.Evidence.Acquire("EV_N5R03_VALVE", EvidenceSource.WorldPickup);
                    break;

                case "n5r10_door":
                    moveTo = Loop(N5R10Loops, "zone:" + ZoneIds.Floor05, true);
                    break;

                case "n5r10_marker":
                    if (!ServiceHub.Evidence.Has("EV_N5R10_MARKER"))
                    {
                        Notify("quest.loop.need_marker", NotificationSeverity.Warning);
                        break;
                    }
                    state.SetFlag(N5R10Looping, false);
                    vitals.NoteInvariantRead();
                    Notify("quest.loop.escaped", NotificationSeverity.Info);
                    break;

                case "n5r12_power":
                    state.SetFlag(N5SprinklerPowerCut, true);
                    Notify("quest.n5r12.power_cut", NotificationSeverity.Info);
                    break;

                case "n5r12_valve":
                    // v5.1 N5-R12: the order is the whole job.
                    if (!state.GetFlag(N5SprinklerPowerCut) && !state.GetFlag(N5SprinklerShocked))
                    {
                        state.SetFlag(N5SprinklerShocked, true);
                        vitals.Damage(10, "reason.electric_shock");
                        Notify("quest.n5r12.shocked", NotificationSeverity.Warning);
                    }
                    ServiceHub.Evidence.Acquire("EV_N5R12_VALVE", EvidenceSource.WorldPickup);
                    break;

                case "n5r14_follow":
                    state.SetFlag(N5R14Collided, true);
                    vitals.Damage(8, "reason.walked_into_2009");
                    vitals.Strain(6, "reason.walked_into_2009");
                    break;
            }

            Persist();
            return moveTo;
        }

        /// <summary>
        /// One trip round a looping corridor (v5.1 N3-R08 / N5-R10): SAN each time up to a
        /// cap, HP for forcing it, and after three the building stops hiding the way out.
        /// </summary>
        static string Loop(string counterId, string restart, bool forced)
        {
            var state = State;
            var vitals = ServiceHub.Vitals;

            state.AddStat(counterId, 1);
            int loops = state.GetStat(counterId);

            if (loops * LoopSanCost <= LoopSanCap) vitals.Strain(LoopSanCost, "reason.lost_in_the_building");
            if (forced) vitals.Damage(5, "reason.forced_the_door");

            if (loops == 1) state.AddStat("PLAYER_LOST_COUNT", 1);
            Notify(loops >= LoopsBeforeClearHint ? "quest.loop.clear_hint" : "quest.loop.again",
                   NotificationSeverity.Warning);
            return restart;
        }

        // -----------------------------------------------------------------

        /// <summary>Deterministic per campaign, so a reload never changes a variant.</summary>
        static int Roll(string salt, int sides)
        {
            int seed = ServiceHub.NightPool != null ? ServiceHub.NightPool.CampaignSeed : 0;
            unchecked
            {
                int h = seed * 31;
                for (int i = 0; i < salt.Length; i++) h = h * 31 + salt[i];
                return ((h % sides) + sides) % sides;
            }
        }

        static void Notify(string key, NotificationSeverity severity)
        {
            EventBus.Publish(new NotificationEvent(key, severity));
        }

        static void Persist()
        {
            if (ServiceHub.Save != null) ServiceHub.Save.RequestAutosave(NO404.Save.SaveReason.EvidenceAcquired);
        }
    }
}
