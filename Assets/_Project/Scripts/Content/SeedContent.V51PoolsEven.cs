using System.Collections.Generic;
using NO404.Cases;
using NO404.Core;
using NO404.Dialogue;
using NO404.Evidence;
using NO404.Phone;
using R = NO404.Cases.SubquestRules;

namespace NO404.ContentData
{
    /// <summary>
    /// Nights 2, 4 and 6 as v5.1 writes them (sections 11, 13, 15): fourteen subquests each
    /// around the main - one fixed story subquest and thirteen random candidates.
    ///
    /// The same shape as <see cref="BuildV51Subquests"/>'s odd nights, on purpose: a place to
    /// go, one or two things there to read, and a report whose options are v5.1's
    /// "선택지 및 결과" rows with v5.1's numbers. The correct option always needs the evidence
    /// (19.2). What data cannot say lives in SubquestRules.Even.
    ///
    /// Three quests happen in a conversation: the 2009 caller (N2-R12), the line from 404
    /// (N4-R03) and Lee Seon-ja's testimony (N4-R10). The dialogue is where the caretaker
    /// decides; the report files what was said, and always has a row for having said nothing.
    /// </summary>
    public static partial class SeedContent
    {
        static IEnumerable<CaseDefinition> EvenNightSubquests()
        {
            foreach (var c in Night2Subquests()) yield return c;
            foreach (var c in Night4Subquests()) yield return c;
            foreach (var c in Night6Subquests()) yield return c;
        }

        // =================================================================
        // evidence
        // =================================================================

        public static EvidenceDefinition[] BuildV51EvenEvidence()
        {
            return new[]
            {
                // ---- night 2 ----
                Evidence_("EV_N2R01_INVOICE", EvidenceType.Document, "N2-R01", false),
                Evidence_("EV_N2R01_ITEM", EvidenceType.PhysicalObject, "N2-R01", false),
                Evidence_("EV_N2R02_REQUEST", EvidenceType.Document, "N2-R02", false),
                Evidence_("EV_N2R02_PHOTO_ID", EvidenceType.Document, "N2-R02", false),
                Evidence_("EV_N2R03_LOAD", EvidenceType.MeterGraph, "N2-R03", false),
                Evidence_("EV_N2R03_REBALANCED", EvidenceType.MeterGraph, "N2-R03", false),
                Evidence_("EV_N2R04_PLATE", EvidenceType.Photo, "N2-R04", false),
                Evidence_("EV_N2R04_REGISTRY", EvidenceType.Document, "N2-R04", false),
                Evidence_("EV_N2R05_CARTON", EvidenceType.PhysicalObject, "N2-R05", false),
                Evidence_("EV_N2R05_SLIP", EvidenceType.Document, "N2-R05", false),
                Evidence_("EV_N2R06_UMBRELLA", EvidenceType.PhysicalObject, "N2-R06", false),
                Evidence_("EV_N2R07_TOKEN", EvidenceType.AccessLog, "N2-R07", false),
                Evidence_("EV_N2R07_DOORSTEP", EvidenceType.Photo, "N2-R07", false),
                Evidence_("EV_N2R08_TEST", EvidenceType.CctvSnapshot, "N2-R08", false),
                Evidence_("EV_N2R09_DB", EvidenceType.Document, "N2-R09", false),
                Evidence_("EV_N2R09_FLOOR", EvidenceType.MeterGraph, "N2-R09", false),
                Evidence_("EV_N2R09_TYRES", EvidenceType.Photo, "N2-R09", false),
                Evidence_("EV_N2R10_LOG", EvidenceType.AccessLog, "N2-R10", false),
                Evidence_("EV_N2R10_SEAL", EvidenceType.Photo, "N2-R10", false),
                Evidence_("EV_N2R11_INDICATOR", EvidenceType.Photo, "N2-R11", false),
                Evidence_("EV_N2R11_PRINTS", EvidenceType.Photo, "N2-R11", false),
                Evidence_("EV_N2R12_CALL", EvidenceType.AudioRecording, "N2-R12", true),
                Evidence_("EV_N2R12_SENSOR", EvidenceType.PhysicalObject, "N2-R12", false),
                Evidence_("EV_N2R13_MEMO", EvidenceType.Document, "N2-R13", false),
                Evidence_("EV_N2R13_LOGBOOK", EvidenceType.Document, "N2-R13", false),
                Evidence_("EV_N2R13_B1_DOOR", EvidenceType.Photo, "N2-R13", false),
                Evidence_("EV_N2R14_INVOICE", EvidenceType.Document, "N2-R14", false),
                Evidence_("EV_N2R14_SENDER", EvidenceType.Document, "N2-R14", false),
                Evidence_("EV_N2R14_PHOTO", EvidenceType.Photo, "N2-R14", false),

                // ---- night 4 ----
                Evidence_("EV_N4R01_STATE", EvidenceType.Testimony, "N4-R01", false),
                Evidence_("EV_N4R02_ORIGINALS", EvidenceType.Document, "N4-R02", false),
                Evidence_("EV_N4R02_BACKUP", EvidenceType.Document, "N4-R02", false),
                Evidence_("EV_N4R03_CALL", EvidenceType.AudioRecording, "N4-R03", false),
                Evidence_("EV_N4R04_PAPER", EvidenceType.Document, "N4-R04", false),
                Evidence_("EV_N4R04_PC", EvidenceType.Document, "N4-R04", false),
                Evidence_("EV_N4R05_PRINT", EvidenceType.Photo, "N4-R05", false),
                Evidence_("EV_N4R05_SCAN", EvidenceType.Photo, "N4-R05", false),
                Evidence_("EV_N4R06_WARNING", EvidenceType.AccessLog, "N4-R06", false),
                Evidence_("EV_N4R06_TERMINAL", EvidenceType.Photo, "N4-R06", false),
                Evidence_("EV_N4R07_VIBRATION", EvidenceType.MeterGraph, "N4-R07", false),
                Evidence_("EV_N4R07_PAPERS", EvidenceType.Document, "N4-R07", false),
                Evidence_("EV_N4R08_TAPE", EvidenceType.AudioRecording, "N4-R08", false),
                Evidence_("EV_N4R09_LOG2009", EvidenceType.AccessLog, "N4-R09", false),
                Evidence_("EV_N4R09_PRINT", EvidenceType.Document, "N4-R09", false),
                Evidence_("EV_N4R10_TESTIMONY", EvidenceType.Testimony, "N4-R10", true),
                Evidence_("EV_N4R11_DRAWING", EvidenceType.Photo, "N4-R11", false),
                Evidence_("EV_N4R11_SCRAPED", EvidenceType.Photo, "N4-R11", false),
                Evidence_("EV_N4R12_MEASURE", EvidenceType.MeterGraph, "N4-R12", false),
                Evidence_("EV_N4R12_PAPER", EvidenceType.Document, "N4-R12", false),
                Evidence_("EV_N4R13_MIRROR", EvidenceType.Photo, "N4-R13", false),
                Evidence_("EV_N4R13_PHOTO", EvidenceType.Photo, "N4-R13", false),
                Evidence_("EV_N4R14_STAMP", EvidenceType.Document, "N4-R14", false),
                Evidence_("EV_N4R14_COMPARE", EvidenceType.Document, "N4-R14", false),

                // ---- night 6 ----
                Evidence_("EV_N6R01_SLIP", EvidenceType.Document, "N6-R01", false),
                Evidence_("EV_N6R02_INDEX", EvidenceType.Document, "N6-R02", false),
                Evidence_("EV_N6R03_REFERENCE", EvidenceType.Photo, "N6-R03", false),
                Evidence_("EV_N6R04_BROADCAST", EvidenceType.AudioRecording, "N6-R04", false),
                Evidence_("EV_N6R05_DOOR", EvidenceType.Photo, "N6-R05", false),
                Evidence_("EV_N6R05_SLIPPER", EvidenceType.PhysicalObject, "N6-R05", false),
                Evidence_("EV_N6R06_NAMEPLATE", EvidenceType.PhysicalObject, "N6-R06", false),
                Evidence_("EV_N6R07_COPY", EvidenceType.Document, "N6-R07", false),
                Evidence_("EV_N6R07_PAPER_TEST", EvidenceType.PhysicalObject, "N6-R07", false),
                Evidence_("EV_N6R08_TEMP", EvidenceType.MeterGraph, "N6-R08", false),
                Evidence_("EV_N6R09_LOCK", EvidenceType.Photo, "N6-R09", false),
                Evidence_("EV_N6R10_CHAT", EvidenceType.Document, "N6-R10", false),
                Evidence_("EV_N6R11_SEAL", EvidenceType.Photo, "N6-R11", false),
                Evidence_("EV_N6R12_DIRECTION", EvidenceType.Photo, "N6-R12", false),
                Evidence_("EV_N6R13_RADIO", EvidenceType.AudioRecording, "N6-R13", false),
                Evidence_("EV_N6R13_CHILD", EvidenceType.AudioRecording, "N6-R13", false),
                Evidence_("EV_N6R14_AIR", EvidenceType.Photo, "N6-R14", false)
            };
        }

