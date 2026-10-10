using System.Collections.Generic;
using NO404.Cases;
using NO404.Core;
using NO404.Evidence;
using R = NO404.Cases.SubquestRules;

namespace NO404.ContentData
{
    /// <summary>
    /// Nights 1, 3 and 5 as v5.1 writes them (sections 10, 12, 14): fourteen subquests each
    /// around the main - one fixed story subquest and thirteen random candidates.
    ///
    /// Every quest is the same shape on purpose. A place to go, one or two things there to
    /// read, and a report whose honest options are exactly v5.1's "선택지 및 결과" rows with
    /// v5.1's numbers. The correct option always needs the evidence, because 19.2 forbids a
    /// quest whose answer is available without looking. What cannot be data - shocks, loops,
    /// variants decided by an earlier night - is in <see cref="SubquestRules"/>.
    ///
    /// Night 2, 4 and 6 still carry only their main. That is the scope of this pass, not an
    /// oversight; <see cref="NightPoolService.HasAuthoredPool"/> is what tells the rest of the
    /// game which nights are whole.
    /// </summary>
    public static partial class SeedContent
    {
        public static CaseDefinition[] BuildV51Subquests()
        {
            var list = new List<CaseDefinition>(42);
            list.AddRange(Night1Subquests());
            list.AddRange(Night3Subquests());
            list.AddRange(Night5Subquests());
            foreach (var c in list) c.evidenceIds = EvidenceOf(c);
            return list.ToArray();
        }

        // =================================================================
        // shape
        // =================================================================

        static CaseDefinition Sub(string id, int night, QuestType type, AnomalyFamily family,
                                  bool story = false, float minutes = 5.5f)
        {
            var c = NewCase(id, night, CaseKind.MainCase, story ? Priority.P0 : Priority.P1);
            // Keys stay in the case namespace either way; only the bookkeeping kind differs.
            if (type == QuestType.Normal && !story) c.kind = CaseKind.RoutineTask;
            c.questType = type;
            c.family = family;
            c.isFixedStory = story;
            c.baseWeight = story ? 0 : 100;
            c.estimatedMinutes = minutes;
            c.trigger = CaseTrigger.Time;
            c.startWindowBegin = At(22, 10);
            c.failSafe = FailSafe(T0300, null, "quest.sub.failsafe");
            c.analyticsName = id.ToLowerInvariant();
            return c;
        }

        static string Key(string caseId) { return "case." + caseId.ToLowerInvariant(); }

        /// <summary>Everything a quest can be investigated with, as the mains list theirs.</summary>
        static string[] EvidenceOf(CaseDefinition c)
        {
            var ids = new List<string>();
            foreach (var o in c.objectives)
                if (o.type == ObjectiveType.AcquireEvidence && !ids.Contains(o.targetId)) ids.Add(o.targetId);
            foreach (var d in c.decisions)
                foreach (var e in d.requiredEvidenceIds)
                    if (!ids.Contains(e)) ids.Add(e);
            return ids.ToArray();
        }

        /// <summary>The prompt on the prop and the line in the HUD are the same words.</summary>
        public static string PropKey(string evidenceId) { return "quest.prop." + evidenceId.ToLowerInvariant(); }

        static ObjectiveDefinition Look(string evidenceId, bool optional = false)
        {
            return Objective("obj_" + evidenceId.ToLowerInvariant(), PropKey(evidenceId),
                             ObjectiveType.AcquireEvidence, evidenceId, optional);
        }

        static ObjectiveDefinition Watch(string cameraId, bool optional = false)
        {
            return Objective("obj_" + cameraId.ToLowerInvariant(), "quest.cam." + cameraId.ToLowerInvariant(),
                             ObjectiveType.ViewCctvChannel, cameraId, optional);
        }

        static ObjectiveDefinition Reach(string zoneId, bool optional = false)
        {
            return Objective("obj_go_" + zoneId.ToLowerInvariant(), "quest.goto." + zoneId.ToLowerInvariant(),
                             ObjectiveType.EnterZone, zoneId, optional);
        }

        static ObjectiveDefinition Record(string residentId, bool optional = true)
        {
            return Objective("obj_" + residentId, "quest.record." + residentId,
                             ObjectiveType.ViewRecord, residentId, optional);
        }

        static DecisionDefinition Choose(string caseId, string suffix, DecisionQuality quality,
                                         string[] needs, ConditionDefinition[] when,
                                         params ConsequenceDefinition[] then)
        {
            return Decision("dec_" + suffix, Key(caseId) + ".decision." + suffix,
                            Key(caseId) + ".result." + suffix, quality, needs, when, then);
        }

        static string[] Needs(params string[] evidence) { return evidence; }
        static ConditionDefinition[] When(params ConditionDefinition[] c) { return c; }

        static ConsequenceDefinition Trust(int d) { return ConsequenceDefinition.Stat(StatIds.CommunityTrust, d, "reason.subquest"); }
        static ConsequenceDefinition Safety(int d) { return ConsequenceDefinition.Stat(StatIds.BuildingSafety, d, "reason.subquest"); }
        static ConsequenceDefinition Archive(int d) { return ConsequenceDefinition.Stat(StatIds.ArchiveIntegrity, d, "reason.subquest"); }
        static ConsequenceDefinition Harin(int d) { return ConsequenceDefinition.Stat(StatIds.HarinResonance, d, "reason.subquest"); }
        static ConsequenceDefinition Alert(int d) { return ConsequenceDefinition.Stat(StatIds.ChairmanAlert, d, "reason.subquest"); }
        static ConsequenceDefinition San(int d, string why) { return ConsequenceDefinition.Sanity(d, why); }
        static ConsequenceDefinition Hp(int d, string why) { return ConsequenceDefinition.Health(d, why); }
        static ConsequenceDefinition Owe(string debt, int n) { return ConsequenceDefinition.Debt(debt, n, "reason.subquest"); }
        static ConsequenceDefinition Set(string flag, bool v = true) { return ConsequenceDefinition.Flag(flag, v); }
        static ConsequenceDefinition Pick(string choice, string v) { return ConsequenceDefinition.Choice(choice, v); }
        static ConsequenceDefinition Give(string evidence) { return ConsequenceDefinition.Evidence(evidence); }

        const DecisionQuality Good = DecisionQuality.Correct;
        const DecisionQuality Half = DecisionQuality.Partial;
        const DecisionQuality Bad = DecisionQuality.Wrong;

