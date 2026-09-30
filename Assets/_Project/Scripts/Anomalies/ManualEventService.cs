using System.Collections.Generic;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Manual;

namespace NO404.Anomalies
{
    /// <summary>
    /// One M event in flight: its state, its numbered progress, and the counters its outcome
    /// is judged on.
    /// </summary>
    public sealed class ManualEventRuntime
    {
        readonly Dictionary<string, int> _counters = new Dictionary<string, int>(8);
        readonly HashSet<string> _completed = new HashSet<string>();

        public ManualEventDefinition Definition { get; private set; }
        public ManualEventState State { get; private set; }
        public int StartedAt { get; private set; }
        public int ResolvedAt { get; private set; }
        /// <summary>Wrong outcomes so far. Drives the spec 26 failsafe and spec 0.10.5.</summary>
        public int WrongAttempts { get; private set; }
        public bool FailSafeFired { get; private set; }

        public ManualEventRuntime(ManualEventDefinition definition)
        {
            Definition = definition;
            State = ManualEventState.Dormant;
        }

        public void SetState(ManualEventState state, int gameSecond)
        {
            if (State == state) return;
            var previous = State;
            State = state;
            if (state == ManualEventState.Active) StartedAt = gameSecond;
            if (state.IsResolved() && ResolvedAt == 0) ResolvedAt = gameSecond;

            EventBus.Publish(new ManualEventStateChangedEvent(Definition.eventId, previous, state));
            Log.Info("Manual", Definition.eventId + " " + previous + " -> " + state);
        }

        // ---- counters ---------------------------------------------------------

        public int Counter(string counterId)
        {
            if (string.IsNullOrEmpty(counterId)) return 0;
            int value;
            return _counters.TryGetValue(counterId, out value) ? value : 0;
        }

        public void SetCounter(string counterId, int value)
        {
            if (string.IsNullOrEmpty(counterId)) return;
            _counters[counterId] = value;
        }

        public void AddCounter(string counterId, int delta)
        {
            SetCounter(counterId, Counter(counterId) + delta);
        }

        public IReadOnlyDictionary<string, int> Counters { get { return _counters; } }

        // ---- objectives --------------------------------------------------------

        public bool CompleteObjective(string objectiveId)
        {
            if (Definition.FindObjective(objectiveId) == null) return false;
            return _completed.Add(objectiveId);
        }

        public bool IsComplete(string objectiveId) { return _completed.Contains(objectiveId); }

        public IEnumerable<string> CompletedObjectives { get { return _completed; } }

        /// <summary>Every non-optional step done, so the outcome can be judged.</summary>
        public bool AllRequiredDone
        {
            get
            {
                var objectives = Definition.objectives;
                for (int i = 0; i < objectives.Length; i++)
                {
                    var o = objectives[i];
                    if (o != null && !o.optional && !_completed.Contains(o.objectiveId)) return false;
                }
                return true;
            }
        }

        /// <summary>The next step the HUD should show, or null when the list is done.</summary>
        public ManualObjectiveDefinition NextObjective
        {
            get
            {
                var objectives = Definition.objectives;
                for (int i = 0; i < objectives.Length; i++)
                {
                    var o = objectives[i];
                    if (o != null && !o.hidden && !_completed.Contains(o.objectiveId)) return o;
                }
                return null;
            }
        }

        // ---- retries ------------------------------------------------------------

        public void NoteWrongAttempt() { WrongAttempts++; }
        public void MarkFailSafeFired() { FailSafeFired = true; }

        /// <summary>
        /// Wipe the attempt so the player can work the procedure again.
        ///
        /// Spec 30.2 case 3 requires a recoverable first mistake, and recovery means starting
        /// the count of knocks or seals or laps from zero - not resuming halfway through a
        /// sequence they got wrong. WrongAttempts deliberately survives: it is the record of
        /// how this went, and the failsafe reads it.
        /// </summary>
        public void ResetForRetry(int gameSecond)
        {
            _counters.Clear();
            _completed.Clear();
            ResolvedAt = 0;
            SetState(ManualEventState.Active, gameSecond);
        }

