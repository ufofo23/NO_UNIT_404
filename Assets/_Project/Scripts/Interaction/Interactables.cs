using UnityEngine;
using NO404.Core;

namespace NO404.Interaction
{
    /// <summary>Common base so the raycaster only needs one GetComponentInParent call.</summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [SerializeField] protected string _labelKey = "ui.prompt.interact";
        [Tooltip("Raycast distance for this object (GDD 8.3: 2.4m default, 1.8m doors, 1.4m documents).")]
        [SerializeField] protected float _range = InteractionRaycaster.DefaultRange;
        [SerializeField] protected bool _repeatable = true;

        [Header("Availability")]
        [Tooltip("Flag that must be set before this object does anything. Empty = always.")]
        [SerializeField] protected string _requiredFlagId;
        [Tooltip("Earliest night this object is usable. -1 = any night.")]
        [SerializeField] protected int _fromNight = -1;

        protected bool _used;

        public float Range { get { return _range; } }

        /// <summary>
        /// Office/lobby objects outlive a single playthrough (GDD 20.5: those zones are always
        /// resident), so a new playthrough has to clear their one-shot state explicitly instead
        /// of relying on the object being re-created.
        /// </summary>
        protected virtual void OnEnable() { EventBus.Subscribe<PlaythroughResetEvent>(HandlePlaythroughReset); }
        protected virtual void OnDisable() { EventBus.Unsubscribe<PlaythroughResetEvent>(HandlePlaythroughReset); }

        void HandlePlaythroughReset(PlaythroughResetEvent evt)
        {
            _used = false;
            OnPlaythroughReset();
        }

        /// <summary>Override to undo any extra one-shot effect (e.g. a hidden renderer).</summary>
        protected virtual void OnPlaythroughReset() { }

        /// <summary>
        /// Gates an object behind story progress. Objects that are not yet available stay in
        /// the world but are inert and show no prompt, so the greybox layout never changes
        /// shape between nights.
        /// </summary>
        public void SetAvailability(string requiredFlagId, int fromNight)
        {
            _requiredFlagId = requiredFlagId;
            _fromNight = fromNight;
        }

        protected bool IsAvailable()
        {
            if (_fromNight >= 0 && ServiceHub.State.NightIndex < _fromNight) return false;
            if (!string.IsNullOrEmpty(_requiredFlagId) && !ServiceHub.State.GetFlag(_requiredFlagId)) return false;
            return true;
        }

