using UnityEngine;
using NO404.Core;
using NO404.Pressure;

namespace NO404.Interaction
{
    /// <summary>What the office door is doing right now (GDD 15.6).</summary>
    public enum OfficeDoorState
    {
        Quiet = 0,
        /// <summary>Three knocks. The pressure ladder has reached the office (Stage.AtTheDoor).</summary>
        Knocking = 1,
        /// <summary>The handle is turning. Stage.Inside - a bolted door is the only thing left.</summary>
        HandleTurning = 2
    }

    /// <summary>
    /// The office door (GDD 15.6).
    ///
    /// GDD 15.1 always claimed the office was the safe room the corridors were measured
    /// against, but nothing had ever been on the other side of its door, so "safe" and
    /// "inert" looked the same from the desk. This gives the room a wall that can be
    /// knocked on and a bolt the player has to decide about.
    ///
    /// The bolt is a real trade, not a panic button. Locked, the night backs off a point a
    /// minute - and every caller at the front interphone stands there until it is opened
    /// again, which is exactly the throughput the performance score is measuring.
    ///
    /// Nothing here can kill. At the top of the ladder NightPressureService charges evidence,
    /// performance and ten game minutes, and the corridor goes quiet (GDD 15.2).
    /// </summary>
    public sealed class OfficeDoorController : InteractableBase
    {
        /// <summary>Real seconds between knock cues while the door is being knocked on.</summary>
        public const float KnockIntervalSeconds = 6f;
        /// <summary>How long the peephole takes to use, in real seconds.</summary>
        public const float PeepholeHoldSeconds = 2f;

        public static OfficeDoorController Instance { get; private set; }

        OfficeDoorState _state = OfficeDoorState.Quiet;
        float _nextKnockAt;
        bool _locked;
        int _scriptedKnocksLeft;

        public OfficeDoorState State { get { return _state; } }
        public bool Locked { get { return _locked; } }

        /// <summary>True while there is genuinely something outside to see (GDD 4.4).</summary>
        public bool SomethingOutside { get { return _state != OfficeDoorState.Quiet; } }

        protected override void OnEnable()
        {
            base.OnEnable();
            Instance = this;
            EventBus.Subscribe<PressureStageChangedEvent>(OnStageChanged);
            EventBus.Subscribe<NightStartedEvent>(OnNightStarted);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<PressureStageChangedEvent>(OnStageChanged);
            EventBus.Unsubscribe<NightStartedEvent>(OnNightStarted);
            if (Instance == this) Instance = null;
        }

        protected override void OnPlaythroughReset()
        {
            _scriptedKnocksLeft = 0;
            SetLocked(false);
            SetState(OfficeDoorState.Quiet);
        }

        void OnNightStarted(NightStartedEvent evt)
        {
            _scriptedKnocksLeft = 0;
            SetLocked(false);
            SetState(OfficeDoorState.Quiet);
        }

        /// <summary>
        /// A knock the script asks for rather than the ladder (GDD 9.1 / 9.2).
        ///
        /// The door is the strongest verb the office has and the ladder does not reach it
        /// until pressure 50, which the first two nights never get near - so a new player met
        /// the bolt and the peephole for the first time somewhere on night 3, well past the
        /// point where they had already decided whether the game was for them. Twice in the
        /// early shifts the script knocks on its own: at the end of the prologue, and on the
        /// first return from the eighth floor, which is the first time the office has been
        /// empty. It costs nothing and moves nothing - the peephole finds an empty corridor,
        /// exactly as GDD 4.4 requires - and it stops after a fixed number of knocks so an
        /// unanswered door does not knock until dawn.
        /// </summary>
        public void ScriptedKnock(int knocks)
        {
            if (knocks <= 0) return;

            // The ladder outranks the script: if something is genuinely at the door already,
            // this would only turn a real warning into a scripted one.
            if (_state != OfficeDoorState.Quiet) return;

            _scriptedKnocksLeft = knocks;
            SetState(OfficeDoorState.Knocking);
        }

        void OnStageChanged(PressureStageChangedEvent evt)
        {
            // The ladder taking over ends the script's count, so a scripted knock that is
            // still running cannot cut a real warning short.
            if (evt.Current == PressureStage.AtTheDoor || evt.Current == PressureStage.Inside)
                _scriptedKnocksLeft = 0;

            switch (evt.Current)
            {
                case PressureStage.AtTheDoor:
                    SetState(OfficeDoorState.Knocking);
                    break;
                case PressureStage.Inside:
                    SetState(OfficeDoorState.HandleTurning);
                    break;
                default:
                    SetState(OfficeDoorState.Quiet);
                    break;
            }
        }

        void SetState(OfficeDoorState next)
        {
            if (_state == next) return;
            if (next != OfficeDoorState.Knocking) _scriptedKnocksLeft = 0;
            _state = next;
            _nextKnockAt = 0f;
            EventBus.Publish(new OfficeDoorChangedEvent(next, _locked));

            if (next == OfficeDoorState.Quiet) return;

            ServiceHub.Captions.Ambient(next == OfficeDoorState.Knocking
                ? "caption.office_door_knock"
                : "caption.office_door_handle");
        }

        void Update()
        {
            if (_state == OfficeDoorState.Quiet) return;
            if (Time.unscaledTime < _nextKnockAt) return;

            _nextKnockAt = Time.unscaledTime + KnockIntervalSeconds;

            // At the door, not in the player's head. Whoever is out there has a location and
            // the player has to be able to hear where it is (GDD 19.3).
            ServiceHub.Audio.PlayCueAt(_state == OfficeDoorState.Knocking
                ? AudioCue.OfficeDoorKnock
                : AudioCue.OfficeDoorHandle,
                transform.position);

            // A scripted knock is a finite number of knocks. The ladder's is not: while the
            // pressure is genuinely at the door the door keeps being knocked on.
            if (_scriptedKnocksLeft <= 0) return;
            if (--_scriptedKnocksLeft <= 0) SetState(OfficeDoorState.Quiet);
        }

