namespace NO404.Core
{
    /// <summary>
    /// Stat ids visible to the player (GDD 7.1), hidden story ones (GDD 7.2), and the v2.1
    /// risk counters (spec 0.6 / 0.10.1).
    ///
    /// The risk counters ride the same clamped, saved, event-publishing dictionary as the
    /// story stats rather than living in their own service. They behave identically - bounded
    /// integers changed by consequences and read by conditions - and putting them anywhere
    /// else would mean a second save path and a second clamp rule to keep in step.
    /// </summary>
    public static class StatIds
    {
        public const string Performance = "Performance";           // 0..100, shown
        public const string CommunityTrust = "CommunityTrust";     // 0..100, shown
        public const string BuildingSafety = "BuildingSafety";     // 0..100, shown
        // v5.0 5.2 puts these three on the same 0..100 scale as the visible three and gives
        // ArchiveIntegrity a starting value of 50. They were 0..10 under v3.0, so every
        // threshold and increment that reads them was multiplied by ten in the same change -
        // the numbers mean what they always meant, on a scale ten times finer.
        public const string ArchiveIntegrity = "ArchiveIntegrity"; // 0..100, hidden
        public const string ChairmanAlert = "ChairmanAlert";       // 0..100, hidden
        public const string HarinResonance = "HarinResonance";     // 0..100, hidden

        // ---- v2.1 risk counters (spec 0.10.1). Never shown as a number (spec 0.10.3). ----

        /// <summary>Unambiguous manual rule breaches, cumulative across the playthrough.</summary>
        public const string ManualViolationCount = "ManualViolationCount"; // 0..99
        /// <summary>How much of the building's distortion the caretaker has taken on.</summary>
        public const string DistortionExposure = "DistortionExposure";     // 0..100
        /// <summary>Memory traded to the anomalous tools A01/A03 (spec 23).</summary>
        public const string MemoryDebt = "MemoryDebt";                     // 0..10
        /// <summary>Tools borrowed from A04 and not returned (spec 23).</summary>
        public const string ToolDebt = "ToolDebt";                         // 0..10

        // ---- what the routine tasks leave behind (spec 16, 32) --------------
        //
        // Spec 32 asks that at least eight routine wrong answers become a physical change on
        // a later night rather than a number in a summary. Most of them can say so through
        // BuildingSafety, which the building already reads. These two cannot: they are about
        // a specific door and a specific key, and nothing existed to remember them.

        /// <summary>T05: the entrance was left on AUTO and the lobby leaks strangers.</summary>
        public const string UnauthorizedEntryRisk = "UnauthorizedEntryRisk";  // 0..10
        /// <summary>T18: a key was reissued without killing the old one, and it still works.</summary>
        public const string ChairmanAccessEase = "ChairmanAccessEase";        // 0..10

        public static readonly string[] All =
        {
            Performance, CommunityTrust, BuildingSafety,
            ArchiveIntegrity, ChairmanAlert, HarinResonance,
            ManualViolationCount, DistortionExposure, MemoryDebt, ToolDebt,
            UnauthorizedEntryRisk, ChairmanAccessEase
        };

        public static readonly string[] Visible = { Performance, CommunityTrust, BuildingSafety };

        /// <summary>The four v2.1 risk counters, in the order spec 0.10.1 lists them.</summary>
        public static readonly string[] Risk =
        {
            ManualViolationCount, DistortionExposure, MemoryDebt, ToolDebt
        };

        /// <summary>
        /// Per-floor risk (spec 0.10.1). One stat per floor, 0..5, named FloorRisk_&lt;floorId&gt;
        /// so it saves and clamps like everything else.
        /// </summary>
        public const string FloorRiskPrefix = "FloorRisk_";
        public const int FloorRiskMax = 5;

        public static string FloorRisk(string floorId) { return FloorRiskPrefix + floorId; }

        public static bool IsFloorRisk(string statId)
        {
            return !string.IsNullOrEmpty(statId) && statId.StartsWith(FloorRiskPrefix);
        }

