using System;
using System.Collections.Generic;

namespace NO404.Save
{
    public enum SaveReason
    {
        NightStart = 0,
        EvidenceAcquired = 1,
        MajorChoice = 2,
        ZoneTransition = 3,
        NightEnd = 4,
        Manual = 5,
        Quit = 6
    }

    // JsonUtility cannot serialize dictionaries, so every map is stored as a list of pairs.

    [Serializable] public sealed class StatSaveEntry { public string key; public int value; }
    [Serializable] public sealed class FlagSaveEntry { public string key; public bool value; }

    [Serializable]
    public sealed class CaseSaveEntry
    {
        public string caseId;
        public int state;
        public string decisionId;
        public int startedAt;
        public int resolvedAt;
        public bool failSafeFired;
        public List<string> completedObjectives = new List<string>();
        public List<string> attachedEvidence = new List<string>();
    }

    [Serializable]
    public sealed class EvidenceSaveEntry
    {
        public string evidenceId;
        public int source;
        public int acquiredAt;
        public float boardX;
        public float boardY;
        public bool placed;
    }

    [Serializable]
    public sealed class EvidenceLinkSaveEntry
    {
        public string a;
        public string b;
        public int relation;
    }

    [Serializable]
    public sealed class DoorSaveEntry
    {
        public string doorId;
        public int state;
        public bool locked;
    }

    [Serializable]
    public sealed class ResidentSaveEntry
    {
        public string residentId;
        public int status;
    }

    [Serializable]
    public sealed class VisitorSaveEntry
    {
        public string visitorId;
        [UnityEngine.Tooltip("VisitorAccessLevel as an int. Was VisitorDecision before schema 10; " +
                             "SaveMigrations.MigrateV9ToV10 is what re-reads the old numbers.")]
        public int decision;
    }

    [Serializable]
    public sealed class SnapshotSaveEntry
    {
        public string snapshotId;
        public string cameraId;
        public int gameSecond;
        public string anomalyId;
        public string attachedCaseId;
    }

    [Serializable]
    public sealed class AccessLogSaveEntry
    {
        public int gameSecond;
        public int subject;
        public string nameKey;
        public string cardId;
        public string cameraId;
        public string locationKey;
        public bool inbound;
        public string contradictionGroup;
    }

    [Serializable]
    public sealed class CircuitSaveEntry
    {
        public string circuitId;
        public bool on;
    }

    [Serializable]
    public sealed class PlayerSaveEntry
    {
        public string zoneId;
        public float posX, posY, posZ;
        public float yaw, pitch;
        public bool flashlightOn;
    }

    /// <summary>
    /// One M event's progress (v2.1 spec 22).
    ///
    /// Counters are two parallel lists rather than a list of pairs for the same reason every
    /// other map here is a list: JsonUtility will not serialize a dictionary, and a pair type
    /// per counter bag is a class nobody would ever read.
    /// </summary>
    [Serializable]
    public sealed class ManualEventSaveEntry
    {
        public string eventId;
        public int state;
        public int startedAt;
        public int resolvedAt;
        public int wrongAttempts;
        public bool failSafeFired;
        public List<string> completedObjectives = new List<string>();
        public List<string> counterKeys = new List<string>();
        public List<int> counterValues = new List<int>();
    }

    /// <summary>
    /// One machine's standing state (v2.1 spec 23).
    ///
    /// A tool that is awake has to stay awake across a load, and a hold has to come back with
    /// the same deadline: spec 30.3 asks that a wrong turn keeps costing after a reload, and
    /// a locker door that closed itself while the game was off would be the plainest possible
    /// way to break that promise.
    /// </summary>
    [Serializable]
    public sealed class AnomalyToolSaveEntry
    {
        public string toolId;
        public bool awake;
        public int useCount;
        public bool holdOpen;
        public bool holdExpired;
        public int holdEndsAt;
        public string heldFlagId;
        public int warningsFired;
    }

    /// <summary>
    /// Where the player is in the stairwell (spec 0.8.2).
    ///
    /// Spec 30.1 requires that a save taken mid-climb reload onto the same landing having
    /// entered from the same floor. Storing only the zone would put them back in a generic
    /// shaft, which is exactly the teleport-to-lobby behaviour spec 0.8.1 bans.
    /// </summary>
    [Serializable]
    public sealed class StairSaveEntry
    {
        public string entryFloor;
        public string currentLanding;
        public int directionHint = 1;
        public int variant;
        public bool inStairwell;
        public int flightsWalked;
    }

    /// <summary>One choice that had a value rather than a yes/no (v5.0 5.4).</summary>
    [Serializable]
    public sealed class ChoiceSaveEntry
    {
        public string key;
        public string value;
    }

    /// <summary>
    /// Save payload (GDD 20.17). schemaVersion is bumped whenever a field changes meaning;
    /// SaveMigrations upgrades older payloads instead of discarding them.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 11;

        public int schemaVersion = CurrentSchemaVersion;
        public string buildVersion = "0.1.0";
        public string saveUtc;
        public int slot;
        public int reason;

        public int nightIndex;
        public int gameSecond = Core.GameClock.ShiftStartSecond;

