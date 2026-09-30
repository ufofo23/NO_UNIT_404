using System;
using UnityEngine;

namespace NO404.Cases
{
    /// <summary>Case lifecycle from GDD 11.1.</summary>
    public enum CaseState
    {
        Dormant = 0,
        Available = 1,
        Accepted = 2,
        Investigating = 3,
        DecisionReady = 4,
        ResolvedCorrect = 5,
        ResolvedPartial = 6,
        ResolvedWrong = 7,
        ConsequenceQueued = 8,
        ConsequenceApplied = 9
    }

    public static class CaseStateExtensions
    {
        public static bool IsResolved(this CaseState state)
        {
            return state == CaseState.ResolvedCorrect
                || state == CaseState.ResolvedPartial
                || state == CaseState.ResolvedWrong
                || state == CaseState.ConsequenceQueued
                || state == CaseState.ConsequenceApplied;
        }

        public static bool IsActive(this CaseState state)
        {
            return state == CaseState.Accepted
                || state == CaseState.Investigating
                || state == CaseState.DecisionReady;
        }
    }

    /// <summary>How a case becomes Available (GDD 11.2 TriggerType).</summary>
    public enum CaseTrigger
    {
        Time = 0,
        PhoneCall = 1,
        Interphone = 2,
        ZoneEntered = 3,
        AppOpened = 4,
        CaseResolved = 5,
        Manual = 6
    }

    public enum CaseKind
    {
        Tutorial = 0,
        MainCase = 1,   // GDD 11.3 C00..C14
        RoutineTask = 2 // GDD 11.4 T01..T18
    }

    public enum Priority { P0 = 0, P1 = 1, P2 = 2 }

    /// <summary>
    /// What kind of night's work a quest is (v5.0 4.5).
    ///
    /// This is the axis the nightly composition quota is written against, and it is not the
    /// same question as <see cref="CaseKind"/>: kind says where a case sits in the campaign's
    /// structure, type says what it feels like to walk into. A routine parking complaint and
    /// an echo in a stairwell are both cases; only one of them is NORMAL.
    /// </summary>
    public enum QuestType
    {
        /// <summary>The night's fixed spine. Exactly one per night and it always runs.</summary>
        Main = 0,
        /// <summary>Real caretaking. Can begin and end without anything being wrong (v5.0 4.5).</summary>
        Normal = 1,
        /// <summary>Opens as ordinary work; the records or the site quietly disagree.</summary>
        Mixed = 2,
        /// <summary>Unambiguously one of the five families.</summary>
        Anomaly = 3,
        /// <summary>Exists because of something the player chose on an earlier night.</summary>
        Consequence = 4,
        /// <summary>Night 6 only: the pressure that closes the campaign out.</summary>
        FinalPressure = 5
    }

    /// <summary>
    /// Which way a night is wrong (v5.0 4.5, 10-15).
    ///
    /// Carried so the director can avoid showing the same kind of impossibility twice in a
    /// row - v5.0 4.4 step 5 penalises a repeat of the family the previous night used, because
    /// the second time a corridor loops it is a mechanic and the first time it is a fright.
    /// </summary>
    public enum AnomalyFamily
    {
        None = 0,
        /// <summary>The same event, twice, out of order. Two couriers; a knock already answered.</summary>
        Echo = 1,
        /// <summary>The building's records assert something the building cannot support.</summary>
        Record = 2,
        /// <summary>Distance and floor count stop agreeing with the doors.</summary>
        Space = 3,
        /// <summary>A person is not who the record, or the face, says they are.</summary>
        Identity = 4,
        /// <summary>Footage or paperwork that has not happened yet.</summary>
        Future = 5
    }

    // ---- conditions (GDD 20.10) -----------------------------------------

