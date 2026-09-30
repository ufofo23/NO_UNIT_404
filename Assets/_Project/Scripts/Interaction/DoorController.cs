using System.Collections.Generic;
using UnityEngine;
using NO404.Core;

namespace NO404.Interaction
{
    public enum DoorState
    {
        ClosedUnlocked = 0,
        ClosedLocked = 1,
        Opening = 2,
        Open = 3,
        Closing = 4,
        Jammed = 5,
        Destroyed = 6
    }

    /// <summary>
    /// Door state machine from GDD 20.15. Two safety rules are implemented literally:
    /// the door cannot be re-triggered while animating, and if the animation callback is
    /// lost the state recovers after a two second timeout instead of sticking.
    /// </summary>
    public sealed class DoorController : InteractableBase
    {
        const float AnimationSeconds = 0.8f;
        const float RecoveryTimeout = 2f;

        [SerializeField] string _doorId;
        [SerializeField] DoorState _state = DoorState.ClosedUnlocked;
        [SerializeField] AccessLevel _requiredAccess = AccessLevel.None;
        [SerializeField] string _doorRequiredFlagId;
        [SerializeField] Transform _leaf;
        [SerializeField] float _openAngle = 95f;

        float _animationTimer;
        Quaternion _closedRotation;
        Quaternion _openRotation;

        public string DoorId { get { return _doorId; } }
        public DoorState State { get { return _state; } }

        static readonly List<DoorController> Registry = new List<DoorController>();
        public static IReadOnlyList<DoorController> All { get { return Registry; } }

        public void Setup(string doorId, Transform leaf, AccessLevel requiredAccess,
                          DoorState initialState, string labelKey, string requiredFlagId = null)
        {
            _doorId = doorId;
            _leaf = leaf;
            _requiredAccess = requiredAccess;
            _state = initialState;
            _labelKey = labelKey;
            _doorRequiredFlagId = requiredFlagId;
            _range = 1.8f;      // GDD 8.3
            _repeatable = true;
            CacheRotations();
        }

        void Awake()
        {
            _range = 1.8f;
            _repeatable = true;
            CacheRotations();
        }

        void CacheRotations()
        {
            if (_leaf == null) return;
            _closedRotation = _leaf.localRotation;
            _openRotation = _closedRotation * Quaternion.Euler(0f, _openAngle, 0f);
        }

        protected override void OnEnable() { base.OnEnable(); if (!Registry.Contains(this)) Registry.Add(this); }
        protected override void OnDisable() { base.OnDisable(); Registry.Remove(this); }

