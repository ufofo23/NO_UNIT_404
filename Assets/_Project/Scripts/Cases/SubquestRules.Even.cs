using NO404.Core;
using NO404.Evidence;

namespace NO404.Cases
{
    /// <summary>
    /// The runtime half of the night 2 / 4 / 6 subquests (v5.1 11, 13, 15).
    ///
    /// Same rule as the odd nights: the quests are data in SeedContent.V51PoolsEven, and what
    /// lives here is only what data cannot say - a hot circuit that bites unless it is
    /// isolated first, a variant fixed the moment a quest opens, smoke that keeps costing
    /// until the door is shut. Everything is written to the saved flag/stat state, so a
    /// reload lands in the same building.
    ///
    /// Three quests are decided in a conversation rather than in the report (N2-R12, N4-R03,
    /// N4-R10). Their dialogue choices set the flags below, and the report only files what
    /// was said - the same shape N1-R01 uses for the door.
    /// </summary>
    public static partial class SubquestRules
    {
        // ---- night 2 --------------------------------------------------------------

        public const string N2R03Isolated = "N2R03_FEEDER_ISOLATED";
        public const string N2R03Shocked = "N2R03_SHOCKED";
        public const string N2R08RealFuture = "N2R08_REAL_FUTURE";
        public const string N2R09WarmFloor = "N2R09_WARM_FLOOR";
        public const string N2R11Anomalous = "N2R11_ANOMALOUS";
        public const string N2R12Asked = "N2R12_ASKED_WHERE";
        public const string N2R12HungUp = "N2R12_HUNG_UP";
        public const string N2R12Went4F = "N2R12_WENT_TO_4F";
        public const string N2R12Missed = "N2R12_MISSED";
        public const string FutureRuleConfirmed = "FUTURE_RULE_CONFIRMED";
        public const string FutureWarningB1 = "FUTURE_WARNING_B1";
        public const string HarinPackage = "HARIN_PACKAGE";
        public const string N2R14Returned = "N2R14_RETURNED";

        // ---- night 4 --------------------------------------------------------------

        public const string N4R01Visited = "N4R01_VISITED";
        public const string N4R03AskedWhere = "N4R03_ASKED_WHERE";
        public const string N4R03AskedName = "N4R03_ASKED_NAME";
        public const string N4R03Listened = "N4R03_LISTENED";
        public const string N4R03HungUp = "N4R03_HUNG_UP";
        public const string N4R03Missed = "N4R03_MISSED";
        public const string N4R10Consent = "N4R10_CONSENT";
        public const string N4R10Hurried = "N4R10_HURRIED";
        public const string N4R10ChoiFirst = "N4R10_CHOI_FIRST";
        public const string N4R10FullPossible = "N4R10_FULL_POSSIBLE";
        public const string N4R11Scraped = "N4R11_SCRAPED";
        public const string N4R12Opened = "N4R12_SLOT_OPENED";
        public const string N4R13Watched = "N4R13_KEPT_WATCHING";

        /// <summary>v5.1 N4-R10: the testimony is whole for somebody she has reason to trust.</summary>
        public const int SunjaTrustForFullTestimony = 60;

        // ---- night 6 --------------------------------------------------------------

        public const string N6R01Safe = "N6R01_SAFE_ZONE";
        public const string N6R02Deleting = "N6R02_AUTO_DELETE";
        public const string N6R04Foreseen = "N6R04_FORESEEN";
        public const string N6R05Opened = "N6R05_DOOR_OPENED";
        public const string N6R06Stabilised = "N6R06_STABILISED";
        public const string N6R06Cleared = "N6R06_CLEARED";
        public const string N6R08Smoke = "N6R08_SMOKE";
        public const string N6R08Backdraft = "N6R08_BACKDRAFT";
        public const string N6R08DoorSecured = "N6R08_DOOR_SECURED";
        public const string N6R08Vented = "N6R08_VENTED";
        public const string N6R08SmokeTaken = "N6R08_SMOKE_TAKEN";
        public const string N6R09Strong = "N6R09_STRONG_LOCK";
        public const string N6R09Cut = "N6R09_CIRCUIT_CUT";
        public const string N6R09Forced = "N6R09_FORCED";

        /// <summary>v5.1 N6-R08: HP -2 every five seconds in the smoke, at most -20.</summary>
        public const int SmokeDamage = 2;
        public const float SmokeIntervalSeconds = 5f;
        public const int SmokeDamageCap = 20;

        /// <summary>v5.1 N6-R02 / N6-R09: the chairman's side acts at the same line N5-R04 uses.</summary>
        public const int ChairmanActsAt = 50;