    public enum ConditionType
    {
        FlagEquals = 0,
        StatGreaterOrEqual = 1,
        EvidenceOwned = 2,
        CaseResolvedAs = 3,
        TimeInWindow = 4,
        PlayerEnteredZone = 5,
        AppOpened = 6,
        DialogueChoiceSelected = 7,
        CharacterStateEquals = 8,
        NightIndexAtLeast = 9,
        StatLessOrEqual = 10,
        /// <summary>Night-5 power budget: is this circuit still switched on (GDD 9.6)?</summary>
        CircuitOn = 11
    }

    /// <summary>
    /// A set of conditions that must all hold. Used where a single AND-list is not enough -
    /// notably the ending conditions in GDD 10, which are of the form "(A and B) or (C and D)".
    /// </summary>
    [Serializable]
    public sealed class ConditionGroup
    {
        public ConditionDefinition[] all = new ConditionDefinition[0];

        public bool Matches(out string failReason)
        {
            return ConditionEvaluator.EvaluateAll(all, out failReason);
        }

        public static ConditionGroup Of(params ConditionDefinition[] conditions)
        {
            return new ConditionGroup { all = conditions ?? new ConditionDefinition[0] };
        }
    }

    [Serializable]
    public sealed class ConditionDefinition
    {
        public ConditionType type;
        [Tooltip("Flag / stat / evidence / case / zone / app / conversation id")]
        public string keyA;
        [Tooltip("Decision id, choice id or character state")]
        public string keyB;
        public int valueA;      // stat threshold, window start (game seconds), night index
        public int valueB;      // window end (game seconds)
        public bool boolValue = true;

        public static ConditionDefinition Flag(string flagId, bool expected = true)
        {
            return new ConditionDefinition { type = ConditionType.FlagEquals, keyA = flagId, boolValue = expected };
        }

        public static ConditionDefinition Stat(string statId, int atLeast)
        {
            return new ConditionDefinition { type = ConditionType.StatGreaterOrEqual, keyA = statId, valueA = atLeast };
        }

        public static ConditionDefinition StatAtMost(string statId, int atMost)
        {
            return new ConditionDefinition { type = ConditionType.StatLessOrEqual, keyA = statId, valueA = atMost };
        }

        public static ConditionDefinition Evidence(string evidenceId)
        {
            return new ConditionDefinition { type = ConditionType.EvidenceOwned, keyA = evidenceId, boolValue = true };
        }

        public static ConditionDefinition NoEvidence(string evidenceId)
        {
            return new ConditionDefinition { type = ConditionType.EvidenceOwned, keyA = evidenceId, boolValue = false };
        }

        public static ConditionDefinition Circuit(string circuitId, bool on = true)
        {
            return new ConditionDefinition { type = ConditionType.CircuitOn, keyA = circuitId, boolValue = on };
        }

        public static ConditionDefinition Night(int atLeast)
        {
            return new ConditionDefinition { type = ConditionType.NightIndexAtLeast, valueA = atLeast };
        }

        public static ConditionDefinition Window(int startSecond, int endSecond)
        {
            return new ConditionDefinition { type = ConditionType.TimeInWindow, valueA = startSecond, valueB = endSecond };
        }

        public static ConditionDefinition Zone(string zoneId)
        {
            return new ConditionDefinition { type = ConditionType.PlayerEnteredZone, keyA = zoneId, boolValue = true };
        }

        public static ConditionDefinition CaseResolved(string caseId, string decisionId)
        {
            return new ConditionDefinition { type = ConditionType.CaseResolvedAs, keyA = caseId, keyB = decisionId };
        }
    }

    // ---- objectives (GDD 20.11) -----------------------------------------

    public enum ObjectiveType
    {
        OpenApp = 0,
        ViewRecord = 1,
        ViewCctvChannel = 2,
        TakeSnapshot = 3,
        CallCharacter = 4,
        EnterZone = 5,
        Interact = 6,
        AcquireEvidence = 7,
        PerformAction = 8,
        SubmitReport = 9,
        JudgeVisitor = 10
    }