        public virtual InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            return CanInteract(context, out reasonKey)
                ? InteractionPrompt.Simple(_labelKey)
                : InteractionPrompt.Blocked(_labelKey, reasonKey);
        }

        public virtual bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;
            if (!IsAvailable()) { reasonKey = "ui.prompt.nothing_here"; return false; }
            if (_used && !_repeatable) { reasonKey = "ui.prompt.already_done"; return false; }
            return true;
        }

        public void Interact(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return;
            OnInteract(context);
            _used = true;
        }

        protected abstract void OnInteract(in PlayerContext context);

        public void Configure(string labelKey, float range, bool repeatable)
        {
            _labelKey = labelKey;
            _range = range;
            _repeatable = repeatable;
        }
    }

    /// <summary>
    /// Picks up a document/object and grants its evidence (GDD 20.14: important pickups are
    /// an autosave trigger, handled inside EvidenceService.Acquire).
    /// </summary>
    public sealed class EvidencePickup : InteractableBase
    {
        [SerializeField] string _evidenceId;
        [SerializeField] bool _hideOnPickup = true;
        [Tooltip("Hide the object entirely until its gate opens, instead of leaving it inert.")]
        [SerializeField] bool _hiddenUntilAvailable;

        public void Setup(string evidenceId, string labelKey, bool hideOnPickup = true)
        {
            _evidenceId = evidenceId;
            _labelKey = labelKey;
            _range = 1.4f;      // small documents / evidence
            _repeatable = false;
            _hideOnPickup = hideOnPickup;
        }

        /// <summary>
        /// For props that the story says appear rather than unlock: the red slipper is not in
        /// the office until the child has been at the door (GDD 9.1). Gated objects otherwise
        /// follow the greybox rule of staying put and merely going inert.
        /// </summary>
        public void HideUntilAvailable()
        {
            _hiddenUntilAvailable = true;
            SyncRenderer();
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            if (!base.CanInteract(context, out reasonKey)) return false;

            if (ServiceHub.Evidence.Has(_evidenceId))
            {
                reasonKey = "ui.prompt.already_done";
                return false;
            }

            return true;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            // The tray belongs to the shift, not to whoever bent down (v3.0 46.1). The host
            // runs the same EvidenceService call and the mirror puts the document in
            // everybody's tray - including this one, which is why nothing is added locally.
            Net.NetShift.Request(Net.NetShift.ShiftAct.AcquireEvidence, _evidenceId, null,
                                 (int)Evidence.EvidenceSource.WorldPickup);
            SyncRenderer();
        }

        /// <summary>A new playthrough puts every pickup back on its desk (GDD 20.5).</summary>
        protected override void OnPlaythroughReset() { SyncRenderer(); }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<SaveRestoredEvent>(HandleSaveRestored);
            EventBus.Subscribe<FlagChangedEvent>(HandleFlagChanged);
            EventBus.Subscribe<NightStartedEvent>(HandleNightStarted);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<SaveRestoredEvent>(HandleSaveRestored);
            EventBus.Unsubscribe<FlagChangedEvent>(HandleFlagChanged);
            EventBus.Unsubscribe<NightStartedEvent>(HandleNightStarted);
        }

        /// <summary>Both gates a pickup can open on are events, so neither needs polling.</summary>
        void HandleFlagChanged(FlagChangedEvent evt)
        {
            if (_hiddenUntilAvailable && evt.FlagId == _requiredFlagId) SyncRenderer();
        }

        void HandleNightStarted(NightStartedEvent evt)
        {
            if (_hiddenUntilAvailable) SyncRenderer();
        }

        /// <summary>
        /// A loaded save may already own this evidence even though PlaythroughResetEvent (fired
        /// earlier in the same restore, before evidence is loaded) just made the pickup visible
        /// again - this puts it back in whatever state the save actually says.
        /// </summary>
        void HandleSaveRestored(SaveRestoredEvent evt) { SyncRenderer(); }

        void SyncRenderer()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null) return;

            if (_hiddenUntilAvailable && !IsAvailable()) { renderer.enabled = false; return; }
            if (!_hideOnPickup) { renderer.enabled = true; return; }

            renderer.enabled = !ServiceHub.Evidence.Has(_evidenceId);
        }
    }

    /// <summary>Opens the facility OS. Interacting again is handled by the PC view itself.</summary>
    public sealed class TerminalInteractable : InteractableBase
    {
        public System.Action OnActivated;

        void Reset() { _labelKey = "ui.prompt.use_pc"; _range = 2.0f; }

        protected override void OnInteract(in PlayerContext context)
        {
            var cb = OnActivated;
            if (cb != null) { cb(); return; }

            // Fallback: OnActivated is only wired once, at boot. If anything ever left it
            // unset, pressing E on the only PC in the office must still open it.
            if (GameLoop.Instance != null) GameLoop.Instance.OpenPc(true);
        }
    }

    /// <summary>Starts a conversation (radio, resident at the door, phone handset).</summary>
    public sealed class DialogueInteractable : InteractableBase
    {
        [SerializeField] string _conversationId;

        public void Setup(string conversationId, string labelKey)
        {
            _conversationId = conversationId;
            _labelKey = labelKey;
            _repeatable = false;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            Net.NetShift.Request(Net.NetShift.ShiftAct.StartDialogue, _conversationId);
        }
    }

    /// <summary>
    /// A one-shot switch: sets a flag and optionally grants evidence. Used for the fire-door
    /// control panel on night 5 and similar single-purpose controls.
    /// </summary>
    public sealed class FlagInteractable : InteractableBase
    {
        [SerializeField] string _flagId;
        [SerializeField] string _evidenceId;

        public void Setup(string flagId, string labelKey, int fromNight, string evidenceId = null)
        {
            _flagId = flagId;
            _labelKey = labelKey;
            _evidenceId = evidenceId;
            _fromNight = fromNight;
            _repeatable = false;
            _range = 1.8f;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            Net.NetShift.Request(Net.NetShift.ShiftAct.SetFlag, _flagId, null, 1);
            if (!string.IsNullOrEmpty(_evidenceId))
                Net.NetShift.Request(Net.NetShift.ShiftAct.AcquireEvidence, _evidenceId, null,
                                     (int)Evidence.EvidenceSource.WorldPickup);
        }
    }

    /// <summary>
    /// Moves the player between greybox zones. Zones live in one scene during the greybox
    /// phase; SceneService.PreloadAdditive takes over when the floors are split into scenes
    /// (GDD 20.5 / first-20-issues #16).
    /// </summary>
    public class ZoneTransition : InteractableBase
    {
        [SerializeField] string _targetZoneId;
        [SerializeField] AccessLevel _requiredAccess = AccessLevel.Staff1;
        [SerializeField] string _gateFlagId;
        [SerializeField] string _gateEvidenceId;
        [SerializeField] string _gateReasonKey;

        /// <summary>
        /// Where this door leads.
        ///
        /// Virtual because the stair doors do not know: v2.1 spec 27 forbids a stair door from
        /// carrying a destination floor at all, so the two stairwell subclasses work theirs
        /// out from where the player is standing.
        /// </summary>
        public virtual string TargetZoneId { get { return _targetZoneId; } }

        /// <summary>Where in the destination the player arrives. Null falls back to its spawn.</summary>
        protected virtual Transform ResolveDestination() { return null; }

        /// <summary>Runs after the player has been moved. Nothing by default.</summary>
        protected virtual void OnTransitioned(string zoneId) { }

        public void Setup(string targetZoneId, AccessLevel requiredAccess, string labelKey)
        {
            _targetZoneId = targetZoneId;
            _requiredAccess = requiredAccess;
            _labelKey = labelKey;
            _range = 1.8f;      // doors and elevator buttons
            _repeatable = true;
        }

        /// <summary>Extra story gate on top of the access level.</summary>
        public void RequireFlag(string flagId, string reasonKey)
        {
            _gateFlagId = flagId;
            _gateReasonKey = reasonKey;
        }

        /// <summary>Requires a specific item to be in hand (e.g. the hydraulic cutter).</summary>
        public void RequireEvidence(string evidenceId, string reasonKey)
        {
            _gateEvidenceId = evidenceId;
            _gateReasonKey = reasonKey;
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            if (!base.CanInteract(context, out reasonKey)) return false;

            if (!ServiceHub.State.HasAccess(_requiredAccess))
            {
                reasonKey = "ui.prompt.no_access";
                return false;
            }

            if (!string.IsNullOrEmpty(_gateFlagId) && !ServiceHub.State.GetFlag(_gateFlagId))
            {
                reasonKey = string.IsNullOrEmpty(_gateReasonKey) ? "ui.prompt.door_locked" : _gateReasonKey;
                return false;
            }

            if (!string.IsNullOrEmpty(_gateEvidenceId) && !ServiceHub.Evidence.Has(_gateEvidenceId))
            {
                reasonKey = string.IsNullOrEmpty(_gateReasonKey) ? "ui.prompt.door_locked" : _gateReasonKey;
                return false;
            }

            // GDD 15.6: the office bolt is the price of hiding. While it is thrown the
            // caretaker is in the one room in the building with no evidence left in it.
            if (ServiceHub.Player != null && ServiceHub.Player.CurrentZone == ZoneIds.Office &&
                OfficeDoorController.Instance != null && OfficeDoorController.Instance.Locked)
            {
                reasonKey = "ui.prompt.office_door_bolted";
                return false;
            }

            // GDD 9.7: during the night-6 fire part of the stairwell is impassable.
            if (ServiceHub.Threat != null && ServiceHub.Threat.IsRouteBlocked(TargetZoneId))
            {
                reasonKey = "ui.prompt.blocked_by_debris";
                return false;
            }

            // GDD 20.5: the destination must finish loading before the door opens, and a
            // failed load leaves the door shut with a retryable message rather than a void.
            // Aiming at the door is what starts the load, so by the time the player presses
            // the key it is normally already resident.
            var streamer = ServiceHub.Zones;
            if (streamer != null && !streamer.IsZoneReady(TargetZoneId))
            {
                streamer.RequestZone(TargetZoneId);
                reasonKey = streamer.HasFailed(TargetZoneId) ? "ui.prompt.zone_load_failed"
                                                              : "ui.prompt.zone_loading";
                return false;
            }

            return true;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            var player = context.Transform != null
                ? context.Transform.GetComponentInParent<Gameplay.PlayerController>()
                : null;

            string zoneId = TargetZoneId;
            var destination = ResolveDestination() ?? Gameplay.ZoneRegistry.FindSpawn(zoneId);
            if (player == null || destination == null) return;

            player.Teleport(destination.position, destination.rotation);

            // Walking through the door is this caretaker's own business; what the building
            // does about somebody being on that floor is the shift's, so it goes to the host
            // and comes back to all three (v3.0 37.3).
            ServiceHub.Player.EnterZone(zoneId);
            Net.NetShift.Request(Net.NetShift.ShiftAct.EnterZone, zoneId);
            OnTransitioned(zoneId);
            ServiceHub.Save.RequestAutosave(Save.SaveReason.ZoneTransition);
        }
    }

    /// <summary>
    /// The basement breaker panel (GDD 15.5).
    ///
    /// The only way to put the night reserve back, and it is three floors below the interphone
    /// on purpose. Buying power always costs whatever happens at the front door while nobody
    /// is watching it - which is the shape the whole shift is supposed to have.
    /// </summary>
    public sealed class BreakerPanel : InteractableBase
    {
        public override InteractionPrompt GetPrompt(in PlayerContext context)
        {
            var power = ServiceHub.Power;
            if (power == null) return InteractionPrompt.Blocked("ui.prompt.breaker", "ui.prompt.nothing_here");

            if (power.BreakerResetsRemaining <= 0)
                return InteractionPrompt.Blocked("ui.prompt.breaker", "ui.prompt.breaker_spent");

            if (!power.BreakerReady)
                return InteractionPrompt.Blocked("ui.prompt.breaker", "ui.prompt.breaker_cooling");

            return InteractionPrompt.Hold("ui.prompt.breaker", 1.5f);
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;

            var power = ServiceHub.Power;
            if (power == null) { reasonKey = "ui.prompt.nothing_here"; return false; }
            if (power.BreakerResetsRemaining <= 0) { reasonKey = "ui.prompt.breaker_spent"; return false; }
            if (!power.BreakerReady) { reasonKey = "ui.prompt.breaker_cooling"; return false; }

            return true;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            Net.NetShift.Request(Net.NetShift.ShiftAct.ResetBreaker);
        }
    }
}