        public static int MaxOf(string statId)
        {
            if (IsFloorRisk(statId)) return FloorRiskMax;
            if (DebtIds.IsDebt(statId)) return DebtIds.Max;

            switch (statId)
            {
                case MemoryDebt:
                case ToolDebt:
                case UnauthorizedEntryRisk:
                case ChairmanAccessEase: return 10;
                case ManualViolationCount: return 99;
                default: return 100;
            }
        }
    }

    /// <summary>Story flags (GDD 7.2 plus the ones nights 2-6 and the endings need).</summary>
    /// <summary>
    /// The five debts a night can leave behind (v5.0 5.3).
    ///
    /// Stored as stats rather than as a structure of their own, because a stat is already a
    /// clamped integer that saves itself, publishes a change event and can be read by a case
    /// condition - which is the whole of what a debt is. MemoryDebt and ToolDebt have worked
    /// this way since v2.1; these five simply join them.
    /// </summary>
    public static class DebtIds
    {
        public const int Max = 5;

        /// <summary>Passes given wrongly, cards lost, visitors who left their route.</summary>
        public const string Access = "ACCESS_DEBT";
        /// <summary>Plant left on a temporary fix: bypassed pumps, jammed doors, leaks.</summary>
        public const string Safety = "SAFETY_DEBT";
        /// <summary>Evidence discarded, records damaged, a backup that never ran.</summary>
        public const string Record = "RECORD_DEBT";
        /// <summary>SPACE and ECHO rules broken often enough that the building keeps the score.</summary>
        public const string Distortion = "DISTORTION_DEBT";
        /// <summary>Residents and honest callers treated as though they were the problem.</summary>
        public const string Trust = "TRUST_DEBT";

        public static readonly string[] All = { Access, Safety, Record, Distortion, Trust };

        public static bool IsDebt(string statId)
        {
            for (int i = 0; i < All.Length; i++) if (All[i] == statId) return true;
            return false;
        }
    }

    /// <summary>
    /// The choices v5.0 5.4 records as a value rather than as a yes/no.
    ///
    /// SUNJA_CARE is not a flag that is set or unset; it is GOOD, DELAYED or DENIED, and a
    /// later night wants to know which. Writing three booleans per choice and hoping exactly
    /// one of them is ever true is how a save ends up saying a resident was both cared for
    /// and refused, so these are stored as one id with one value (GameStateService.SetChoice).
    /// </summary>
    public static class ChoiceIds
    {
        public const string SunjaCare = "SUNJA_CARE";                   // GOOD / DELAYED / DENIED
        public const string PumpStatus = "PUMP_STATUS";                 // REPAIRED / BYPASS / IGNORED
        public const string FireSensorStatus = "FIRE_SENSOR_STATUS";    // FIXED / TEMP / IGNORED
        public const string JunhoStatus = "JUNHO_STATUS";               // TRUSTED / NEUTRAL / REJECTED
        public const string OldPackageStatus = "OLD_PACKAGE_STATUS";    // PRESERVED / OPENED / RETURNED / LOST
        public const string ContractorAccess = "CONTRACTOR_ACCESS";     // RESTRICTED / ESCORTED / FULL
        public const string ServiceRecorder = "SERVICE_RECORDER_STATUS";// SAFE / REMOVED / DAMAGED
        public const string Db404Action = "DB404_ACTION";               // PRINT / EXPORT / KEEP / DELETE
        public const string SunjaTestimony = "SUNJA_TESTIMONY";         // FULL / PARTIAL / NONE
        public const string FireDoorStatus = "FIRE_DOOR_STATUS";        // SAFE / JAMMED / OPEN

        public static readonly string[] All =
        {
            SunjaCare, PumpStatus, FireSensorStatus, JunhoStatus, OldPackageStatus,
            ContractorAccess, ServiceRecorder, Db404Action, SunjaTestimony, FireDoorStatus
        };
    }

    public static class FlagIds
    {
        // ---- v5.0 5.4: the yes/no half of the world state ----------------------