    [Serializable]
    public sealed class ObjectiveDefinition
    {
        public string objectiveId;
        [Tooltip("Localization key. Must not spoil information the player has not learned yet.")]
        public string titleKey;
        public ObjectiveType type;
        public string targetId;
        public bool optional;
        [Tooltip("Hidden objectives track progress without appearing in the HUD.")]
        public bool hidden;
    }

    // ---- decisions and consequences --------------------------------------

    public enum DecisionQuality { Correct = 0, Partial = 1, Wrong = 2 }

    public enum ConsequenceType
    {
        StatDelta = 0,
        SetFlag = 1,
        GrantAccess = 2,
        StartCase = 3,
        GrantEvidence = 4,
        UnlockAchievement = 5,
        Notify = 6,

        // ---- v2.1 (spec 0.10, 26) ----------------------------------------
        //
        // The risk counters are stats, so StatDelta could reach them. These entries exist
        // anyway because they carry the meaning the plain stat write loses: raising a floor's
        // risk has to publish FloorRiskChangedEvent for the dressing code, and crossing a
        // distortion band has to open the correction procedure. Content that wrote
        // StatDelta("FloorRisk_F4") would change the number and nothing else.

        /// <summary>targetId = floor id, amount = delta (spec 0.10.3).</summary>
        AddFloorRisk = 7,
        /// <summary>amount = delta to DistortionExposure (spec 0.10.4).</summary>
        AddDistortion = 8,
        /// <summary>
        /// targetId = the rule key broken. Counts a manual violation and applies its cost
        /// (spec 0.10.2 B). Only for a rule the player had been given.
        /// </summary>
        RecordViolation = 9,
        /// <summary>targetId = manual page id to add to the binder (spec 0.9.2).</summary>
        UnlockManualPage = 10,
        /// <summary>targetId = sequence id, e.g. F4_LIGHTS_OUT (spec 26 onWrong).</summary>
        StartSequence = 11,
        /// <summary>amount = delta to MemoryDebt (spec 23 A01/A03).</summary>
        AddMemoryDebt = 12,
        /// <summary>amount = delta to ToolDebt (spec 23 A04).</summary>
        AddToolDebt = 13,

        // ---- v5.0 --------------------------------------------------------

        /// <summary>
        /// targetId = choice id, stringValue = the value it took (v5.0 5.4).
        ///
        /// Separate from SetFlag because these are not yes/no. A night that asks how Sunja
        /// was treated wants one of GOOD, DELAYED or DENIED, and three flags could answer
        /// all three at once.
        /// </summary>
        SetChoice = 14,

        /// <summary>
        /// amount = HP delta, reasonKey = why (v5.0 6).
        ///
        /// Negative harms, positive heals. The reason travels because v5.0 6.1 will not
        /// allow a loss the player could not have seen coming, and a consequence that cannot
        /// say what hurt them is exactly that.
        /// </summary>
        Hp = 15,

        /// <summary>amount = SAN delta, reasonKey = why (v5.0 7).</summary>
        San = 16
    }

    [Serializable]
    public sealed class ConsequenceDefinition
    {
        public ConsequenceType type;
        public string targetId;
        public int amount;
        public bool boolValue = true;
        [Tooltip("The value a SetChoice consequence records (v5.0 5.4).")]
        public string stringValue;
        [Tooltip("Deferred to the start of the next night instead of applying immediately.")]
        public bool nextNight;
        public string reasonKey;

        public static ConsequenceDefinition Stat(string statId, int delta, string reasonKey = null)
        {
            return new ConsequenceDefinition
            {
                type = ConsequenceType.StatDelta, targetId = statId, amount = delta, reasonKey = reasonKey
            };
        }

        public static ConsequenceDefinition Flag(string flagId, bool value = true)
        {
            return new ConsequenceDefinition { type = ConsequenceType.SetFlag, targetId = flagId, boolValue = value };
        }

        public static ConsequenceDefinition Notify(string notifyKey)
        {
            return new ConsequenceDefinition { type = ConsequenceType.Notify, targetId = notifyKey };
        }