        // ---- the bolt --------------------------------------------------------

        public override InteractionPrompt GetPrompt(in PlayerContext context)
        {
            var power = ServiceHub.Power;
            if (power != null && power.IsDead)
                return InteractionPrompt.Blocked("ui.prompt.office_door_lock", "ui.prompt.bolt_dead");

            return InteractionPrompt.Simple(_locked ? "ui.prompt.office_door_unlock" : "ui.prompt.office_door_lock");
        }

        public override bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;

            // The bolt is electric. With the reserve at zero it will not throw (GDD 15.5).
            var power = ServiceHub.Power;
            if (power != null && power.IsDead && !_locked) { reasonKey = "ui.prompt.bolt_dead"; return false; }

            return true;
        }

        protected override void OnInteract(in PlayerContext context)
        {
            // One bolt on one door. Whoever throws it, it is thrown for the shift - and the
            // callers it holds at the front interphone are held for everybody's performance
            // score, which is the whole trade GDD 15.6 is describing.
            Net.NetShift.Request(Net.NetShift.ShiftAct.OfficeBolt, null, null, _locked ? 0 : 1);
        }

        /// <summary>
        /// The office door as the host has it (v3.0 46.1).
        ///
        /// The bolt and the knocking both reach a client this way: the pressure ladder that
        /// drives the state runs on the host only, and the bolt is a decision the host made on
        /// somebody's behalf. The notification is deliberately not raised again here - the
        /// host published one and NetShift already handed it on - but the sound and the event
        /// are, because those are how this copy's room learns what happened in it.
        /// </summary>
        public void ApplyNetworkState(OfficeDoorState state, bool locked)
        {
            bool changed = _state != state || _locked != locked;
            if (!changed) return;

            if (_locked != locked)
            {
                _locked = locked;
                if (ServiceHub.Interphone != null) ServiceHub.Interphone.SetDoorBlocked(locked);
                ServiceHub.Audio.PlayCueAt(AudioCue.DoorOpen, transform.position);
            }

            if (_state != state)
            {
                _state = state;
                _nextKnockAt = 0f;
                _scriptedKnocksLeft = 0;

                if (state != OfficeDoorState.Quiet)
                    ServiceHub.Captions.Ambient(state == OfficeDoorState.Knocking
                        ? "caption.office_door_knock"
                        : "caption.office_door_handle");
            }

            EventBus.Publish(new OfficeDoorChangedEvent(_state, _locked));
        }

        public void SetLocked(bool locked)
        {
            if (_locked == locked) return;
            _locked = locked;

            if (ServiceHub.Pressure != null) ServiceHub.Pressure.OfficeDoorLocked = locked;
            if (ServiceHub.Interphone != null) ServiceHub.Interphone.SetDoorBlocked(locked);

            ServiceHub.Audio.PlayCueAt(AudioCue.DoorOpen, transform.position);
            EventBus.Publish(new OfficeDoorChangedEvent(_state, _locked));
            EventBus.Publish(new NotificationEvent(locked ? "ui.notify.office_door_locked"
                                                          : "ui.notify.office_door_unlocked",
                                                  NotificationSeverity.Info));
            Log.Info("OfficeDoor", locked ? "bolted" : "unbolted");
        }

        /// <summary>
        /// What the person with their eye to the hole sees.
        ///
        /// Local, and only theirs: two other caretakers upstairs did not look, and telling
        /// them what is in the corridor would give away the one thing this verb costs two
        /// seconds of blindness to find out.
        /// </summary>
        public void PeepholeCaption()
        {
            ServiceHub.Captions.Ambient(!SomethingOutside ? "caption.peephole_empty"
                                      : _state == OfficeDoorState.Knocking ? "caption.peephole_figure"
                                      : "caption.peephole_close");
        }

        /// <summary>
        /// Called by the peephole. Seeing what is actually there is worth ten points of
        /// pressure; seeing nothing is worth nothing, which is the whole reason to look.
        ///
        /// Host only, through NetShift: the pressure ladder is one ladder for the building,
        /// and a client charging its own mirrored copy would have the number wiped by the next
        /// push - so looking would have been free for everybody but the host.
        /// </summary>
        public void UsePeephole()
        {
            if (ServiceHub.Pressure == null) return;
            if (!SomethingOutside) return;

            ServiceHub.Pressure.NotePeephole();
            SetState(OfficeDoorState.Quiet);
        }
    }

    /// <summary>
    /// The peephole, next to the bolt. A two-second hold with no way to react during it -
    /// which is the cost of finding out, and the reason it is a separate verb from locking.
    /// </summary>
    public sealed class OfficeDoorPeephole : InteractableBase
    {
        [SerializeField] OfficeDoorController _door;

        public void Bind(OfficeDoorController door) { _door = door; }

        public override InteractionPrompt GetPrompt(in PlayerContext context)
        {
            return InteractionPrompt.Hold("ui.prompt.office_door_peephole",
                                          OfficeDoorController.PeepholeHoldSeconds);
        }

        protected override void OnInteract(in PlayerContext context)
        {
            if (_door == null) _door = OfficeDoorController.Instance;
            if (_door == null) return;

            _door.PeepholeCaption();
            Net.NetShift.Request(Net.NetShift.ShiftAct.Peephole);
        }
    }
}