        /// <summary>N1-M01: the 2009-10 bill for a unit that does not exist was kept.</summary>
        public const string BillPreserved404 = "N1_404_BILL_PRESERVED";
        /// <summary>N2-M01: two independent invariants agreed, so the ECHO rule is known.</summary>
        public const string EchoRuleConfirmed = "ECHO_RULE_CONFIRMED";
        /// <summary>N3-M01: the seal, the stairs and the analogue gauge were checked first.</summary>
        public const string SpaceRuleConfirmed = "SPACE_RULE_CONFIRMED";
        /// <summary>N4-M01: the 404 household record survived the chairman's instruction.</summary>
        public const string HarinRecordPreserved = "HARIN_RECORD_PRESERVED";

        // ---- v5.1 5.4: ending presentation. Only fixed story quests may write these. ----

        /// <summary>N1-R01: Seon-ja's care went ahead (escort or floor pass).</summary>
        public const string EndSunjaTrusted = "END_SUNJA_TRUSTED";
        /// <summary>N3-R12: the child's cup and blanket survived, as objects or as photographs.</summary>
        public const string EndChildItemsPreserved = "END_CHILD_ITEMS_PRESERVED";
        /// <summary>N5-R13: the last broadcast was matched to a place and a second witness.</summary>
        public const string EndDongsikBroadcastVerified = "END_DONGSIK_LAST_BROADCAST_VERIFIED";
        //
        // Two of v5.0 5.4's flags were already here under other names and mean exactly what
        // v5.0 means by them, so they are not written twice: DONGSIK_SIGNAL_FOUND is
        // DongsikSignalFound below, and ORIGINAL_LEDGER_SECURED is LedgerSecured. Adding a
        // second constant for either would give the same fact two save keys and let a night
        // read the one nobody wrote.


        // GDD 7.2
        public const string SunjaHealthy = "SunjaHealthy";
        public const string MinseoTrusted = "MinseoTrusted";
        public const string JiwooTrusted = "JiwooTrusted";
        public const string TaehoCooperates = "TaehoCooperates";
        public const string DongsikFound = "DongsikFound";
        public const string ArchiveCopied = "ArchiveCopied";
        public const string FireDoorUnlocked = "FireDoorUnlocked";
        /// <summary>Night 0: the child has appeared on CAM-03, so the slipper is by the door.</summary>
        public const string SlipperDropped = "SlipperDropped";

        // Progression
        public const string PrologueDone = "PrologueDone";
        public const string Knows404 = "Knows404";
        public const string KnowsPastFootage = "KnowsPastFootage";   // night 2: the lobby feed is 2009
        public const string Floor16Found = "Floor16Found";           // night 3: the maintenance level
        public const string HasBlueprint = "HasBlueprint";           // night 3: Taeho hands over the plan
        public const string ElevatorLogDeleted = "ElevatorLogDeleted";
        public const string Db404Deleted = "Db404Deleted";           // night 4
        public const string Db404Exported = "Db404Exported";
        public const string SunjaTestimony = "SunjaTestimony";
        public const string ArchiveRoomEntered = "ArchiveRoomEntered";
        public const string DongsikLocated = "DongsikLocated";       // night 5: alive behind the wall
        public const string DongsikRescued = "DongsikRescued";       // night 6
        public const string FireDepartmentCalled = "FireDepartmentCalled";
        public const string FireEscaped = "FireEscaped";

        // Night 6 outcomes
        public const string EvacuationOrdered = "EvacuationOrdered";
        public const string EvacuationSuccess = "EvacuationSuccess";
        public const string EvacuationFailed = "EvacuationFailed";
        public const string LedgerSecured = "LedgerSecured";         // the original ledger from 404
        public const string LedgerLost = "LedgerLost";
        public const string RecordsBurned = "RecordsBurned";
        public const string ChairmanDeal = "ChairmanDeal";
        public const string ChairmanTrapped = "ChairmanTrapped";

        // Final submission (GDD 9.7 step 6) - exactly one is set
        public const string SubmittedToPolice = "SubmittedToPolice";
        public const string SubmittedToMedia = "SubmittedToMedia";
        public const string SubmittedToResidents = "SubmittedToResidents";
        public const string SubmittedToUnion = "SubmittedToUnion";
        public const string SubmissionDeleted = "SubmissionDeleted";