        public static ConsequenceDefinition Achievement(string achievementId)
        {
            return new ConsequenceDefinition { type = ConsequenceType.UnlockAchievement, targetId = achievementId };
        }

        public static ConsequenceDefinition Evidence(string evidenceId)
        {
            return new ConsequenceDefinition { type = ConsequenceType.GrantEvidence, targetId = evidenceId };
        }

        // ---- v2.1 helpers (spec 0.10) ------------------------------------

        public static ConsequenceDefinition FloorRisk(string floorId, int delta)
        {
            return new ConsequenceDefinition
            {
                type = ConsequenceType.AddFloorRisk, targetId = floorId, amount = delta
            };
        }

        public static ConsequenceDefinition Distortion(int delta)
        {
            return new ConsequenceDefinition { type = ConsequenceType.AddDistortion, amount = delta };
        }

        public static ConsequenceDefinition Violation(string ruleKey)
        {
            return new ConsequenceDefinition { type = ConsequenceType.RecordViolation, targetId = ruleKey };
        }

        /// <summary>Records which way a valued choice went (v5.0 5.4).</summary>
        public static ConsequenceDefinition Choice(string choiceId, string value)
        {
            return new ConsequenceDefinition
            {
                type = ConsequenceType.SetChoice, targetId = choiceId, stringValue = value
            };
        }

        /// <summary>What this cost the caretaker's body (v5.0 6). Negative harms.</summary>
        public static ConsequenceDefinition Health(int delta, string reasonKey)
        {
            return new ConsequenceDefinition
            {
                type = ConsequenceType.Hp, amount = delta, reasonKey = reasonKey
            };
        }

        /// <summary>What this cost their nerve (v5.0 7). Negative strains.</summary>
        public static ConsequenceDefinition Sanity(int delta, string reasonKey)
        {
            return new ConsequenceDefinition
            {
                type = ConsequenceType.San, amount = delta, reasonKey = reasonKey
            };
        }

        /// <summary>One of the five debts a night can leave behind (v5.0 5.3).</summary>
        public static ConsequenceDefinition Debt(string debtId, int amount, string reasonKey = null)
        {
            return new ConsequenceDefinition
            {
                type = ConsequenceType.StatDelta, targetId = debtId, amount = amount, reasonKey = reasonKey
            };
        }

        public static ConsequenceDefinition ManualPage(string pageId)
        {
            return new ConsequenceDefinition { type = ConsequenceType.UnlockManualPage, targetId = pageId };
        }

        public static ConsequenceDefinition Sequence(string sequenceId)
        {
            return new ConsequenceDefinition { type = ConsequenceType.StartSequence, targetId = sequenceId };
        }

        public static ConsequenceDefinition MemoryDebt(int delta)
        {
            return new ConsequenceDefinition { type = ConsequenceType.AddMemoryDebt, amount = delta };
        }

        public static ConsequenceDefinition ToolDebt(int delta)
        {
            return new ConsequenceDefinition { type = ConsequenceType.AddToolDebt, amount = delta };
        }
    }

    [Serializable]
    public sealed class DecisionDefinition
    {
        public string decisionId;
        public string labelKey;
        public string resultKey;
        public DecisionQuality quality = DecisionQuality.Partial;
        [Tooltip("Evidence the report must contain for this decision to be selectable.")]
        public string[] requiredEvidenceIds = new string[0];
        public ConditionDefinition[] availability = new ConditionDefinition[0];
        public ConsequenceDefinition[] consequences = new ConsequenceDefinition[0];
    }

    /// <summary>
    /// Anti-softlock rule from GDD 11.2 / 20.16: if a P0 case is still unresolved when the
    /// fail-safe time is reached, the information is re-exposed through another channel.
    /// </summary>
    [Serializable]
    public sealed class FailSafeDefinition
    {
        public bool enabled;
        [Tooltip("Game second at which the alternate route opens.")]
        public int triggerGameSecond;
        public string alternateEvidenceId;
        public string notifyKey;
        public string forceDecisionId;
    }
}