        /// <summary>v5.1 N6-M01's balanced route, reused: a building in this shape keeps its smoke out.</summary>
        public const int SmokeFreeSafety = 60;

        /// <summary>What a slow, careful job costs on the clock (v5.1 N6-R06 / N6-R09 "시간 소모").</summary>
        public const int CarefulWorkSeconds = 10 * 60;

        static float _smokeSeconds;

        static bool IsEvenNight(string action)
        {
            return action != null && action.Length > 2 && action[0] == 'n' &&
                   (action[1] == '2' || action[1] == '4' || action[1] == '6');
        }

        // -----------------------------------------------------------------
        // the draw
        // -----------------------------------------------------------------

        static int EvenWeightBonus(string caseId, GameStateService state)
        {
            switch (caseId)
            {
                // v5.1 N4-R01: how night 1 went is why anybody is asking after her.
                case "N4-R01": return state.ChoiceIs(ChoiceIds.SunjaCare, "GOOD") ? 0 : 30;
                // v5.1 N2-R14: a parcel sent back comes back another way.
                case "N4-R12": return state.GetFlag(N2R14Returned) ? 40 : 0;
                case "N6-R02": return state.GetStat(StatIds.ChairmanAlert) / 2;
                case "N6-R03": return state.GetDebt(DebtIds.Distortion) >= 2 ? 30 : 0;
                case "N6-R08": return SmokeComesIn(state) ? 40 : 0;
                case "N6-R09": return state.GetStat(StatIds.ChairmanAlert) / 2;
                case "N6-R13": return state.GetFlag(FlagIds.DongsikSignalFound) ? 30 : 0;
            }
            return 0;
        }

        static bool SmokeComesIn(GameStateService state)
        {
            return state.GetStat(StatIds.BuildingSafety) < SmokeFreeSafety ||
                   !state.ChoiceIs(ChoiceIds.FireDoorStatus, "SAFE");
        }

        // -----------------------------------------------------------------
        // lifecycle
        // -----------------------------------------------------------------

        static void EvenStarted(string caseId, GameStateService state)
        {
            switch (caseId)
            {
                case "N2-R08":
                    // v5.1 N2-R08: some nights the feed really is ten seconds ahead, some nights
                    // it is a loop that only looks like it.
                    state.SetFlag(N2R08RealFuture, Roll(caseId, 2) == 0);
                    break;

                case "N2-R09":
                    // Warm concrete and wet tread: a car was there and has gone. Cold and dry:
                    // the record is of a car that never came.
                    state.SetFlag(N2R09WarmFloor, Roll(caseId, 2) == 0);
                    break;

                case "N2-R11":
                    state.SetFlag(N2R11Anomalous, Roll(caseId, 2) == 0);
                    break;

                case "N4-R01":
                    Notify(state.ChoiceIs(ChoiceIds.SunjaCare, "GOOD") ? "quest.n4r01.variant.good"
                         : state.ChoiceIs(ChoiceIds.SunjaCare, "DENIED") ? "quest.n4r01.variant.denied"
                         : "quest.n4r01.variant.delayed", NotificationSeverity.Info);
                    break;

                case "N4-R10":
                    // v5.1 N4-R10: how much she says depends on night 1 and on trust.
                    state.SetFlag(N4R10FullPossible,
                                  state.ChoiceIs(ChoiceIds.SunjaCare, "GOOD") || state.GetFlag(N4R01Visited) ||
                                  state.GetStat(StatIds.CommunityTrust) >= SunjaTrustForFullTestimony);
                    break;

                case "N6-R01":
                    state.SetFlag(N6R01Safe, Roll(caseId, 2) == 0);
                    Notify(state.GetFlag(N6R01Safe) ? "quest.n6r01.variant.safe" : "quest.n6r01.variant.danger",
                           NotificationSeverity.Info);
                    break;

                case "N6-R02":
                    state.SetFlag(N6R02Deleting, state.GetStat(StatIds.ChairmanAlert) >= ChairmanActsAt);
                    Notify(state.GetFlag(N6R02Deleting) ? "quest.n6r02.variant.deleting" : "quest.n6r02.variant.integrity",
                           NotificationSeverity.Warning);
                    break;

                case "N6-R04":
                    // v5.1 N6-R04: only somebody who has already tested a future knows what to
                    // do with one.
                    state.SetFlag(N6R04Foreseen, state.GetFlag(FutureWarning404) || state.GetFlag(FutureRuleConfirmed));
                    break;

                case "N6-R08":
                    state.SetFlag(N6R08Smoke, SmokeComesIn(state));
                    state.SetFlag(N6R08Backdraft, state.GetFlag(N6R08Smoke) && Roll(caseId, 2) == 0);
                    state.SetStat(N6R08SmokeTaken, 0);
                    _smokeSeconds = 0f;
                    Notify(state.GetFlag(N6R08Smoke) ? "quest.n6r08.variant.smoke" : "quest.n6r08.variant.haze",
                           NotificationSeverity.Warning);
                    break;

                case "N6-R09":
                    state.SetFlag(N6R09Strong, state.GetStat(StatIds.ChairmanAlert) >= ChairmanActsAt);
                    Notify(state.GetFlag(N6R09Strong) ? "quest.n6r09.variant.strong" : "quest.n6r09.variant.weak",
                           NotificationSeverity.Warning);
                    break;
            }
        }

