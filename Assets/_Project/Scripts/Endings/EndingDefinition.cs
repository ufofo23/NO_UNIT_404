using UnityEngine;
using NO404.Cases;

namespace NO404.Endings
{
    [CreateAssetMenu(menuName = "NO404/Endings/Ending", fileName = "ENDING_")]
    public sealed class EndingDefinition : ScriptableObject
    {
        public string endingId;
        public string titleKey;
        public string summaryKey;
        [Tooltip("Longer epilogue text shown on the ending screen.")]
        public string bodyKey;
        public string achievementId;

        [Header("Selection")]
        [Tooltip("Lower runs first. The first ending whose conditions all hold is the one shown.")]
        public int evaluationOrder = 100;
        [Tooltip("Every condition must pass.")]
        public ConditionDefinition[] requireAll = new ConditionDefinition[0];
        [Tooltip("At least one must pass. Empty = ignored.")]
        public ConditionDefinition[] requireAny = new ConditionDefinition[0];
        [Tooltip("At least one whole group must pass. Lets an ending express (A and B) or (C and D).")]
        public ConditionGroup[] requireAnyGroup = new ConditionGroup[0];
        [Tooltip("None may pass. Empty = ignored.")]
        public ConditionDefinition[] forbid = new ConditionDefinition[0];

        [Header("Presentation")]
        [Tooltip("Shown in the gallery before the player has reached it.")]
        public string lockedHintKey;
        [Tooltip("Endings that change the shell (GDD 10.3 title corruption, 10.5 loop).")]
        public EndingAftermath aftermath = EndingAftermath.None;

        public bool Matches(out string failReason)
        {
            if (!ConditionEvaluator.EvaluateAll(requireAll, out failReason)) return false;

            if (requireAny != null && requireAny.Length > 0 && !ConditionEvaluator.EvaluateAny(requireAny))
            {
                failReason = "none of the alternative conditions held";
                return false;
            }

            if (requireAnyGroup != null && requireAnyGroup.Length > 0)
            {
                bool anyGroupMatched = false;
                for (int i = 0; i < requireAnyGroup.Length && !anyGroupMatched; i++)
                {
                    string ignored;
                    if (requireAnyGroup[i] != null && requireAnyGroup[i].Matches(out ignored)) anyGroupMatched = true;
                }

                if (!anyGroupMatched)
                {
                    failReason = "no alternative condition group held";
                    return false;
                }
            }

            if (forbid != null && forbid.Length > 0 && ConditionEvaluator.EvaluateAny(forbid))
            {
                failReason = "a forbidden condition held";
                return false;
            }

            failReason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Persistent side effects an ending leaves on the shell (GDD 10.3 / 10.5).
    /// Kept as data so the meta-layer never hard-codes an ending id.
    /// </summary>
    public enum EndingAftermath
    {
        None = 0,
        /// <summary>The game title loses its first characters on the main menu.</summary>
        CorruptTitle = 1,
        /// <summary>The next new game starts from the loop variant of the prologue.</summary>
        LoopPrologue = 2
    }
}