        // =================================================================
        // evidence
        // =================================================================

        public static EvidenceDefinition[] BuildV51Evidence()
        {
            return new[]
            {
                // ---- night 1 ----
                Evidence_("EV_N1R02_PRESSURE", EvidenceType.MeterGraph, "N1-R02", false),
                Evidence_("EV_N1R02_FILTER", EvidenceType.PhysicalObject, "N1-R02", false),
                Evidence_("EV_N1R03_SENSOR", EvidenceType.PhysicalObject, "N1-R03", false),
                Evidence_("EV_N1R04_PLATE", EvidenceType.Photo, "N1-R04", false),
                Evidence_("EV_N1R04_DB", EvidenceType.Document, "N1-R04", false),
                Evidence_("EV_N1R05_DAMPER", EvidenceType.PhysicalObject, "N1-R05", false),
                Evidence_("EV_N1R06_TRAP", EvidenceType.PhysicalObject, "N1-R06", false),
                Evidence_("EV_N1R06_SEPARATE", EvidenceType.MeterGraph, "N1-R06", false),
                Evidence_("EV_N1R07_SENSOR", EvidenceType.PhysicalObject, "N1-R07", false),
                Evidence_("EV_N1R08_SHELF", EvidenceType.PhysicalObject, "N1-R08", false),
                Evidence_("EV_N1R09_ENVELOPE", EvidenceType.Document, "N1-R09", false),
                Evidence_("EV_N1R10_LOG", EvidenceType.MeterGraph, "N1-R10", false),
                Evidence_("EV_N1R10_METER", EvidenceType.MeterGraph, "N1-R10", false),
                Evidence_("EV_N1R11_LIFT_LOG", EvidenceType.Document, "N1-R11", false),
                Evidence_("EV_N1R11_GHOST_PRESS", EvidenceType.Photo, "N1-R11", false),
                Evidence_("EV_N1R12_CLOCK", EvidenceType.Photo, "N1-R12", false),
                Evidence_("EV_N1R12_LOOP_CLIP", EvidenceType.CctvSnapshot, "N1-R12", false),
                Evidence_("EV_N1R13_AFTERIMAGE", EvidenceType.CctvSnapshot, "N1-R13", false),
                Evidence_("EV_N1R14_STICKER", EvidenceType.PhysicalObject, "N1-R14", false),
                Evidence_("EV_N1R14_FURNITURE", EvidenceType.PhysicalObject, "N1-R14", false),

                // ---- night 3 ----
                Evidence_("EV_N3R01_WORK_ORDER", EvidenceType.Document, "N3-R01", false),
                Evidence_("EV_N3R01_BADGE", EvidenceType.Document, "N3-R01", false),
                Evidence_("EV_N3R02_WORK_ORDER", EvidenceType.Document, "N3-R02", false),
                Evidence_("EV_N3R02_SEAL", EvidenceType.Photo, "N3-R02", false),
                Evidence_("EV_N3R02_CONTROL_LOG", EvidenceType.Document, "N3-R02", false),
                Evidence_("EV_N3R03_CLOSER", EvidenceType.PhysicalObject, "N3-R03", false),
                Evidence_("EV_N3R04_WALL_LENGTH", EvidenceType.MeterGraph, "N3-R04", false),
                Evidence_("EV_N3R04_REPLY", EvidenceType.AudioRecording, "N3-R04", false),
                Evidence_("EV_N3R05_LAMP", EvidenceType.PhysicalObject, "N3-R05", false),
                Evidence_("EV_N3R06_SEAL", EvidenceType.Photo, "N3-R06", false),
                Evidence_("EV_N3R06_HANDOVER", EvidenceType.Document, "N3-R06", false),
                Evidence_("EV_N3R06_INNER", EvidenceType.Document, "N3-R06", false),
                Evidence_("EV_N3R07_PLAN", EvidenceType.Document, "N3-R07", false),
                Evidence_("EV_N3R07_COUNT", EvidenceType.AudioRecording, "N3-R07", false),
                Evidence_("EV_N3R08_EXTINGUISHER", EvidenceType.Photo, "N3-R08", false),
                Evidence_("EV_N3R08_WINDOW", EvidenceType.Photo, "N3-R08", false),
                Evidence_("EV_N3R09_CONTROL_LOG", EvidenceType.Document, "N3-R09", false),
                Evidence_("EV_N3R09_POSITION", EvidenceType.Photo, "N3-R09", false),
                Evidence_("EV_N3R10_MEASURE", EvidenceType.MeterGraph, "N3-R10", false),
                Evidence_("EV_N3R11_LANDING_PAINT", EvidenceType.Photo, "N3-R11", false),
                Evidence_("EV_N3R11_SEAL", EvidenceType.Photo, "N3-R11", false),
                Evidence_("EV_N3R12_CHILD_ITEMS", EvidenceType.PhysicalObject, "N3-R12", true),
                Evidence_("EV_N3R13_LAMP_NO", EvidenceType.Photo, "N3-R13", false),
                Evidence_("EV_N3R13_SEAL_NO", EvidenceType.Photo, "N3-R13", false),
                Evidence_("EV_N3R14_FUTURE_VOICE", EvidenceType.AudioRecording, "N3-R14", false),

                // ---- night 5 ----
                Evidence_("EV_N5R01_LOAD", EvidenceType.MeterGraph, "N5-R01", false),
                Evidence_("EV_N5R01_RESET", EvidenceType.MeterGraph, "N5-R01", false),
                Evidence_("EV_N5R02_STATE", EvidenceType.Testimony, "N5-R02", false),
                Evidence_("EV_N5R03_GAUGE", EvidenceType.MeterGraph, "N5-R03", false),
                Evidence_("EV_N5R03_VALVE", EvidenceType.PhysicalObject, "N5-R03", false),
                Evidence_("EV_N5R04_SEAL", EvidenceType.Photo, "N5-R04", false),
                Evidence_("EV_N5R04_LOG", EvidenceType.AccessLog, "N5-R04", false),
                Evidence_("EV_N5R05_PACKAGE", EvidenceType.PhysicalObject, "N5-R05", false),
                Evidence_("EV_N5R05_CONTENTS", EvidenceType.PhysicalObject, "N5-R05", false),
                Evidence_("EV_N5R06_SLOT", EvidenceType.PhysicalObject, "N5-R06", false),
                Evidence_("EV_N5R06_2009_VOICE", EvidenceType.AudioRecording, "N5-R06", true),
                Evidence_("EV_N5R07_TEMP", EvidenceType.MeterGraph, "N5-R07", false),
                Evidence_("EV_N5R07_PANEL", EvidenceType.Document, "N5-R07", false),
                Evidence_("EV_N5R08_LED", EvidenceType.Photo, "N5-R08", false),
                Evidence_("EV_N5R09_SEAL", EvidenceType.Photo, "N5-R09", false),
                Evidence_("EV_N5R10_MARKER", EvidenceType.Photo, "N5-R10", false),
                Evidence_("EV_N5R11_STAIRS", EvidenceType.Photo, "N5-R11", false),
                Evidence_("EV_N5R11_RISK", EvidenceType.Document, "N5-R11", false),
                Evidence_("EV_N5R12_TEMP", EvidenceType.MeterGraph, "N5-R12", false),
                Evidence_("EV_N5R12_VALVE", EvidenceType.PhysicalObject, "N5-R12", false),
                Evidence_("EV_N5R13_PATTERN", EvidenceType.AudioRecording, "N5-R13", true),
                Evidence_("EV_N5R13_POSITION", EvidenceType.Photo, "N5-R13", false),
                Evidence_("EV_N5R13_KNOCK", EvidenceType.AudioRecording, "N5-R13", false),
                Evidence_("EV_N5R14_OVERLAP", EvidenceType.MeterGraph, "N5-R14", false)
            };
        }