        /// <summary>True when the evidence went outside the building's own chain of command.</summary>
        public const string EvidencePublished = "EvidencePublished";

        // ---- v2.1 (spec 0.6) --------------------------------------------------
        //
        // Spec 0.6 lists sixteen flags. Twelve already existed above under the same or an
        // equivalent name; these four are the ones v2.1 adds, plus the manual-event flags the
        // spec 30.3 persistence chains need.

        /// <summary>C03: the real Junho was let in, so night 5 can compare delivery timestamps.</summary>
        public const string JunhoTrusted = "JunhoTrusted";
        /// <summary>C05 / M01: the maintenance plan survived the chairman's deletion order.</summary>
        public const string MaintenanceMapOwned = "MaintenanceMapOwned";
        /// <summary>The 2009 fire cassette recovered on night 2.</summary>
        public const string OldFireTapeOwned = "OldFireTapeOwned";
        /// <summary>Night 6 C13 met the strong-report bar in spec 7.3.</summary>
        public const string FinalReportStrong = "FinalReportStrong";
        /// <summary>The original ledger was carried out of 404 rather than copied or burned.</summary>
        public const string OriginalLedgerOwned = "OriginalLedgerOwned";
        public const string OriginalLedgerUploaded = "OriginalLedgerUploaded";
        /// <summary>Night 5: Dongsik's tapping was heard and located behind the 4F wall.</summary>
        public const string DongsikSignalFound = "DongsikSignalFound";

        // Manual-event persistence chains (spec 30.3).

        /// <summary>M03: tomorrow's patrol log was acted on, so Ji-woo is not on the roof.</summary>
        public const string JiwooExposurePrevented = "JiwooExposurePrevented";
        /// <summary>M09: the pump was force-stopped, so the building has no water.</summary>
        public const string WaterOutage = "WaterOutage";
        /// <summary>T01 left a car in the B1 fire lane; night 6 evacuation has to route around it.</summary>
        public const string FireLaneBlocked = "FireLaneBlocked";
        /// <summary>M08: the courtyard route out of the building is safe on night 6.</summary>
        public const string CourtyardEscapeSafe = "CourtyardEscapeSafe";
        /// <summary>DistortionExposure hit 100 and the correction procedure is outstanding (spec 0.10.4).</summary>
        public const string EmergencyCorrectionDue = "EmergencyCorrectionDue";
        /// <summary>M11: the bay that reads occupied while standing empty was dealt with.</summary>
        public const string GhostCarResolved = "GhostCarResolved";
        /// <summary>M13: a postcard was read and its claims logged as claims (spec 22 M13).</summary>
        public const string OptionalClueUnlocked = "OptionalClueUnlocked";

        // Routine tasks whose wrong answer takes someone out of the last night (spec 16).
        //
        // Both are read together with the trust flag they cancel, which is how spec 16 states
        // them: T10 only matters if Sun-ja was well enough to walk out unaided in the first
        // place, and T11 only matters if Ji-woo was going to help at all.

        /// <summary>T10: the heating was never restored and Sun-ja cannot move on her own.</summary>
        public const string SunjaCritical = "SunjaCritical";
        /// <summary>T11: the roof door was left to itself, and Ji-woo will not be knocking.</summary>
        public const string JiwooRisk = "JiwooRisk";
        /// <summary>T03: the valve was never shut and the fifth floor is still wet.</summary>
        public const string LeakUnrepaired = "LeakUnrepaired";
        /// <summary>One of the A01..A05 machines has been traded with (spec 23 A02 balance).</summary>
        public const string AnomalyToolUsed = "AnomalyToolUsed";
        /// <summary>
        /// A01 has been asked where a tool that vanished went, so A04 will take it back.
        ///
        /// Spec 23 A04 makes the recovery two steps on purpose - find out, then go and get it -
        /// and this is the first half remembering it happened.
        /// </summary>
        public const string ToolRecoveryKnown = "ToolRecoveryKnown";
    }

