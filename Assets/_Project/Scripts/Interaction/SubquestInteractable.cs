using NO404.Cases;
using NO404.Core;
using NO404.Evidence;
using NO404.Gameplay;
using UnityEngine;

namespace NO404.Interaction
{
    /// <summary>
    /// A subquest prop (v5.1 10-15). Either a piece of evidence to read or an action with a
    /// cost, and only there while its own quest is open.
    ///
    /// The building carries every prop all week, so a prop outside its quest is hidden - no
    /// renderer, no collider - rather than left standing with "nothing here yet" on it. With
    /// six nights of props in the same lobby, an inert prop is not a quiet one: it is a box on
    /// the floor that says nothing, every night but one. It appears when the quest is handed
    /// out and goes when the report is filed, the same rule ManualProp keeps for the v2.1
    /// events.
    ///
    /// Eligibility is read from saved state every time, never cached, so a streamed-out floor
    /// that comes back - or a save, or a co-op mirror - is exactly as the shift left it.
    /// </summary>
    public sealed class SubquestInteractable : InteractableBase
    {
        /// <summary>How often a prop re-reads whether it should be there, in frames.</summary>
        const int SyncEveryFrames = 10;

        string _caseId, _evidenceId, _action, _requiredFlag;
        bool _synced, _shown;

        public string CaseId { get { return _caseId; } }
        public string EvidenceId { get { return _evidenceId; } }
        public string Action { get { return _action; } }
        public string RequiredFlag { get { return _requiredFlag; } }

        /// <summary>Whether the prop is in the room right now: its quest open, its flag set.</summary>
        public bool Shown { get { return _shown; } }

        /// <summary>
        /// Whether it should be. Open quest, and the condition the prop was placed under - the
        /// night-3 marker that exists only if one was put down, the playback that exists only
        /// if the recorder survived.
        /// </summary>
        public bool ShouldShow
        {
            get
            {
                if (string.IsNullOrEmpty(_caseId) || !SubquestRules.Active(_caseId)) return false;
                return string.IsNullOrEmpty(_requiredFlag) || ServiceHub.State.GetFlag(_requiredFlag);
            }
        }

        public void Setup(string caseId, string evidenceId, string action, string labelKey,
                          string requiredFlag = null)
        {
            _caseId = caseId;
            _evidenceId = evidenceId;
            _action = action;
            _requiredFlag = requiredFlag;
            Configure(labelKey, 2.2f, true);
            Sync();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<CaseStartedEvent>(HandleCaseStarted);
            EventBus.Subscribe<NightStartedEvent>(HandleNightStarted);
            EventBus.Subscribe<SaveRestoredEvent>(HandleSaveRestored);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<CaseStartedEvent>(HandleCaseStarted);
            EventBus.Unsubscribe<NightStartedEvent>(HandleNightStarted);
            EventBus.Unsubscribe<SaveRestoredEvent>(HandleSaveRestored);
        }

        void Start() { Sync(); }

        // The events make it appear the moment the quest is handed out. The poll is what puts
        // it away again - there is no event for a report being filed - and what keeps a co-op
        // client, whose cases arrive in the mirror rather than through these events, in step.
        void Update()
        {
            if ((Time.frameCount + GetInstanceID()) % SyncEveryFrames != 0) return;
            if (!_synced || ShouldShowNow() != _shown) Sync();
        }

        bool ShouldShowNow() { return ServiceHub.Ready && ShouldShow; }

        void HandleCaseStarted(CaseStartedEvent evt) { Sync(); }
        void HandleNightStarted(NightStartedEvent evt) { Sync(); }
        void HandleSaveRestored(SaveRestoredEvent evt) { Sync(); }
        protected override void OnPlaythroughReset() { Sync(); }

        /// <summary>Puts the prop in or out of the room to match its quest. Public for tests.</summary>
        public void Sync()
        {
            bool show = ShouldShowNow();

            // Read fresh rather than cached: the kit is still being assembled when Setup runs,
            // and the colliders it strips from its pieces are only destroyed at frame end.
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = show;
            foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = show;

            _shown = show;
            _synced = true;
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = "ui.prompt.nothing_here";
            if (!SubquestRules.Active(_caseId)) return false;
            if (!string.IsNullOrEmpty(_requiredFlag) && !ServiceHub.State.GetFlag(_requiredFlag)) return false;
            if (!string.IsNullOrEmpty(_action)) return SubquestRules.CanAct(_action);
            return !ServiceHub.Evidence.Has(_evidenceId);
        }

        protected override void OnInteract(in PlayerContext context)
        {
            if (string.IsNullOrEmpty(_action))
            {
                ServiceHub.Evidence.Acquire(_evidenceId, EvidenceSource.WorldPickup);
                return;
            }

            string moveTo = SubquestRules.Act(_action);
            if (string.IsNullOrEmpty(moveTo)) return;

            var player = context.Transform != null ? context.Transform.GetComponentInParent<PlayerController>() : null;
            if (player == null) return;

            UnityEngine.Transform target = null;
            if (moveTo.StartsWith("zone:")) target = ZoneRegistry.FindSpawn(moveTo.Substring(5));
            else if (moveTo.StartsWith("landing:")) target = WorldBuilder.LandingAnchor(moveTo.Substring(8));

            if (target != null) player.Teleport(target.position, target.rotation);
        }
    }
}