        // =================================================================
        // Night 1 (v5.1 10) - real work, and the first crack
        // =================================================================

        static IEnumerable<CaseDefinition> Night1Subquests()
        {
            // ---- N1-R01 방문간호사 김민서 (FIXED_STORY, ending O) -------------
            {
                const string id = "N1-R01";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None, story: true);
                c.objectives = new[]
                {
                    Objective("obj_judge", Key(id) + ".objective.judge", ObjectiveType.JudgeVisitor, "vis_minseo"),
                    Record("res_303")
                };
                // The door decides; the report files what the door decided (one row is open).
                c.decisions = new[]
                {
                    Choose(id, "escort", Good, null, When(ConditionDefinition.Flag(R.MinseoEscorted)),
                        Trust(6), Pick(ChoiceIds.SunjaCare, "GOOD"), Set(FlagIds.EndSunjaTrusted)),
                    Choose(id, "floorpass", Good, null, When(ConditionDefinition.Flag(R.MinseoFloorPass)),
                        Trust(3), Pick(ChoiceIds.SunjaCare, "GOOD"), Set(FlagIds.EndSunjaTrusted)),
                    Choose(id, "lobby", Half, null, When(ConditionDefinition.Flag(R.MinseoLobbyOnly)),
                        Trust(-4), Pick(ChoiceIds.SunjaCare, "DELAYED")),
                    Choose(id, "deny", Bad, null, When(ConditionDefinition.Flag(R.MinseoDenied)),
                        Trust(-10), Pick(ChoiceIds.SunjaCare, "DENIED"), Owe(DebtIds.Trust, 1),
                        Set(FlagIds.SunjaHealthy, false)),
                    // Fail-safe: nobody ever reached the door. Care was late, not refused.
                    Choose(id, "unresolved", Half, null, When(
                            ConditionDefinition.Flag(R.MinseoEscorted, false), ConditionDefinition.Flag(R.MinseoFloorPass, false),
                            ConditionDefinition.Flag(R.MinseoLobbyOnly, false), ConditionDefinition.Flag(R.MinseoDenied, false)),
                        Trust(-4), Pick(ChoiceIds.SunjaCare, "DELAYED"))
                };
                yield return c;
            }

            // ---- N1-R02 B1 저수조 압력 저하 (NORMAL) ---------------------------
            {
                const string id = "N1-R02";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None);
                c.mutexGroup = "b1_pump";
                c.objectives = new[] { Look("EV_N1R02_PRESSURE"), Look("EV_N1R02_FILTER", true) };
                c.decisions = new[]
                {
                    Choose(id, "repair", Good, Needs("EV_N1R02_FILTER"), null,
                        Pick(ChoiceIds.PumpStatus, "REPAIRED"), Safety(8)),
                    Choose(id, "bypass", Half, null, null,
                        Pick(ChoiceIds.PumpStatus, "BYPASS"), Owe(DebtIds.Safety, 1)),
                    Choose(id, "ignore", Bad, null, null,
                        Pick(ChoiceIds.PumpStatus, "IGNORED"), Safety(-10))
                };
                yield return c;
            }

            // ---- N1-R03 공동현관 자동문 닫힘 불량 (NORMAL) ----------------------
            {
                const string id = "N1-R03";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N1R03_SENSOR") };
                c.decisions = new[]
                {
                    Choose(id, "clean", Good, Needs("EV_N1R03_SENSOR"), null, Safety(4)),
                    Choose(id, "manual_lock", Half, null, null, Trust(-2)),
                    Choose(id, "leave", Bad, null, null, Owe(DebtIds.Access, 1))
                };
                yield return c;
            }

            // ---- N1-R04 B1 불법 주차 차량 (NORMAL) ------------------------------
            {
                const string id = "N1-R04";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N1R04_PLATE"), Look("EV_N1R04_DB", true) };
                c.decisions = new[]
                {
                    Choose(id, "call_owner", Good, Needs("EV_N1R04_PLATE", "EV_N1R04_DB"), null, Trust(2), Safety(3)),
                    Choose(id, "tow_warning", Half, null, null, Trust(-3), Safety(3)),
                    Choose(id, "ignore", Bad, null, null, Safety(-6))
                };
                yield return c;
            }