    /// <summary>
    /// Zone ids. One id per playable space in the compressed building (v2.1 spec 0.7.1).
    ///
    /// The floor a zone belongs to is not encoded here - Gameplay.FloorPlan owns that mapping,
    /// because the moment a zone id implies its own floor, movement code starts parsing names.
    /// </summary>
    public static class ZoneIds
    {
        // ---- B2: the plant level (spec 25 "가장 위험한 설비층") -------------
        public const string Machinery = "Machinery";         // electrical / generator room
        public const string PumpRoom = "PumpRoom";           // 급수 펌프실 (M09)
        public const string PipeRoom = "PipeRoom";           // 배관실 (M10)
        public const string Toolroom = "Toolroom";           // 정비 공구실 (A04)
        public const string Archive = "Archive";             // 기록실 (C11)

        // ---- B1: movement and surveillance ---------------------------------
        public const string Parking = "Parking";
        public const string RecyclingYard = "RecyclingYard"; // 분리수거장 (M05)

        // ---- 1F: the hub that feels safe -----------------------------------
        public const string Office = "Office";
        public const string Lobby = "Lobby";                 // 로비 / 공동현관 / 우편함 / 택배함
        public const string Laundry = "Laundry";             // 코인세탁실 (M12)
        public const string ConvenienceStore = "ConvenienceStore"; // 편의점 (A01)
        public const string Playground = "Playground";       // 작은 놀이터 (M08)

        // ---- 2F: shared resident facilities --------------------------------
        public const string Floor02 = "Floor02";
        public const string Lounge = "Lounge";               // 주민 휴게실 / VCR (A03)
        public const string FitnessRoom = "FitnessRoom";     // 피트니스룸 (M14)
        public const string Terrace = "Terrace";             // 옥외 테라스 (M15)

        // ---- 3F..6F --------------------------------------------------------
        public const string Floor03 = "Floor03";             // 303 선자 / 304 공실 / 305 신고자
        public const string Floor04 = "Floor04";             // 401-403, [빈 벽], 405-406
        public const string ServicePassage = "ServicePassage"; // 4F hidden service corridor (C05)
        public const string Unit404 = "Unit404";
        public const string Floor05 = "Floor05";             // 504 흔적 (M17)
        public const string Floor06 = "Floor06";             // 602 최지우
        public const string Rooftop = "Rooftop";

        // ---- vertical circulation. Belongs to no floor. --------------------
        public const string Elevator = "Elevator";
        public const string Stairwell = "Stairwell";

        /// <summary>
        /// The thirteenth floor (C05 / M01).
        ///
        /// Named for what it is rather than as "Floor13", because it is not one: spec 0.7.1
        /// puts no thirteenth storey in the building, and FloorPlan has no row for it. The
        /// player can be standing in this zone, but only an anomaly can put them there.
        /// </summary>
        public const string PhantomFloor13 = "PhantomFloor13";

        public static readonly string[] All =
        {
            Machinery, PumpRoom, PipeRoom, Toolroom, Archive,
            Parking, RecyclingYard,
            Office, Lobby, Laundry, ConvenienceStore, Playground,
            Floor02, Lounge, FitnessRoom, Terrace,
            Floor03, Floor04, ServicePassage, Unit404, Floor05, Floor06,
            Elevator, Stairwell
        };
    }