        static int EvenStrainFor(string evidenceId)
        {
            var state = State;
            switch (evidenceId)
            {
                case "EV_N2R06_UMBRELLA": return 3;
                case "EV_N2R07_TOKEN": return 2;
                case "EV_N2R09_FLOOR": return state.GetFlag(N2R09WarmFloor) ? 0 : 3;
                case "EV_N2R11_INDICATOR": return 3;
                case "EV_N2R13_MEMO": return 4;
                case "EV_N4R05_PRINT": return 7;
                case "EV_N4R08_TAPE": return 5;
                case "EV_N4R09_LOG2009": return 4;
                case "EV_N4R11_DRAWING": return 4;
                case "EV_N4R13_MIRROR": return 5;
                case "EV_N4R14_STAMP": return 4;
                case "EV_N6R04_BROADCAST": return 5;
                case "EV_N6R07_COPY": return 4;
                case "EV_N6R11_SEAL": return 5;
            }
            return 0;
        }

        static void EvenTick(float deltaTime, GameStateService state, PlayerTracker player)
        {
            // v5.1 N6-R08: the smoke keeps costing while the caretaker is in it, until the door
            // is shut or the air is moving the right way, and never more than the cap.
            bool inSmoke = state.GetFlag(N6R08Smoke) && Active("N6-R08") &&
                           (player.CurrentZone == ZoneIds.Floor04 || player.CurrentZone == ZoneIds.ServicePassage) &&
                           !state.GetFlag(N6R08DoorSecured) &&
                           !(state.GetFlag(N6R08Vented) && !state.GetFlag(N6R08Backdraft)) &&
                           state.GetStat(N6R08SmokeTaken) < SmokeDamageCap;
            if (!inSmoke) { _smokeSeconds = 0f; return; }

            _smokeSeconds += deltaTime;
            if (_smokeSeconds < SmokeIntervalSeconds) return;
            _smokeSeconds = 0f;

            state.AddStat(N6R08SmokeTaken, SmokeDamage);
            ServiceHub.Vitals.Damage(SmokeDamage, "reason.smoke");
        }

        static void EvenResolved(string caseId, string decisionId, GameStateService state)
        {
            switch (caseId)
            {
                case "N2-R14":
                    state.SetFlag(N2R14Returned, decisionId == "dec_return");
                    break;

                case "N6-R08":
                    _smokeSeconds = 0f;
                    break;
            }
        }

        // -----------------------------------------------------------------
        // world actions
        // -----------------------------------------------------------------

        static bool EvenCanAct(string action, GameStateService state)
        {
            switch (action)
            {
                case "n2r03_isolate": return !state.GetFlag(N2R03Isolated);
                case "n2r03_rebalance": return !ServiceHub.Evidence.Has("EV_N2R03_REBALANCED");
                case "n2r08_deviate": return !ServiceHub.Evidence.Has("EV_N2R08_TEST");
                case "n4r10_door": return !ServiceHub.Evidence.Has("EV_N4R10_TESTIMONY") && !ServiceHub.Dialogue.IsActive;
                case "n4r11_scrape": return !state.GetFlag(N4R11Scraped);
                case "n4r12_open": return !state.GetFlag(N4R12Opened);
                case "n4r13_watch": return !state.GetFlag(N4R13Watched);
                case "n6r05_open": return !state.GetFlag(N6R05Opened);
                case "n6r06_stabilise": return !state.GetFlag(N6R06Stabilised) && !state.GetFlag(N6R06Cleared);
                case "n6r06_clear": return !state.GetFlag(N6R06Stabilised) && !state.GetFlag(N6R06Cleared);
                case "n6r08_firedoor": return !state.GetFlag(N6R08DoorSecured);
                case "n6r08_vent": return !state.GetFlag(N6R08Vented) && !state.GetFlag(N6R08DoorSecured);
                case "n6r09_cut": return !state.GetFlag(N6R09Cut);
                case "n6r09_force": return !state.GetFlag(N6R09Forced) && !state.GetFlag(N6R09Cut);
            }
            return false;
        }

