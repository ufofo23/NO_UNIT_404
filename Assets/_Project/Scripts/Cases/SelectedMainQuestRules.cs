using NO404.Core;
using NO404.Evidence;

namespace NO404.Cases
{
    /// <summary>v5.1 investigation facts. Flags and counters use the existing save state.</summary>
    public static class SelectedMainQuestRules
    {
        static float _smokeSeconds;
        public static void Tick(float deltaTime)
        {
            bool smoke = Active("N5-M01") &&
                (ServiceHub.Player.CurrentZone == ZoneIds.Floor04 || ServiceHub.Player.CurrentZone == ZoneIds.ServicePassage) &&
                !ServiceHub.State.GetFlag(FlagIds.FireDoorUnlocked);
            if (!smoke) { _smokeSeconds = 0; return; }
            if (!ServiceHub.State.GetFlag("N5_SMOKE_WARNING_SHOWN"))
            {
                ServiceHub.State.SetFlag("N5_SMOKE_WARNING_SHOWN", true);
                EventBus.Publish(new NotificationEvent("quest.smoke_warning", NotificationSeverity.Warning));
            }
            _smokeSeconds += deltaTime;
            if (_smokeSeconds < 8f) return;
            _smokeSeconds = 0;
            ServiceHub.Vitals.Damage(1, "reason.smoke");
        }
        public static bool Handles(string id) => id == "N1-M01" || id == "N3-M01" || id == "N5-M01";
        public static void Started(string id)
        {
            if (id == "N2-M01" && ServiceHub.State.GetFlag(FlagIds.BillPreserved404))
            {
                ServiceHub.State.SetFlag("N2_BILL_YEAR_HINT_RECORDED", true);
                EventBus.Publish(new NotificationEvent("quest.bill_year_hint", NotificationSeverity.Info));
            }
        }
        public static bool Active(string id)
        {
            var quest = ServiceHub.Cases?.Find(id);
            return quest != null && quest.State.IsActive();
        }

        public static void EvidenceRead(string id)
        {
            int strain = id == "EV_404_BILL" ? 3 : id == "EV_HEIGHT_MARKS" ? 5 :
                         id == "EV_FIRE_TAPE_2009" ? 8 : 0;
            if (strain > 0 && !ServiceHub.State.GetFlag(id + "_READ"))
            {
                ServiceHub.State.SetFlag(id + "_READ", true);
                ServiceHub.Vitals.Strain(strain, "reason.main_evidence");
            }
            RefreshFacts();
        }

        public static void RefreshFacts()
        {
            var state = ServiceHub.State;
            var evidence = ServiceHub.Evidence;
            if (state.NightIndex == 3 && evidence.Has("EV_SEAL_NUMBER") && evidence.Has("EV_N3_LIFT_LOG") && evidence.Has("EV_N3_ANALOG"))
                state.SetFlag("N3_PHYSICAL_LOCATION_VERIFIED", true);
            if (state.NightIndex == 5 && evidence.Has("EV_FIRE_TAPE_2009") && evidence.Has("EV_DONGSIK_ID") && evidence.Has("EV_N5_WORK_LOG"))
            {
                if (!state.GetFlag("DONGSIK_LAST_BROADCAST_FOUND"))
                {
                    state.SetFlag("DONGSIK_LAST_BROADCAST_FOUND", true);
                    state.SetFlag(FlagIds.DongsikSignalFound, true);
                    ServiceHub.Vitals.Strain(5, "reason.last_rescue_echo");
                    EventBus.Publish(new NotificationEvent("quest.n5.last_rescue", NotificationSeverity.Info));
                }
                if (evidence.Has("EV_N5_LAST_POSITION"))
                {
                    state.SetFlag("DONGSIK_REMAINS_LOCATION_HINT", true);
                    state.SetFlag(FlagIds.DongsikLocated, true);
                }
            }
            if (state.NightIndex == 5 && evidence.Has("EV_CHOI_APPROVAL") && evidence.Has("EV_404_DELETION"))
                state.SetFlag("CHOI_2009_RESPONSIBILITY_CONFIRMED", true);
        }