        /// <summary>Bring a nightly-repeating event back for a fresh night (spec 22 M13).</summary>
        public void ResetForNight()
        {
            _counters.Clear();
            _completed.Clear();
            State = ManualEventState.Dormant;
            StartedAt = 0;
            ResolvedAt = 0;
            FailSafeFired = false;
        }

        public void RestoreFrom(ManualEventState state, int startedAt, int resolvedAt,
                                int wrongAttempts, bool failSafeFired,
                                IEnumerable<string> completed,
                                IEnumerable<KeyValuePair<string, int>> counters)
        {
            State = state;
            StartedAt = startedAt;
            ResolvedAt = resolvedAt;
            WrongAttempts = wrongAttempts;
            FailSafeFired = failSafeFired;

            _completed.Clear();
            if (completed != null) foreach (var id in completed) _completed.Add(id);

            _counters.Clear();
            if (counters != null) foreach (var kv in counters) _counters[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// Runs the M01..M18 anomaly events (spec 22) through the spec 0.2 lifecycle.
    ///
    /// Two rules from the spec are enforced here rather than left to content, because content
    /// gets them wrong one event at a time and the player experiences the result as the game
    /// being unfair:
    ///
    /// - Spec 0.9.2: an event whose manual page the player has never been given cannot count a
    ///   wrong answer as a manual violation. It is still wrong, and it still costs; it is not
    ///   a breach of a rule nobody stated.
    /// - Spec 0.10.5 / 0.5: after enough wrong attempts the failsafe opens a way through, and
    ///   the event can always be attempted again. Nothing here can dead-end a night.
    /// </summary>
    public sealed class ManualEventService
    {
        readonly ContentDatabase _content;
        readonly GameClock _clock;
        readonly GameStateService _state;
        readonly ManualService _manual;
        readonly RiskService _risk;

        readonly Dictionary<string, ManualEventRuntime> _events = new Dictionary<string, ManualEventRuntime>();
        readonly List<ManualEventRuntime> _active = new List<ManualEventRuntime>();

        public ManualEventService(ContentDatabase content, GameClock clock, GameStateService state,
                                  ManualService manual, RiskService risk)
        {
            _content = content;
            _clock = clock;
            _state = state;
            _manual = manual;
            _risk = risk;
            BuildRuntimes();
        }

        void BuildRuntimes()
        {
            _events.Clear();
            foreach (var definition in _content.ManualEvents)
            {
                if (definition == null || string.IsNullOrEmpty(definition.eventId)) continue;
                _events[definition.eventId] = new ManualEventRuntime(definition);
            }
        }

        public IReadOnlyList<ManualEventRuntime> ActiveEvents { get { return _active; } }
        public IEnumerable<ManualEventRuntime> AllEvents { get { return _events.Values; } }

        public ManualEventRuntime Find(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            ManualEventRuntime runtime;
            return _events.TryGetValue(eventId, out runtime) ? runtime : null;
        }

        // ---- scheduling ---------------------------------------------------------

        public void Reset()
        {
            _active.Clear();
            BuildRuntimes();
        }

        public void BeginNight(int nightIndex)
        {
            _active.Clear();
            foreach (var runtime in _events.Values)
            {
                var def = runtime.Definition;
                if (!AppearsOn(def, nightIndex)) continue;

                // A repeating event was resolved on a previous night and has to come back
                // clean; a one-off that is still unresolved simply stays where it was.
                if (def.repeatsNightly) runtime.ResetForNight();
                if (!runtime.State.IsResolved()) runtime.SetState(ManualEventState.Dormant, _clock.GameSecond);
            }

            // The head of the chain opens with the shift. Everything else on this night waits
            // for the event before it, so this is the only push the night ever needs.
            if (!AutoStartAllowed) return;

            foreach (var runtime in _events.Values)
            {
                var def = runtime.Definition;
                if (def.trigger != ManualEventTrigger.NightStart) continue;
                if (!AppearsOn(def, nightIndex) || runtime.State != ManualEventState.Dormant) continue;
                if (!ConditionsHold(def)) continue;

                Begin(def.eventId);
            }
        }

        /// <summary>Whether an event belongs to this night (spec 22; M13 spans nights 1-5).</summary>
        static bool AppearsOn(ManualEventDefinition def, int nightIndex)
        {
            if (!def.repeatsNightly) return def.nightIndex == nightIndex;
            int last = def.lastNightIndex > 0 ? def.lastNightIndex : def.nightIndex;
            return nightIndex >= def.nightIndex && nightIndex <= last;
        }

        /// <summary>Called once per frame by GameLoop, after the case scheduler.</summary>
        public void Tick()
        {
            // Nothing here starts an event any more. A night is opened by BeginNight and
            // carried by NotifyEventResolved; the only per-frame job left is watching whether
            // an event in flight has earned its recovery route.
            //
            // Spec 26 failsafe: open it once the attempts have been spent.
            for (int i = 0; i < _active.Count; i++) TryFailSafe(_active[i]);
        }

        /// <summary>
        /// One event closed, so the next one opens.
        ///
        /// Fires on every outcome, not only a correct one. Spec 0.3 says a core event does not
        /// disappear and spec 0.5 says the player always has something left to try - a chain
        /// that stalled on a wrong answer would take the rest of the night with it, which is
        /// the worst version of both.
        /// </summary>
        public void NotifyEventResolved(string eventId)
        {
            if (string.IsNullOrEmpty(eventId) || !AutoStartAllowed) return;

            foreach (var runtime in _events.Values)
            {
                var def = runtime.Definition;
                if (def.trigger != ManualEventTrigger.AfterEvent || def.triggerTarget != eventId) continue;
                if (runtime.State != ManualEventState.Dormant) continue;
                if (!AppearsOn(def, _state.NightIndex)) continue;
                if (!ConditionsHold(def)) continue;

                Begin(def.eventId);
            }
        }

        bool ConditionsHold(ManualEventDefinition def)
        {
            string failReason;
            return ConditionEvaluator.EvaluateAll(def.startConditions, out failReason);
        }

        public void NotifyZoneEntered(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || !AutoStartAllowed) return;
            foreach (var runtime in _events.Values)
            {
                var def = runtime.Definition;
                if (def.trigger != ManualEventTrigger.ZoneEntered || def.triggerTarget != zoneId) continue;
                if (runtime.State != ManualEventState.Available && runtime.State != ManualEventState.Dormant) continue;
                if (!AppearsOn(def, _state.NightIndex)) continue;
                if (!ConditionsHold(def)) continue;

                Begin(def.eventId);
            }
        }

        public void NotifyCaseResolved(string caseId)
        {
            if (string.IsNullOrEmpty(caseId) || !AutoStartAllowed) return;
            foreach (var runtime in _events.Values)
            {
                var def = runtime.Definition;
                if (def.trigger != ManualEventTrigger.CaseResolved || def.triggerTarget != caseId) continue;
                if (runtime.State.IsResolved() || runtime.State == ManualEventState.Active) continue;
                if (!ConditionsHold(def)) continue;

                Begin(def.eventId);
            }
        }

        /// <summary>Show the player something is wrong without registering a task (spec 0.2 Teased).</summary>
        public void Tease(string eventId)
        {
            var runtime = Find(eventId);
            if (runtime == null || runtime.State != ManualEventState.Dormant) return;
            runtime.SetState(ManualEventState.Teased, _clock.GameSecond);
        }

        /// <summary>
        /// Whether the night may open an M event on its own.
        ///
        /// dev.mainonly is the only thing that ever says no. An M event is never a main quest
        /// - spec 22's M01-M18 are the night response manual, which is the work a main is
        /// hidden inside - so a shift running the spine alone must not have one open on top of
        /// it. <see cref="Begin"/> itself is deliberately not gated: a tester who names an
        /// event through the console's manual.start is asking for that one on purpose.
        /// </summary>
        static bool AutoStartAllowed { get { return !MainOnlyMode.Active; } }

        public bool Begin(string eventId)
        {
            var runtime = Find(eventId);
            if (runtime == null)
            {
                Log.Error("Manual", "cannot begin unknown event " + eventId);
                return false;
            }
            if (runtime.State == ManualEventState.Active || runtime.State.IsResolved()) return false;

            runtime.SetState(ManualEventState.Active, _clock.GameSecond);
            if (!_active.Contains(runtime)) _active.Add(runtime);

            EventBus.Publish(new NotificationEvent(
                string.IsNullOrEmpty(runtime.Definition.summaryKey)
                    ? "notify.manual.event_started"
                    : runtime.Definition.summaryKey,
                NotificationSeverity.Warning));
            return true;
        }

        // ---- player actions -------------------------------------------------------

        public void SetCounter(string eventId, string counterId, int value)
        {
            var runtime = Find(eventId);
            if (runtime == null || runtime.State != ManualEventState.Active) return;
            runtime.SetCounter(counterId, value);
        }

        public void AddCounter(string eventId, string counterId, int delta = 1)
        {
            var runtime = Find(eventId);
            if (runtime == null || runtime.State != ManualEventState.Active) return;
            runtime.AddCounter(counterId, delta);
        }

        public int Counter(string eventId, string counterId)
        {
            var runtime = Find(eventId);
            return runtime == null ? 0 : runtime.Counter(counterId);
        }

        public bool CompleteObjective(string eventId, string objectiveId)
        {
            var runtime = Find(eventId);
            if (runtime == null || runtime.State != ManualEventState.Active) return false;
            if (!runtime.CompleteObjective(objectiveId)) return false;

            EventBus.Publish(new ObjectiveChangedEvent(eventId, objectiveId, true));

            // The procedure being finished is what closes the event. There is no separate
            // "submit" for an M event - spec 0.9.4 keeps it a thing you do rather than a thing
            // you conclude, and the last step of every page is the step that ends it.
            if (runtime.AllRequiredDone) Resolve(eventId);
            return true;
        }

        // ---- judging ----------------------------------------------------------------

        /// <summary>
        /// Close the event out and apply what the player earned.
        ///
        /// Which rule was broken is worked out here rather than passed in: the failing clause
        /// of correctConditions carries the manual rule it tests, so the answer comes from the
        /// same place the judgement does and cannot disagree with it.
        /// </summary>
        public ManualEventState Resolve(string eventId)
        {
            var runtime = Find(eventId);
            if (runtime == null || runtime.State != ManualEventState.Active)
                return runtime == null ? ManualEventState.Dormant : runtime.State;

            var def = runtime.Definition;
            string brokenRule;
            var outcome = Judge(runtime, out brokenRule);

            runtime.SetState(outcome, _clock.GameSecond);
            _active.Remove(runtime);

            switch (outcome)
            {
                case ManualEventState.ResolvedCorrect:
                    ServiceHub.Cases.ApplyConsequences(def.onCorrect);
                    break;

                case ManualEventState.ResolvedPartial:
                    ServiceHub.Cases.ApplyConsequences(def.onPartial.Length > 0 ? def.onPartial : def.onWrong);
                    break;

                default:
                    runtime.NoteWrongAttempt();
                    ServiceHub.Cases.ApplyConsequences(def.onWrong);
                    ChargeViolationIfKnown(runtime, brokenRule);
                    break;
            }

            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);

            // Consequences first, then the hand-off: the next event may be gated on a flag or
            // a piece of evidence this one just produced.
            NotifyEventResolved(eventId);
            return outcome;
        }

        ManualEventState Judge(ManualEventRuntime runtime, out string brokenRule)
        {
            brokenRule = null;
            var def = runtime.Definition;

            if (!runtime.AllRequiredDone) return ManualEventState.ResolvedWrong;
            if (Matches(runtime, def.correctConditions, out brokenRule)) return ManualEventState.ResolvedCorrect;

            string ignored;
            if (def.partialConditions.Length > 0 && Matches(runtime, def.partialConditions, out ignored))
                return ManualEventState.ResolvedPartial;

            return ManualEventState.ResolvedWrong;
        }

        /// <summary>
        /// All clauses hold. On failure <paramref name="brokenRule"/> is the manual rule of the
        /// first clause that did not - null when that clause names no rule, which is how an
        /// unfinished job is told from a broken one.
        /// </summary>
        static bool Matches(ManualEventRuntime runtime, ManualCheckDefinition[] checks,
                            out string brokenRule)
        {
            brokenRule = null;
            if (checks == null || checks.Length == 0) return true;

            for (int i = 0; i < checks.Length; i++)
            {
                var check = checks[i];
                if (check == null) continue;

                int left = runtime.Counter(check.counterId);
                int right = string.IsNullOrEmpty(check.otherCounterId)
                    ? check.value
                    : runtime.Counter(check.otherCounterId);

                bool holds;
                switch (check.compare)
                {
                    case ManualCompare.Equals: holds = left == right; break;
                    case ManualCompare.NotEquals: holds = left != right; break;
                    case ManualCompare.AtLeast: holds = left >= right; break;
                    case ManualCompare.AtMost: holds = left <= right; break;
                    default: holds = true; break;
                }

                if (holds) continue;

                brokenRule = check.ruleKey;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Spec 0.9.2 in one method: breaking a rule you were never given is a mistake, not a
        /// violation. The event's own onWrong consequences have already applied either way -
        /// this only decides whether ManualViolationCount moves and the floor gets worse.
        /// </summary>
        void ChargeViolationIfKnown(ManualEventRuntime runtime, string ruleKey)
        {
            if (string.IsNullOrEmpty(ruleKey)) return;

            var def = runtime.Definition;
            if (!_manual.IsUnlocked(def.manualPageId))
            {
                Log.Info("Manual", def.eventId + " wrong, but the page was never issued - no violation");
                return;
            }

            _risk.RecordViolation(def.eventId, ruleKey, 15, def.floorId);
        }

        /// <summary>
        /// Try the event again. Always permitted: spec 0.3 says a core event never disappears,
        /// and spec 0.5 says the player must always have something left to do.
        /// </summary>
        public bool Retry(string eventId)
        {
            var runtime = Find(eventId);
            if (runtime == null || !runtime.State.IsResolved()) return false;
            if (runtime.State == ManualEventState.ResolvedCorrect) return false;

            runtime.ResetForRetry(_clock.GameSecond);
            if (!_active.Contains(runtime)) _active.Add(runtime);
            return true;
        }

        void TryFailSafe(ManualEventRuntime runtime)
        {
            var failSafe = runtime.Definition.failSafe;
            if (failSafe == null || failSafe.afterWrongAttempts <= 0) return;
            if (runtime.FailSafeFired || runtime.WrongAttempts < failSafe.afterWrongAttempts) return;

            runtime.MarkFailSafeFired();

            if (!string.IsNullOrEmpty(failSafe.revealPageId)) _manual.Unlock(failSafe.revealPageId, "failsafe");
            if (!string.IsNullOrEmpty(failSafe.notifyKey))
                EventBus.Publish(new NotificationEvent(failSafe.notifyKey, NotificationSeverity.Info));

            Log.Info("Manual", runtime.Definition.eventId + " failsafe opened: " + failSafe.action);
        }

        /// <summary>
        /// Whether a lethal outcome is permitted right now (spec 0.10.5).
        ///
        /// Three things must all be true: the event opted in, the manual actually marks a
        /// prohibition on this page as lethal, and the player has already broken it once. A
        /// first breach of a rule, however clearly written, never kills.
        /// </summary>
        public bool MayBeLethal(string eventId)
        {
            var runtime = Find(eventId);
            if (runtime == null || !runtime.Definition.lethalOnRepeat) return false;

            var page = _manual.Find(runtime.Definition.manualPageId);
            if (page == null || !page.HasLethalProhibition) return false;

            return runtime.WrongAttempts >= 1;
        }

        // ---- save -------------------------------------------------------------------

        public void LoadFrom(IEnumerable<Save.ManualEventSaveEntry> entries)
        {
            Reset();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                var runtime = Find(entry.eventId);
                if (runtime == null)
                {
                    Log.Warn("Manual", "save references unknown event " + entry.eventId);
                    continue;
                }

                var counters = new List<KeyValuePair<string, int>>();
                if (entry.counterKeys != null && entry.counterValues != null)
                {
                    int count = System.Math.Min(entry.counterKeys.Count, entry.counterValues.Count);
                    for (int i = 0; i < count; i++)
                        counters.Add(new KeyValuePair<string, int>(entry.counterKeys[i], entry.counterValues[i]));
                }

                runtime.RestoreFrom((ManualEventState)entry.state, entry.startedAt, entry.resolvedAt,
                                    entry.wrongAttempts, entry.failSafeFired,
                                    entry.completedObjectives, counters);

                if (runtime.State == ManualEventState.Active && !_active.Contains(runtime))
                    _active.Add(runtime);
            }
        }
    }
}