        static void EvenAct(string action, GameStateService state, VitalService vitals)
        {
            switch (action)
            {
                case "n2r03_isolate":
                    state.SetFlag(N2R03Isolated, true);
                    Notify("quest.n2r03.isolated", NotificationSeverity.Info);
                    break;

                case "n2r03_rebalance":
                    // v5.1 N2-R03: HP -8 for touching it live. Once; after that the caretaker
                    // knows better and is only told again.
                    if (state.GetFlag(N2R03Isolated))
                        ServiceHub.Evidence.Acquire("EV_N2R03_REBALANCED", EvidenceSource.WorldPickup);
                    else if (!state.GetFlag(N2R03Shocked))
                    {
                        state.SetFlag(N2R03Shocked, true);
                        vitals.Damage(8, "reason.electric_shock");
                        Notify("quest.n2r03.shocked", NotificationSeverity.Warning);
                    }
                    else Notify("quest.n2r03.live", NotificationSeverity.Warning);
                    break;

                case "n2r08_deviate":
                    ServiceHub.Evidence.Acquire("EV_N2R08_TEST", EvidenceSource.WorldPickup);
                    Notify(state.GetFlag(N2R08RealFuture) ? "quest.n2r08.feed_followed" : "quest.n2r08.feed_ignored",
                           NotificationSeverity.Warning);
                    break;

                case "n4r10_door":
                    ServiceHub.Dialogue.Start("D_N4R10_SUNJA");
                    break;

                case "n4r11_scrape":
                    // v5.1 N4-R11: the second outline, and part of a word - never the name.
                    state.SetFlag(N4R11Scraped, true);
                    vitals.Damage(2, "reason.scraped_the_wall");
                    vitals.Strain(5, "reason.scraped_the_wall");
                    ServiceHub.Evidence.Acquire("EV_N4R11_SCRAPED", EvidenceSource.WorldPickup);
                    break;

                case "n4r12_open":
                    state.SetFlag(N4R12Opened, true);
                    vitals.Strain(6, "reason.opened_box_404");
                    ServiceHub.Evidence.Acquire("EV_N4R12_PAPER", EvidenceSource.WorldPickup);
                    break;

                case "n4r13_watch":
                    state.SetFlag(N4R13Watched, true);
                    vitals.Strain(3, "reason.watched_the_reflection");
                    Notify("quest.n4r13.two_children", NotificationSeverity.Warning);
                    break;

                case "n6r05_open":
                    // v5.1 N6-R05: SAN -10 for the memory behind it. The report records it.
                    state.SetFlag(N6R05Opened, true);
                    vitals.Strain(10, "reason.opened_her_door");
                    Notify("quest.n6r05.memory", NotificationSeverity.Warning);
                    break;

                case "n6r06_stabilise":
                    state.SetFlag(N6R06Stabilised, true);
                    SpendTime(CarefulWorkSeconds);
                    Notify("quest.n6r06.stabilised", NotificationSeverity.Info);
                    break;

                case "n6r06_clear":
                    state.SetFlag(N6R06Cleared, true);
                    vitals.Damage(8, "reason.debris");
                    Notify("quest.n6r06.cleared", NotificationSeverity.Warning);
                    break;

                case "n6r08_firedoor":
                    state.SetFlag(N6R08DoorSecured, true);
                    Notify("quest.n6r08.door_secured", NotificationSeverity.Info);
                    break;

                case "n6r08_vent":
                    state.SetFlag(N6R08Vented, true);
                    if (state.GetFlag(N6R08Backdraft))
                    {
                        vitals.Damage(6, "reason.smoke");
                        Notify("quest.n6r08.backdraft", NotificationSeverity.Warning);
                    }
                    else Notify("quest.n6r08.vented", NotificationSeverity.Info);
                    break;

                case "n6r09_cut":
                    state.SetFlag(N6R09Cut, true);
                    SpendTime(CarefulWorkSeconds / 2);
                    Notify("quest.n6r09.released", NotificationSeverity.Info);
                    break;

                case "n6r09_force":
                    state.SetFlag(N6R09Forced, true);
                    vitals.Damage(5, "reason.forced_the_door");
                    Notify("quest.n6r09.forced", NotificationSeverity.Warning);
                    break;
            }
        }

        /// <summary>Careful work costs the clock, never past the end of the shift.</summary>
        static void SpendTime(int seconds)
        {
            var clock = ServiceHub.Clock;
            if (clock == null) return;
            int target = clock.GameSecond + seconds;
            if (target >= GameClock.ShiftEndSecond) target = GameClock.ShiftEndSecond - 60;
            if (target > clock.GameSecond) clock.SetGameSecond(target);
        }
    }
}