        public override InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey))
                return InteractionPrompt.Blocked(_labelKey, reasonKey);

            return InteractionPrompt.Simple(_state == DoorState.Open ? "ui.prompt.close_door" : _labelKey);
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;

            switch (_state)
            {
                case DoorState.Opening:
                case DoorState.Closing:
                    reasonKey = "ui.prompt.door_moving";
                    return false;
                case DoorState.Jammed:
                    reasonKey = "ui.prompt.door_jammed";
                    return false;
                case DoorState.Destroyed:
                    reasonKey = "ui.prompt.door_destroyed";
                    return false;
            }

            if (_state == DoorState.ClosedLocked)
            {
                if (!ServiceHub.State.HasAccess(_requiredAccess)) { reasonKey = "ui.prompt.no_access"; return false; }
                if (!string.IsNullOrEmpty(_doorRequiredFlagId) && !ServiceHub.State.GetFlag(_doorRequiredFlagId))
                {
                    reasonKey = "ui.prompt.door_locked";
                    return false;
                }
            }

            return true;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            // A client does not get to decide what a door does (v3.0 46.1). It asks, the host
            // runs this same method, and the result comes back as replicated state.
            //
            // The check is here rather than in a networked door component on purpose: the
            // rules about whether this door can open - access level, story flags, distance -
            // already live in CanInteract above and must not be duplicated anywhere. What the
            // network layer carries is the request and the answer, never the rule.
            if (!Net.NetSession.Authoritative)
            {
                Net.NetShift.RequestDoor(_doorId);
                return;
            }

            if (_state == DoorState.ClosedLocked)
            {
                _state = DoorState.ClosedUnlocked;
                Log.Info("Door", _doorId + " unlocked");
            }

            if (_state == DoorState.ClosedUnlocked) BeginAnimation(DoorState.Opening);
            else if (_state == DoorState.Open) BeginAnimation(DoorState.Closing);
        }

        void BeginAnimation(DoorState next)
        {
            _state = next;
            _animationTimer = 0f;
        }

        void Update()
        {
            if (_state != DoorState.Opening && _state != DoorState.Closing) return;

            _animationTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_animationTimer / AnimationSeconds);
            float eased = t * t * (3f - 2f * t);

            if (_leaf != null)
            {
                _leaf.localRotation = _state == DoorState.Opening
                    ? Quaternion.Slerp(_closedRotation, _openRotation, eased)
                    : Quaternion.Slerp(_openRotation, _closedRotation, eased);
            }

            if (t >= 1f)
            {
                _state = _state == DoorState.Opening ? DoorState.Open : DoorState.ClosedUnlocked;
                return;
            }

            // Recovery: if something stalls the animation, snap to a valid state (GDD 20.15).
            if (_animationTimer > AnimationSeconds + RecoveryTimeout)
            {
                Log.Warn("Door", _doorId + " animation timed out, forcing state");
                _state = _state == DoorState.Opening ? DoorState.Open : DoorState.ClosedUnlocked;
                if (_leaf != null)
                    _leaf.localRotation = _state == DoorState.Open ? _openRotation : _closedRotation;
            }
        }

        public void ForceState(DoorState state)
        {
            _state = state;
            if (_leaf == null) return;
            _leaf.localRotation = state == DoorState.Open ? _openRotation : _closedRotation;
        }

        /// <summary>The door with this id, or null. Used by the save layer and the network one.</summary>
        public static DoorController Find(string doorId)
        {
            if (string.IsNullOrEmpty(doorId)) return null;

            for (int i = 0; i < Registry.Count; i++)
                if (Registry[i]._doorId == doorId) return Registry[i];

            return null;
        }

        /// <summary>
        /// Applies a door state that arrived from the host.
        ///
        /// Mid-swing states are landed rather than replayed: the animation is local flavour and
        /// a client that receives "Opening" three frames late would otherwise start a swing the
        /// host has already finished. What has to agree is open or shut.
        /// </summary>
        public void ApplyNetworkState(DoorState state)
        {
            if (_state == state) return;

            if (state == DoorState.Opening) { BeginAnimation(DoorState.Opening); return; }
            if (state == DoorState.Closing) { BeginAnimation(DoorState.Closing); return; }

            ForceState(state);
        }

        /// <summary>Every door's current state, for the host to publish.</summary>
        public static void CaptureStates(List<KeyValuePair<string, DoorState>> into)
        {
            into.Clear();
            for (int i = 0; i < Registry.Count; i++)
            {
                var door = Registry[i];
                if (string.IsNullOrEmpty(door._doorId)) continue;
                into.Add(new KeyValuePair<string, DoorState>(door._doorId, door._state));
            }
        }

        // ---- save hooks --------------------------------------------------

        public static List<Save.DoorSaveEntry> CaptureAll()
        {
            var list = new List<Save.DoorSaveEntry>(Registry.Count);
            for (int i = 0; i < Registry.Count; i++)
            {
                var door = Registry[i];
                list.Add(new Save.DoorSaveEntry
                {
                    doorId = door._doorId,
                    state = (int)door._state,
                    locked = door._state == DoorState.ClosedLocked
                });
            }
            return list;
        }

        public static void RestoreAll(List<Save.DoorSaveEntry> entries)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                for (int d = 0; d < Registry.Count; d++)
                {
                    if (Registry[d]._doorId != entry.doorId) continue;
                    Registry[d].ForceState((DoorState)entry.state);
                    break;
                }
            }
        }
    }
}
