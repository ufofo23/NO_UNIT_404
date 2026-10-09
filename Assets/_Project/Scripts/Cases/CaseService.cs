using System;
using System.Collections.Generic;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Cases
{
    public interface ICaseService
    {
        IReadOnlyList<CaseRuntime> ActiveCases { get; }
        bool TryStartCase(string caseId);
        bool TryAdvanceObjective(string caseId, string objectiveId);
        DecisionResult SubmitDecision(string caseId, string decisionId, IReadOnlyList<string> evidenceIds);
    }

    /// <summary>
    /// Runs the case state machine (GDD 11.1) plus the scheduler rules from GDD 20.16:
    /// urgent cases queue behind important conversations, and unresolved P0 cases re-expose
    /// their information through a fail-safe instead of dead-ending the night.
    /// </summary>
    public sealed class CaseService : ICaseService
    {
        /// <summary>
        /// How long a case that was queued behind a conversation waits once it ends.
        ///
        /// Derived rather than written down, so it stays the same beat when the shift rate
        /// changes (GDD 6.2): a breath between the last line and the next task card, not a
        /// number that quietly became a quarter of a minute.
        /// </summary>
        static readonly int DeferredStartDelaySeconds = GameClock.RealSeconds(3);

        readonly ContentDatabase _content;
        readonly GameClock _clock;
        readonly GameStateService _state;

        readonly Dictionary<string, CaseRuntime> _cases = new Dictionary<string, CaseRuntime>();
        readonly List<CaseRuntime> _active = new List<CaseRuntime>();
        readonly List<string> _pendingStarts = new List<string>();
        readonly List<ConsequenceDefinition> _nextNightQueue = new List<ConsequenceDefinition>();

        /// <summary>
        /// Tonight's draw (v5.0 4.1). Empty means the pool has not run - a debug jump, or a
        /// campaign loaded from before the pool existed - and in that case every case
        /// scheduled for the night is playable, which is what the game did before v5.0.
        /// </summary>
        readonly HashSet<string> _drawn = new HashSet<string>(StringComparer.Ordinal);

        int _deferUntilGameSecond;

        public CaseService(ContentDatabase content, GameClock clock, GameStateService state)
        {
            _content = content;
            _clock = clock;
            _state = state;
            BuildRuntimes();
        }

        void BuildRuntimes()
        {
            _cases.Clear();
            foreach (var definition in _content.Cases)
            {
                if (definition == null || string.IsNullOrEmpty(definition.caseId)) continue;
                _cases[definition.caseId] = new CaseRuntime(definition);
            }
        }

        public IReadOnlyList<CaseRuntime> ActiveCases { get { return _active; } }
        public IEnumerable<CaseRuntime> AllCases { get { return _cases.Values; } }

        public CaseRuntime Find(string caseId)
        {
            if (string.IsNullOrEmpty(caseId)) return null;
            CaseRuntime runtime;
            return _cases.TryGetValue(caseId, out runtime) ? runtime : null;
        }

        /// <summary>The single case whose objective the HUD shows.</summary>
        public CaseRuntime TrackedCase { get; private set; }

        public void SetTracked(string caseId)
        {
            var runtime = Find(caseId);
            if (runtime != null && runtime.State.IsActive()) TrackedCase = runtime;
        }

        // ---- lifecycle ---------------------------------------------------

        public void Reset()
        {
            _drawn.Clear();
            _active.Clear();
            _pendingStarts.Clear();
            _nextNightQueue.Clear();
            _nightChain.Clear();
            _chainIndex = 0;
            TrackedCase = null;
            _deferUntilGameSecond = 0;
            BuildRuntimes();
        }

        public void BeginNight(int nightIndex)
        {
            // Apply anything the previous night deferred (GDD 11.2 Consequences).
            //
            // Before the draw, deliberately: v5.0 4.4 step 6 wants a consequence quest to be
            // eligible on the night its cause matures, and the flags that make it eligible are
            // set right here.
            for (int i = 0; i < _nextNightQueue.Count; i++) ApplyConsequence(_nextNightQueue[i]);
            _nextNightQueue.Clear();

            _active.Clear();
            TrackedCase = null;

            DrawNight(nightIndex);
            BuildNightChain(nightIndex);
        }

        /// <summary>
        /// Runs the night's pool and records what it produced (v5.0 4).
        ///
        /// The draw happens here rather than in the pool service's own night hook so that it
        /// sits between the deferred consequences above and the chain below - the three are
        /// one operation and the order between them is the whole of why a choice on night 1
        /// can put an event on night 5.
        /// </summary>
        void DrawNight(int nightIndex)
        {
            _drawn.Clear();

            var pool = ServiceHub.NightPool;
            if (pool == null) return;

            var candidates = new List<CaseDefinition>();
            foreach (var runtime in _cases.Values)
                if (runtime.Definition != null && runtime.Definition.nightIndex == nightIndex)
                    candidates.Add(runtime.Definition);

            if (candidates.Count == 0) return;

            var vitals = ServiceHub.Vitals;
            var selected = pool.SelectForNight(nightIndex, candidates, new NightPoolService.Conditions
            {
                Hp = vitals != null ? vitals.Hp : VitalService.Max,
                San = vitals != null ? vitals.San : VitalService.Max
            });

            for (int i = 0; i < selected.Count; i++) _drawn.Add(selected[i]);
        }

        /// <summary>
        /// Puts a loaded night's draw back without re-running it (v5.0 4.4 step 11).
        /// </summary>
        public void RestoreDraw(IEnumerable<string> selected)
        {
            _drawn.Clear();
            if (selected == null) return;
            foreach (var id in selected) _drawn.Add(id);
        }

        /// <summary>The ids tonight actually drew, for the save and for the acceptance tests.</summary>
        public IEnumerable<string> DrawnTonight { get { return _drawn; } }

        /// <summary>
        /// Tonight's roster: the cases filed under this night that the draw actually placed.
        ///
        /// Anything counting the night's work has to read this rather than every case filed
        /// under the night, because a case the pool did not draw never leaves Dormant. A
        /// count that includes them reports work the player was never given and, worse, the
        /// clock-off button reads the same set - an undrawn case looks exactly like one the
        /// queue has not reached yet, and the shift can then never be closed (v5.0 4.1).
        /// </summary>
        public IEnumerable<CaseRuntime> RosterTonight
        {
            get
            {
                foreach (var runtime in _cases.Values)
                {
                    var def = runtime.Definition;
                    if (def == null || def.nightIndex != _state.NightIndex) continue;
                    if (!IsDrawnTonight(def)) continue;

                    yield return runtime;
                }
            }
        }

        /// <summary>
        /// Whether this case id is one tonight is actually running.
        ///
        /// For everything authored against a case that is not itself a case - a phone call, a
        /// CCTV anomaly - so it can be scheduled with the case it belongs to and skipped with
        /// it. An empty id is not running: something that names no case belongs to the night
        /// at large, and the night at large is what dev.mainonly is switching off.
        ///
        /// The night is checked here as well as the draw, which <see cref="IsDrawnTonight"/>
        /// deliberately does not do - it answers "may this case start", and a case filed under
        /// another night is none of tonight's business either way. This one answers "is this
        /// happening tonight", and for that another night's case is a no.
        /// </summary>
        public bool IsRunningTonight(string caseId)
        {
            if (string.IsNullOrEmpty(caseId)) return false;

            var runtime = Find(caseId);
            if (runtime == null || runtime.Definition == null) return false;
            if (runtime.Definition.nightIndex != _state.NightIndex) return false;

            return IsDrawnTonight(runtime.Definition);
        }

        /// <summary>
        /// Whether a case is part of tonight's shift.
        ///
        /// A case belonging to another night is left alone entirely - the campaign has always
        /// scheduled by night index and that has not changed. What is new is that a case
        /// belonging to *this* night must also have been drawn.
        /// </summary>
        bool IsDrawnTonight(CaseDefinition def)
        {
            if (_drawn.Count == 0) return true;
            if (def == null) return false;
            if (def.nightIndex != _state.NightIndex) return true;

            return _drawn.Contains(def.caseId);
        }

        /// <summary>Called once per frame by GameLoop.</summary>
        public void Tick()
        {
            int now = _clock.GameSecond;

            // Deferred starts (a conversation was open when the case wanted to fire).
            if (_pendingStarts.Count > 0 && now >= _deferUntilGameSecond && !IsConversationBlocking())
            {
                var id = _pendingStarts[0];
                _pendingStarts.RemoveAt(0);
                StartInternal(id);
            }

            // The night's scheduled work is a queue, not a timetable (see PumpChain).
            PumpChain();

            foreach (var runtime in _cases.Values)
            {
                if (runtime.State.IsActive())
                {
                    var def = runtime.Definition;

                    // Investigating -> DecisionReady once every required objective is done.
                    if (runtime.State != CaseState.DecisionReady && runtime.AllRequiredObjectivesComplete()
                        && def.decisions != null && def.decisions.Length > 0)
                    {
                        runtime.SetState(CaseState.DecisionReady, now);
                        EventBus.Publish(new NotificationEvent("ui.notify.decision_ready", NotificationSeverity.Task));
                    }

                    TickFailSafe(runtime, now);
                }
            }

            RefreshTracked();
        }

        /// <summary>
        /// The night's scheduled work, in the order it should arrive.
        ///
        /// The authored start times are what give the order, so nothing had to be re-authored
        /// to build this - but the times themselves no longer gate anything. They used to: a
        /// case whose window had not opened could not start however ready the caretaker was,
        /// and one whose window had closed while they were busy never started at all. The
        /// result was a shift where finishing early bought nothing and the next job arrived
        /// because a clock said so rather than because the last one was done.
        ///
        /// Cases with any other trigger are not in here. A call, an interphone buzz or a case
        /// that waits on another one already has something that starts it.
        /// </summary>
        readonly List<string> _nightChain = new List<string>();
        int _chainIndex;

        /// <summary>
        /// Rebuilds tonight's queue from case states that arrived rather than were played.
        ///
        /// The queue is not saved and not sent - it is derived from the content - so anything
        /// that installs a set of case states from outside has to put it back, or the shift
        /// has no idea what it is holding on. That is a loaded save as much as it is a
        /// mirrored one (v3.0 46.1): the clock-off button and the dashboard both read this,
        /// and both of them went blank after a reload for exactly this reason.
        ///
        /// The index is derived the same way PumpChain derives it, by walking past whatever is
        /// already finished.
        /// </summary>
        public void RebuildNightChain(int nightIndex)
        {
            BuildNightChain(nightIndex, quiet: true);

            while (_chainIndex < _nightChain.Count)
            {
                var runtime = Find(_nightChain[_chainIndex]);
                if (runtime != null && !runtime.State.IsResolved()) break;
                _chainIndex++;
            }
        }

        void BuildNightChain(int nightIndex, bool quiet = false)
        {
            _nightChain.Clear();
            _chainIndex = 0;

            var scheduled = new List<CaseDefinition>();
            foreach (var runtime in _cases.Values)
            {
                var def = runtime.Definition;
                if (def.nightIndex != nightIndex || def.trigger != CaseTrigger.Time) continue;
                if (!IsDrawnTonight(def)) continue;

                scheduled.Add(def);
            }

            scheduled.Sort((a, b) =>
            {
                int byTime = a.startWindowBegin.CompareTo(b.startWindowBegin);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.caseId, b.caseId);
            });

            var ordered = ShiftOrder(scheduled);
            for (int i = 0; i < ordered.Count; i++) _nightChain.Add(ordered[i].caseId);

            // A mirrored shift rebuilds this twice a second; only a night that actually began
            // is worth a line in the log.
            if (quiet) return;

            Log.Info("Cases", "night " + nightIndex + " queue: " +
                              (_nightChain.Count == 0 ? "(none)" : string.Join(" -> ", _nightChain.ToArray())));
        }

        /// <summary>
        /// v5.1 4.2: random A, the story subquest, random B, the main, then the rest.
        ///
        /// Which random lands in which slot is the draw's business; this only fixes where the
        /// two placed quests sit among them, so the main's conflict arrives in the middle of
        /// the shift rather than whenever its authored time happened to sort it.
        /// </summary>
        static List<CaseDefinition> ShiftOrder(List<CaseDefinition> sorted)
        {
            CaseDefinition main = null, story = null;
            var randoms = new List<CaseDefinition>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                var def = sorted[i];
                if (def.isFixedMain && main == null) main = def;
                else if (def.isFixedStory && story == null) story = def;
                else randoms.Add(def);
            }

            if (story == null) return sorted;

            var ordered = new List<CaseDefinition>(sorted.Count);
            int next = 0;
            if (next < randoms.Count) ordered.Add(randoms[next++]);
            ordered.Add(story);
            if (next < randoms.Count) ordered.Add(randoms[next++]);
            if (main != null) ordered.Add(main);
            while (next < randoms.Count) ordered.Add(randoms[next++]);
            return ordered;
        }

        /// <summary>
        /// Hand over the next job, if the last one is done with.
        ///
        /// One at a time and immediately: the moment a case reaches a resolved state the next
        /// is registered, so the pace of the shift belongs to how fast the caretaker works.
        /// A case that cannot start yet holds the queue rather than being skipped - its
        /// condition may well become true later, and skipping it would lose the night's work
        /// silently (spec 0.3).
        /// </summary>
        void PumpChain()
        {
            while (_chainIndex < _nightChain.Count)
            {
                var runtime = Find(_nightChain[_chainIndex]);

                if (runtime == null) { _chainIndex++; continue; }

                if (runtime.State.IsResolved()) { _chainIndex++; continue; }

                if (runtime.State == CaseState.Dormant || runtime.State == CaseState.Available)
                {
                    if (CanStart(runtime)) TryStartCase(runtime.CaseId);
                    return;     // whether it started or not, the queue waits here
                }

                return;         // it is open; nothing else arrives until it closes
            }
        }

        /// <summary>The case the queue is currently holding on, for the HUD and for tests.</summary>
        public CaseRuntime CurrentQueuedCase
        {
            get { return _chainIndex < _nightChain.Count ? Find(_nightChain[_chainIndex]) : null; }
        }

        /// <summary>
        /// Tonight's cases in the order they will be handed out.
        ///
        /// startWindowBegin only sorts this list; it is not a clock. A case starts when the one
        /// before it is closed, so the night is paced by the caretaker rather than by the hour -
        /// which is why anything that wants to happen "when the last case starts" has to watch
        /// this order rather than a time.
        /// </summary>
        public IReadOnlyList<string> NightChain { get { return _nightChain; } }

        /// <summary>
        /// Only the first case of the night was ever auto-tracked, and tracking stayed on it
        /// after it closed - so the evidence board's report panel went dead for every case
        /// after the first unless the player noticed the Track button. A case waiting on a
        /// decision wins; otherwise any still-open case will do.
        /// </summary>
        void RefreshTracked()
        {
            if (TrackedCase != null && TrackedCase.State.IsActive()) return;

            CaseRuntime fallback = null;

            for (int i = 0; i < _active.Count; i++)
            {
                var runtime = _active[i];
                if (!runtime.State.IsActive()) continue;

                if (runtime.State == CaseState.DecisionReady) { TrackedCase = runtime; return; }
                if (fallback == null) fallback = runtime;
            }

            TrackedCase = fallback;
        }

        /// <summary>
        /// When this case stops waiting, in game seconds. GDD 24.2 stretches or tightens the
        /// window with the difficulty option. 0 means the case has no deadline at all.
        ///
        /// Public because GDD 16.7 puts it on the dashboard card: a deadline the player cannot
        /// see is not pressure, it is an ambush.
        /// </summary>
        public static int DeadlineFor(CaseRuntime runtime)
        {
            if (runtime == null) return 0;

            var fs = runtime.Definition.failSafe;
            if (fs == null || !fs.enabled || fs.triggerGameSecond <= 0) return 0;

            // How long the author gave the player, rather than the hour it used to expire at.
            // With the queue driving arrivals a case can open at any point in the shift, and an
            // absolute deadline would have already passed for anything that arrived late -
            // firing the fail-safe the moment the job was handed over.
            int window = fs.triggerGameSecond - runtime.Definition.startWindowBegin;
            int allowed = (int)(window * DifficultyProfile.DeadlineFactor);

            int anchor = runtime.StartedAtGameSecond > 0
                ? runtime.StartedAtGameSecond
                : runtime.Definition.startWindowBegin;

            return anchor + allowed;
        }

        /// <summary>Game seconds left on the deadline. int.MaxValue when there is no deadline.</summary>
        public int SecondsToDeadline(CaseRuntime runtime)
        {
            int deadline = DeadlineFor(runtime);
            if (deadline <= 0) return int.MaxValue;
            return deadline - _clock.GameSecond;
        }

        /// <summary>The open case that runs out of time first, or null when nothing is ticking.</summary>
        public CaseRuntime MostUrgentCase()
        {
            CaseRuntime soonest = null;
            int best = int.MaxValue;

            for (int i = 0; i < _active.Count; i++)
            {
                var runtime = _active[i];
                if (!runtime.State.IsActive() || runtime.FailSafeFired) continue;

                int left = SecondsToDeadline(runtime);
                if (left >= best) continue;

                best = left;
                soonest = runtime;
            }

            return soonest;
        }

        void TickFailSafe(CaseRuntime runtime, int now)
        {
            var fs = runtime.Definition.failSafe;
            if (fs == null || !fs.enabled || runtime.FailSafeFired) return;
            if (fs.triggerGameSecond <= 0) return;

            int deadline = DeadlineFor(runtime);
            if (now < deadline) return;

            runtime.MarkFailSafeFired();
            Log.Warn("Cases", runtime.CaseId + " fail-safe fired at " + GameClock.FormatSecond(now));

            if (!string.IsNullOrEmpty(fs.alternateEvidenceId))
                ServiceHub.Evidence.Acquire(fs.alternateEvidenceId, Evidence.EvidenceSource.FailSafe);

            if (!string.IsNullOrEmpty(fs.notifyKey))
                EventBus.Publish(new NotificationEvent(fs.notifyKey, NotificationSeverity.Warning));

            // Complete any objective the player can no longer reach so the night can end.
            var objectives = runtime.Definition.objectives;
            if (objectives != null)
                for (int i = 0; i < objectives.Length; i++)
                    if (objectives[i] != null && !objectives[i].optional)
                        Complete(runtime, objectives[i]);

            // GDD 15.4: a deadline that came and went is the building doing the caretaker's
            // job for them, and the corridor notices. 11.5 already removed the credit; this
            // adds the cost.
            if (ServiceHub.Pressure != null) ServiceHub.Pressure.NoteDeadlineMissed();

            ServiceHub.Analytics.Track("case_failsafe", runtime.CaseId);
        }

        static bool IsConversationBlocking()
        {
            var dialogue = ServiceHub.Dialogue;
            return dialogue != null && dialogue.IsBlocking;
        }

        bool CanStart(CaseRuntime runtime)
        {
            var def = runtime.Definition;

            // v5.0 4.1: fifteen are authored and five or six happen. An undrawn quest is not
            // blocked, it is not part of tonight at all - which is why this is tested before
            // the conditions rather than as one of them.
            if (!IsDrawnTonight(def))
            {
                Log.Trace("Cases", def.caseId + " was not drawn for tonight");
                return false;
            }

            string reason;
            if (!ConditionEvaluator.EvaluateAll(def.startConditions, out reason))
            {
                Log.Trace("Cases", def.caseId + " blocked: " + reason);
                return false;
            }

            if (def.blockedBy != null && def.blockedBy.Length > 0 && ConditionEvaluator.EvaluateAny(def.blockedBy))
            {
                Log.Trace("Cases", def.caseId + " blocked by blockedBy condition");
                return false;
            }

            return true;
        }

        public bool TryStartCase(string caseId)
        {
            var runtime = Find(caseId);
            if (runtime == null) { Log.Warn("Cases", "unknown case " + caseId); return false; }
            if (runtime.State != CaseState.Dormant && runtime.State != CaseState.Available) return false;
            if (!CanStart(runtime)) return false;

            if (IsConversationBlocking())
            {
                if (!_pendingStarts.Contains(caseId))
                {
                    _pendingStarts.Add(caseId);
                    _deferUntilGameSecond = _clock.GameSecond + DeferredStartDelaySeconds;
                    Log.Info("Cases", caseId + " queued behind an active conversation");
                }
                return false;
            }

            StartInternal(caseId);
            return true;
        }

        void StartInternal(string caseId)
        {
            var runtime = Find(caseId);
            if (runtime == null || runtime.State.IsActive() || runtime.State.IsResolved()) return;

            runtime.SetState(CaseState.Accepted, _clock.GameSecond);
            runtime.SetState(CaseState.Investigating, _clock.GameSecond);
            if (!_active.Contains(runtime)) _active.Add(runtime);
            if (TrackedCase == null) TrackedCase = runtime;

            if (SelectedMainQuestRules.Handles(caseId) || SubquestRules.Handles(caseId))
                foreach (var objective in runtime.Definition.objectives)
                    if (objective.type == ObjectiveType.AcquireEvidence && ServiceHub.Evidence.Has(objective.targetId))
                        runtime.CompleteObjective(objective.objectiveId);
            SelectedMainQuestRules.Started(caseId);
            SubquestRules.Started(caseId);
            EventBus.Publish(new CaseStartedEvent(caseId));
            EventBus.Publish(new NotificationEvent("ui.notify.new_task", NotificationSeverity.Task));
            ServiceHub.Analytics.Track(AnalyticsService.Events.CaseStarted, caseId);
            Log.Info("Cases", "started " + caseId);
        }

        // ---- triggers driven by other systems ----------------------------

        public void NotifyZoneEntered(string zoneId) { FireTrigger(CaseTrigger.ZoneEntered, zoneId); }
        public void NotifyAppOpened(string appId) { FireTrigger(CaseTrigger.AppOpened, appId); }
        public void NotifyPhoneAnswered(string conversationId) { FireTrigger(CaseTrigger.PhoneCall, conversationId); }
        public void NotifyInterphone(string visitorId) { FireTrigger(CaseTrigger.Interphone, visitorId); }

        void FireTrigger(CaseTrigger trigger, string target)
        {
            foreach (var runtime in _cases.Values)
            {
                var def = runtime.Definition;
                if (runtime.State != CaseState.Dormant) continue;
                if (def.trigger != trigger) continue;
                if (def.nightIndex != _state.NightIndex) continue;
                if (!string.IsNullOrEmpty(def.triggerTarget) && def.triggerTarget != target) continue;
                TryStartCase(def.caseId);
            }
        }

        /// <summary>
        /// Advances any active case that is waiting on this objective type/target.
        /// Called by the CCTV, DB, interaction and dialogue systems.
        /// </summary>
        public void NotifyObjective(ObjectiveType type, string targetId)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var runtime = _active[i];
                var objectives = runtime.Definition.objectives;
                if (objectives == null) continue;

                for (int o = 0; o < objectives.Length; o++)
                {
                    var obj = objectives[o];
                    if (obj == null || obj.type != type) continue;
                    if (!string.IsNullOrEmpty(obj.targetId) && obj.targetId != targetId) continue;
                    if (runtime.IsObjectiveComplete(obj.objectiveId)) continue;

                    Complete(runtime, obj);
                    Log.Info("Cases", runtime.CaseId + " objective done: " + obj.objectiveId);
                }
            }
        }

        public bool TryAdvanceObjective(string caseId, string objectiveId)
        {
            var runtime = Find(caseId);
            if (runtime == null || !runtime.State.IsActive()) return false;
            return Complete(runtime, runtime.Definition.FindObjective(objectiveId));
        }

        /// <summary>
        /// Completes an objective and applies what completing it costs or gives.
        ///
        /// Some things happen to the caretaker when they find something out, not when they
        /// file the report about it: v5.1 11 charges SAN for first seeing the same man in two
        /// places, whatever is decided afterwards. That belongs to the step, so it is authored
        /// on the step and applied exactly once, however the step came to be completed.
        /// </summary>
        bool Complete(CaseRuntime runtime, ObjectiveDefinition objective)
        {
            if (objective == null || !runtime.CompleteObjective(objective.objectiveId)) return false;

            ApplyConsequences(objective.onComplete);
            return true;
        }

        // ---- decisions ---------------------------------------------------

        public DecisionResult SubmitDecision(string caseId, string decisionId, IReadOnlyList<string> evidenceIds)
        {
            var runtime = Find(caseId);
            if (runtime == null) return DecisionResult.Rejected("ui.report.error.unknown_case");
            if (runtime.State.IsResolved()) return DecisionResult.Rejected("ui.report.error.already_resolved");

            var decision = runtime.Definition.FindDecision(decisionId);
            if (decision == null) return DecisionResult.Rejected("ui.report.error.unknown_decision");

            if ((SelectedMainQuestRules.Handles(caseId) || SubquestRules.Handles(caseId)) &&
                (!runtime.State.IsActive() || !runtime.AllRequiredObjectivesComplete()))
                return DecisionResult.Rejected("ui.report.error.investigate_first");
            string lostKey = SubquestRules.ReportBlockedKey(caseId);
            if (lostKey != null) return DecisionResult.Rejected(lostKey);
            if (caseId == "N3-M01" && _state.GetFlag("N3_PLAYER_LOST"))
                return DecisionResult.Rejected("quest.lost_marker");

            string reason;
            if (!ConditionEvaluator.EvaluateAll(decision.availability, out reason))
            {
                Log.Info("Cases", "decision unavailable: " + reason);
                return DecisionResult.Rejected("ui.report.error.unavailable");
            }

            if (decision.requiredEvidenceIds != null && decision.requiredEvidenceIds.Length > 0)
            {
                for (int i = 0; i < decision.requiredEvidenceIds.Length; i++)
                {
                    var required = decision.requiredEvidenceIds[i];
                    if (evidenceIds == null || !Contains(evidenceIds, required))
                        return DecisionResult.Rejected("ui.report.error.missing_evidence");
                }
            }

            if (evidenceIds != null)
                for (int i = 0; i < evidenceIds.Count; i++) runtime.AttachEvidence(evidenceIds[i]);

            runtime.SetDecision(decisionId);

            // GDD 14.4 forbids a dead end, so the fail-safe still hands over the facts. It
            // does not hand over the credit: a case the building solved for the player closes
            // as Partial at best and adds nothing to the archive. Missing something now costs
            // an outcome instead of costing nothing at all.
            var quality = decision.quality;
            bool rescued = runtime.FailSafeFired && quality == DecisionQuality.Correct;
            if (rescued)
            {
                quality = DecisionQuality.Partial;
                EventBus.Publish(new NotificationEvent("ui.notify.failsafe_no_credit",
                                                       NotificationSeverity.Warning));
            }

            var resolvedState = quality == DecisionQuality.Correct ? CaseState.ResolvedCorrect
                              : quality == DecisionQuality.Partial ? CaseState.ResolvedPartial
                              : CaseState.ResolvedWrong;

            runtime.SetState(resolvedState, _clock.GameSecond);
            _active.Remove(runtime);
            if (TrackedCase == runtime) TrackedCase = _active.Count > 0 ? _active[0] : null;

            runtime.SetState(CaseState.ConsequenceQueued, _clock.GameSecond);
            ApplyConsequences(Earned(decision.consequences, runtime.FailSafeFired));
            ApplyConsequences(Earned(runtime.Definition.consequences, runtime.FailSafeFired));
            runtime.SetState(CaseState.ConsequenceApplied, _clock.GameSecond);

            SelectedMainQuestRules.Resolved(caseId, decisionId);
            SubquestRules.Resolved(caseId, decisionId);

            // A case waiting on CaseTrigger.CaseResolved (e.g. C04 waits on C03) only ever
            // gets a chance to start here.
            FireTrigger(CaseTrigger.CaseResolved, caseId);

            // And the night's queue moves on now, not on the next frame - the caretaker should
            // see the next job land as they file the last one.
            PumpChain();
            ServiceHub.ManualEvents.NotifyCaseResolved(caseId);

            // GDD 15.4: closing a case properly is the largest single relief in the game.
            if (ServiceHub.Pressure != null && quality == DecisionQuality.Correct)
                ServiceHub.Pressure.NoteCaseClosedWell();

            ServiceHub.Hints.RegisterDecision(quality);
            ServiceHub.Analytics.Track(AnalyticsService.Events.DecisionSelected, caseId + ":" + decisionId);
            ServiceHub.Analytics.Track(AnalyticsService.Events.CaseCompleted, caseId);
            Log.Info("Cases", caseId + " resolved as " + decisionId + " (" + quality +
                              (rescued ? ", downgraded: fail-safe" : string.Empty) + ")");

            return DecisionResult.Ok(quality, decision.resultKey);
        }

        static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == value) return true;
            return false;
        }

        // ---- consequences ------------------------------------------------

        /// <summary>
        /// Strips the rewards a rescued case did not earn.
        ///
        /// Only positive ArchiveIntegrity is removed, and only when the fail-safe fired.
        /// That stat is the measure of the player's own diligence - it is what separates the
        /// truth ending from the quiet one - so a fact the building volunteered at 01:00 must
        /// not raise it. Everything else survives: flags, evidence, story triggers and the
        /// penalties all still apply, because the case still happened.
        /// </summary>
        static ConsequenceDefinition[] Earned(ConsequenceDefinition[] consequences, bool failSafeFired)
        {
            if (!failSafeFired || consequences == null || consequences.Length == 0) return consequences;

            var kept = new List<ConsequenceDefinition>(consequences.Length);
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c != null && c.type == ConsequenceType.StatDelta
                    && c.targetId == StatIds.ArchiveIntegrity && c.amount > 0) continue;

                kept.Add(c);
            }

            return kept.ToArray();
        }

        public void ApplyConsequences(ConsequenceDefinition[] consequences)
        {
            if (consequences == null) return;
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c == null) continue;
                if (c.nextNight) { _nextNightQueue.Add(c); continue; }
                ApplyConsequence(c);
            }
        }

        void ApplyConsequence(ConsequenceDefinition c)
        {
            string unmet;
            if (!ConditionEvaluator.EvaluateAll(c.when, out unmet)) return;

            switch (c.type)
            {
                case ConsequenceType.StatDelta:
                    _state.AddStat(c.targetId, c.amount, string.IsNullOrEmpty(c.reasonKey) ? "case" : c.reasonKey);
                    break;

                case ConsequenceType.SetFlag:
                    _state.SetFlag(c.targetId, c.boolValue);
                    break;

                case ConsequenceType.GrantAccess:
                    _state.GrantAccess((AccessLevel)c.amount);
                    break;

                case ConsequenceType.StartCase:
                    TryStartCase(c.targetId);
                    break;

                case ConsequenceType.GrantEvidence:
                    ServiceHub.Evidence.Acquire(c.targetId, Evidence.EvidenceSource.Dialogue);
                    break;

                case ConsequenceType.UnlockAchievement:
                    ServiceHub.Steam.Unlock(c.targetId);
                    break;

                case ConsequenceType.Notify:
                    EventBus.Publish(new NotificationEvent(c.targetId, NotificationSeverity.Info));
                    break;

                // ---- v2.1 (spec 0.10) --------------------------------------

                case ConsequenceType.AddFloorRisk:
                    ServiceHub.Risk.AddFloorRisk(c.targetId, c.amount,
                        string.IsNullOrEmpty(c.reasonKey) ? "case" : c.reasonKey);
                    break;

                case ConsequenceType.AddDistortion:
                    ServiceHub.Risk.AddExposure(c.amount,
                        string.IsNullOrEmpty(c.reasonKey) ? "case" : c.reasonKey);
                    break;

                case ConsequenceType.RecordViolation:
                    // amount, when set, is the exposure cost; 0 falls back to the standard 15.
                    ServiceHub.Risk.RecordViolation(null, c.targetId,
                        c.amount > 0 ? c.amount : 15);
                    break;

                case ConsequenceType.UnlockManualPage:
                    ServiceHub.Manual.Unlock(c.targetId, "case");
                    break;

                case ConsequenceType.StartSequence:
                    EventBus.Publish(new NotificationEvent(c.targetId, NotificationSeverity.Warning));
                    break;

                case ConsequenceType.AddMemoryDebt:
                    ServiceHub.Risk.AddMemoryDebt(c.amount);
                    break;

                case ConsequenceType.SetChoice:
                    _state.SetChoice(c.targetId, c.stringValue);
                    break;

                case ConsequenceType.Hp:
                    if (c.amount < 0) ServiceHub.Vitals.Damage(-c.amount, c.reasonKey ?? "reason.injury");
                    else ServiceHub.Vitals.Heal(c.amount, c.reasonKey ?? "reason.treated");
                    break;

                case ConsequenceType.San:
                    if (c.amount < 0) ServiceHub.Vitals.Strain(-c.amount, c.reasonKey ?? "reason.strain");
                    else ServiceHub.Vitals.Calm(c.amount, c.reasonKey ?? "reason.steadied");
                    break;

                case ConsequenceType.AddToolDebt:
                    ServiceHub.Risk.AddToolDebt(c.amount);
                    break;
            }
        }

        public IReadOnlyList<ConsequenceDefinition> NextNightQueue { get { return _nextNightQueue; } }

        /// <summary>
        /// Put back the consequences an earlier night deferred (v2.1 spec 30.3).
        ///
        /// Separate from <see cref="LoadFrom"/> because that one calls Reset, which empties
        /// this queue - and because the queue is not per-case state. It is a promise the world
        /// made about the next shift, and until v2.1 it lived only in memory: a caretaker who
        /// left a car in the fire lane on night 1, saved, and came back the following evening
        /// found the lane clear. Spec 30.3 opens with exactly that scenario.
        /// </summary>
        public void LoadNextNightQueue(IEnumerable<ConsequenceDefinition> deferred)
        {
            _nextNightQueue.Clear();
            if (deferred == null) return;

            foreach (var consequence in deferred)
            {
                if (consequence == null) continue;

                // A queued entry has already been recognised as deferred once. Keeping the
                // flag set is what stops BeginNight from re-queueing it forever, since
                // ApplyConsequence is reached directly from there rather than through
                // ApplyConsequences.
                _nextNightQueue.Add(consequence);
            }

            Log.Info("Cases", _nextNightQueue.Count + " deferred consequence(s) restored");
        }

        // ---- save --------------------------------------------------------

        public void LoadFrom(IEnumerable<Save.CaseSaveEntry> entries)
        {
            Reset();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                var runtime = Find(entry.caseId);
                if (runtime == null) { Log.Warn("Cases", "save references unknown case " + entry.caseId); continue; }

                runtime.RestoreFrom((CaseState)entry.state, entry.decisionId, entry.startedAt, entry.resolvedAt,
                                    entry.failSafeFired, entry.completedObjectives, entry.attachedEvidence);

                if (runtime.State.IsActive() && !_active.Contains(runtime)) _active.Add(runtime);
            }

            TrackedCase = _active.Count > 0 ? _active[0] : null;
        }
    }
}