            // ---- N1-R05 602 층간소음 민원 (NORMAL) ------------------------------
            {
                const string id = "N1-R05";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None);
                // Optional on purpose: closing it over the phone is a real (worse) answer.
                c.objectives = new[] { Look("EV_N1R05_DAMPER", true) };
                c.decisions = new[]
                {
                    Choose(id, "checked", Good, Needs("EV_N1R05_DAMPER"), null, San(1, "reason.read_correctly"), Trust(1)),
                    Choose(id, "phone_only", Half, null, null, Trust(-2)),
                    Choose(id, "log_anomaly", Bad, null, null, Set("N1R05_MISFILED"))
                };
                yield return c;
            }

            // ---- N1-R06 세탁실 배수 역류 (NORMAL) -------------------------------
            {
                const string id = "N1-R06";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None);
                c.mutexGroup = "b1_pump";
                c.objectives = new[] { Look("EV_N1R06_TRAP"), Look("EV_N1R06_SEPARATE", true) };
                c.decisions = new[]
                {
                    Choose(id, "clean", Good, Needs("EV_N1R06_TRAP"), null, Safety(3)),
                    Choose(id, "close", Half, null, null, Trust(-1)),
                    Choose(id, "ignore", Bad, null, null, Safety(-4), Set(R.N1R06Slippery))
                };
                yield return c;
            }

            // ---- N1-R07 3F 화재감지기 배터리 경보 (NORMAL) ----------------------
            {
                const string id = "N1-R07";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N1R07_SENSOR") };
                c.decisions = new[]
                {
                    Choose(id, "replace", Good, Needs("EV_N1R07_SENSOR"), null,
                        Pick(ChoiceIds.FireSensorStatus, "FIXED"), Safety(6)),
                    Choose(id, "mute", Half, null, null,
                        Pick(ChoiceIds.FireSensorStatus, "TEMP"), Owe(DebtIds.Safety, 1)),
                    Choose(id, "ignore", Bad, null, null,
                        Pick(ChoiceIds.FireSensorStatus, "IGNORED"), Safety(-8))
                };
                yield return c;
            }

            // ---- N1-R08 분실 택배 확인 요청 (NORMAL) ----------------------------
            {
                const string id = "N1-R08";
                var c = Sub(id, 1, QuestType.Normal, AnomalyFamily.None);
                c.objectives = new[] { Watch("CAM-02", true), Look("EV_N1R08_SHELF") };
                c.decisions = new[]
                {
                    Choose(id, "deliver", Good, Needs("EV_N1R08_SHELF"), null, Trust(4)),
                    Choose(id, "blame_courier", Half, null, null, Trust(-2)),
                    Choose(id, "leave", Bad, null, null, Trust(-5))
                };
                yield return c;
            }

            // ---- N1-R09 우편함 명의 불일치 (MIXED / RECORD) ---------------------
            {
                const string id = "N1-R09";
                var c = Sub(id, 1, QuestType.Mixed, AnomalyFamily.Record);
                c.objectives = new[] { Look("EV_N1R09_ENVELOPE"), Record("res_403") };
                c.decisions = new[]
                {
                    Choose(id, "keep", Good, Needs("EV_N1R09_ENVELOPE"), null, Archive(3), San(-2, "reason.old_name")),
                    Choose(id, "return_bin", Half, null, null),
                    Choose(id, "discard", Bad, null, null, Owe(DebtIds.Record, 1))
                };
                yield return c;
            }

            // ---- N1-R10 수도 사용량 304 단발 상승 (MIXED / RECORD) --------------
            {
                const string id = "N1-R10";
                var c = Sub(id, 1, QuestType.Mixed, AnomalyFamily.Record);
                c.mutexGroup = "unit_304";
                c.objectives = new[] { Look("EV_N1R10_LOG"), Look("EV_N1R10_METER", true) };
                c.decisions = new[]
                {
                    Choose(id, "field_check", Good, Needs("EV_N1R10_METER"), null, Set("N1R10_CHECKED")),
                    Choose(id, "remote_shutoff", Half, null, null, Trust(-2)),
                    Choose(id, "ignore", Bad, null, null, Set("N1R10_IGNORED"))
                };
                yield return c;
            }

            // ---- N1-R11 엘리베이터 4층 버튼 반복 입력 (MIXED / FUTURE) ----------
            {
                const string id = "N1-R11";
                var c = Sub(id, 1, QuestType.Mixed, AnomalyFamily.Future);
                c.objectives = new[]
                {
                    Look("EV_N1R11_LIFT_LOG"), Watch("CAM-08", true), Look("EV_N1R11_GHOST_PRESS", true)
                };
                c.decisions = new[]
                {
                    Choose(id, "maintenance_log", Good, Needs("EV_N1R11_LIFT_LOG"), null, Archive(2)),
                    Choose(id, "reboot", Half, null, null),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N1-R12 CCTV 타임스탬프 1분 역행 (ANOMALY / ECHO) ---------------
            {
                const string id = "N1-R12";
                var c = Sub(id, 1, QuestType.Anomaly, AnomalyFamily.Echo);
                c.mutexGroup = "cam_3f";
                c.objectives = new[] { Watch("CAM-06"), Look("EV_N1R12_CLOCK") };
                c.decisions = new[]
                {
                    Choose(id, "keep_clip", Good, Needs("EV_N1R12_CLOCK"), null,
                        Archive(4), Set("N1R12_CLIP_KEPT"), Give("EV_N1R12_LOOP_CLIP")),
                    Choose(id, "reboot", Half, null, null),
                    Choose(id, "keep_watching", Half, null, null,
                        Archive(2), Harin(2), San(-5, "reason.watched_the_loop"))
                };
                yield return c;
            }

            // ---- N1-R13 복귀한 플레이어의 CCTV 잔상 (ANOMALY / IDENTITY) --------
            {
                const string id = "N1-R13";
                var c = Sub(id, 1, QuestType.Anomaly, AnomalyFamily.Identity);
                c.mutexGroup = "cam_3f";
                c.objectives = new[] { Reach(ZoneIds.Floor03), Watch("CAM-06") };
                c.decisions = new[]
                {
                    Choose(id, "keep_clip", Good, null, null,
                        Archive(4), Harin(3), San(-5, "reason.saw_yourself"), Give("EV_N1R13_AFTERIMAGE")),
                    Choose(id, "cut_power", Half, null, null, Harin(3), San(-5, "reason.saw_yourself")),
                    Choose(id, "recheck", Half, null, null, Harin(3), San(-8, "reason.saw_yourself"))
                };
                yield return c;
            }

            // ---- N1-R14 폐기 스티커의 404 번호 (ANOMALY / RECORD) ---------------
            {
                const string id = "N1-R14";
                var c = Sub(id, 1, QuestType.Anomaly, AnomalyFamily.Record);
                c.objectives = new[] { Look("EV_N1R14_STICKER") };
                c.decisions = new[]
                {
                    Choose(id, "take_sticker", Good, Needs("EV_N1R14_STICKER"), null, Archive(5), Harin(2)),
                    Choose(id, "keep_furniture", Good, Needs("EV_N1R14_STICKER"), null,
                        Archive(5), Safety(-1), Give("EV_N1R14_FURNITURE")),
                    Choose(id, "discard", Bad, null, null, Owe(DebtIds.Record, 1))
                };
                yield return c;
            }
        }

        // =================================================================
        // Night 3 (v5.1 12) - the building's own rules start to slip
        // =================================================================

        static IEnumerable<CaseDefinition> Night3Subquests()
        {
            // ---- N3-R01 엘리베이터 정기 점검 (NORMAL) ---------------------------
            {
                const string id = "N3-R01";
                var c = Sub(id, 3, QuestType.Normal, AnomalyFamily.None);
                c.mutexGroup = "contractor";
                c.objectives = new[] { Look("EV_N3R01_WORK_ORDER"), Look("EV_N3R01_BADGE") };
                c.decisions = new[]
                {
                    Choose(id, "b1_only", Good, Needs("EV_N3R01_WORK_ORDER", "EV_N3R01_BADGE"), null, Safety(3)),
                    Choose(id, "full_pass", Bad, null, null, Owe(DebtIds.Access, 1)),
                    Choose(id, "refuse", Half, null, null, Safety(-2), Trust(-1))
                };
                yield return c;
            }

            // ---- N3-R02 완벽한 수리기사 (MIXED / RECORD) ------------------------
            {
                const string id = "N3-R02";
                var c = Sub(id, 3, QuestType.Mixed, AnomalyFamily.Record);
                c.mutexGroup = "contractor";
                c.objectives = new[] { Look("EV_N3R02_WORK_ORDER"), Look("EV_N3R02_SEAL") };
                c.decisions = new[]
                {
                    Choose(id, "restrict", Good, Needs("EV_N3R02_SEAL"), null,
                        Pick(ChoiceIds.ContractorAccess, "RESTRICTED"), Pick(ChoiceIds.ServiceRecorder, "SAFE")),
                    Choose(id, "escort", Good, Needs("EV_N3R02_SEAL"), null,
                        Pick(ChoiceIds.ContractorAccess, "ESCORTED"), Pick(ChoiceIds.ServiceRecorder, "SAFE"),
                        Alert(5), Give("EV_N3R02_CONTROL_LOG")),
                    Choose(id, "full_pass", Bad, null, null,
                        Pick(ChoiceIds.ContractorAccess, "FULL"), Pick(ChoiceIds.ServiceRecorder, "REMOVED"),
                        Owe(DebtIds.Access, 1)),
                    Choose(id, "reject", Half, null, null,
                        Pick(ChoiceIds.ServiceRecorder, "SAFE"), Safety(-3))
                };
                yield return c;
            }

            // ---- N3-R03 4F 방화문 도어클로저 불량 (NORMAL) ----------------------
            {
                const string id = "N3-R03";
                var c = Sub(id, 3, QuestType.Normal, AnomalyFamily.None);
                c.mutexGroup = "f4_fire_door";
                c.objectives = new[] { Look("EV_N3R03_CLOSER") };
                c.decisions = new[]
                {
                    Choose(id, "repair", Good, Needs("EV_N3R03_CLOSER"), When(ConditionDefinition.Flag(R.N3R03TensionSet)),
                        Safety(6), Set(R.FireDoorPrep)),
                    Choose(id, "temporary", Half, null, null, Safety(1), Owe(DebtIds.Safety, 1)),
                    Choose(id, "ignore", Bad, null, null, Safety(-8))
                };
                yield return c;
            }

            // ---- N3-R04 504 가구 끄는 소리 (MIXED / SPACE) ----------------------
            {
                const string id = "N3-R04";
                var c = Sub(id, 3, QuestType.Mixed, AnomalyFamily.Space);
                c.mutexGroup = "f5_corridor";
                c.objectives = new[] { Look("EV_N3R04_WALL_LENGTH") };
                c.decisions = new[]
                {
                    Choose(id, "measured", Good, Needs("EV_N3R04_WALL_LENGTH"), null, Archive(3), Set("N3R04_MEASURED")),
                    Choose(id, "knocked", Half, Needs("EV_N3R04_REPLY"), null, Harin(2)),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N3-R05 계단 비상등 교체 (NORMAL) -------------------------------
            {
                const string id = "N3-R05";
                var c = Sub(id, 3, QuestType.Normal, AnomalyFamily.None);
                c.mutexGroup = "stair_f4";
                c.objectives = new[] { Look("EV_N3R05_LAMP") };
                c.decisions = new[]
                {
                    Choose(id, "replace", Good, Needs("EV_N3R05_LAMP"), null, Safety(3), Set(R.StairLightFixed)),
                    Choose(id, "torch_mark", Half, null, null, Safety(1)),
                    Choose(id, "ignore", Bad, null, null, Set("STAIR_DARK"))
                };
                yield return c;
            }

            // ---- N3-R06 봉인번호 불일치 (MIXED / RECORD) ------------------------
            {
                const string id = "N3-R06";
                var c = Sub(id, 3, QuestType.Mixed, AnomalyFamily.Record);
                c.objectives = new[] { Look("EV_N3R06_SEAL"), Look("EV_N3R06_HANDOVER"), Watch("CAM-10", true) };
                c.decisions = new[]
                {
                    Choose(id, "isolate", Good, Needs("EV_N3R06_SEAL", "EV_N3R06_HANDOVER"), null, Archive(4)),
                    Choose(id, "break_seal", Half, null, null, Archive(2), Alert(3), Give("EV_N3R06_INNER")),
                    Choose(id, "edit_pc", Bad, null, null, Owe(DebtIds.Record, 1))
                };
                yield return c;
            }

            // ---- N3-R07 404 벽 노크 (ANOMALY / SPACE) ---------------------------
            {
                const string id = "N3-R07";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Space);
                c.mutexGroup = "f4_wall";
                c.objectives = new[] { Look("EV_N3R07_PLAN", true), Look("EV_N3R07_COUNT") };
                c.decisions = new[]
                {
                    Choose(id, "answer_same", Good, Needs("EV_N3R07_COUNT"), null, Harin(4), Set(R.WallResponse404)),
                    Choose(id, "random_knock", Bad, null, null,
                        San(-6, "reason.corridor_bent"), Owe(DebtIds.Distortion, 1)),
                    Choose(id, "ignore", Half, null, null, Set("N3R07_IGNORED"))
                };
                yield return c;
            }

            // ---- N3-R08 무한 5층 복도 (ANOMALY / SPACE) -------------------------
            {
                const string id = "N3-R08";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Space);
                c.mutexGroup = "f5_corridor";
                c.objectives = new[] { Look("EV_N3R08_EXTINGUISHER"), Look("EV_N3R08_WINDOW", true) };
                c.decisions = new[]
                {
                    Choose(id, "invariant", Good, Needs("EV_N3R08_EXTINGUISHER"), When(ConditionDefinition.Flag(R.N3R08Escaped)),
                        Set(FlagIds.SpaceRuleConfirmed), San(2, "reason.read_correctly")),
                    Choose(id, "followed_numbers", Bad, null, When(ConditionDefinition.Stat(R.N3R08Loops, 2)),
                        Owe(DebtIds.Distortion, 1)),
                    // Always on the report: forcing the loop is the answer nobody can be refused.
                    Choose(id, "ran_through", Bad, null, null, Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N3-R09 엘리베이터 선행 도착 4S (ANOMALY / FUTURE) ---------------
            {
                const string id = "N3-R09";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Future);
                c.objectives = new[] { Look("EV_N3R09_CONTROL_LOG"), Look("EV_N3R09_POSITION") };
                c.decisions = new[]
                {
                    Choose(id, "keep_log", Good, Needs("EV_N3R09_CONTROL_LOG", "EV_N3R09_POSITION"), null, Archive(2)),
                    Choose(id, "reboot", Half, null, null),
                    Choose(id, "ride_now", Half, null, null, San(-3, "reason.rode_into_4s"), Set("N3R09_EARLY"))
                };
                yield return c;
            }

            // ---- N3-R10 도면보다 긴 복도 (ANOMALY / SPACE) ----------------------
            {
                const string id = "N3-R10";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Space);
                c.mutexGroup = "f4_wall";
                c.objectives = new[] { Look("EV_N3R10_MEASURE") };
                c.decisions = new[]
                {
                    Choose(id, "place_marker", Good, Needs("EV_N3R10_MEASURE"), null, Set(R.F4ReferenceMarker)),
                    Choose(id, "keep_measuring", Half, null, null, San(-4, "reason.corridor_bent"), Archive(2)),
                    Choose(id, "ignore", Bad, null, null, Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N3-R11 방 번호 순환 (ANOMALY / SPACE) --------------------------
            {
                const string id = "N3-R11";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Space);
                c.mutexGroup = "f4_fire_door";
                c.objectives = new[] { Look("EV_N3R11_LANDING_PAINT"), Look("EV_N3R11_SEAL") };
                c.decisions = new[]
                {
                    Choose(id, "physical", Good, Needs("EV_N3R11_LANDING_PAINT", "EV_N3R11_SEAL"), null,
                        San(2, "reason.read_correctly")),
                    Choose(id, "trusted_plate", Bad, null, null,
                        San(-7, "reason.lost_in_the_building"), Owe(DebtIds.Distortion, 1)),
                    Choose(id, "reride", Half, null, null)
                };
                yield return c;
            }

            // ---- N3-R12 점검구 안의 생활용품 (FIXED_STORY, ending O) -------------
            {
                const string id = "N3-R12";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Record, story: true);
                c.objectives = new[] { Look("EV_N3R12_CHILD_ITEMS") };
                c.decisions = new[]
                {
                    Choose(id, "photo_keep", Good, Needs("EV_N3R12_CHILD_ITEMS"), null,
                        Archive(6), Harin(4), Set(FlagIds.EndChildItemsPreserved)),
                    Choose(id, "photo_only", Half, Needs("EV_N3R12_CHILD_ITEMS"), null,
                        Archive(3), Set(FlagIds.EndChildItemsPreserved)),
                    Choose(id, "discard", Bad, null, null,
                        Owe(DebtIds.Record, 1), Set(FlagIds.EndChildItemsPreserved, false))
                };
                yield return c;
            }

            // ---- N3-R13 내려갔는데 같은 층 (ANOMALY / SPACE) --------------------
            {
                const string id = "N3-R13";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Space);
                c.mutexGroup = "stair_f4";
                c.objectives = new[] { Look("EV_N3R13_LAMP_NO"), Look("EV_N3R13_SEAL_NO") };
                c.decisions = new[]
                {
                    Choose(id, "marker", Good, Needs("EV_N3R13_LAMP_NO", "EV_N3R13_SEAL_NO"), null,
                        San(2, "reason.read_correctly")),
                    Choose(id, "kept_going", Bad, null, When(ConditionDefinition.Stat(R.N3R13Loops, 1)),
                        Owe(DebtIds.Distortion, 1)),
                    Choose(id, "via_1f", Half, null, null)
                };
                yield return c;
            }

            // ---- N3-R14 서비스 통로에서 들리는 자신의 음성 (ANOMALY / FUTURE) ---
            {
                const string id = "N3-R14";
                var c = Sub(id, 3, QuestType.Anomaly, AnomalyFamily.Future);
                c.objectives = new[] { Look("EV_N3R14_FUTURE_VOICE") };
                c.decisions = new[]
                {
                    Choose(id, "save_recording", Good, Needs("EV_N3R14_FUTURE_VOICE"), null,
                        Archive(4), Set(R.FutureWarning404)),
                    Choose(id, "keep_closed", Good, null, When(ConditionDefinition.Flag(R.N3R14Opened, false))),
                    Choose(id, "opened", Bad, null, When(ConditionDefinition.Flag(R.N3R14Opened)))
                };
                yield return c;
            }
        }

        // =================================================================
        // Night 5 (v5.1 14) - the bills come due
        // =================================================================

        static IEnumerable<CaseDefinition> Night5Subquests()
        {
            // ---- N5-R01 전력 우선순위 배분 (CONSEQUENCE) ------------------------
            {
                const string id = "N5-R01";
                var c = Sub(id, 5, QuestType.Consequence, AnomalyFamily.None);
                c.mutexGroup = "power";
                // Every option is a reading of the load, so the deadline hands the reading over.
                c.failSafe = FailSafe(T0300, "EV_N5R01_LOAD", "quest.sub.failsafe");
                c.objectives = new[] { Look("EV_N5R01_LOAD"), Look("EV_N5R01_RESET", true) };
                c.decisions = new[]
                {
                    Choose(id, "cctv_elev_archive", Good, Needs("EV_N5R01_LOAD"), null, Archive(6), Safety(-4), Trust(-2)),
                    Choose(id, "lights_heat_elev", Good, Needs("EV_N5R01_LOAD"), null, Safety(6), Trust(4), Archive(-4)),
                    Choose(id, "cctv_lights_archive", Good, Needs("EV_N5R01_LOAD"), null, Archive(6), Safety(2))
                };
                yield return c;
            }

            // ---- N5-R02 선자 대피 협조 (CONSEQUENCE) ----------------------------
            {
                const string id = "N5-R02";
                var c = Sub(id, 5, QuestType.Consequence, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N5R02_STATE") };
                c.decisions = new[]
                {
                    Choose(id, "escort", Good, Needs("EV_N5R02_STATE"),
                        When(ConditionDefinition.Flag(R.N5SunjaClosed, false), ConditionDefinition.Stat(StatIds.BuildingSafety, 40)),
                        Trust(5)),
                    Choose(id, "escort_smoke", Good, Needs("EV_N5R02_STATE"),
                        When(ConditionDefinition.Flag(R.N5SunjaClosed, false), ConditionDefinition.StatAtMost(StatIds.BuildingSafety, 39)),
                        Trust(5), Hp(-6, "reason.smoke")),
                    Choose(id, "escort_closed", Half, Needs("EV_N5R02_STATE"), When(ConditionDefinition.Flag(R.N5SunjaClosed)),
                        Trust(2)),
                    Choose(id, "elevator", Good, null, When(ConditionDefinition.Flag(R.ElevatorShed, false)), Trust(3)),
                    Choose(id, "later", Bad, null, null, Trust(-6), Safety(-4))
                };
                yield return c;
            }

            // ---- N5-R03 펌프 과부하 재발 (CONSEQUENCE) --------------------------
            {
                const string id = "N5-R03";
                var c = Sub(id, 5, QuestType.Consequence, AnomalyFamily.None);
                c.mutexGroup = "power";
                c.objectives = new[] { Look("EV_N5R03_GAUGE"), Look("EV_N5R03_VALVE", true) };
                c.decisions = new[]
                {
                    Choose(id, "stop_repair", Good, Needs("EV_N5R03_GAUGE"), null,
                        Safety(8), Pick(ChoiceIds.PumpStatus, "REPAIRED")),
                    Choose(id, "bypass_again", Half, null, null,
                        Owe(DebtIds.Safety, 1), Pick(ChoiceIds.PumpStatus, "BYPASS")),
                    Choose(id, "leave", Bad, null, null, Safety(-12))
                };
                yield return c;
            }

            // ---- N5-R04 기록실 조기 침입 (CONSEQUENCE) --------------------------
            {
                const string id = "N5-R04";
                var c = Sub(id, 5, QuestType.Consequence, AnomalyFamily.None);
                c.mutexGroup = "archive_b1";
                c.objectives = new[] { Watch("CAM-10"), Look("EV_N5R04_SEAL"), Look("EV_N5R04_LOG", true) };
                c.decisions = new[]
                {
                    Choose(id, "remote_lock", Good, Needs("EV_N5R04_SEAL"), When(ConditionDefinition.Flag(R.N5ArchiveDoorIntact)),
                        Archive(4)),
                    Choose(id, "block", Good, Needs("EV_N5R04_SEAL"), null,
                        Hp(-5, "reason.debris"), San(-5, "reason.faced_the_silhouette"), Archive(8), Alert(5)),
                    Choose(id, "backup", Half, null, null, Archive(6), Alert(5)),
                    Choose(id, "evacuate", Half, null, null, Trust(4), Archive(-6), Owe(DebtIds.Record, 1))
                };
                yield return c;
            }

            // ---- N5-R05 분실 패키지의 4F 재등장 (CONSEQUENCE) -------------------
            {
                const string id = "N5-R05";
                var c = Sub(id, 5, QuestType.Consequence, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N5R05_PACKAGE") };
                c.decisions = new[]
                {
                    Choose(id, "recover", Good, Needs("EV_N5R05_PACKAGE"), When(ConditionDefinition.Flag(R.N5PackageReal)),
                        Owe(DebtIds.Access, -1), Archive(3), Pick(ChoiceIds.OldPackageStatus, "PRESERVED")),
                    Choose(id, "recover_box", Good, Needs("EV_N5R05_PACKAGE"), When(ConditionDefinition.Flag(R.N5PackageReal, false)),
                        Safety(2)),
                    Choose(id, "open_here", Half, Needs("EV_N5R05_PACKAGE"), null,
                        San(-4, "reason.opened_the_package"), Archive(2), Give("EV_N5R05_CONTENTS")),
                    Choose(id, "leave", Bad, null, null)
                };
                yield return c;
            }

            // ---- N5-R06 수리기사가 제거한 기록장치 (CONSEQUENCE) ----------------
            {
                const string id = "N5-R06";
                var c = Sub(id, 5, QuestType.Consequence, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N5R06_SLOT"), Look("EV_N5R06_2009_VOICE", true) };
                c.decisions = new[]
                {
                    Choose(id, "restore", Good, Needs("EV_N5R06_SLOT", "EV_N5R06_2009_VOICE"),
                        When(ConditionDefinition.Flag(R.N5RecorderSafe)), Archive(8)),
                    Choose(id, "restore_remnant", Good, Needs("EV_N5R06_SLOT"),
                        When(ConditionDefinition.Flag(R.N5RecorderGone)), Archive(5)),
                    Choose(id, "give_up", Half, null, null),
                    Choose(id, "ask_choi", Bad, null, null, Alert(5))
                };
                yield return c;
            }

            // ---- N5-R07 화재감지기 신뢰도 붕괴 (CONSEQUENCE) --------------------
            {
                const string id = "N5-R07";
                var c = Sub(id, 5, QuestType.Consequence, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N5R07_PANEL"), Look("EV_N5R07_TEMP", true) };
                c.decisions = new[]
                {
                    Choose(id, "physical", Good, Needs("EV_N5R07_TEMP"), null, Safety(4)),
                    Choose(id, "sensor_only", Half, null, When(ConditionDefinition.Flag(R.N5SensorTrue)), Safety(2)),
                    Choose(id, "sensor_misled", Bad, null, When(ConditionDefinition.Flag(R.N5SensorUnreliable)),
                        Hp(-6, "reason.smoke"), Safety(-4)),
                    Choose(id, "general_alarm", Half, null, null, Trust(3), Safety(-2))
                };
                yield return c;
            }

            // ---- N5-R08 모든 CCTV가 관리실을 비춤 (ANOMALY / SPACE) -------------
            {
                const string id = "N5-R08";
                var c = Sub(id, 5, QuestType.Anomaly, AnomalyFamily.Space);
                c.objectives = new[] { Watch("CAM-04"), Look("EV_N5R08_LED") };
                c.decisions = new[]
                {
                    Choose(id, "isolate", Good, Needs("EV_N5R08_LED"), null, San(-6, "reason.office_on_every_screen")),
                    Choose(id, "power_off", Half, null, null, Set("N5_CCTV_DOWN")),
                    Choose(id, "keep_watching", Bad, null, null,
                        San(-10, "reason.office_on_every_screen"), Harin(4), Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N5-R09 방화문 봉인 intact 모순 (ANOMALY / ECHO) ----------------
            {
                const string id = "N5-R09";
                var c = Sub(id, 5, QuestType.Anomaly, AnomalyFamily.Echo);
                c.mutexGroup = "f4_fire_door";
                c.objectives = new[] { Watch("CAM-04", true), Look("EV_N5R09_SEAL") };
                c.decisions = new[]
                {
                    Choose(id, "field_first", Good, Needs("EV_N5R09_SEAL"), null),
                    Choose(id, "chase_feed", Bad, null, null, Owe(DebtIds.Distortion, 1)),
                    Choose(id, "remove_seal", Half, null, null, Safety(-4))
                };
                yield return c;
            }

            // ---- N5-R10 5F Lost 재발 (ANOMALY / SPACE) --------------------------
            {
                const string id = "N5-R10";
                var c = Sub(id, 5, QuestType.Anomaly, AnomalyFamily.Space);
                c.objectives = new[] { Look("EV_N5R10_MARKER") };
                c.decisions = new[]
                {
                    Choose(id, "marker_exit", Good, Needs("EV_N5R10_MARKER"), When(ConditionDefinition.Flag(R.N5R10Looping, false)),
                        Owe(DebtIds.Distortion, -1), San(2, "reason.read_correctly")),
                    Choose(id, "random_doors", Bad, null, null, Owe(DebtIds.Distortion, 1))
                };
                yield return c;
            }

            // ---- N5-R11 주민 로비 혼잡 (MIXED) ----------------------------------
            {
                const string id = "N5-R11";
                var c = Sub(id, 5, QuestType.Mixed, AnomalyFamily.None);
                c.objectives = new[] { Look("EV_N5R11_STAIRS"), Look("EV_N5R11_RISK") };
                c.decisions = new[]
                {
                    Choose(id, "partial", Good, Needs("EV_N5R11_STAIRS", "EV_N5R11_RISK"), null, Safety(4)),
                    Choose(id, "everyone", Half, null, null, Hp(-3, "reason.crowd"), Trust(2)),
                    Choose(id, "dismiss", Bad, null, null, Trust(-8))
                };
                yield return c;
            }

            // ---- N5-R12 스프링클러 오작동 (MIXED) -------------------------------
            {
                const string id = "N5-R12";
                var c = Sub(id, 5, QuestType.Mixed, AnomalyFamily.None);
                c.mutexGroup = "archive_b1";
                c.objectives = new[] { Look("EV_N5R12_TEMP"), Look("EV_N5R12_VALVE") };
                c.decisions = new[]
                {
                    Choose(id, "power_then_valve", Good, Needs("EV_N5R12_VALVE"),
                        When(ConditionDefinition.Flag(R.N5SprinklerShocked, false)), Safety(4), Archive(2)),
                    Choose(id, "valve_only", Half, Needs("EV_N5R12_VALVE"),
                        When(ConditionDefinition.Flag(R.N5SprinklerShocked)), Safety(1)),
                    Choose(id, "ignore", Bad, null, null, Archive(-6), Safety(-6))
                };
                yield return c;
            }

            // ---- N5-R13 박동식 이름의 무전 (FIXED_STORY, ending O) ---------------
            {
                const string id = "N5-R13";
                var c = Sub(id, 5, QuestType.Anomaly, AnomalyFamily.Echo, story: true);
                c.objectives = new[]
                {
                    Look("EV_N5R13_PATTERN"), Look("EV_N5R13_POSITION", true), Look("EV_N5R13_KNOCK", true)
                };
                c.decisions = new[]
                {
                    Choose(id, "crosscheck", Good, Needs("EV_N5R13_PATTERN", "EV_N5R13_POSITION"),
                        When(ConditionDefinition.Flag(R.N5R13CrossChecked)),
                        Set(FlagIds.EndDongsikBroadcastVerified), Set(FlagIds.DongsikSignalFound), Harin(5)),
                    Choose(id, "chase_now", Half, null, null,
                        San(-6, "reason.last_rescue_echo"), Owe(DebtIds.Distortion, 1)),
                    Choose(id, "ignore", Bad, null, null, Set("N5R13_IGNORED"))
                };
                yield return c;
            }

            // ---- N5-R14 현재와 2009 복도 중첩 (ANOMALY / ECHO) ------------------
            {
                const string id = "N5-R14";
                var c = Sub(id, 5, QuestType.Anomaly, AnomalyFamily.Echo);
                c.mutexGroup = "f4_corridor";
                c.objectives = new[] { Look("EV_N5R14_OVERLAP") };
                c.decisions = new[]
                {
                    Choose(id, "physical", Good, Needs("EV_N5R14_OVERLAP"), When(ConditionDefinition.Flag(R.N5R14Collided, false))),
                    Choose(id, "followed_sight", Bad, null, When(ConditionDefinition.Flag(R.N5R14Collided)),
                        Owe(DebtIds.Distortion, 1)),
                    Choose(id, "retreat", Half, null, null)
                };
                yield return c;
            }
        }
    }
}