    /// <summary>
    /// The eighteen night-anomaly response cases M01..M18 (v2.1 spec 22) and the five
    /// anomalous tools A01..A05 (spec 23).
    ///
    /// Ids are the spec's own, so a designer reading section 22 and a programmer reading a
    /// stack trace are looking at the same name.
    /// </summary>
    public static class ManualEventIds
    {
        public const string M01_PhantomFloor = "M01";      // 존재하지 않는 13층
        public const string M02_BackwardsWalker = "M02";   // 반대로 걷는 주차장 주민
        public const string M03_TomorrowsLog = "M03";      // 폐가전의 내일 순찰일지
        public const string M04_CeilingStain = "M04";      // 404호 천장 오염 민원
        public const string M05_NotRecyclable = "M05";     // 분리수거장의 '재활용불가'
        public const string M06_LostParcel = "M06";        // 택배 분실 소동
        public const string M07_CorridorLoop = "M07";      // 5층 복도 무한 루프
        public const string M08_ShadowChildren = "M08";    // 놀이터의 그림자 아이들
        public const string M09_BlackWater = "M09";        // B2 급수 펌프실: 검은 물
        public const string M10_PipeGrowth = "M10";        // B2 배관실: 벽면의 배관 확장
        public const string M11_GhostParking = "M11";      // B1 잔상 주차
        public const string M12_NightLaundry = "M12";      // 1F 코인세탁실: 밤샘 세탁물
        public const string M13_Postcards = "M13";         // 1F 우편함: 수신인 불명의 엽서
        public const string M14_Treadmills = "M14";        // 2F 트레드밀 무인 가동
        public const string M15_PlanterGrowth = "M15";     // 2F 화단 이상 증식
        public const string M16_KnockEcho = "M16";         // 4F 404 벽: 노크 소리의 반사
        public const string M17_Unit504Noise = "M17";      // 5F 504호 흔적
        public const string M18_RoofFigure = "M18";        // 옥상: 투신 예고의 환영

        public static readonly string[] All =
        {
            M01_PhantomFloor, M02_BackwardsWalker, M03_TomorrowsLog, M04_CeilingStain,
            M05_NotRecyclable, M06_LostParcel, M07_CorridorLoop, M08_ShadowChildren,
            M09_BlackWater, M10_PipeGrowth, M11_GhostParking, M12_NightLaundry,
            M13_Postcards, M14_Treadmills, M15_PlanterGrowth, M16_KnockEcho,
            M17_Unit504Noise, M18_RoofFigure
        };

        // Spec 23: anomalous tools. None of them is on the path to any ending (spec 32).
        public const string A01_ReceiptPrinter = "A01";
        public const string A02_WishParcelLocker = "A02";
        public const string A03_LoungeVcr = "A03";
        public const string A04_EndlessToolbox = "A04";
        public const string A05_LostAndFoundMachine = "A05";

        public static readonly string[] Tools =
        {
            A01_ReceiptPrinter, A02_WishParcelLocker, A03_LoungeVcr,
            A04_EndlessToolbox, A05_LostAndFoundMachine
        };

        /// <summary>Manual page id for an event, in the spec 26 "MANUAL_M16" form.</summary>
        public static string PageOf(string eventId) { return "MANUAL_" + eventId; }
    }

    /// <summary>
    /// Objects the caretaker can be carrying (v2.1 spec 23 A02 / A04 / A05).
    ///
    /// Held as flags rather than as an inventory, because that is all any of them ever needs
    /// to be: nothing in the game stacks, combines, weighs or drops an object, and the one
    /// question ever asked of one is whether the caretaker has it. An inventory system would
    /// be a second place for the same boolean to live and a second place for it to disagree
    /// with the save.
    ///
    /// The ids are namespaced under item. so that a flag standing for a thing in a hand is
    /// never mistaken for a flag standing for something that happened.
    /// </summary>
    public static class ItemIds
    {
        // A02 - the locker with no number (spec 23 A02).
        public const string PipeRoomKey = "item.pipe_room_key";
        public const string OfficeRemote = "item.office_remote";
        public const string HazardSeals = "item.hazard_seals";
        public const string Neutraliser = "item.neutraliser";
        public const string RedBall = "item.red_ball";

        // A04 - the toolbox that never runs out (spec 23 A04). Every one of these is on loan.
        public const string Stethoscope = "item.stethoscope";
        public const string PruningShears = "item.pruning_shears";
        public const string StickerRoller = "item.sticker_roller";
        public const string InsulatedRod = "item.insulated_rod";
        public const string WallCamera = "item.wall_camera";
        public const string CutterHead = "item.cutter_head";

