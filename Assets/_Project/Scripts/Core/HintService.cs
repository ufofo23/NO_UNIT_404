using NO404.Cases;

namespace NO404.Core
{
    /// <summary>
    /// The four-step hint ladder from GDD 24.3: restate the objective, name the app or zone,
    /// name the record, then give the exact path. Steps arrive at 3, 6 and 9 minutes on the
    /// same objective, scaled by difficulty, and the whole thing can be switched off.
    ///
    /// GDD 24.1 also asks for stronger help after two wrong judgements in a row, so the
    /// ladder starts one rung higher in that case.
    /// </summary>
    public sealed class HintService
    {
        public const float FirstStepSeconds = 180f;
        public const float StepSeconds = 180f;

        string _trackedObjectiveId;
        float _objectiveSinceRealtime;

        public int Level { get; private set; }
        public string CurrentHint { get; private set; }

        /// <summary>Two wrong calls in a row raise the starting rung (GDD 24.1).</summary>
        public int ConsecutiveWrongDecisions { get; private set; }

        public void RegisterDecision(DecisionQuality quality)
        {
            ConsecutiveWrongDecisions = quality == DecisionQuality.Wrong
                ? ConsecutiveWrongDecisions + 1
                : 0;
        }

        public void Reset()
        {
            _trackedObjectiveId = null;
            _objectiveSinceRealtime = 0f;
            Level = 0;
            CurrentHint = string.Empty;
            ConsecutiveWrongDecisions = 0;
        }

        public void Tick(float realtimeNow)
        {
            var tracked = ServiceHub.Cases.TrackedCase;
            var objective = tracked != null ? tracked.CurrentVisibleObjective() : null;

            if (objective == null)
            {
                _trackedObjectiveId = null;
                Level = 0;
                CurrentHint = string.Empty;
                return;
            }

            if (objective.objectiveId != _trackedObjectiveId)
            {
                _trackedObjectiveId = objective.objectiveId;
                _objectiveSinceRealtime = realtimeNow;
                Level = 0;
                CurrentHint = string.Empty;
            }

            var mode = DifficultyProfile.Hints;
            if (mode == HintMode.Off) { Level = 0; CurrentHint = string.Empty; return; }

            int level = LevelFor(realtimeNow - _objectiveSinceRealtime, mode);
            if (level == Level) return;

            Level = level;
            CurrentHint = level <= 0 ? string.Empty : Describe(objective, level);
        }

        int LevelFor(float elapsed, HintMode mode)
        {
            // "Always" skips the wait but still escalates on the same ladder.
            float first = mode == HintMode.Always ? 0f : FirstStepSeconds * DifficultyProfile.HintDelayFactor;
            float step = StepSeconds * DifficultyProfile.HintDelayFactor;

            int level = 0;
            if (elapsed >= first) level = 1;
            if (elapsed >= first + step) level = 2;
            if (elapsed >= first + step * 2f) level = 3;
            if (elapsed >= first + step * 3f) level = 4;

            if (level > 0 && ConsecutiveWrongDecisions >= 2) level++;
            return level > 4 ? 4 : level;
        }

        /// <summary>Builds the hint text from the objective's own data - nothing is authored twice.</summary>
        static string Describe(ObjectiveDefinition objective, int level)
        {
            if (level <= 1) return Loc.T("ui.hint.restate", Loc.T(objective.titleKey));

            switch (objective.type)
            {
                case ObjectiveType.OpenApp:
                    return level >= 4
                        ? Loc.T("ui.hint.path.app", Loc.T("ui.app." + objective.targetId))
                        : Loc.T("ui.hint.app", Loc.T("ui.app." + objective.targetId));

                case ObjectiveType.ViewRecord:
                {
                    if (level == 2) return Loc.T("ui.hint.app", Loc.T("ui.app." + AppIds.Residents));
                    var resident = ServiceHub.Content.FindResident(objective.targetId);
                    var name = resident != null ? Loc.T(resident.nameKey) + " " + resident.unitNumber
                                                : objective.targetId;
                    return level >= 4 ? Loc.T("ui.hint.path.record", name) : Loc.T("ui.hint.record", name);
                }

                case ObjectiveType.ViewCctvChannel:
                {
                    if (level == 2) return Loc.T("ui.hint.app", Loc.T("ui.app." + AppIds.Cctv));
                    var channel = ServiceHub.Content.FindChannel(objective.targetId);
                    var label = channel != null ? objective.targetId + " " + Loc.T(channel.labelKey)
                                                : objective.targetId;
                    return level >= 4 ? Loc.T("ui.hint.path.channel", label) : Loc.T("ui.hint.channel", label);
                }

                case ObjectiveType.EnterZone:
                    return Loc.T(level >= 4 ? "ui.hint.path.zone" : "ui.hint.zone",
                                 Loc.T("ui.zone." + objective.targetId));

                case ObjectiveType.AcquireEvidence:
                {
                    var evidence = ServiceHub.Content.FindEvidence(objective.targetId);
                    var name = evidence != null ? Loc.T(evidence.displayNameKey) : objective.targetId;
                    return level >= 4 ? Loc.T("ui.hint.path.evidence", name) : Loc.T("ui.hint.evidence", name);
                }

                case ObjectiveType.CallCharacter:
                    return Loc.T(level >= 4 ? "ui.hint.path.person" : "ui.hint.person");

                case ObjectiveType.JudgeVisitor:
                    return Loc.T("ui.hint.visitor");

                case ObjectiveType.TakeSnapshot:
                    return Loc.T("ui.hint.snapshot");

                case ObjectiveType.SubmitReport:
                    return Loc.T("ui.hint.report");

                default:
                    return Loc.T("ui.hint.restate", Loc.T(objective.titleKey));
            }
        }
    }
}
