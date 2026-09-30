using UnityEngine;

namespace NO404.Interaction
{
    public enum InteractionKind
    {
        Instant = 0,
        Hold = 1,
        Inspect = 2,
        UseItem = 3,
        Toggle = 4,
        Dialogue = 5,
        SceneTransition = 6
    }

    public struct InteractionPrompt
    {
        public string LabelKey;
        public InteractionKind Kind;
        public float HoldSeconds;
        public bool Enabled;
        /// <summary>Localization key explaining why it is blocked. Shown instead of the label.</summary>
        public string DisabledReasonKey;

        public static InteractionPrompt Simple(string labelKey)
        {
            return new InteractionPrompt { LabelKey = labelKey, Kind = InteractionKind.Instant, Enabled = true };
        }

        public static InteractionPrompt Hold(string labelKey, float seconds)
        {
            return new InteractionPrompt
            {
                LabelKey = labelKey, Kind = InteractionKind.Hold, HoldSeconds = seconds, Enabled = true
            };
        }

        public static InteractionPrompt Blocked(string labelKey, string reasonKey)
        {
            return new InteractionPrompt
            {
                LabelKey = labelKey, Kind = InteractionKind.Instant, Enabled = false, DisabledReasonKey = reasonKey
            };
        }
    }

    /// <summary>Everything the interactable needs to know about who is interacting.</summary>
    public struct PlayerContext
    {
        public Transform Transform;
        public string ZoneId;
        public float DistanceToTarget;
        public bool Crouching;
    }

    public interface IInteractable
    {
        InteractionPrompt GetPrompt(in PlayerContext context);
        bool CanInteract(in PlayerContext context, out string reasonKey);
        void Interact(in PlayerContext context);
    }
}