        // A03 / A05 - what the roof machine gives back, and the tape that starts the VCR.
        public const string BlackTape = "item.vhs_black_01";
        /// <summary>
        /// Spec 23 A05: what the lost-property machine takes instead of blood.
        ///
        /// The spec offers a collected token or a voucher and rules out asking the player to
        /// hurt themselves; this is the token. Two of them exist in the building, so the
        /// machine is a choice about what to take rather than a shelf to empty.
        /// </summary>
        public const string InsectToken = "item.insect_token";
        public const string BrassKeyCopy = "item.brass_key_copy";
        public const string SchoolEraser = "item.school_eraser";
        public const string DongsikLighter = "item.dongsik_lighter";
        /// <summary>Spec 23 A05: a family photograph of people the caretaker cannot place.</summary>
        public const string UnknownPhoto = "item.unknown_photo";

        public static readonly string[] All =
        {
            PipeRoomKey, OfficeRemote, HazardSeals, Neutraliser, RedBall,
            Stethoscope, PruningShears, StickerRoller, InsulatedRod, WallCamera, CutterHead,
            BlackTape, InsectToken, BrassKeyCopy, SchoolEraser, DongsikLighter, UnknownPhoto
        };

        /// <summary>The A04 loans. Every one of these is what ToolDebt is a debt of.</summary>
        public static readonly string[] Loans =
        {
            Stethoscope, PruningShears, StickerRoller, InsulatedRod, WallCamera, CutterHead
        };
    }

    /// <summary>
    /// Systems that compete for the limited power budget on night 5 (GDD 9.6).
    /// At most three may stay on at once.
    /// </summary>
    public static class CircuitIds
    {
        public const string Elevator = "circuit.elevator";
        public const string CorridorLights = "circuit.corridor_lights";
        public const string Cctv = "circuit.cctv";
        public const string Heating = "circuit.heating";
        public const string RecordServer = "circuit.record_server";

        public static readonly string[] All =
        {
            Elevator, CorridorLights, Cctv, Heating, RecordServer
        };

        public const int MaxSimultaneous = 3;
    }

    /// <summary>Ending ids (GDD 10).</summary>
    /// <summary>
    /// The four grades and the failure result (v5.0 8.3).
    ///
    /// v5.0 replaced five condition-matched endings with four graded on the caretaker's own
    /// condition. The ids are kept - the gallery lives in the settings file and survives a
    /// deleted playthrough, so renaming them would silently empty somebody's collection - but
    /// what each one now means comes from HP and SAN rather than from a list of flags.
    ///
    /// NoEnding is not a fifth grade. It is having pushed the campaign to its last scene in a
    /// state that cannot carry it (v5.0 8.1), and it is deliberately not in the gallery.
    /// </summary>
    public static class EndingIds
    {
        /// <summary>A - 기록된 404. Saw the truth and did not lose themselves to it.</summary>
        public const string A_RecordedPeople = "ENDING_A";
        /// <summary>B - 살아남은 증언. Marked, but the testimony gets out.</summary>
        public const string B_SafeSilence = "ENDING_B";
        /// <summary>C - 404의 생존자. The truth survives; the person carrying it barely does.</summary>
        public const string C_Erased404 = "ENDING_C";
        /// <summary>D - 기억의 대가. Out of the building with fragments.</summary>
        public const string D_CommunityCollapse = "ENDING_D";
        /// <summary>Neither a grade nor a gallery entry: the campaign was not survivable.</summary>
        public const string NoEnding = "ENDING_NONE";

        /// <summary>The four gradeable endings, best first.</summary>
        public static readonly string[] All =
        {
            A_RecordedPeople, B_SafeSilence, C_Erased404, D_CommunityCollapse
        };
    }

    /// <summary>Facility OS applications (GDD 16.6).</summary>
    public static class AppIds
    {
        public const string Home = "home";
        public const string Cctv = "cctv";
        public const string Phone = "phone";
        public const string Residents = "residents";
        public const string Access = "access";
        public const string Facility = "facility";
        public const string Evidence = "evidence";
        public const string Settings = "settings";

        public static readonly string[] Order =
        {
            Home, Cctv, Phone, Residents, Access, Facility, Evidence, Settings
        };
    }

    /// <summary>
    /// Render layers. GDD 4.4: an anomaly is only ever real on the screen - walking to the
    /// spot has to find nothing there. Staged anomaly props live on their own layer, which
    /// the CCTV cameras render and the player camera culls.
    /// </summary>
    public static class Layers
    {
        public const string CctvOnlyName = "CctvOnly";

