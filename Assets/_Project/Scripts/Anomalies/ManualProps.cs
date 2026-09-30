using UnityEngine;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Anomalies
{
    /// <summary>
    /// Base for anything in the world that belongs to one M event.
    ///
    /// Every prop here is built into its zone once, when the greybox is generated, and spends
    /// most of the game switched off. It wakes when its event goes Active and goes quiet again
    /// when the event resolves - which is what lets a floor be an ordinary corridor on night 2
    /// and the site of an anomaly on night 5 without the streamed scene changing shape
    /// (v2.1 spec 0.7.2).
    ///
    /// Subscribing to the event lifecycle rather than polling also makes a load work for free:
    /// the runtime comes back Active and publishes, and everything belonging to it turns
    /// itself back on with the counters it had (spec 30.2 case 5).
    /// </summary>
    public abstract class ManualProp : MonoBehaviour
    {
        [SerializeField] protected string _eventId;

        /// <summary>Props the player is not meant to see until the event starts.</summary>
        [SerializeField] protected bool _hiddenUntilActive = true;

        /// <summary>
        /// A second counter this prop raises alongside its own.
        ///
        /// Several procedures in spec 22 have one act mean two things - posting the notice is
        /// both the choice of cause and the fact that a notice went up; fitting cartridge C
        /// after A is both "C is in" and "the order was right". Carrying that as one optional
        /// field beats a bespoke component per case.
        /// </summary>
        [SerializeField] protected string _alsoSetsCounterId;

        protected ManualEventRuntime Runtime
        {
            get { return ServiceHub.ManualEvents == null ? null : ServiceHub.ManualEvents.Find(_eventId); }
        }

        protected bool EventIsActive
        {
            get
            {
                var runtime = Runtime;
                return runtime != null && runtime.State == ManualEventState.Active;
            }
        }

        public string EventId { get { return _eventId; } }

        protected virtual void OnEnable()
        {
            EventBus.Subscribe<ManualEventStateChangedEvent>(HandleStateChanged);
            EventBus.Subscribe<SaveRestoredEvent>(HandleSaveRestored);
            SyncToEventState();
        }

        /// <summary>
        /// Sync once more, now that the prop knows which event it belongs to.
        ///
        /// The sync in OnEnable happens too early to be the only one. Every prop is created by
        /// AddComponent followed by Setup, and Unity enables the component on the first of
        /// those - so that pass asks whether an event named null is running, decides it is
        /// not, and switches the prop off. Nothing ever asked again: the next sync waits on a
        /// ManualEventStateChangedEvent, and an event that was already Active before the floor
        /// was built has no state left to change.
        ///
        /// The result was a caretaker who walked off an anomaly's floor mid-procedure and came
        /// back to find every prop dead - the event still running, the objectives still open,
        /// and nothing in the room that would take an interaction. Start runs after the
        /// builder has finished the frame, which is the first moment the answer is knowable.
        /// </summary>
        protected virtual void Start()
        {
            SyncToEventState();
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<ManualEventStateChangedEvent>(HandleStateChanged);
            EventBus.Unsubscribe<SaveRestoredEvent>(HandleSaveRestored);
        }

        void HandleStateChanged(ManualEventStateChangedEvent evt)
        {
            if (evt.EventId == _eventId) SyncToEventState();
        }

        void HandleSaveRestored(SaveRestoredEvent evt) { SyncToEventState(); }

        protected void SyncToEventState()
        {
            bool active = EventIsActive;

            if (_hiddenUntilActive)
            {
                // Children as well as the root. A greybox prop is one cube with one renderer
                // on it, so reading only the root was correct for exactly as long as every
                // prop was a cube: a model has its meshes in children, and a prop that went on
                // hiding only its non-existent root renderer would stand in the room all night
                // before its event ever started (spec 0.7.2).
                var renderers = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = active;

                var colliders = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = active;
            }

            OnEventActiveChanged(active);
        }

        protected virtual void OnEventActiveChanged(bool active) { }

        // ---- counter helpers -------------------------------------------------

        protected int Counter(string counterId)
        {
            var runtime = Runtime;
            return runtime == null ? 0 : runtime.Counter(counterId);
        }

        // Every write a prop can make goes to the host and comes back in the mirror (v3.0
        // 46.1). A procedure is one procedure however many caretakers are walking it: two
        // people working opposite ends of the same treadmill room must be filling in the same
        // form, not one each.

        protected void SetCounter(string counterId, int value)
        {
            Net.NetShift.Request(Net.NetShift.ShiftAct.ManualCounterSet, _eventId, counterId, value);
        }

        protected void AddCounter(string counterId, int delta = 1)
        {
            Net.NetShift.Request(Net.NetShift.ShiftAct.ManualCounterAdd, _eventId, counterId, delta);
        }

        protected void CompleteObjective(string objectiveId)
        {
            if (!string.IsNullOrEmpty(objectiveId))
                Net.NetShift.Request(Net.NetShift.ShiftAct.ManualObjective, _eventId, objectiveId);
        }

        protected void RaiseAlso()
        {
            if (!string.IsNullOrEmpty(_alsoSetsCounterId)) SetCounter(_alsoSetsCounterId, 1);
        }

        /// <summary>
        /// End the event here and now.
        ///
        /// Used by the shortcuts the manual forbids. Spec 22 makes several of them genuinely
        /// finish the job - the emergency stop does stop the pump, the wall breaker does stop
        /// the treadmills - and the judgement then fails on an unfinished procedure, which is
        /// the honest outcome rather than a button that refuses to be pressed.
        /// </summary>
        protected void EndEventNow()
        {
            Net.NetShift.Request(Net.NetShift.ShiftAct.ManualResolve, _eventId);
        }

        /// <summary>
        /// What this prop can write: the counters it raises and the steps it can tick off.
        ///
        /// Exists so the coverage can be tested rather than trusted. An event whose judgement
        /// reads a counter nothing in the world sets is unwinnable, and an event with a step
        /// nothing completes never resolves at all - both look completely fine in the data and
        /// are only visible by walking the floor. ManualStageCoverageTests walks it instead.
        /// </summary>
        public virtual void CollectWrites(System.Collections.Generic.List<string> counters,
                                          System.Collections.Generic.List<string> objectives)
        {
            if (!string.IsNullOrEmpty(_alsoSetsCounterId)) counters.Add(_alsoSetsCounterId);
        }

        protected static void Add(System.Collections.Generic.List<string> into, string value)
        {
            if (!string.IsNullOrEmpty(value)) into.Add(value);
        }
    }

    /// <summary>
    /// Ticks a step off once a tally has reached its target.
    ///
    /// M04 is the reason: the number of marks on the ceiling is decided when the event starts
    /// (seven, or ten if the 404 record was deleted - spec 31), so no single mark can be the
    /// one that finishes the cleaning. Something has to watch the count against the target,
    /// and that something must not be the mark itself.
    /// </summary>
    public sealed class ManualThresholdProp : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] string _targetCounterId;
        [SerializeField] int _target = 1;
        [SerializeField] string _objectiveId;
        [SerializeField] string _doneKey;

        bool _fired;

        public ManualThresholdProp Setup(string eventId, string counterId, string objectiveId,
                                         string targetCounterId = null, int target = 1,
                                         string doneKey = null)
        {
            _eventId = eventId;
            _counterId = counterId;
            _objectiveId = objectiveId;
            _targetCounterId = targetCounterId;
            _target = target;
            _doneKey = doneKey;
            _hiddenUntilActive = false;
            return this;
        }

        int Target
        {
            get
            {
                return string.IsNullOrEmpty(_targetCounterId) ? _target : Counter(_targetCounterId);
            }
        }

        void Update()
        {
            if (!EventIsActive || _fired) return;

            int target = Target;
            if (target <= 0 || Counter(_counterId) < target) return;

            _fired = true;
            CompleteObjective(_objectiveId);
            if (!string.IsNullOrEmpty(_doneKey))
                EventBus.Publish(new NotificationEvent(_doneKey, NotificationSeverity.Info));
        }

        protected override void OnEventActiveChanged(bool active)
        {
            if (!active) _fired = false;
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(objectives, _objectiveId);
        }
    }

    /// <summary>
    /// The workhorse: an interactable that moves one counter and optionally ticks off one
    /// step of the procedure.
    ///
    /// Seals, stamps, barriers, stickers, notices, cartridges, cuttings, the return shelf -
    /// all of spec 22 that amounts to "do this to that" is this component with different
    /// numbers, which is the point. Eighteen bespoke scripts would each have their own way of
    /// being subtly wrong.
    /// </summary>
    public sealed class ManualActionProp : ManualProp, IInteractable
    {
        [SerializeField] string _labelKey = "ui.prompt.interact";
        [SerializeField] string _counterId;
        [SerializeField] int _delta = 1;
        [SerializeField] int _maxCount;                 // 0 = single use
        [SerializeField] string _objectiveId;
        [SerializeField] string _requiresCounterId;
        [SerializeField] int _requiresAtLeast = 1;
        [SerializeField] string _blockedReasonKey = "ui.prompt.nothing_here";
        [SerializeField] int _objectiveAt;              // 0 = at the full count
        [SerializeField] string _completionCounterId;
        [SerializeField] float _range = 1.8f;
        [SerializeField] bool _repeatable;

        public ManualActionProp Setup(string eventId, string labelKey, string counterId,
                          string objectiveId = null, int delta = 1, int maxCount = 0,
                          bool repeatable = false)
        {
            _eventId = eventId;
            _labelKey = labelKey;
            _counterId = counterId;
            _objectiveId = objectiveId;
            _delta = delta;
            _maxCount = maxCount;
            _repeatable = repeatable || maxCount > 1;
            return this;
        }

        /// <summary>Gate this action behind another counter having got far enough.</summary>
        public ManualActionProp RequiresCounter(string counterId, string blockedReasonKey,
                                                int atLeast = 1)
        {
            _requiresCounterId = counterId;
            _blockedReasonKey = blockedReasonKey;
            _requiresAtLeast = atLeast;
            return this;
        }

        /// <summary>
        /// Tick the step off before the counter is full.
        ///
        /// Spec 22 M03 wants two of three instructions carried out and M15 six of eight
        /// malformations cut. In both, doing more is allowed and doing the minimum must be
        /// enough to finish - so the step completes at the bar rather than at the maximum.
        /// </summary>
        public ManualActionProp ObjectiveAt(int count)
        {
            _objectiveAt = count;
            return this;
        }

        /// <summary>Raise a tally elsewhere once this prop has been fully worked.</summary>
        public ManualActionProp OnCompleteAdds(string counterId)
        {
            _completionCounterId = counterId;
            return this;
        }

        public ManualActionProp WithRange(float range)
        {
            _range = range;
            return this;
        }

        /// <summary>Leave the prop visible before the event starts (a locker, a valve, a bin).</summary>
        public ManualActionProp AlwaysVisible()
        {
            _hiddenUntilActive = false;
            return this;
        }

        public ManualActionProp Also(string counterId)
        {
            _alsoSetsCounterId = counterId;
            return this;
        }

        public float Range { get { return _range; } }

        public InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            return CanInteract(context, out reasonKey)
                ? InteractionPrompt.Simple(_labelKey)
                : InteractionPrompt.Blocked(_labelKey, reasonKey);
        }

        public bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;

            if (!EventIsActive) { reasonKey = "ui.prompt.nothing_here"; return false; }

            int value = Counter(_counterId);
            if (_maxCount > 0 && value >= _maxCount) { reasonKey = "ui.prompt.already_done"; return false; }
            if (!_repeatable && value != 0) { reasonKey = "ui.prompt.already_done"; return false; }

            if (!string.IsNullOrEmpty(_requiresCounterId) &&
                Counter(_requiresCounterId) < _requiresAtLeast)
            {
                reasonKey = _blockedReasonKey;
                return false;
            }

            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;

            AddCounter(_counterId, _delta);
            RaiseAlso();

            int value = Counter(_counterId);
            int full = _maxCount > 0 ? _maxCount : 1;

            if (value >= full && !string.IsNullOrEmpty(_completionCounterId))
                AddCounter(_completionCounterId);

            // The step is done when the counter reaches its bar, so a four-bag job ticks off
            // on the fourth bag rather than the first.
            int bar = _objectiveAt > 0 ? _objectiveAt : full;
            if (value >= bar) CompleteObjective(_objectiveId);
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(counters, _completionCounterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// An action the manual forbids, which is nonetheless available and works.
    ///
    /// Spec 0.4 will not let the UI mark the wrong answer, and spec 22 leans on that
    /// repeatedly: the emergency stop in the pump room is red and stops the pump, the wall
    /// breaker is the most visible control in the gym, and opening the parcel is one press.
    /// The whole design of those beats is that the prohibited thing is the easy thing - so
    /// this is an ordinary unmarked interactable that happens to set a counter the judgement
    /// fails on.
    /// </summary>
    public sealed class ManualForbiddenProp : ManualProp, IInteractable
    {
        [SerializeField] string _labelKey = "ui.prompt.interact";
        [SerializeField] string _counterId;
        [SerializeField] string _objectiveId;
        [SerializeField] bool _endsEvent;
        [SerializeField] float _range = 1.8f;

        public ManualForbiddenProp Setup(string eventId, string labelKey, string counterId, string objectiveId = null)
        {
            _eventId = eventId;
            _labelKey = labelKey;
            _counterId = counterId;
            _objectiveId = objectiveId;
            _hiddenUntilActive = false;
            return this;
        }

        /// <summary>This shortcut really does finish the job - wrongly (spec 22 M09/M14).</summary>
        public ManualForbiddenProp EndsEvent()
        {
            _endsEvent = true;
            return this;
        }

        public ManualForbiddenProp WithRange(float range)
        {
            _range = range;
            return this;
        }

        public ManualForbiddenProp Also(string counterId)
        {
            _alsoSetsCounterId = counterId;
            return this;
        }

        public float Range { get { return _range; } }

        public InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            return CanInteract(context, out reasonKey)
                ? InteractionPrompt.Simple(_labelKey)
                : InteractionPrompt.Blocked(_labelKey, reasonKey);
        }

        public bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;
            if (!EventIsActive) { reasonKey = "ui.prompt.nothing_here"; return false; }
            if (Counter(_counterId) != 0) { reasonKey = "ui.prompt.already_done"; return false; }
            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;

            SetCounter(_counterId, 1);
            RaiseAlso();
            CompleteObjective(_objectiveId);
            if (_endsEvent) EndEventNow();
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// A control that has to be worked a set number of times, with a pause between.
    ///
    /// Broadcasts (M01, three of them 1.5s apart), knocks (M16, matching what was heard), taps
    /// (M14, twice on the rules board). The gap is enforced rather than suggested because in
    /// every one of those the count *is* the answer, and a player holding the key down would
    /// reach it by accident.
    /// </summary>
    public sealed class ManualSequenceProp : ManualProp, IInteractable
    {
        [SerializeField] string _labelKey = "ui.prompt.interact";
        [SerializeField] string _counterId;
        [SerializeField] string _objectiveId;
        [SerializeField] float _minimumGapSeconds = 1.5f;
        [SerializeField] int _completeAt;               // 0 = something else closes the step
        [SerializeField] string _requiresCounterId;
        [SerializeField] string _earlyCounterId;        // set if used before the cue
        [SerializeField] float _range = 2.0f;

        float _nextAllowedRealtime;

        public ManualSequenceProp Setup(string eventId, string labelKey, string counterId,
                          string objectiveId = null, float minimumGapSeconds = 1.5f,
                          int completeAt = 0)
        {
            _eventId = eventId;
            _labelKey = labelKey;
            _counterId = counterId;
            _objectiveId = objectiveId;
            _minimumGapSeconds = minimumGapSeconds;
            _completeAt = completeAt;
            _hiddenUntilActive = false;
            return this;
        }

        public ManualSequenceProp WithRange(float range)
        {
            _range = range;
            return this;
        }

        /// <summary>
        /// A panel that is part of the room rather than part of the anomaly.
        ///
        /// The broadcast panels and the wall are fixtures the caretaker walks past on ordinary
        /// nights; they do nothing until their event is running, but a control that appears out
        /// of nowhere reads as the game telling the player what to do (spec 0.4).
        /// </summary>
        public ManualSequenceProp AlwaysVisible()
        {
            _hiddenUntilActive = false;
            return this;
        }

        /// <summary>
        /// Record that the player went first, instead of blocking them.
        ///
        /// Spec 22 M16 forbids knocking before the set has finished, and spec 0.4 forbids the
        /// UI from saying so. So the wall answers - and the reply is marked as having come too
        /// early, which is what the judgement reads.
        /// </summary>
        public ManualSequenceProp EarlyUseSets(string counterId, string afterCounterId)
        {
            _earlyCounterId = counterId;
            _requiresCounterId = afterCounterId;
            return this;
        }

        public float Range { get { return _range; } }

        public InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            return CanInteract(context, out reasonKey)
                ? InteractionPrompt.Simple(_labelKey)
                : InteractionPrompt.Blocked(_labelKey, reasonKey);
        }

        public bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;
            if (!EventIsActive) { reasonKey = "ui.prompt.nothing_here"; return false; }
            if (Time.realtimeSinceStartup < _nextAllowedRealtime)
            {
                reasonKey = "ui.prompt.manual_wait";
                return false;
            }
            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;

            if (!string.IsNullOrEmpty(_requiresCounterId) && !string.IsNullOrEmpty(_earlyCounterId) &&
                Counter(_requiresCounterId) == 0)
                SetCounter(_earlyCounterId, 1);

            _nextAllowedRealtime = Time.realtimeSinceStartup + _minimumGapSeconds;
            AddCounter(_counterId);
            RaiseAlso();

            // Deliberately allowed to overshoot. A fourth broadcast is a wrong answer the
            // player has to be able to give (spec 22 M01), not an input the panel refuses.
            if (_completeAt > 0 && Counter(_counterId) == _completeAt) CompleteObjective(_objectiveId);
        }

        protected override void OnEventActiveChanged(bool active)
        {
            if (active) _nextAllowedRealtime = 0f;
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(counters, _earlyCounterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// Pick one of several answers. The choice is recorded as an index, so the judgement is a
    /// plain comparison (spec 22 M17 wants B, M10 wants V4).
    ///
    /// Built as one interactable per option rather than as a menu, because in both cases the
    /// options are places in the room: the answer is which wall the notice goes on, not which
    /// line was highlighted.
    /// </summary>
    public sealed class ManualChoiceProp : ManualProp, IInteractable
    {
        [SerializeField] string _labelKey = "ui.prompt.interact";
        [SerializeField] string _counterId;
        [SerializeField] string _objectiveId;
        [SerializeField] int _optionIndex;
        [SerializeField] string _requiresCounterId;
        [SerializeField] string _blockedReasonKey = "ui.prompt.nothing_here";
        [SerializeField] float _range = 1.8f;

        public ManualChoiceProp Setup(string eventId, string labelKey, string counterId, int optionIndex,
                          string objectiveId = null)
        {
            _eventId = eventId;
            _labelKey = labelKey;
            _counterId = counterId;
            _optionIndex = optionIndex;
            _objectiveId = objectiveId;
            _hiddenUntilActive = false;
            return this;
        }

        public ManualChoiceProp RequiresCounter(string counterId, string blockedReasonKey)
        {
            _requiresCounterId = counterId;
            _blockedReasonKey = blockedReasonKey;
            return this;
        }

        public ManualChoiceProp WithRange(float range)
        {
            _range = range;
            return this;
        }

        public ManualChoiceProp Also(string counterId)
        {
            _alsoSetsCounterId = counterId;
            return this;
        }

        public float Range { get { return _range; } }

        public InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            return CanInteract(context, out reasonKey)
                ? InteractionPrompt.Simple(_labelKey)
                : InteractionPrompt.Blocked(_labelKey, reasonKey);
        }

        public bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;
            if (!EventIsActive) { reasonKey = "ui.prompt.nothing_here"; return false; }
            if (Counter(_counterId) != 0) { reasonKey = "ui.prompt.already_done"; return false; }
            if (!string.IsNullOrEmpty(_requiresCounterId) && Counter(_requiresCounterId) == 0)
            {
                reasonKey = _blockedReasonKey;
                return false;
            }
            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;

            SetCounter(_counterId, _optionIndex);
            RaiseAlso();
            CompleteObjective(_objectiveId);
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// Watches how close the player gets, and records it when they cross a line the manual
    /// drew.
    ///
    /// Spec 22 judges M02 on four metres and M18 on five, and neither is an interaction - the
    /// player breaks those rules by walking. A trigger volume would do it, but the distance is
    /// the specified quantity and a collider sized to match is a second place for the same
    /// number to live.
    /// </summary>
    public sealed class ManualProximityWatcher : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] float _distance = 4f;
        [SerializeField] string _warnKey;
        [SerializeField] string _objectiveId;
        [SerializeField] string _armedAfterCounterId;
        [SerializeField] string _disarmedByCounterId;

        bool _warned;

        public ManualProximityWatcher Setup(string eventId, string counterId, float distance, string warnKey = null)
        {
            _eventId = eventId;
            _counterId = counterId;
            _distance = distance;
            _warnKey = warnKey;
            _hiddenUntilActive = false;
            return this;
        }

        /// <summary>
        /// The window this rule applies in.
        ///
        /// Spec 22 M11 forbids entering the bay only until the exit log appears, and M04
        /// forbids turning round only while there is cleaning left to do. Outside its window a
        /// prohibition is not one, and a watcher that ignored that would punish the player for
        /// walking through a room they had finished with.
        /// </summary>
        public ManualProximityWatcher ArmedBetween(string afterCounterId, string untilCounterId)
        {
            _armedAfterCounterId = afterCounterId;
            _disarmedByCounterId = untilCounterId;
            return this;
        }

        /// <summary>Coming close is the step, not the breach (spec 22 M11 "visit the bay").</summary>
        public ManualProximityWatcher Completes(string objectiveId)
        {
            _objectiveId = objectiveId;
            return this;
        }

        bool Armed
        {
            get
            {
                if (!string.IsNullOrEmpty(_armedAfterCounterId) && Counter(_armedAfterCounterId) == 0)
                    return false;
                if (!string.IsNullOrEmpty(_disarmedByCounterId) && Counter(_disarmedByCounterId) != 0)
                    return false;
                return true;
            }
        }

        void Update()
        {
            if (!EventIsActive || Counter(_counterId) != 0 || !Armed) return;

            var player = PlayerController.Active;
            if (player == null) return;

            if (Vector3.Distance(player.transform.position, transform.position) > _distance) return;

            SetCounter(_counterId, 1);
            RaiseAlso();
            CompleteObjective(_objectiveId);

            if (!_warned && !string.IsNullOrEmpty(_warnKey))
            {
                _warned = true;
                EventBus.Publish(new NotificationEvent(_warnKey, NotificationSeverity.Warning));
            }
        }

        protected override void OnEventActiveChanged(bool active)
        {
            if (active) _warned = false;
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// Watches whether the player looked at something they were told not to look at.
    ///
    /// M02 forbids the face, M04 forbids turning round while cleaning. Both are "did you point
    /// the camera at it", and both need a dwell rather than a glance: a player sweeping the
    /// room should not fail a rule because the figure crossed the middle of the screen.
    /// </summary>
    public sealed class ManualGazeWatcher : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] float _dwellSeconds = 0.6f;
        [SerializeField] float _maxAngle = 22f;
        [SerializeField] float _maxDistance = 25f;
        [SerializeField] string _warnKey;
        [SerializeField] string _armedAfterCounterId;
        [SerializeField] string _disarmedByCounterId;

        float _dwell;

        public ManualGazeWatcher Setup(string eventId, string counterId, float dwellSeconds = 0.6f,
                          float maxAngle = 22f, float maxDistance = 25f, string warnKey = null)
        {
            _eventId = eventId;
            _counterId = counterId;
            _dwellSeconds = dwellSeconds;
            _maxAngle = maxAngle;
            _maxDistance = maxDistance;
            _warnKey = warnKey;
            _hiddenUntilActive = false;
            return this;
        }

        /// <summary>The window this prohibition applies in. See ManualProximityWatcher.</summary>
        public ManualGazeWatcher ArmedBetween(string afterCounterId, string untilCounterId)
        {
            _armedAfterCounterId = afterCounterId;
            _disarmedByCounterId = untilCounterId;
            return this;
        }

        bool Armed
        {
            get
            {
                if (!string.IsNullOrEmpty(_armedAfterCounterId) && Counter(_armedAfterCounterId) == 0)
                    return false;
                if (!string.IsNullOrEmpty(_disarmedByCounterId) && Counter(_disarmedByCounterId) != 0)
                    return false;
                return true;
            }
        }

        void Update()
        {
            if (!EventIsActive || Counter(_counterId) != 0 || !Armed) { _dwell = 0f; return; }

            var player = PlayerController.Active;
            var camera = player == null ? null : player.Camera;
            if (camera == null) { _dwell = 0f; return; }

            var toTarget = transform.position - camera.transform.position;
            if (toTarget.sqrMagnitude > _maxDistance * _maxDistance) { _dwell = 0f; return; }
            if (Vector3.Angle(camera.transform.forward, toTarget) > _maxAngle) { _dwell = 0f; return; }

            _dwell += Time.deltaTime;
            if (_dwell < _dwellSeconds) return;

            SetCounter(_counterId, 1);
            RaiseAlso();
            if (!string.IsNullOrEmpty(_warnKey))
                EventBus.Publish(new NotificationEvent(_warnKey, NotificationSeverity.Warning));
        }

        protected override void OnEventActiveChanged(bool active) { _dwell = 0f; }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
        }

    }

    /// <summary>
    /// Waits, then opens something.
    ///
    /// The washing machine that has to be allowed to finish (M12), the knocking that has to be
    /// heard out (M16), the ten seconds of silence afterwards, the pump circulating between
    /// cartridges (M09). Spec 22 asks for patience four separate times, and the reason it can:
    /// the timer runs on game seconds, so waiting costs the player the one resource the shift
    /// actually meters.
    /// </summary>
    public sealed class ManualTimerProp : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] string _objectiveId;
        [SerializeField] int _durationGameSeconds = 60;
        [SerializeField] string _startAfterCounterId;   // empty = starts with the event
        [SerializeField] int _startAfterAtLeast = 1;
        [SerializeField] int _setsValue = 1;
        [SerializeField] string _doneKey;

        int _endsAtGameSecond = -1;

        public ManualTimerProp Setup(string eventId, string counterId, int durationGameSeconds,
                          string objectiveId = null, string startAfterCounterId = null,
                          string doneKey = null)
        {
            _eventId = eventId;
            _counterId = counterId;
            _durationGameSeconds = durationGameSeconds;
            _objectiveId = objectiveId;
            _startAfterCounterId = startAfterCounterId;
            _doneKey = doneKey;
            _hiddenUntilActive = false;
            return this;
        }

        /// <summary>Wait for the trigger counter to reach a count, not merely to move.</summary>
        public ManualTimerProp StartsAt(int atLeast)
        {
            _startAfterAtLeast = atLeast;
            return this;
        }

        /// <summary>
        /// What the counter is set to when the wait ends.
        ///
        /// M09 is judged on a pressure reading rather than a flag, so the timer standing for
        /// the system recovering has to leave 24 behind rather than 1.
        /// </summary>
        public ManualTimerProp Sets(int value)
        {
            _setsValue = value;
            return this;
        }

        public ManualTimerProp Also(string counterId)
        {
            _alsoSetsCounterId = counterId;
            return this;
        }

        /// <summary>Game seconds left, or -1 when the timer is not running.</summary>
        public int Remaining
        {
            get
            {
                return _endsAtGameSecond < 0 ? -1
                     : Mathf.Max(0, _endsAtGameSecond - ServiceHub.Clock.GameSecond);
            }
        }

        void Update()
        {
            if (!EventIsActive || Counter(_counterId) != 0) return;

            if (_endsAtGameSecond < 0)
            {
                bool ready = string.IsNullOrEmpty(_startAfterCounterId)
                          || Counter(_startAfterCounterId) >= _startAfterAtLeast;
                if (!ready) return;

                _endsAtGameSecond = ServiceHub.Clock.GameSecond + _durationGameSeconds;
                return;
            }

            if (ServiceHub.Clock.GameSecond < _endsAtGameSecond) return;

            SetCounter(_counterId, _setsValue);
            RaiseAlso();
            CompleteObjective(_objectiveId);
            if (!string.IsNullOrEmpty(_doneKey))
                EventBus.Publish(new NotificationEvent(_doneKey, NotificationSeverity.Info));
        }

        protected override void OnEventActiveChanged(bool active)
        {
            // Restarting on every activation would let a player reset a wait by leaving the
            // floor and coming back, which is precisely the impatience M12 is about.
            if (!active) _endsAtGameSecond = -1;
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// Records that the player reached somewhere, or left it.
    ///
    /// M02 and M06 both end on going somewhere - the nearest exit, the office door - and M01
    /// ends on reaching the service level. All three are the same question asked of a place
    /// rather than an object.
    /// </summary>
    public sealed class ManualZoneWatcher : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] string _objectiveId;
        [SerializeField] string _zoneId;
        [SerializeField] bool _onLeaving;

        public ManualZoneWatcher Setup(string eventId, string counterId, string zoneId,
                          string objectiveId = null, bool onLeaving = false)
        {
            _eventId = eventId;
            _counterId = counterId;
            _zoneId = zoneId;
            _objectiveId = objectiveId;
            _onLeaving = onLeaving;
            _hiddenUntilActive = false;
            return this;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<ZoneChangedEvent>(OnZoneChanged);
        }

        void OnZoneChanged(ZoneChangedEvent evt)
        {
            if (!EventIsActive || Counter(_counterId) != 0) return;

            bool matched = _onLeaving ? evt.ZoneId != _zoneId : evt.ZoneId == _zoneId;
            if (!matched) return;

            SetCounter(_counterId, 1);
            RaiseAlso();
            CompleteObjective(_objectiveId);
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// Counts how many independent systems the caretaker consulted.
    ///
    /// M06 needs the recipient checked against two records out of three, and M17 needs the old
    /// complaint file read. Both happen at the desk, in the apps that already exist - so this
    /// listens to the real investigation UI rather than putting a "check the records" button in
    /// the corridor, and the two-source bar stays the same bar the visitor judgements use
    /// (GDD 13.2).
    /// </summary>
    public sealed class ManualAppWatcher : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] string _objectiveId;
        [SerializeField] string[] _appIds = new string[0];
        [SerializeField] int _objectiveAt = 1;

        readonly System.Collections.Generic.HashSet<string> _seen =
            new System.Collections.Generic.HashSet<string>();

        public ManualAppWatcher Setup(string eventId, string counterId, string[] appIds,
                          string objectiveId = null, int objectiveAt = 1)
        {
            _eventId = eventId;
            _counterId = counterId;
            _appIds = appIds ?? new string[0];
            _objectiveId = objectiveId;
            _objectiveAt = objectiveAt;
            _hiddenUntilActive = false;
            return this;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<AppOpenedEvent>(OnAppOpened);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<AppOpenedEvent>(OnAppOpened);
        }

        void OnAppOpened(AppOpenedEvent evt)
        {
            if (!EventIsActive) return;

            bool relevant = false;
            for (int i = 0; i < _appIds.Length; i++) if (_appIds[i] == evt.AppId) relevant = true;
            if (!relevant || !_seen.Add(evt.AppId)) return;

            // Distinct sources, not visits: opening the resident database four times is one
            // record checked, which is the whole point of the rule.
            SetCounter(_counterId, _seen.Count);
            if (_seen.Count >= _objectiveAt) CompleteObjective(_objectiveId);
        }

        protected override void OnEventActiveChanged(bool active)
        {
            if (!active) _seen.Clear();
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
            Add(objectives, _objectiveId);
        }

    }

    /// <summary>
    /// Notices the caretaker bringing a camera up when the manual said not to.
    ///
    /// The two prohibitions this serves (M01 on the lift camera, M02 on the car park) are the
    /// only rules in spec 22 that are broken from the desk rather than in the room, and they
    /// are the reason CctvChannelViewedEvent exists.
    /// </summary>
    public sealed class ManualCameraWatcher : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] string _cameraId;
        [SerializeField] string _warnKey;
        [SerializeField] string _disarmedByCounterId;

        public ManualCameraWatcher Setup(string eventId, string counterId, string cameraId,
                          string warnKey = null, string disarmedByCounterId = null)
        {
            _eventId = eventId;
            _counterId = counterId;
            _cameraId = cameraId;
            _warnKey = warnKey;
            _disarmedByCounterId = disarmedByCounterId;
            _hiddenUntilActive = false;
            return this;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<CctvChannelViewedEvent>(OnChannelViewed);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<CctvChannelViewedEvent>(OnChannelViewed);
        }

        void OnChannelViewed(CctvChannelViewedEvent evt)
        {
            if (!EventIsActive || evt.CameraId != _cameraId) return;
            if (Counter(_counterId) != 0) return;

            // M01 forbids the camera only while the floor sensor is lit. Once it is out the
            // manual tells the player to go and read the log, so watching then is the job.
            if (!string.IsNullOrEmpty(_disarmedByCounterId) && Counter(_disarmedByCounterId) != 0) return;

            SetCounter(_counterId, 1);
            if (!string.IsNullOrEmpty(_warnKey))
                EventBus.Publish(new NotificationEvent(_warnKey, NotificationSeverity.Warning));
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
        }

    }

    /// <summary>
    /// Notices the caretaker running where the manual said to walk (spec 22 M07 step 1).
    /// </summary>
    public sealed class ManualSprintWatcher : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] string _zoneId;

        public ManualSprintWatcher Setup(string eventId, string counterId, string zoneId)
        {
            _eventId = eventId;
            _counterId = counterId;
            _zoneId = zoneId;
            _hiddenUntilActive = false;
            return this;
        }

        void Update()
        {
            if (!EventIsActive || Counter(_counterId) != 0) return;
            if (ServiceHub.Player.CurrentZone != _zoneId) return;

            var player = PlayerController.Active;
            if (player == null || !player.IsSprinting) return;

            SetCounter(_counterId, 1);
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
        }

    }

    /// <summary>
    /// Sets a counter to its starting value when the event begins.
    ///
    /// Only M04 needs this, and it needs it for a reason worth keeping: spec 31 says the
    /// number of stains on the ceiling depends on what the caretaker did to the 404 record the
    /// night before - seven normally, ten if they deleted it outright. The judgement compares
    /// what was cleaned against what was there, so what was there has to be written down
    /// somewhere the judgement can read, at the moment the event starts.
    /// </summary>
    public sealed class ManualInitProp : ManualProp
    {
        [SerializeField] string _counterId;
        [SerializeField] int _value = 1;
        [SerializeField] string _worseIfFlagId;
        [SerializeField] int _worseValue;

        public ManualInitProp Setup(string eventId, string counterId, int value,
                          string worseIfFlagId = null, int worseValue = 0)
        {
            _eventId = eventId;
            _counterId = counterId;
            _value = value;
            _worseIfFlagId = worseIfFlagId;
            _worseValue = worseValue;
            _hiddenUntilActive = false;
            return this;
        }

        public int Value
        {
            get
            {
                bool worse = !string.IsNullOrEmpty(_worseIfFlagId) &&
                             ServiceHub.State.GetFlag(_worseIfFlagId);
                return worse ? _worseValue : _value;
            }
        }

        protected override void OnEventActiveChanged(bool active)
        {
            // Only on the way in, and only once: a retry after a wrong answer clears the
            // counters, and re-seeding then is exactly right, but a reload must not move the
            // target the player has already been cleaning towards.
            if (active && Counter(_counterId) == 0) SetCounter(_counterId, Value);
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _counterId);
        }

    }

    /// <summary>
    /// The wall that knocks (spec 22 M16).
    ///
    /// The only prop with logic of its own, because the count is the puzzle: the set is three
    /// to seven, chosen once and then fixed. Fixing it in a counter rather than in a field is
    /// what makes it survive a save - spec 22 says the number is settled the first time the
    /// event fires, and a reload that re-rolled it would make the answer unlearnable.
    /// </summary>
    public sealed class ManualKnockWall : ManualProp
    {
        [SerializeField] string _sourceCounterId = "sourceCount";
        [SerializeField] string _finishedCounterId = "sequenceHeard";
        [SerializeField] string _objectiveId;
        [SerializeField] string _countObjectiveId;
        [SerializeField] int _secondsPerKnock = 20;

        int _endsAtGameSecond = -1;

        public ManualKnockWall Setup(string eventId, string objectiveId, string countObjectiveId)
        {
            _eventId = eventId;
            _objectiveId = objectiveId;
            _countObjectiveId = countObjectiveId;
            _hiddenUntilActive = false;
            return this;
        }

        void Update()
        {
            if (!EventIsActive || Counter(_finishedCounterId) != 0) return;

            if (Counter(_sourceCounterId) == 0)
            {
                SetCounter(_sourceCounterId, Random.Range(3, 8));
                _endsAtGameSecond = -1;
            }

            if (_endsAtGameSecond < 0)
            {
                _endsAtGameSecond = ServiceHub.Clock.GameSecond +
                                    Counter(_sourceCounterId) * _secondsPerKnock;
                return;
            }

            if (ServiceHub.Clock.GameSecond < _endsAtGameSecond) return;

            SetCounter(_finishedCounterId, 1);
            CompleteObjective(_countObjectiveId);
            CompleteObjective(_objectiveId);
            EventBus.Publish(new NotificationEvent("notify.m16.set_ended", NotificationSeverity.Info));
        }

        protected override void OnEventActiveChanged(bool active)
        {
            if (!active) _endsAtGameSecond = -1;
        }

        public override void CollectWrites(System.Collections.Generic.List<string> counters,
                                           System.Collections.Generic.List<string> objectives)
        {
            base.CollectWrites(counters, objectives);
            Add(counters, _sourceCounterId);
            Add(counters, _finishedCounterId);
            Add(objectives, _objectiveId);
            Add(objectives, _countObjectiveId);
        }

    }
}