        public PlayerSaveEntry player = new PlayerSaveEntry();

        public List<StatSaveEntry> stats = new List<StatSaveEntry>();
        public List<FlagSaveEntry> flags = new List<FlagSaveEntry>();
        public List<ChoiceSaveEntry> choices = new List<ChoiceSaveEntry>();
        public List<int> accessLevels = new List<int>();

        public List<CaseSaveEntry> cases = new List<CaseSaveEntry>();
        public List<EvidenceSaveEntry> evidence = new List<EvidenceSaveEntry>();
        public List<EvidenceLinkSaveEntry> evidenceLinks = new List<EvidenceLinkSaveEntry>();
        public List<DoorSaveEntry> doors = new List<DoorSaveEntry>();
        public List<ResidentSaveEntry> residents = new List<ResidentSaveEntry>();
        public List<VisitorSaveEntry> visitors = new List<VisitorSaveEntry>();
        public List<SnapshotSaveEntry> snapshots = new List<SnapshotSaveEntry>();
        public List<AccessLogSaveEntry> accessLog = new List<AccessLogSaveEntry>();
        public List<CircuitSaveEntry> circuits = new List<CircuitSaveEntry>();

        // GDD 15.4 / 15.5. Both are per-night, but a save can land mid-shift, so both go in
        // the file - restoring a night at 03:00 with a fresh reserve would hand the player a
        // free reset of everything the night had earned.
        public int pressure;
        public int pressureNightIndex;
        public List<string> pressureIntruders = new List<string>();
        public int powerReserve = Facility.NightPowerService.Full;
        public bool corridorLightsOn = true;
        public int breakerResetsUsed;

        /// <summary>GDD 12.3 catalogue numbers filed at least once this playthrough.</summary>
        public List<int> cataloguedAnomalies = new List<int>();

        public List<string> firedAnomalies = new List<string>();
        public List<string> answeredCalls = new List<string>();
        public List<string> missedCalls = new List<string>();
        public List<string> visitedZones = new List<string>();
        public List<string> openedApps = new List<string>();
        public List<string> dialogueChoices = new List<string>();
        public List<string> unlockedAchievements = new List<string>();

        public string CurrentDialogueId;
        public string CurrentDialogueNodeId;

        // ---- v5.0 (schema 11) -----------------------------------------------

        /// <summary>
        /// The caretaker themselves (v5.0 6, 7).
        ///
        /// These four decide whether the campaign can reach an ending at all, so they are
        /// saved with the shift rather than recomputed: a reload that handed back a full
        /// first-aid box would undo the only scarce resource the campaign has.
        /// </summary>
        public int hp = Core.VitalService.Max;
        public int san = Core.VitalService.Max;
        public int firstAidRemaining = Core.VitalService.FirstAidKitsPerCampaign;
        public int groundingRemaining = Core.VitalService.GroundingPerNight;

        /// <summary>
        /// The seed this campaign draws its nightly quest pools from (v5.0 4.4 step 10).
        ///
        /// Saved so that a reload replays the same night. v5.0 4.4 step 11 forbids re-drawing:
        /// a shift that reshuffles itself on load turns every contradiction the player is
        /// trying to pin down into something they cannot check twice.
        /// </summary>
        public int campaignSeed;

        /// <summary>The quest ids this night actually drew, in the order the director queued them.</summary>
        public List<string> selectedQuests = new List<string>();

        /// <summary>
        /// The night before's draw, kept for the repeat penalty (v5.0 4.4 step 5).
        ///
        /// Saved rather than remembered, because the penalty is the only thing stopping two
        /// consecutive nights from opening on the same kind of impossibility - and a campaign
        /// resumed the next evening would otherwise have forgotten what it showed last.
        /// </summary>
        public List<string> previousNightQuests = new List<string>();

        // ---- v2.1 (schema 7) -------------------------------------------------

        /// <summary>Manual pages the caretaker has obtained (spec 0.9.2).</summary>
        public List<string> manualPages = new List<string>();
        public List<ManualEventSaveEntry> manualEvents = new List<ManualEventSaveEntry>();
        public StairSaveEntry stairs = new StairSaveEntry();

        // ---- v2.1 tools (schema 8) -------------------------------------------

        /// <summary>The A01..A05 machines (spec 23).</summary>
        public List<AnomalyToolSaveEntry> anomalyTools = new List<AnomalyToolSaveEntry>();

        // ---- deferred consequences (schema 9) ---------------------------------

        /// <summary>
        /// What tonight owes tomorrow (spec 30.3).
        ///
        /// Stored as the authored consequences themselves rather than as a list of ids,
        /// because that is what CaseService holds and what it will apply: anything else would
        /// be a second encoding of the same thing, and the two would drift the first time a
        /// consequence gained a field.
        /// </summary>
        public List<Cases.ConsequenceDefinition> deferredConsequences =
            new List<Cases.ConsequenceDefinition>();
    }

    /// <summary>Header shown in the load menu without deserializing the whole payload.</summary>
    public struct SaveHeader
    {
        public bool Exists;
        public int Slot;
        public int NightIndex;
        public int GameSecond;
        public string SaveUtc;
        public SaveReason Reason;
    }
}
