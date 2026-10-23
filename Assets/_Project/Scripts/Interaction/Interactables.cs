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
        [Tooltip("Last night this object is usable. -1 = no end.")]
        [SerializeField] protected int _untilNight = -1;
        [Tooltip("Once this flag is set the object does nothing. Empty = never.")]
        [SerializeField] protected string _blockingFlagId;

        protected bool _used;

        /// <summary>What an object does while its gate is shut.</summary>
        protected enum WhenUnavailable
        {
            /// <summary>Stays, and says "nothing here yet" when looked at. The old default.</summary>
            Prompt = 0,
            /// <summary>Not in the room at all: no renderer, no collider. Things that arrive.</summary>
            Hidden = 1,
            /// <summary>Stays in the room but cannot be aimed at: a wall clock, a doormat.</summary>
            Quiet = 2
        }

        [SerializeField] protected WhenUnavailable _whenUnavailable = WhenUnavailable.Prompt;

        public float Range { get { return _range; } }

        /// <summary>
        /// For a thing the story says arrives - a slipper at the door, a body in a burnt room.
        /// Until its gate opens it is not there, rather than standing about inert.
        /// </summary>
        public void HideWhenUnavailable() { _whenUnavailable = WhenUnavailable.Hidden; SyncVisibility(); }

        /// <summary>
        /// For a fixture that belongs to the building all week but only means something on its
        /// night: it stays where it is and simply cannot be aimed at, so it never says
        /// "nothing here yet".
        /// </summary>
        public void QuietWhenUnavailable() { _whenUnavailable = WhenUnavailable.Quiet; SyncVisibility(); }

        /// <summary>Whether it is drawn and solid. Gate shut and Hidden: no.</summary>
        protected virtual bool ShouldBeVisible()
        {
            return _whenUnavailable != WhenUnavailable.Hidden || IsAvailable();
        }

        /// <summary>Whether the interaction ray can land on it. Gate shut and Quiet: no.</summary>
        protected virtual bool ShouldBeTargetable()
        {
            return ShouldBeVisible() && (_whenUnavailable != WhenUnavailable.Quiet || IsAvailable());
        }

        /// <summary>Puts renderers and colliders in step with the gate. Public for tests.</summary>
        public void SyncVisibility()
        {
            if (_whenUnavailable == WhenUnavailable.Prompt && !ManagesOwnVisibility) return;
            if (!ServiceHub.Ready) return;

            bool visible = ShouldBeVisible();
            bool targetable = visible && ShouldBeTargetable();
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = visible;
            foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = targetable;
        }

        /// <summary>Set by a subclass that hides itself for reasons beyond the gate.</summary>
        protected virtual bool ManagesOwnVisibility { get { return false; } }

        /// <summary>
        /// Office/lobby objects outlive a single playthrough (GDD 20.5: those zones are always
        /// resident), so a new playthrough has to clear their one-shot state explicitly instead
        /// of relying on the object being re-created.
        /// </summary>
        protected virtual void OnEnable()
        {
            EventBus.Subscribe<PlaythroughResetEvent>(HandlePlaythroughReset);
            EventBus.Subscribe<NightStartedEvent>(HandleNightStartedForVisibility);
            EventBus.Subscribe<FlagChangedEvent>(HandleFlagChangedForVisibility);
            EventBus.Subscribe<SaveRestoredEvent>(HandleSaveRestoredForVisibility);
            EventBus.Subscribe<EvidenceAcquiredEvent>(HandleEvidenceForVisibility);
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<PlaythroughResetEvent>(HandlePlaythroughReset);
            EventBus.Unsubscribe<NightStartedEvent>(HandleNightStartedForVisibility);
            EventBus.Unsubscribe<FlagChangedEvent>(HandleFlagChangedForVisibility);
            EventBus.Unsubscribe<SaveRestoredEvent>(HandleSaveRestoredForVisibility);
            EventBus.Unsubscribe<EvidenceAcquiredEvent>(HandleEvidenceForVisibility);
        }

        // Everything a gate can open or close on is one of these, so nothing polls.
        void HandleNightStartedForVisibility(NightStartedEvent evt) { SyncVisibility(); }
        void HandleFlagChangedForVisibility(FlagChangedEvent evt) { SyncVisibility(); }
        void HandleSaveRestoredForVisibility(SaveRestoredEvent evt) { SyncVisibility(); }
        void HandleEvidenceForVisibility(EvidenceAcquiredEvent evt) { SyncVisibility(); }

        protected virtual void Start() { SyncVisibility(); }

        void HandlePlaythroughReset(PlaythroughResetEvent evt)
        {
            _used = false;
            OnPlaythroughReset();
            SyncVisibility();
        }

        /// <summary>Override to undo any extra one-shot effect (e.g. a hidden renderer).</summary>
        protected virtual void OnPlaythroughReset() { }

        /// <summary>
        /// Gates an object behind story progress. Objects that are not yet available stay in
        /// the world but are inert and show no prompt, so the greybox layout never changes
        /// shape between nights.
        /// </summary>
        /// <summary>
        /// Something else happening first takes this away: the fire reaching the room, the
        /// chance having been spent on the other thing.
        /// </summary>
        public void BlockWhen(string flagId) { _blockingFlagId = flagId; SyncVisibility(); }

        public void SetAvailability(string requiredFlagId, int fromNight, int untilNight = -1)
        {
            _requiredFlagId = requiredFlagId;
            _fromNight = fromNight;
            _untilNight = untilNight;
            SyncVisibility();
        }

        protected bool IsAvailable()
        {
            if (_fromNight >= 0 && ServiceHub.State.NightIndex < _fromNight) return false;
            if (_untilNight >= 0 && ServiceHub.State.NightIndex > _untilNight) return false;
            if (!string.IsNullOrEmpty(_blockingFlagId) && ServiceHub.State.GetFlag(_blockingFlagId)) return false;
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

        public string EvidenceId { get { return _evidenceId; } }
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
        /// the office until the child has been at the door (GDD 9.1).
        /// </summary>
        public void HideUntilAvailable()
        {
            _hiddenUntilAvailable = true;
            HideWhenUnavailable();
        }

        protected override bool ManagesOwnVisibility { get { return _hideOnPickup; } }

        protected override bool ShouldBeVisible()
        {
            if (_hiddenUntilAvailable && !IsAvailable()) return false;
            if (!base.ShouldBeVisible()) return false;
            // Taken is gone. Read-not-taken (a clock, a ledger) stays on the wall.
            return !_hideOnPickup || ServiceHub.Evidence == null || !ServiceHub.Evidence.Has(_evidenceId);
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
            SyncVisibility();
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
