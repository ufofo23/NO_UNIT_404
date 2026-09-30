using System.Collections.Generic;

namespace NO404.Cases
{
    /// <summary>Live state of one case. Fully serialized into the save file.</summary>
    public sealed class CaseRuntime
    {
        public readonly CaseDefinition Definition;

        readonly HashSet<string> _completedObjectives = new HashSet<string>();
        readonly List<string> _attachedEvidence = new List<string>();

        public CaseState State { get; private set; }
        public string ChosenDecisionId { get; private set; }
        public int StartedAtGameSecond { get; private set; }
        public int ResolvedAtGameSecond { get; private set; }
        public bool FailSafeFired { get; private set; }

        public CaseRuntime(CaseDefinition definition)
        {
            Definition = definition;
            State = CaseState.Dormant;
            ChosenDecisionId = string.Empty;
        }

        public string CaseId { get { return Definition != null ? Definition.caseId : string.Empty; } }
        public IReadOnlyCollection<string> CompletedObjectives { get { return _completedObjectives; } }
        public IReadOnlyList<string> AttachedEvidence { get { return _attachedEvidence; } }

        public void SetState(CaseState next, int gameSecond)
        {
            if (State == next) return;
            var previous = State;
            State = next;

            if (next == CaseState.Accepted && StartedAtGameSecond == 0) StartedAtGameSecond = gameSecond;
            if (next.IsResolved() && ResolvedAtGameSecond == 0) ResolvedAtGameSecond = gameSecond;

            Core.EventBus.Publish(new Core.CaseStateChangedEvent(CaseId, previous, next));
        }

        public bool IsObjectiveComplete(string objectiveId) { return _completedObjectives.Contains(objectiveId); }

        public bool CompleteObjective(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId)) return false;
            if (!_completedObjectives.Add(objectiveId)) return false;
            Core.EventBus.Publish(new Core.ObjectiveChangedEvent(CaseId, objectiveId, true));
            return true;
        }

        /// <summary>All non-optional objectives done.</summary>
        public bool AllRequiredObjectivesComplete()
        {
            var objectives = Definition.objectives;
            if (objectives == null) return true;

            for (int i = 0; i < objectives.Length; i++)
            {
                var o = objectives[i];
                if (o == null || o.optional) continue;
                if (!_completedObjectives.Contains(o.objectiveId)) return false;
            }
            return true;
        }

        /// <summary>The one objective shown in the HUD (GDD 16.5: exactly one at a time).</summary>
        public ObjectiveDefinition CurrentVisibleObjective()
        {
            var objectives = Definition.objectives;
            if (objectives == null) return null;

            for (int i = 0; i < objectives.Length; i++)
            {
                var o = objectives[i];
                if (o == null || o.hidden) continue;
                if (!_completedObjectives.Contains(o.objectiveId)) return o;
            }
            return null;
        }

        /// <summary>
        /// Null while the case has no outcome yet. A wrong/partial state has no chosen
        /// decision to look up, so it maps straight from the state; a correct-family state
        /// resolves through whichever decision the player actually picked.
        /// </summary>
        public DecisionQuality? ResolvedQuality()
        {
            switch (State)
            {
                case CaseState.ResolvedWrong:
                    return DecisionQuality.Wrong;

                case CaseState.ResolvedPartial:
                    return DecisionQuality.Partial;

                case CaseState.ResolvedCorrect:
                case CaseState.ConsequenceQueued:
                case CaseState.ConsequenceApplied:
                    var decision = Definition.FindDecision(ChosenDecisionId);
                    return decision != null ? decision.quality : DecisionQuality.Partial;

                default:
                    return null;
            }
        }

        public void SetDecision(string decisionId) { ChosenDecisionId = decisionId ?? string.Empty; }
        public void MarkFailSafeFired() { FailSafeFired = true; }

        public void AttachEvidence(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId) || _attachedEvidence.Contains(evidenceId)) return;
            _attachedEvidence.Add(evidenceId);
        }

        public void DetachEvidence(string evidenceId) { _attachedEvidence.Remove(evidenceId); }

        public void RestoreFrom(CaseState state, string decisionId, int startedAt, int resolvedAt,
                                bool failSafeFired, IEnumerable<string> objectives, IEnumerable<string> evidence)
        {
            State = state;
            ChosenDecisionId = decisionId ?? string.Empty;
            StartedAtGameSecond = startedAt;
            ResolvedAtGameSecond = resolvedAt;
            FailSafeFired = failSafeFired;

            _completedObjectives.Clear();
            if (objectives != null) foreach (var o in objectives) _completedObjectives.Add(o);

            _attachedEvidence.Clear();
            if (evidence != null) foreach (var e in evidence) _attachedEvidence.Add(e);
        }
    }

    /// <summary>Outcome of SubmitDecision, shown in the report result panel.</summary>
    public readonly struct DecisionResult
    {
        public readonly bool Accepted;
        public readonly DecisionQuality Quality;
        public readonly string ResultKey;
        public readonly string RejectReason;

        DecisionResult(bool accepted, DecisionQuality quality, string resultKey, string rejectReason)
        {
            Accepted = accepted; Quality = quality; ResultKey = resultKey; RejectReason = rejectReason;
        }

        public static DecisionResult Ok(DecisionQuality quality, string resultKey)
        {
            return new DecisionResult(true, quality, resultKey, string.Empty);
        }

        public static DecisionResult Rejected(string reason)
        {
            return new DecisionResult(false, DecisionQuality.Wrong, string.Empty, reason);
        }
    }
}
