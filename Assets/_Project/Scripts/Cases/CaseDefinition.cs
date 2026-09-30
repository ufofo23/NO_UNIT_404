using UnityEngine;

namespace NO404.Cases
{
    [CreateAssetMenu(menuName = "NO404/Cases/Case Definition", fileName = "CASE_")]
    public sealed class CaseDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string caseId;
        public string titleKey;
        public string summaryKey;
        public CaseKind kind = CaseKind.MainCase;
        public Priority priority = Priority.P0;

        [Header("Night pool (v5.0 4)")]
        [Tooltip("What this feels like to walk into. Drives the nightly composition quota.")]
        public QuestType questType = QuestType.Normal;
        [Tooltip("Which way this one is wrong, if it is. Used to avoid repeating a family.")]
        public AnomalyFamily family = AnomalyFamily.None;

        /// <summary>
        /// The night's spine (v5.0 4.1). Exactly one per night, and it is never drawn for -
        /// it is placed first and the random slots are filled around it.
        /// </summary>
        public bool isFixedMain;

        [Tooltip("Relative chance of being drawn. Weight 0 means never drawn at random.")]
        public int baseWeight = 100;

        [Tooltip("Roughly how long this takes, in minutes. Budgets the optional fifth slot.")]
        public float estimatedMinutes = 6f;

        /// <summary>
        /// Quests that cannot share a night (v5.0 4.4 step 7).
        ///
        /// Two events that both take the pump room offline, or two that both explain the same
        /// contradiction, do not make a fuller night - they make one of them a lie.
        /// </summary>
        public string mutexGroup;

        [Header("Availability")]
        public int nightIndex;
        [Tooltip("Earliest game second the case may become available.")]
        public int startWindowBegin = Core.GameClock.ShiftStartSecond;
        [Tooltip("Latest game second the case may become available. 0 = end of shift.")]
        public int startWindowEnd;
        public CaseTrigger trigger = CaseTrigger.Time;
        [Tooltip("Zone id / app id / case id depending on the trigger.")]
        public string triggerTarget;
        public ConditionDefinition[] startConditions = new ConditionDefinition[0];
        public ConditionDefinition[] blockedBy = new ConditionDefinition[0];

        [Header("Content")]
        public string[] evidenceIds = new string[0];
        public ObjectiveDefinition[] objectives = new ObjectiveDefinition[0];
        public DecisionDefinition[] decisions = new DecisionDefinition[0];
        public ConsequenceDefinition[] consequences = new ConsequenceDefinition[0];
        public FailSafeDefinition failSafe = new FailSafeDefinition();

        [Header("Analytics")]
        public string analyticsName;

        public int ResolvedWindowEnd
        {
            get { return startWindowEnd > 0 ? startWindowEnd : Core.GameClock.ShiftEndSecond; }
        }

        public DecisionDefinition FindDecision(string decisionId)
        {
            if (decisions == null) return null;
            for (int i = 0; i < decisions.Length; i++)
                if (decisions[i] != null && decisions[i].decisionId == decisionId) return decisions[i];
            return null;
        }

        public ObjectiveDefinition FindObjective(string objectiveId)
        {
            if (objectives == null) return null;
            for (int i = 0; i < objectives.Length; i++)
                if (objectives[i] != null && objectives[i].objectiveId == objectiveId) return objectives[i];
            return null;
        }
    }
}
