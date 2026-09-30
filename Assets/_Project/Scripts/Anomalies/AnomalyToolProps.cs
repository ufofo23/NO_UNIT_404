using UnityEngine;
using NO404.Core;

using NO404.Interaction;

namespace NO404.Anomalies
{
    /// <summary>
    /// The machine itself: a POS, a locker rack, a television, a toolbox, a vending machine.
    ///
    /// Every one of them is built with its zone and stays there for the whole game, because
    /// spec 23 is careful that these are not apparitions - they are fixtures the caretaker has
    /// walked past every night, and the change is that one night they answer. So the prop is
    /// always visible and always interactable; what changes is whether the interaction opens a
    /// menu or gets the flat refusal of an ordinary appliance.
    /// </summary>
    public sealed class AnomalyToolTerminal : MonoBehaviour, IInteractable
    {
        [SerializeField] string _toolId;
        [SerializeField] string _awakeLabelKey = "ui.prompt.interact";
        [SerializeField] float _range = 2.0f;

        public AnomalyToolTerminal Setup(string toolId, string awakeLabelKey)
        {
            _toolId = toolId;
            _awakeLabelKey = awakeLabelKey;
            return this;
        }

        public AnomalyToolTerminal WithRange(float range)
        {
            _range = range;
            return this;
        }

        public string ToolId { get { return _toolId; } }
        public float Range { get { return _range; } }

        AnomalyToolDefinition Definition
        {
            get
            {
                var runtime = ServiceHub.AnomalyTools == null ? null : ServiceHub.AnomalyTools.Find(_toolId);
                return runtime == null ? null : runtime.Definition;
            }
        }

        public InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            return CanInteract(context, out reasonKey)
                ? InteractionPrompt.Simple(_awakeLabelKey)
                : InteractionPrompt.Blocked(_awakeLabelKey, reasonKey);
        }

        public bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;

            var tools = ServiceHub.AnomalyTools;
            if (tools == null) { reasonKey = "ui.prompt.nothing_here"; return false; }

            if (!tools.IsAwake(_toolId))
            {
                // Spec 23: before its night, the machine is exactly what it looks like. The
                // refusal says so in its own words rather than as a locked-content message.
                var definition = Definition;
                reasonKey = definition != null && !string.IsNullOrEmpty(definition.dormantKey)
                    ? definition.dormantKey
                    : "ui.prompt.nothing_here";
                return false;
            }

            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;
            ServiceHub.AnomalyTools.Open(_toolId);
        }
    }

    /// <summary>
    /// The one interaction that ends a hold: the locker door, and the toolbox return slot.
    ///
    /// Spec 23 A02 is specific that hammering the interact key is not the way out of the
    /// locker - doing the thing properly once is - so this is an ordinary interactable that
    /// only exists while there is something to close, and does its whole job in one press.
    ///
    /// It sits beside the terminal rather than on it so that a caretaker who walked away with
    /// the menu closed still has something to come back to.
    /// </summary>
    public sealed class AnomalyToolHoldRelease : MonoBehaviour, IInteractable
    {
        [SerializeField] string _toolId;
        [SerializeField] float _range = 2.0f;

        public AnomalyToolHoldRelease Setup(string toolId)
        {
            _toolId = toolId;
            return this;
        }

        public string ToolId { get { return _toolId; } }
        public float Range { get { return _range; } }

        string LabelKey
        {
            get
            {
                var tools = ServiceHub.AnomalyTools;
                var runtime = tools == null ? null : tools.Find(_toolId);
                return runtime != null && !string.IsNullOrEmpty(runtime.Definition.holdReleaseKey)
                    ? runtime.Definition.holdReleaseKey
                    : "ui.prompt.interact";
            }
        }

        public InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            return CanInteract(context, out reasonKey)
                ? InteractionPrompt.Simple(LabelKey)
                : InteractionPrompt.Blocked(LabelKey, reasonKey);
        }

        public bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;
            var tools = ServiceHub.AnomalyTools;
            if (tools == null || !tools.HasOpenHold(_toolId))
            {
                reasonKey = "ui.prompt.nothing_here";
                return false;
            }
            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;
            ServiceHub.AnomalyTools.ReleaseHold(_toolId);
        }
    }

    /// <summary>
    /// A token for the roof machine (spec 23 A05).
    ///
    /// Two of these exist in the building and each buys one thing, which is the whole balance
    /// of A05: the machine is a decision about what the caretaker most wants back, not a shelf
    /// to be emptied on the last night.
    /// </summary>
    public sealed class AnomalyTokenPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] string _labelKey = "ui.prompt.a05.take_token";
        [SerializeField] float _range = 1.6f;
        bool _taken;

        public AnomalyTokenPickup Setup(string labelKey)
        {
            if (!string.IsNullOrEmpty(labelKey)) _labelKey = labelKey;
            return this;
        }

        public float Range { get { return _range; } }

        void OnEnable() { EventBus.Subscribe<PlaythroughResetEvent>(HandleReset); }
        void OnDisable() { EventBus.Unsubscribe<PlaythroughResetEvent>(HandleReset); }

        void HandleReset(PlaythroughResetEvent evt) { _taken = false; }

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
            if (_taken) { reasonKey = "ui.prompt.already_done"; return false; }
            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;

            _taken = true;
            ServiceHub.State.SetFlag(ItemIds.InsectToken, true);
            EventBus.Publish(new NotificationEvent("notify.a05.token_taken", NotificationSeverity.Info));
        }
    }

    /// <summary>
    /// The ten seconds after the photograph (spec 23 A05).
    ///
    /// The pull is toward this transform, which is where the parapet is - a fact about the
    /// roof rather than about the machine, which is why the service says only how long and
    /// this says which way. Spec 23 rules out a forced fall, so what the caretaker feels is a
    /// bias they can walk out of by walking the other way, and the bias ends on its own.
    /// </summary>
    public sealed class AnomalyDriftAnchor : MonoBehaviour
    {
        [SerializeField] string _toolId;

        public AnomalyDriftAnchor Setup(string toolId)
        {
            _toolId = toolId;
            return this;
        }

        void OnEnable() { EventBus.Subscribe<AnomalyDriftEvent>(HandleDrift); }
        void OnDisable() { EventBus.Unsubscribe<AnomalyDriftEvent>(HandleDrift); }

        void HandleDrift(AnomalyDriftEvent evt)
        {
            if (evt.ToolId != _toolId) return;
            EventBus.Publish(new PlayerDriftEvent(transform.position, evt.EndsAtGameSecond));
        }
    }
}