        public static bool CanAct(string action)
        {
            var state = ServiceHub.State;
            if (action == "compare_height")
                return Active("N3-M01") && state.GetFlag("PROLOGUE_HEALTH_RECORD_READ") &&
                       ServiceHub.Evidence.Has("EV_HEIGHT_MARKS") && !state.GetFlag("N3_HEIGHT_MATCH_NOTED");
            if (action == "force_304") return Active("N1-M01") && !state.GetFlag("N1_304_DOOR_DAMAGED");
            if (action == "open_hatch") return Active("N3-M01") && !state.GetFlag("N3_DEEP_HATCH_OPENED");
            if (action == "wrong_door") return Active("N3-M01") && !state.GetFlag("N3_PLAYER_LOST");
            if (action == "return_marker") return Active("N3-M01") && state.GetFlag("N3_PLAYER_LOST");
            if (action == "health_record") return state.NightIndex <= 3 && !state.GetFlag("PROLOGUE_HEALTH_RECORD_READ");
            return false;
        }

        public static void Act(string action)
        {
            if (!CanAct(action)) return;
            var state = ServiceHub.State;
            if (action == "health_record")
            {
                state.SetFlag("PROLOGUE_HEALTH_RECORD_READ", true);
                EventBus.Publish(new NotificationEvent("quest.health_record", NotificationSeverity.Info));
            }
            else if (action == "compare_height")
            {
                state.SetFlag("N3_HEIGHT_MATCH_NOTED", true);
                EventBus.Publish(new NotificationEvent("quest.height_note", NotificationSeverity.Info));
            }
            else if (action == "force_304")
            {
                state.SetFlag("N1_304_DOOR_DAMAGED", true);
                state.AddStat(StatIds.BuildingSafety, -3, "reason.door_damage");
                ServiceHub.Vitals.Damage(5, "reason.door_damage");
                ServiceHub.Evidence.Acquire("EV_304_SOUND", EvidenceSource.WorldPickup);
            }
            else if (action == "open_hatch")
            {
                state.SetFlag("N3_DEEP_HATCH_OPENED", true);
                ServiceHub.Vitals.Damage(12, "reason.fell_through_the_hatch");
                ServiceHub.Vitals.Strain(3, "reason.main_evidence");
                state.AddStat(StatIds.ArchiveIntegrity, 7, "reason.correct_report");
                ServiceHub.Evidence.Acquire("E09_OLD_FIRE_EXTINGUISHER_SERIAL", EvidenceSource.WorldPickup);
            }
            else if (action == "wrong_door")
            {
                state.AddStat("N3_WRONG_DOOR_COUNT", 1);
                if (state.GetStat("N3_WRONG_DOOR_COUNT") >= 2)
                {
                    state.SetFlag("N3_PLAYER_LOST", true);
                    state.AddStat("PLAYER_LOST_COUNT", 1);
                    state.AddStat(DebtIds.Distortion, 1);
                    ServiceHub.Vitals.Strain(10, "reason.lost_in_the_building");
                    EventBus.Publish(new NotificationEvent("quest.lost_marker", NotificationSeverity.Warning));
                }
            }
            else if (action == "return_marker")
            {
                state.SetFlag("N3_PLAYER_LOST", false);
                state.SetStat("N3_WRONG_DOOR_COUNT", 0);
                ServiceHub.Vitals.NoteInvariantRead();
            }
            ServiceHub.Save.RequestAutosave(NO404.Save.SaveReason.EvidenceAcquired);
        }

        public static void Resolved(string caseId, string decision)
        {
            if (caseId == "N1-M01")
            {
                ServiceHub.State.SetFlag("N1_404_BILL_DISCARDED", decision == "dec_discard_bill");
                if (decision == "dec_discard_bill") ServiceHub.Evidence.Remove("EV_404_BILL");
                if (decision != "dec_preserve_bill") ServiceHub.State.SetFlag(FlagIds.BillPreserved404, false);
            }
            RefreshFacts();
        }
    }
}
