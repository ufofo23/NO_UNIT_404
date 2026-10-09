using NO404.Core;

namespace NO404.Cases
{
    /// <summary>
    /// Evaluates serialized conditions (GDD 20.10). Every result carries the reason it failed
    /// so the dev console can explain why a case refuses to start.
    /// </summary>
    public static class ConditionEvaluator
    {
        public static bool Evaluate(ConditionDefinition condition, out string reason)
        {
            reason = string.Empty;
            if (condition == null) { return true; }

            var state = ServiceHub.State;
            var player = ServiceHub.Player;

            switch (condition.type)
            {
                case ConditionType.FlagEquals:
                {
                    bool actual = state.GetFlag(condition.keyA);
                    if (actual == condition.boolValue) return true;
                    reason = "flag " + condition.keyA + " is " + actual + ", expected " + condition.boolValue;
                    return false;
                }

                case ConditionType.StatGreaterOrEqual:
                {
                    int actual = state.GetStat(condition.keyA);
                    if (actual >= condition.valueA) return true;
                    reason = "stat " + condition.keyA + " is " + actual + ", need >= " + condition.valueA;
                    return false;
                }

                case ConditionType.StatLessOrEqual:
                {
                    int actual = state.GetStat(condition.keyA);
                    if (actual <= condition.valueA) return true;
                    reason = "stat " + condition.keyA + " is " + actual + ", need <= " + condition.valueA;
                    return false;
                }

                case ConditionType.EvidenceOwned:
                {
                    bool owned = ServiceHub.Evidence.Has(condition.keyA);
                    if (owned == condition.boolValue) return true;
                    reason = "evidence " + condition.keyA + " owned=" + owned;
                    return false;
                }

                case ConditionType.CaseResolvedAs:
                {
                    var runtime = ServiceHub.Cases.Find(condition.keyA);
                    if (runtime == null) { reason = "case " + condition.keyA + " unknown"; return false; }
                    if (!runtime.State.IsResolved()) { reason = "case " + condition.keyA + " not resolved"; return false; }
                    if (string.IsNullOrEmpty(condition.keyB) || runtime.ChosenDecisionId == condition.keyB) return true;
                    reason = "case " + condition.keyA + " resolved as " + runtime.ChosenDecisionId;
                    return false;
                }

                case ConditionType.TimeInWindow:
                {
                    int now = ServiceHub.Clock.GameSecond;
                    if (GameClock.InWindow(now, condition.valueA, condition.valueB)) return true;
                    reason = "time " + GameClock.FormatSecond(now) + " outside window";
                    return false;
                }

                case ConditionType.PlayerEnteredZone:
                {
                    bool visited = player.HasVisited(condition.keyA);
                    if (visited == condition.boolValue) return true;
                    reason = "zone " + condition.keyA + " visited=" + visited;
                    return false;
                }

                case ConditionType.AppOpened:
                {
                    bool opened = player.HasOpenedApp(condition.keyA);
                    if (opened == condition.boolValue) return true;
                    reason = "app " + condition.keyA + " opened=" + opened;
                    return false;
                }

                case ConditionType.DialogueChoiceSelected:
                {
                    string key = string.IsNullOrEmpty(condition.keyA)
                        ? condition.keyB
                        : condition.keyA + "/" + condition.keyB;
                    bool chosen = player.HasChosen(key);
                    if (chosen == condition.boolValue) return true;
                    reason = "choice " + key + " selected=" + chosen;
                    return false;
                }

                case ConditionType.CharacterStateEquals:
                {
                    // Character states are stored as flags of the form "<characterId>.<state>".
                    string flag = condition.keyA + "." + condition.keyB;
                    bool actual = state.GetFlag(flag);
                    if (actual == condition.boolValue) return true;
                    reason = "character state " + flag + " is " + actual;
                    return false;
                }

                case ConditionType.CircuitOn:
                {
                    bool on = ServiceHub.Facility.IsCircuitOn(condition.keyA);
                    if (on == condition.boolValue) return true;
                    reason = "circuit " + condition.keyA + " on=" + on;
                    return false;
                }

                case ConditionType.NightIndexAtLeast:
                {
                    if (state.NightIndex >= condition.valueA) return true;
                    reason = "night " + state.NightIndex + " < " + condition.valueA;
                    return false;
                }

                case ConditionType.VisitorAccessRank:
                {
                    // Pending ranks below Reject, so a caller nobody has judged matches nothing.
                    int furthest = -1;
                    foreach (var visitorId in VisitorIds(condition.keyA))
                    {
                        int rank = Visitors.VisitorAccess.RiskRank(ServiceHub.Interphone.DecisionFor(visitorId));
                        if (rank > furthest) furthest = rank;
                    }

                    if (furthest >= condition.valueA && furthest <= condition.valueB) return true;
                    reason = "access rank for " + condition.keyA + " is " + furthest +
                             ", need " + condition.valueA + ".." + condition.valueB;
                    return false;
                }

                case ConditionType.VisitorWasHeld:
                {
                    bool held = false;
                    foreach (var visitorId in VisitorIds(condition.keyA))
                        if (state.GetFlag(Visitors.InterphoneService.HeldFlag(visitorId))) held = true;

                    if (held == condition.boolValue) return true;
                    reason = condition.keyA + " held=" + held;
                    return false;
                }

                case ConditionType.EvidenceTagCount:
                {
                    int count = 0;
                    foreach (var pair in ServiceHub.Evidence.Owned)
                    {
                        var definition = pair.Value.Definition;
                        if (definition == null || definition.tags == null) continue;
                        if (!string.IsNullOrEmpty(condition.keyB) && definition.ownerCaseId != condition.keyB) continue;
                        if (System.Array.IndexOf(definition.tags, condition.keyA) >= 0) count++;
                    }

                    if ((count >= condition.valueA) == condition.boolValue) return true;
                    reason = count + " evidence tagged " + condition.keyA + ", wanted " +
                             (condition.boolValue ? ">= " : "< ") + condition.valueA;
                    return false;
                }
            }

            reason = "unhandled condition type " + condition.type;
            return false;
        }

        static string[] VisitorIds(string joined)
        {
            return string.IsNullOrEmpty(joined) ? new string[0] : joined.Split('|');
        }

        public static bool EvaluateAll(ConditionDefinition[] conditions, out string failReason)
        {
            failReason = string.Empty;
            if (conditions == null) return true;

            for (int i = 0; i < conditions.Length; i++)
            {
                string reason;
                if (!Evaluate(conditions[i], out reason))
                {
                    failReason = reason;
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when at least one condition passes. Empty array counts as false.</summary>
        public static bool EvaluateAny(ConditionDefinition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return false;
            for (int i = 0; i < conditions.Length; i++)
            {
                string reason;
                if (Evaluate(conditions[i], out reason)) return true;
            }
            return false;
        }
    }
}
