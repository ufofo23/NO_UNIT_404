using NO404.Cases;
using NO404.Core;
using NO404.Evidence;
using NO404.Gameplay;

namespace NO404.Interaction
{
    /// <summary>
    /// A night 1/3/5 subquest prop (v5.1 10, 12, 14). Either a piece of evidence to read or
    /// an action with a cost, and inert unless its own quest is open - so the building can
    /// carry every prop all week without a single one speaking out of turn.
    ///
    /// Eligibility is read from saved state every time, never cached, so a streamed-out floor
    /// that comes back is exactly as the player left it.
    /// </summary>
    public sealed class SubquestInteractable : InteractableBase
    {
        string _caseId, _evidenceId, _action, _requiredFlag;

        public string CaseId { get { return _caseId; } }
        public string EvidenceId { get { return _evidenceId; } }
        public string Action { get { return _action; } }
        public string RequiredFlag { get { return _requiredFlag; } }

        public void Setup(string caseId, string evidenceId, string action, string labelKey,
                          string requiredFlag = null)
        {
            _caseId = caseId;
            _evidenceId = evidenceId;
            _action = action;
            _requiredFlag = requiredFlag;
            Configure(labelKey, 2.2f, true);
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
