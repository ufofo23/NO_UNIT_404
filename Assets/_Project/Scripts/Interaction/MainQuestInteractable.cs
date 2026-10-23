using NO404.Cases;
using NO404.Core;
using NO404.Evidence;
using UnityEngine;

namespace NO404.Interaction
{
    /// <summary>
    /// Scene-streaming-safe quest interaction: eligibility comes from saved state.
    ///
    /// A main's things are only in the building while its main is open - the bill in the
    /// mailbox, the signed approval on the basement wall. A fixture the building has all week
    /// (a seal, a gauge, a hatch) stays where it is and is simply quiet outside its night, so
    /// nothing says "nothing here yet".
    /// </summary>
    public sealed class MainQuestInteractable : InteractableBase
    {
        const int SyncEveryFrames = 10;

        string _caseId, _evidenceId, _action;
        bool _fixture, _synced, _lastOpen;

        public string CaseId { get { return _caseId; } }

        public void Setup(string caseId, string evidenceId, string action, string label, bool fixture = false)
        {
            _caseId = caseId; _evidenceId = evidenceId; _action = action; _fixture = fixture;
            Configure(label, 2.2f, true);
            SyncVisibility();
        }

        bool QuestOpen
        {
            get { return string.IsNullOrEmpty(_caseId) || SelectedMainQuestRules.Active(_caseId); }
        }

        protected override bool ManagesOwnVisibility { get { return !string.IsNullOrEmpty(_caseId); } }
        protected override bool ShouldBeVisible() { return _fixture || QuestOpen; }
        protected override bool ShouldBeTargetable() { return QuestOpen; }

        // It arrives the moment its main is handed out...
        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<CaseStartedEvent>(HandleCaseStarted);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<CaseStartedEvent>(HandleCaseStarted);
        }

        void HandleCaseStarted(CaseStartedEvent evt) { SyncVisibility(); }

        // ...and there is no event for a report being filed, so it looks again now and then.
        void Update()
        {
            if ((Time.frameCount + GetInstanceID()) % SyncEveryFrames != 0 || !ServiceHub.Ready) return;
            bool open = QuestOpen;
            if (_synced && open == _lastOpen) return;
            _synced = true;
            _lastOpen = open;
            SyncVisibility();
        }
        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = "ui.prompt.nothing_here";
            if (!string.IsNullOrEmpty(_caseId) && !SelectedMainQuestRules.Active(_caseId)) return false;
            if (!string.IsNullOrEmpty(_action)) return SelectedMainQuestRules.CanAct(_action);
            return !ServiceHub.Evidence.Has(_evidenceId);
        }
        protected override void OnInteract(in PlayerContext context)
        {
            if (!string.IsNullOrEmpty(_action))
            {
                SelectedMainQuestRules.Act(_action);
                if (_action == "wrong_door" || _action == "return_marker")
                {
                    var player = context.Transform != null ? context.Transform.GetComponentInParent<NO404.Gameplay.PlayerController>() : null;
                    var entrance = NO404.Gameplay.ZoneRegistry.FindSpawn(ZoneIds.ServicePassage);
                    if (player != null && entrance != null) player.Teleport(entrance.position, entrance.rotation);
                }
            }
            else ServiceHub.Evidence.Acquire(_evidenceId, EvidenceSource.WorldPickup);
        }
    }
}
