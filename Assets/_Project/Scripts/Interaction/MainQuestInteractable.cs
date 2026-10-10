using NO404.Cases;
using NO404.Core;
using NO404.Evidence;

namespace NO404.Interaction
{
    /// <summary>Scene-streaming-safe quest interaction: eligibility comes from saved state.</summary>
    public sealed class MainQuestInteractable : InteractableBase
    {
        string _caseId, _evidenceId, _action;
        public void Setup(string caseId, string evidenceId, string action, string label)
        {
            _caseId = caseId; _evidenceId = evidenceId; _action = action;
            Configure(label, 2.2f, true);
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