        // =================================================================
        // Night 2 (v5.1 11) - time and records start to disagree
        // =================================================================

        static IEnumerable<CaseDefinition> Night2Subquests()
        {
            // ---- N2-R01 정상 반품 택배 (NORMAL) ----------------------------------
            {
                const string id = "N2-R01";
                var c = Sub(id, 2, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N2R01_INVOICE"), Look("EV_N2R01_ITEM") };
                c.decisions = new[]
                {
                    Choose(id, "hand_over", Good, Needs("EV_N2R01_INVOICE", "EV_N2R01_ITEM"), null,
                        Trust(2), San(1, "reason.read_correctly")),
                    Choose(id, "over_check", Half, null, null, Trust(-2)),
                    Choose(id, "refuse", Bad, null, null, Trust(-4))
                };
                yield return c;
            }

            // ---- N2-R02 주민 출입카드 분실 (NORMAL) ------------------------------
            {
                const string id = "N2-R02";
                var c = Sub(id, 2, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N2R02_REQUEST"), Look("EV_N2R02_PHOTO_ID") };
                c.decisions = new[]
                {
                    Choose(id, "floor_card", Good, Needs("EV_N2R02_REQUEST", "EV_N2R02_PHOTO_ID"), null, Trust(3)),
                    Choose(id, "full_temporary", Bad, null, null, Owe(DebtIds.Access, 1)),
                    Choose(id, "refuse", Half, null, null, Trust(-3))
                };
                yield return c;
            }

            // ---- N2-R03 B1 차단기 발열 (NORMAL) ----------------------------------
            {
                const string id = "N2-R03";
                var c = Sub(id, 2, QuestType.Normal, AnomalyFamily.None);
                c.mutexGroup = "b1_power";
                c.objectives = new[] { Look("EV_N2R03_LOAD"), Look("EV_N2R03_REBALANCED", true) };
                c.decisions = new[]
                {
                    Choose(id, "rebalance", Good, Needs("EV_N2R03_REBALANCED"), null,
                        Safety(5), Set("N2R03_LOAD_SPLIT")),
                    Choose(id, "temp_fan", Half, null, null, Safety(1), Owe(DebtIds.Safety, 1)),
                    Choose(id, "ignore", Bad, null, null, Safety(-6))
                };
                yield return c;
            }

            // ---- N2-R04 주차 게이트 번호판 오인식 (NORMAL) -----------------------
            {
                const string id = "N2-R04";
                var c = Sub(id, 2, QuestType.Normal, AnomalyFamily.None);
                c.mutexGroup = "parking_gate";
                c.objectives = new[] { Look("EV_N2R04_PLATE"), Look("EV_N2R04_REGISTRY") };
                c.decisions = new[]
                {
                    Choose(id, "fix_db", Good, Needs("EV_N2R04_PLATE", "EV_N2R04_REGISTRY"), null, Trust(2)),
                    Choose(id, "manual_open", Half, null, null),
                    Choose(id, "refuse", Bad, null, null, Trust(-3))
                };
                yield return c;
            }

            // ---- N2-R05 오래된 반품 상자 (MIXED / ECHO) --------------------------
            {
                const string id = "N2-R05";
                var c = Sub(id, 2, QuestType.Mixed, AnomalyFamily.Echo);
                c.mutexGroup = "old_parcel";
                c.objectives = new[] { Look("EV_N2R05_CARTON"), Look("EV_N2R05_SLIP", true) };
                c.decisions = new[]
                {
                    Choose(id, "open_record", Good, Needs("EV_N2R05_CARTON"), null,
                        Pick(ChoiceIds.OldPackageStatus, "OPENED"), Archive(6), San(-4, "reason.opened_the_package")),
                    Choose(id, "seal", Good, Needs("EV_N2R05_CARTON"), null,
                        Pick(ChoiceIds.OldPackageStatus, "PRESERVED"), Archive(5)),
                    Choose(id, "return", Half, null, null, Pick(ChoiceIds.OldPackageStatus, "RETURNED")),
                    Choose(id, "leave", Bad, null, null, Pick(ChoiceIds.OldPackageStatus, "LOST"))
                };
                yield return c;
            }

            // ---- N2-R06 3F 창문 날씨 불일치 (MIXED / ECHO) -----------------------
            {
                const string id = "N2-R06";
                var c = Sub(id, 2, QuestType.Mixed, AnomalyFamily.Echo);
                c.mutexGroup = "cam_3f";
                c.objectives = new[] { Watch("CAM-06"), Look("EV_N2R06_UMBRELLA") };
                c.decisions = new[]
                {
                    Choose(id, "keep_clip", Good, Needs("EV_N2R06_UMBRELLA"), null, Archive(3), Set("N2R06_CLIP_KEPT")),
                    Choose(id, "swap_camera", Half, null, null),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N2-R07 팀원 이름으로 온 인터폰 — 솔로 버전 (ANOMALY / IDENTITY) -
            {
                const string id = "N2-R07";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Identity);
                c.mutexGroup = "access_log";
                c.objectives = new[] { Look("EV_N2R07_TOKEN"), Look("EV_N2R07_DOORSTEP", true) };
                c.decisions = new[]
                {
                    Choose(id, "block_token", Good, Needs("EV_N2R07_TOKEN"), null,
                        San(-4, "reason.saw_your_own_id"), Alert(2)),
                    Choose(id, "open_door", Bad, null, null, Owe(DebtIds.Access, 1), Set("N2R07_F4_SENSOR_FAULT")),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Access, 1), Alert(2))
                };
                yield return c;
            }

            // ---- N2-R08 CCTV의 10초 미래 (ANOMALY / FUTURE) ----------------------
            {
                const string id = "N2-R08";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Future);
                c.mutexGroup = "cam_lobby";
                c.objectives = new[] { Watch("CAM-01"), Look("EV_N2R08_TEST") };
                c.decisions = new[]
                {
                    Choose(id, "experiment", Good, Needs("EV_N2R08_TEST"), null,
                        Set(R.FutureRuleConfirmed), San(-4, "reason.watched_yourself_ahead"), Harin(4)),
                    Choose(id, "follow_real", Half, null, When(ConditionDefinition.Flag(R.N2R08RealFuture)),
                        San(-5, "reason.watched_yourself_ahead"), Harin(4)),
                    Choose(id, "follow_false", Bad, null, When(ConditionDefinition.Flag(R.N2R08RealFuture, false)),
                        San(-5, "reason.watched_yourself_ahead"), Hp(-5, "reason.followed_the_feed")),
                    Choose(id, "power_off", Half, null, null)
                };
                yield return c;
            }

            // ---- N2-R09 빈 주차칸의 차량 (ANOMALY / RECORD) ----------------------
            {
                const string id = "N2-R09";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Record);
                c.mutexGroup = "parking_gate";
                c.objectives = new[] { Look("EV_N2R09_DB"), Look("EV_N2R09_FLOOR"), Look("EV_N2R09_TYRES", true) };
                c.decisions = new[]
                {
                    Choose(id, "physical", Good, Needs("EV_N2R09_DB", "EV_N2R09_FLOOR"), null, Archive(2)),
                    Choose(id, "delete_db", Bad, null, null, Owe(DebtIds.Record, 1)),
                    Choose(id, "lock_gate", Half, null, null, Trust(-2))
                };
                yield return c;
            }