        static int _cctvOnly = -2;   // -2 = not looked up yet, -1 = the layer is missing

        /// <summary>The CCTV-only layer index, or -1 when the project has no such layer.</summary>
        public static int CctvOnly
        {
            get
            {
                if (_cctvOnly == -2) _cctvOnly = UnityEngine.LayerMask.NameToLayer(CctvOnlyName);
                return _cctvOnly;
            }
        }

        /// <summary>Culling mask with the CCTV-only layer removed. Safe if the layer is missing.</summary>
        public static int WithoutCctvOnly(int cullingMask)
        {
            return CctvOnly < 0 ? cullingMask : cullingMask & ~(1 << CctvOnly);
        }
    }

    /// <summary>Access levels for doors (GDD 15.3).</summary>
    public enum AccessLevel
    {
        None = 0,
        Staff1 = 1,
        Staff2 = 2,
        Maintenance = 3,
        Archive = 4,
        Key404 = 5
    }

    /// <summary>Steam achievement ids (GDD 20.19).</summary>
    public static class AchievementIds
    {
        public const string FirstShift = "ACH_FIRST_SHIFT";
        public const string GoodCaretaker = "ACH_GOOD_CARETAKER";
        public const string NoUnit404 = "ACH_NO_UNIT_404";
        public const string TwoCouriers = "ACH_TWO_COURIERS";
        public const string Floor16 = "ACH_FLOOR_16";
        public const string Witness = "ACH_WITNESS";
        public const string Backup = "ACH_BACKUP";
        public const string PowerManager = "ACH_POWER_MANAGER";
        public const string FoundDongsik = "ACH_FOUND_DONGSIK";
        public const string TrueRecord = "ACH_TRUE_RECORD";
        public const string Silence = "ACH_SILENCE";
        public const string Erased = "ACH_ERASED";
        public const string Collapse = "ACH_COLLAPSE";
        public const string Loop = "ACH_LOOP";

        // GDD 20.19 lists 14 achievements as examples against a P0 target of 24; the
        // remaining ten below round the catalogue out to that count.
        public const string FlawlessNight = "ACH_FLAWLESS_NIGHT";     // every case that night resolved Correct
        public const string Unseen = "ACH_UNSEEN";                    // night 5 ends with zero times caught
        public const string AllEvidenceTypes = "ACH_ALL_TYPES";       // one of each of the 8 evidence types
        public const string EveryEye = "ACH_EVERY_EYE";               // no unreviewed CCTV motion left at night's end
        public const string LinkedTruth = "ACH_LINKED_TRUTH";         // first evidence-board link made
        public const string PerfectLine = "ACH_PERFECT_LINE";         // reached an ending without ever missing a call
        public const string TrustedCaretaker = "ACH_TRUSTED_CARETAKER"; // CommunityTrust hits 100
        public const string SafeHouse = "ACH_SAFE_HOUSE";             // BuildingSafety hits 100
        public const string Completionist = "ACH_COMPLETIONIST";      // every ending unlocked in the gallery
        public const string NightChief = "ACH_NIGHT_CHIEF";           // reached an ending on 야간 책임자 difficulty

        /// <summary>
        /// 자력 해결 - reached an ending without ever trading with A01..A05 (spec 23 A02).
        ///
        /// The only thing the anomalous tools take away. Spec 23 is careful that they must not
        /// close an ending off, so what leaning on them costs is the claim to have got through
        /// the building unaided, and nothing further.
        /// </summary>
        public const string Unaided = "ACH_UNAIDED";

        public static readonly string[] All =
        {
            FirstShift, GoodCaretaker, NoUnit404, TwoCouriers, Floor16, Witness, Backup,
            PowerManager, FoundDongsik, TrueRecord, Silence, Erased, Collapse, Loop,
            FlawlessNight, Unseen, AllEvidenceTypes, EveryEye, LinkedTruth, PerfectLine,
            TrustedCaretaker, SafeHouse, Completionist, NightChief, Unaided
        };
    }
}