            // ---- N2-R10 복제 출입카드 (ANOMALY / IDENTITY) -----------------------
            {
                const string id = "N2-R10";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Identity);
                c.mutexGroup = "access_log";
                c.objectives = new[] { Look("EV_N2R10_LOG"), Look("EV_N2R10_SEAL", true) };
                c.decisions = new[]
                {
                    Choose(id, "suspend", Good, Needs("EV_N2R10_LOG"), null, Trust(-1)),
                    Choose(id, "check_4f", Good, Needs("EV_N2R10_LOG", "EV_N2R10_SEAL"), null,
                        Archive(2), San(-5, "reason.saw_the_card_upstairs")),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Access, 2))
                };
                yield return c;
            }

            // ---- N2-R11 엘리베이터 선행 하차 (ANOMALY / FUTURE) ------------------
            {
                const string id = "N2-R11";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Future);
                c.objectives = new[]
                {
                    Watch("CAM-08"), Look("EV_N2R11_INDICATOR"), Look("EV_N2R11_PRINTS", true)
                };
                c.decisions = new[]
                {
                    Choose(id, "log_offset", Good, Needs("EV_N2R11_INDICATOR"), null, Archive(3)),
                    Choose(id, "chase_resident", Half, null, When(ConditionDefinition.Flag(R.N2R11Anomalous, false)),
                        Trust(-2)),
                    Choose(id, "chase_trace", Half, Needs("EV_N2R11_PRINTS"), When(ConditionDefinition.Flag(R.N2R11Anomalous)),
                        Archive(2), Harin(2)),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N2-R12 2009년 주민의 전화 (FIXED_STORY, ending O) ----------------
            {
                const string id = "N2-R12";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Echo, story: true);
                // The call rings when this opens (CALL_N2R12_2009) and is the investigation;
                // the 4F sensor is the physical check v5.1 offers instead of staying on.
                c.objectives = new[] { Look("EV_N2R12_CALL"), Look("EV_N2R12_SENSOR", true) };
                c.decisions = new[]
                {
                    Choose(id, "kept_line", Good, null, When(ConditionDefinition.Flag(R.N2R12Asked)),
                        Archive(4), Harin(3), San(-6, "reason.the_2009_caller"), Set(FlagIds.End2009CallPreserved)),
                    Choose(id, "checked_4f", Good, Needs("EV_N2R12_SENSOR"), When(ConditionDefinition.Flag(R.N2R12Asked, false)),
                        Harin(3), San(-3, "reason.the_2009_caller")),
                    // Fail-safe: hung up, or never picked up. The call is lost, nothing else.
                    Choose(id, "let_go", Half, null, When(ConditionDefinition.Flag(R.N2R12Asked, false)),
                        Set(FlagIds.End2009CallPreserved, false))
                };
                yield return c;
            }

            // ---- N2-R13 우편함의 미래 근무 메모 (ANOMALY / FUTURE) ---------------
            {
                const string id = "N2-R13";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Future);
                c.objectives = new[]
                {
                    Look("EV_N2R13_MEMO"), Look("EV_N2R13_LOGBOOK", true), Look("EV_N2R13_B1_DOOR", true)
                };
                c.decisions = new[]
                {
                    Choose(id, "keep", Good, Needs("EV_N2R13_MEMO"), null, Archive(3), Set(R.FutureWarningB1)),
                    Choose(id, "b1_check", Good, Needs("EV_N2R13_MEMO", "EV_N2R13_B1_DOOR"), null,
                        Safety(2), Set(R.FutureWarningB1)),
                    Choose(id, "discard", Half, null, null)
                };
                yield return c;
            }

            // ---- N2-R14 404 수취인 택배 (ANOMALY / RECORD) -----------------------
            {
                const string id = "N2-R14";
                var c = Sub(id, 2, QuestType.Anomaly, AnomalyFamily.Record);
                c.mutexGroup = "old_parcel";
                c.objectives = new[] { Look("EV_N2R14_INVOICE"), Look("EV_N2R14_SENDER", true) };
                c.decisions = new[]
                {
                    Choose(id, "seal", Good, Needs("EV_N2R14_INVOICE"), null, Archive(6), Set(R.HarinPackage)),
                    Choose(id, "open", Half, Needs("EV_N2R14_INVOICE"), null,
                        San(-6, "reason.opened_the_package"), Archive(2), Give("EV_N2R14_PHOTO")),
                    Choose(id, "return", Half, null, null),
                    Choose(id, "discard", Bad, null, null, Owe(DebtIds.Record, 1))
                };
                yield return c;
            }
        }

        // =================================================================
        // Night 4 (v5.1 13) - the records start to name people
        // =================================================================

        static IEnumerable<CaseDefinition> Night4Subquests()
        {
            // ---- N4-R01 303 주민 안부 확인 (NORMAL) ------------------------------
            {
                const string id = "N4-R01";
                var c = Sub(id, 4, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N4R01_STATE") };
                c.decisions = new[]
                {
                    Choose(id, "visit", Good, Needs("EV_N4R01_STATE"), null, Trust(5), Set(R.N4R01Visited)),
                    Choose(id, "phone", Half, null, null, Trust(2)),
                    Choose(id, "ignore", Bad, null, null, Trust(-6))
                };
                yield return c;
            }

            // ---- N4-R02 기록실 데이터 마이그레이션 요청 (MIXED / RECORD) ----------
            {
                const string id = "N4-R02";
                var c = Sub(id, 4, QuestType.Mixed, AnomalyFamily.Record);
                c.objectives = new[] { Look("EV_N4R02_ORIGINALS"), Look("EV_N4R02_BACKUP") };
                c.decisions = new[]
                {
                    Choose(id, "keep_and_copy", Good, Needs("EV_N4R02_ORIGINALS", "EV_N4R02_BACKUP"), null,
                        Archive(5), Alert(4)),
                    Choose(id, "overwrite", Bad, null, null, Archive(-6)),
                    Choose(id, "refuse", Half, null, null, Alert(8))
                };
                yield return c;
            }

            // ---- N4-R03 404 발신 전화 (ANOMALY / ECHO) ---------------------------
            {
                const string id = "N4-R03";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Echo);
                // CALL_N4_404 rings when this opens; answering or missing it both leave the log.
                c.objectives = new[] { Look("EV_N4R03_CALL") };
                c.decisions = new[]
                {
                    Choose(id, "asked_where", Good, null, When(ConditionDefinition.Flag(R.N4R03AskedWhere)),
                        San(-5, "reason.the_404_line"), Archive(2), Set("N4R03_B1_HINT")),
                    Choose(id, "asked_name", Half, null, When(ConditionDefinition.Flag(R.N4R03AskedName)),
                        Harin(6), San(-7, "reason.the_404_line")),
                    Choose(id, "listened", Half, null, When(ConditionDefinition.Flag(R.N4R03Listened)),
                        San(-6, "reason.the_404_line"), Set("N4R03_FUTURE_HINT")),
                    // Fail-safe: hung up, or never picked up. Safe, and nothing learned.
                    Choose(id, "hung_up", Half, null, When(
                            ConditionDefinition.Flag(R.N4R03AskedWhere, false), ConditionDefinition.Flag(R.N4R03AskedName, false),
                            ConditionDefinition.Flag(R.N4R03Listened, false)))
                };
                yield return c;
            }

            // ---- N4-R04 PC 매뉴얼 vs 종이 매뉴얼 (ANOMALY / RECORD) --------------
            {
                const string id = "N4-R04";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Record);
                c.objectives = new[] { Look("EV_N4R04_PAPER"), Look("EV_N4R04_PC") };
                c.decisions = new[]
                {
                    Choose(id, "keep_paper", Good, Needs("EV_N4R04_PAPER", "EV_N4R04_PC"), null,
                        Archive(4), Set("MANUAL_SOURCE_PAPER")),
                    Choose(id, "record_both", Half, Needs("EV_N4R04_PAPER", "EV_N4R04_PC"), null, Set("MANUAL_CONFLICT")),
                    Choose(id, "trust_pc", Bad, null, null, Set("MANUAL_SOURCE_PC"))
                };
                yield return c;
            }

            // ---- N4-R05 가족사진에서 사라지는 아이 (ANOMALY / RECORD) ------------
            {
                const string id = "N4-R05";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Record);
                c.objectives = new[] { Look("EV_N4R05_PRINT"), Look("EV_N4R05_SCAN", true) };
                c.decisions = new[]
                {
                    Choose(id, "seal_original", Good, Needs("EV_N4R05_PRINT"), null, Archive(6), Set("N4R05_PHOTO_SEALED")),
                    Choose(id, "scan_only", Half, Needs("EV_N4R05_PRINT"), null, Archive(3)),
                    Choose(id, "hand_to_choi", Bad, null, null, Alert(-2), Owe(DebtIds.Record, 1))
                };
                yield return c;
            }

            // ---- N4-R06 직원ID 중복 로그인 (ANOMALY / IDENTITY) ------------------
            {
                const string id = "N4-R06";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Identity);
                c.mutexGroup = "access_log";
                c.objectives = new[] { Look("EV_N4R06_WARNING"), Look("EV_N4R06_TERMINAL", true) };
                c.decisions = new[]
                {
                    Choose(id, "end_session", Good, Needs("EV_N4R06_WARNING"), null, Set("N4R06_SESSION_ENDED")),
                    Choose(id, "check_b1", Good, Needs("EV_N4R06_WARNING", "EV_N4R06_TERMINAL"), null,
                        Archive(2), San(-5, "reason.saw_yourself_logged_in")),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Access, 1), Alert(3))
                };
                yield return c;
            }

            // ---- N4-R07 봉인상자 안쪽에서 노크 (ANOMALY / RECORD) ----------------
            {
                const string id = "N4-R07";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Record);
                c.mutexGroup = "archive_cabinet";
                c.objectives = new[] { Look("EV_N4R07_VIBRATION") };
                c.decisions = new[]
                {
                    Choose(id, "keep_seal", Good, Needs("EV_N4R07_VIBRATION"), null,
                        Archive(4), San(-5, "reason.knocking_box")),
                    Choose(id, "open_now", Half, null, null,
                        San(-8, "reason.knocking_box"), Archive(1), Give("EV_N4R07_PAPERS")),
                    Choose(id, "move_out", Bad, null, null, Owe(DebtIds.Record, 1))
                };
                yield return c;
            }

            // ---- N4-R08 2009 화재 신고 테이프 (ANOMALY / ECHO) -------------------
            {
                const string id = "N4-R08";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Echo);
                c.objectives = new[] { Look("EV_N4R08_TAPE") };
                c.decisions = new[]
                {
                    Choose(id, "copy_keep", Good, Needs("EV_N4R08_TAPE"), null, Archive(8), Set("N4R08_TAPE_PRESERVED")),
                    Choose(id, "digital_only", Half, Needs("EV_N4R08_TAPE"), null, Archive(4)),
                    Choose(id, "report_choi", Bad, null, null, Set("N4R08_TAPE_TAKEN"))
                };
                yield return c;
            }

            // ---- N4-R09 2009 출입로그 재생 (ANOMALY / ECHO) ----------------------
            {
                const string id = "N4-R09";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Echo);
                c.mutexGroup = "access_log";
                c.objectives = new[] { Look("EV_N4R09_LOG2009") };
                c.decisions = new[]
                {
                    Choose(id, "print", Good, Needs("EV_N4R09_LOG2009"), null, Archive(6), Alert(5), Give("EV_N4R09_PRINT")),
                    Choose(id, "screenshot", Good, Needs("EV_N4R09_LOG2009"), null, Archive(3)),
                    Choose(id, "close", Half, null, null)
                };
                yield return c;
            }

            // ---- N4-R10 선자의 증언 (FIXED_STORY, ending O) -----------------------
            {
                const string id = "N4-R10";
                var c = Sub(id, 4, QuestType.Mixed, AnomalyFamily.None, story: true);
                // The testimony is the conversation at 303 (D_N4R10_SUNJA). How much of it
                // there is was settled when the quest opened (SubquestRules.N4R10FullPossible).
                c.objectives = new[] { Look("EV_N4R10_TESTIMONY") };
                c.decisions = new[]
                {
                    Choose(id, "recorded_full", Good, null,
                        When(ConditionDefinition.Flag(R.N4R10Consent), ConditionDefinition.Flag(R.N4R10FullPossible)),
                        Archive(6), Trust(3), Pick(ChoiceIds.SunjaTestimony, "FULL"),
                        Set(FlagIds.SunjaTestimony), Set(FlagIds.EndSunjaTestimonyFull)),
                    Choose(id, "recorded_partial", Good, null,
                        When(ConditionDefinition.Flag(R.N4R10Consent), ConditionDefinition.Flag(R.N4R10FullPossible, false)),
                        Archive(6), Trust(3), Pick(ChoiceIds.SunjaTestimony, "PARTIAL"), Set(FlagIds.SunjaTestimony)),
                    Choose(id, "hurried", Half, null, When(ConditionDefinition.Flag(R.N4R10Hurried)),
                        Pick(ChoiceIds.SunjaTestimony, "PARTIAL"), Set(FlagIds.SunjaTestimony)),
                    Choose(id, "choi_first", Half, null, When(ConditionDefinition.Flag(R.N4R10ChoiFirst)),
                        Trust(-2), Pick(ChoiceIds.SunjaTestimony, "PARTIAL"), Set(FlagIds.SunjaTestimony), Set("N4R10_CHOI_INFO")),
                    // Fail-safe: the door was never knocked on. Nothing was said.
                    Choose(id, "unheard", Half, null, When(
                            ConditionDefinition.Flag(R.N4R10Consent, false), ConditionDefinition.Flag(R.N4R10Hurried, false),
                            ConditionDefinition.Flag(R.N4R10ChoiFirst, false)),
                        Pick(ChoiceIds.SunjaTestimony, "NONE"), Set(FlagIds.EndSunjaTestimonyFull, false))
                };
                yield return c;
            }

            // ---- N4-R11 아이 둘의 연필 낙서 (ANOMALY / RECORD) -------------------
            {
                const string id = "N4-R11";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Record);
                c.mutexGroup = "f4_wall";
                c.objectives = new[] { Look("EV_N4R11_DRAWING"), Look("EV_N4R11_SCRAPED", true) };
                c.decisions = new[]
                {
                    Choose(id, "photo_keep", Good, Needs("EV_N4R11_DRAWING"), null, Harin(5), Archive(3)),
                    Choose(id, "scraped", Half, Needs("EV_N4R11_SCRAPED"), null, Harin(2)),
                    Choose(id, "paint_over", Bad, null, null, Owe(DebtIds.Record, 1))
                };
                yield return c;
            }

            // ---- N4-R12 404 우편함이 잠깐 생성됨 (ANOMALY / SPACE) ---------------
            {
                const string id = "N4-R12";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Space);
                c.objectives = new[] { Look("EV_N4R12_MEASURE"), Look("EV_N4R12_PAPER", true) };
                c.decisions = new[]
                {
                    Choose(id, "took_contents", Good, Needs("EV_N4R12_PAPER"), null, Archive(4), Harin(2)),
                    Choose(id, "measured", Half, Needs("EV_N4R12_MEASURE"), null, Set("N4R12_MEASURED")),
                    Choose(id, "ignore", Half, null, null)
                };
                yield return c;
            }

            // ---- N4-R13 엘리베이터 안 가족사진 반사 (ANOMALY / IDENTITY) ---------
            {
                const string id = "N4-R13";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Identity);
                c.objectives = new[] { Watch("CAM-08"), Look("EV_N4R13_MIRROR") };
                c.decisions = new[]
                {
                    Choose(id, "got_off", Good, Needs("EV_N4R13_MIRROR"), When(ConditionDefinition.Flag(R.N4R13Watched, false))),
                    Choose(id, "kept_watching", Half, null, When(ConditionDefinition.Flag(R.N4R13Watched)),
                        Harin(4), San(-5, "reason.watched_the_reflection")),
                    Choose(id, "photo", Half, Needs("EV_N4R13_MIRROR"), null, Archive(2), Give("EV_N4R13_PHOTO"))
                };
                yield return c;
            }

            // ---- N4-R14 기록실 캐비닛의 현재 날짜 (ANOMALY / FUTURE) -------------
            {
                const string id = "N4-R14";
                var c = Sub(id, 4, QuestType.Anomaly, AnomalyFamily.Future);
                c.mutexGroup = "archive_cabinet";
                c.objectives = new[] { Look("EV_N4R14_STAMP") };
                c.decisions = new[]
                {
                    Choose(id, "seal", Good, Needs("EV_N4R14_STAMP"), null, Archive(5)),
                    Choose(id, "compare_stamp", Half, Needs("EV_N4R14_STAMP"), null, Alert(2), Give("EV_N4R14_COMPARE")),
                    Choose(id, "discard", Bad, null, null, Owe(DebtIds.Record, 1))
                };
                yield return c;
            }
        }

        // =================================================================
        // Night 6 (v5.1 15) - the last shift, and everything it costs
        // =================================================================

        static IEnumerable<CaseDefinition> Night6Subquests()
        {
            // ---- N6-R01 마지막 정상 주민 요청 (NORMAL) ---------------------------
            {
                const string id = "N6-R01";
                var c = Sub(id, 6, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N6R01_SLIP") };
                c.decisions = new[]
                {
                    Choose(id, "helped", Good, Needs("EV_N6R01_SLIP"), When(ConditionDefinition.Flag(R.N6R01Safe)),
                        Trust(3), San(1, "reason.read_correctly")),
                    Choose(id, "escorted_in", Half, Needs("EV_N6R01_SLIP"), When(ConditionDefinition.Flag(R.N6R01Safe, false)),
                        Trust(3), Hp(-5, "reason.smoke")),
                    Choose(id, "declined", Half, null, null)
                };
                yield return c;
            }

            // ---- N6-R02 기록서버 자동 삭제 카운트 (FINAL_PRESSURE) ----------------
            {
                const string id = "N6-R02";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.None);
                c.mutexGroup = "archive_server";
                c.objectives = new[] { Look("EV_N6R02_INDEX") };
                c.decisions = new[]
                {
                    Choose(id, "core_four", Good, Needs("EV_N6R02_INDEX"), null, Archive(4)),
                    Choose(id, "full_backup", Half, null, When(ConditionDefinition.Flag(R.N6R02Deleting, false)), Archive(2)),
                    Choose(id, "full_backup_lost", Bad, null, When(ConditionDefinition.Flag(R.N6R02Deleting)), Archive(-4)),
                    Choose(id, "give_up", Bad, null, null, Archive(-8))
                };
                yield return c;
            }

            // ---- N6-R03 404 복도 구조 변화 (FINAL_PRESSURE / SPACE) --------------
            {
                const string id = "N6-R03";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Space);
                c.mutexGroup = "f4_corridor";
                // The night-3 marker, if one was put down, or the seal that is always there.
                c.objectives = new[] { Look("EV_N6R03_REFERENCE") };
                c.decisions = new[]
                {
                    Choose(id, "followed_marker", Good, Needs("EV_N6R03_REFERENCE"), null, Owe(DebtIds.Distortion, -1)),
                    Choose(id, "wall_contact", Half, null, null),
                    Choose(id, "trusted_eyes", Bad, null, null,
                        San(-8, "reason.corridor_bent"), Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N6-R04 미래의 자신의 구조 방송 (FINAL_PRESSURE / FUTURE) --------
            {
                const string id = "N6-R04";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Future);
                c.objectives = new[] { Look("EV_N6R04_BROADCAST"), Watch("CAM-04", true) };
                c.decisions = new[]
                {
                    Choose(id, "checked_now", Good, Needs("EV_N6R04_BROADCAST"), null),
                    Choose(id, "followed_foreseen", Half, null, When(ConditionDefinition.Flag(R.N6R04Foreseen)), Harin(2)),
                    Choose(id, "followed_blind", Bad, null, When(ConditionDefinition.Flag(R.N6R04Foreseen, false)),
                        Hp(-5, "reason.followed_the_broadcast"), Owe(DebtIds.Distortion, 1)),
                    Choose(id, "cut_broadcast", Half, null, null, San(2, "reason.quiet_again"))
                };
                yield return c;
            }

            // ---- N6-R05 하린의 방 문 (FIXED_STORY, ending O) ---------------------
            {
                const string id = "N6-R05";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Identity, story: true);
                c.objectives = new[] { Look("EV_N6R05_DOOR"), Look("EV_N6R05_SLIPPER", true) };
                c.decisions = new[]
                {
                    Choose(id, "noted_closed", Good, Needs("EV_N6R05_DOOR"), When(ConditionDefinition.Flag(R.N6R05Opened, false)),
                        San(-3, "reason.her_door"), Set(FlagIds.EndHarinDoorOpened, false)),
                    Choose(id, "opened", Good, Needs("EV_N6R05_DOOR"), When(ConditionDefinition.Flag(R.N6R05Opened)),
                        Harin(3), Set(FlagIds.EndHarinDoorOpened)),
                    Choose(id, "took_slipper", Half, Needs("EV_N6R05_DOOR", "EV_N6R05_SLIPPER"),
                        When(ConditionDefinition.Flag(R.N6R05Opened, false)),
                        Harin(3), San(-5, "reason.her_door"), Set(FlagIds.EndHarinDoorOpened, false))
                };
                yield return c;
            }

            // ---- N6-R06 박동식 유해 발견 지점 붕괴 (FINAL_PRESSURE / SPACE) ------
            {
                const string id = "N6-R06";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Space);
                c.mutexGroup = "service_passage";
                c.objectives = new[] { Look("EV_N6R06_NAMEPLATE") };
                // v5.1 N6-R06 only changes how hard the way in is; the ending flag stays N6-M01's.
                c.decisions = new[]
                {
                    Choose(id, "stabilised", Good, Needs("EV_N6R06_NAMEPLATE"), When(ConditionDefinition.Flag(R.N6R06Stabilised)),
                        Set("DONGSIK_REMAINS_ACCESS_SAFE")),
                    Choose(id, "cleared", Half, Needs("EV_N6R06_NAMEPLATE"), When(ConditionDefinition.Flag(R.N6R06Cleared)),
                        Set("DONGSIK_REMAINS_ACCESS_SAFE")),
                    Choose(id, "detoured", Half, null, When(
                            ConditionDefinition.Flag(R.N6R06Stabilised, false), ConditionDefinition.Flag(R.N6R06Cleared, false)),
                        San(-5, "reason.corridor_bent"))
                };
                yield return c;
            }

            // ---- N6-R07 원본 장부와 복제 장부 (FINAL_PRESSURE / RECORD) ----------
            {
                const string id = "N6-R07";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Record);
                c.objectives = new[] { Look("EV_N6R07_COPY"), Look("EV_N6R07_PAPER_TEST", true) };
                // Only how sure the caretaker is. Securing the ledger is N6-M01's (v5.1 N6-R07).
                c.decisions = new[]
                {
                    Choose(id, "physical_test", Good, Needs("EV_N6R07_COPY", "EV_N6R07_PAPER_TEST"), null,
                        Archive(2), Set("LEDGER_CONFIDENCE_HIGH")),
                    Choose(id, "took_both", Half, Needs("EV_N6R07_COPY"), null, Archive(1)),
                    Choose(id, "read_screen", Bad, null, null, Set("LEDGER_MISREAD"), Owe(DebtIds.Record, 1))
                };
                yield return c;
            }

            // ---- N6-R08 연기 유입 (FINAL_PRESSURE) -------------------------------
            {
                const string id = "N6-R08";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.None);
                c.mutexGroup = "f4_fire_door";
                c.objectives = new[] { Look("EV_N6R08_TEMP") };
                c.decisions = new[]
                {
                    Choose(id, "door_secured", Good, Needs("EV_N6R08_TEMP"), When(ConditionDefinition.Flag(R.N6R08DoorSecured)),
                        Safety(3)),
                    Choose(id, "vented", Half, null,
                        When(ConditionDefinition.Flag(R.N6R08Vented), ConditionDefinition.Flag(R.N6R08DoorSecured, false)),
                        Safety(1)),
                    Choose(id, "pushed_on", Bad, null, null, Safety(-4))
                };
                yield return c;
            }

            // ---- N6-R09 관리실 원격 잠금 (FINAL_PRESSURE) ------------------------
            {
                const string id = "N6-R09";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N6R09_LOCK") };
                c.decisions = new[]
                {
                    Choose(id, "cut_circuit", Good, Needs("EV_N6R09_LOCK"), When(ConditionDefinition.Flag(R.N6R09Cut)), Alert(2)),
                    Choose(id, "forced", Half, null,
                        When(ConditionDefinition.Flag(R.N6R09Forced), ConditionDefinition.Flag(R.N6R09Cut, false)), Safety(-1)),
                    Choose(id, "took_stairs", Half, null,
                        When(ConditionDefinition.Flag(R.N6R09Cut, false), ConditionDefinition.Flag(R.N6R09Forced, false)),
                        San(-3, "reason.lost_in_the_building"))
                };
                yield return c;
            }

            // ---- N6-R10 주민 단체방의 404 소문 (FINAL_PRESSURE) ------------------
            {
                const string id = "N6-R10";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N6R10_CHAT") };
                c.decisions = new[]
                {
                    Choose(id, "facts_believed", Good, Needs("EV_N6R10_CHAT"),
                        When(ConditionDefinition.Stat(StatIds.CommunityTrust, 50)), Trust(3), Safety(2)),
                    Choose(id, "facts_doubted", Good, Needs("EV_N6R10_CHAT"),
                        When(ConditionDefinition.StatAtMost(StatIds.CommunityTrust, 49)), Trust(1)),
                    Choose(id, "whole_truth", Half, null, null, Archive(2), Trust(-3)),
                    Choose(id, "silence", Bad, null, null, Trust(-5), Safety(-2))
                };
                yield return c;
            }

            // ---- N6-R11 CCTV 속 빈 404 (FINAL_PRESSURE / ECHO) -------------------
            {
                const string id = "N6-R11";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Echo);
                c.mutexGroup = "service_passage";
                c.objectives = new[] { Watch("CAM-04"), Look("EV_N6R11_SEAL") };
                c.decisions = new[]
                {
                    Choose(id, "cctv_as_aid", Good, Needs("EV_N6R11_SEAL"), null),
                    Choose(id, "camera_off", Half, null, null),
                    Choose(id, "followed_feed", Bad, null, null,
                        San(-6, "reason.followed_the_feed"), Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N6-R12 모든 CCTV의 관리실 재현 (FINAL_PRESSURE / SPACE) ---------
            {
                const string id = "N6-R12";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Space);
                c.mutexGroup = "cam_lobby";
                c.objectives = new[] { Watch("CAM-01"), Look("EV_N6R12_DIRECTION") };
                c.decisions = new[]
                {
                    Choose(id, "isolate", Good, Needs("EV_N6R12_DIRECTION"), null, San(-6, "reason.office_on_every_screen")),
                    Choose(id, "power_off", Half, null, null, Set("N6_CCTV_DOWN")),
                    Choose(id, "kept_watching", Bad, null, null,
                        San(-10, "reason.office_on_every_screen"), Harin(4), Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N6-R13 404 문 뒤의 두 목소리 (FINAL_PRESSURE / IDENTITY) --------
            {
                const string id = "N6-R13";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Identity);
                c.mutexGroup = "service_passage";
                c.objectives = new[] { Look("EV_N6R13_RADIO"), Look("EV_N6R13_CHILD", true) };
                c.decisions = new[]
                {
                    Choose(id, "radio_pattern", Good, Needs("EV_N6R13_RADIO"), null,
                        San(-5, "reason.two_voices"), Set("DONGSIK_REMAINS_HINT")),
                    Choose(id, "marked_both", Good, Needs("EV_N6R13_RADIO", "EV_N6R13_CHILD"), null,
                        San(-5, "reason.two_voices"), Harin(2), Set("DONGSIK_REMAINS_HINT")),
                    Choose(id, "chased_child", Half, Needs("EV_N6R13_CHILD"), null,
                        San(-8, "reason.two_voices"), Harin(3)),
                    Choose(id, "moved_on", Bad, null, null)
                };
                yield return c;
            }

            // ---- N6-R14 최종 출구의 층수 오류 (FINAL_PRESSURE / SPACE) -----------
            {
                const string id = "N6-R14";
                var c = Sub(id, 6, QuestType.FinalPressure, AnomalyFamily.Space);
                c.mutexGroup = "stair_f1";
                c.objectives = new[] { Look("EV_N6R14_AIR") };
                c.decisions = new[]
                {
                    Choose(id, "physical_exit", Good, Needs("EV_N6R14_AIR"), null, San(1, "reason.read_correctly")),
                    Choose(id, "followed_number", Bad, null, null,
                        San(-8, "reason.lost_in_the_building"), Hp(-5, "reason.lost_in_the_building")),
                    Choose(id, "lift_running", Half, null, When(ConditionDefinition.Flag(R.ElevatorShed, false))),
                    Choose(id, "lift_dead", Bad, null, When(ConditionDefinition.Flag(R.ElevatorShed)),
                        Hp(-5, "reason.stuck_in_the_lift"))
                };
                yield return c;
            }
        }

        // =================================================================
        // the conversations
        // =================================================================

        public static DialogueDefinition[] BuildEvenSubquestDialogues()
        {
            return new[] { Call2009Caller(), SunjaAt303() };
        }

        public static PhoneCallDefinition[] BuildEvenSubquestCalls()
        {
            // v5.1 N2-R12: a disconnected extension. It belongs to the quest's beat and rings
            // when the quest opens; a missed call still leaves the line in the log.
            var call = Call("CALL_N2R12_2009", "caller.extension_closed", "4-407", "D_N2R12_CALL_2009",
                            CallPriority.Resident, 2, At(23, 0), "N2-R12",
                            new[]
                            {
                                ConsequenceDefinition.Flag(R.N2R12Missed, true),
                                ConsequenceDefinition.Evidence("EV_N2R12_CALL")
                            }, 0);
            call.onAnswered = new ConsequenceDefinition[0];
            return new[] { call };
        }

        static DialogueDefinition Call2009Caller()
        {
            return Conversation("D_N2R12_CALL_2009", DialogueChannel.Phone, 0.25f, 0f,
                Node("start", "speaker.unknown", "dlg.n2r12.start", "smell"),
                Node("smell", "speaker.unknown", "dlg.n2r12.smell", null,
                    Choices(
                        Choice("ask_where", "dlg.n2r12.choice.where", "where", false,
                            ConsequenceDefinition.Flag(R.N2R12Asked, true),
                            ConsequenceDefinition.Evidence("EV_N2R12_CALL")),
                        Choice("go_4f", "dlg.n2r12.choice.go", "go", false,
                            ConsequenceDefinition.Flag(R.N2R12Went4F, true),
                            ConsequenceDefinition.Evidence("EV_N2R12_CALL")),
                        Choice("hang_up", "dlg.n2r12.choice.hang_up", "cut", true,
                            ConsequenceDefinition.Flag(R.N2R12HungUp, true),
                            ConsequenceDefinition.Evidence("EV_N2R12_CALL")))),
                Node("where", "speaker.unknown", "dlg.n2r12.where", "dead"),
                Node("dead", "speaker.unknown", "dlg.n2r12.dead", null),
                Node("go", "speaker.unknown", "dlg.n2r12.go", null),
                Node("cut", "speaker.unknown", "dlg.n2r12.cut", null));
        }

        /// <summary>
        /// v5.1 N4-R03, in the conversation the night-4 404 call has always had. Every answer
        /// leaves the call in the log, so the report always has something to file.
        /// </summary>
        static DialogueDefinition Call404Line()
        {
            return Conversation("D_N4_CALL_404", DialogueChannel.Phone, 0.25f, 0f,
                Node("start", "speaker.unknown", "dlg.n4.call404.start", "child"),
                Node("child", "speaker.child", "dlg.n4.call404.child", null,
                    Choices(
                        Choice("ask_where", "dlg.n4.call404.choice.where", "where", false,
                            ConsequenceDefinition.Flag(R.N4R03AskedWhere, true),
                            ConsequenceDefinition.Flag(FlagIds.Knows404, true),
                            ConsequenceDefinition.Evidence("EV_N4R03_CALL")),
                        Choice("ask_name", "dlg.n4.call404.choice.name", "name", false,
                            ConsequenceDefinition.Flag(R.N4R03AskedName, true),
                            ConsequenceDefinition.Flag(FlagIds.Knows404, true),
                            ConsequenceDefinition.Evidence("EV_N4R03_CALL")),
                        Choice("listen", "dlg.n4.call404.choice.listen", "listen", false,
                            ConsequenceDefinition.Flag(R.N4R03Listened, true),
                            ConsequenceDefinition.Evidence("EV_N4R03_CALL")),
                        Choice("hang_up", "dlg.n4.call404.choice.hang_up", "hung_up", true,
                            ConsequenceDefinition.Flag(R.N4R03HungUp, true),
                            ConsequenceDefinition.Evidence("EV_N4R03_CALL")))),
                Node("where", "speaker.child", "dlg.n4.call404.where", null),
                Node("name", "speaker.child", "dlg.n4.call404.name", null),
                Node("listen", "speaker.unknown", "dlg.n4.call404.listen", null),
                Node("hung_up", "speaker.unknown", "dlg.n4.call404.hung_up", null));
        }

        static DialogueDefinition SunjaAt303()
        {
            return Conversation("D_N4R10_SUNJA", DialogueChannel.FaceToFace, 0.25f, 0f,
                Node("start", "speaker.sunja", "dlg.n4r10.start", "remember"),
                Node("remember", "speaker.sunja", "dlg.n4r10.remember", null,
                    Choices(
                        Choice("consent", "dlg.n4r10.choice.consent", "consent", false,
                            ConsequenceDefinition.Flag(R.N4R10Consent, true),
                            ConsequenceDefinition.Evidence("EV_N4R10_TESTIMONY")),
                        Choice("hurry", "dlg.n4r10.choice.hurry", "hurry", false,
                            ConsequenceDefinition.Flag(R.N4R10Hurried, true),
                            ConsequenceDefinition.Evidence("EV_N4R10_TESTIMONY")),
                        Choice("choi_first", "dlg.n4r10.choice.choi", "choi", false,
                            ConsequenceDefinition.Flag(R.N4R10ChoiFirst, true),
                            ConsequenceDefinition.Evidence("EV_N4R10_TESTIMONY")))),
                Node("consent", "speaker.sunja", "dlg.n4r10.consent", null),
                Node("hurry", "speaker.sunja", "dlg.n4r10.hurry", null),
                Node("choi", "speaker.sunja", "dlg.n4r10.choi", null));
        }
    }
}
