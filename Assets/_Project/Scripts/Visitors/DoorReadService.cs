using System;
using System.Collections.Generic;
using NO404.Core;

namespace NO404.Visitors
{
    /// <summary>
    /// Reading the person at the door (GDD 13.5 - 13.7).
    ///
    /// The verification half of this game was a database exercise: open three apps, compare
    /// three strings, press the button the comparison told you to press. It was correct and it
    /// was not a game, because at no point did the caretaker decide anything - the records
    /// decided, and the caretaker typed it in.
    ///
    /// This is the other half. The caller is a person with a state, the caretaker can push on
    /// that state, and pushing costs the two things a night is actually made of: minutes off
    /// the clock, and the goodwill of the people who live here. A liar breaks under pressure.
    /// So does a frightened neighbour's brother-in-law who is three months behind on rent, and
    /// breaking him is how a caretaker loses the building.
    ///
    /// Nothing here reports a verdict. The service says what happened; what it meant is the
    /// player's problem, which is the entire point.
    /// </summary>
    public sealed class DoorReadService
    {
        /// <summary>
        /// Game seconds each tactic costs, mirrored from <see cref="DoorRead"/> so callers of
        /// this service do not have to know where the table lives.
        /// </summary>
        public const int AskAgainSeconds = 45;
        public const int ConfrontSeconds = 90;
        public const int SilenceSeconds = 75;
        public const int ShowMeSeconds = 60;
        public const int ReassureSeconds = 40;

        /// <summary>Past this, a caller stops cooperating and starts remembering.</summary>
        public const int WalkAwayAgitation = 95;

        /// <summary>What it costs to break somebody who had nothing to hide (GDD 13.6).</summary>
        public const int InnocentBrokenTrustCost = -5;
        public const int InnocentWalkedTrustCost = -8;

        /// <summary>
        /// How much of the way to breaking a caller already is when they arrive, because of
        /// what happened the last time they stood here (GDD 13.4).
        ///
        /// Percentages of their own capacity. Being turned away from a door you had every
        /// right to walk through is not forgotten by the following Tuesday, and the caretaker
        /// who did it has less room to work with when the same person comes back - which is
        /// the whole reason the roster was cut small enough for people to come back at all.
        /// </summary>
        public const int RefusedBeforeAgitation = 25;
        public const int HeldBeforeAgitation = 10;

        readonly HashSet<string> _usedTactics = new HashSet<string>();
        readonly HashSet<string> _revealed = new HashSet<string>();
        readonly HashSet<string> _noted = new HashSet<string>();

        VisitorDefinition _visitor;

        /// <summary>
        /// The pressure actually applied so far, in the same units the responses are authored
        /// in. Agitation is this as a percentage of what was available.
        /// </summary>
        int _applied;

        public VisitorDefinition Visitor { get { return _visitor; } }

        /// <summary>
        /// How far the caretaker has pushed, as a share of how far they could - 0 to 100, and
        /// never shown as a number (GDD 13.5). The panel describes it in words, because a
        /// person watching another person through a camera does not get a readout.
        /// </summary>
        public int Agitation { get; private set; }

        /// <summary>True once the caller has gone past their breaking point.</summary>
        public bool Cracked { get; private set; }

        /// <summary>Set when a caller was pushed until they stopped answering.</summary>
        public bool WalkedOff { get; private set; }

        /// <summary>The last thing the caller said back, or null.</summary>
        public string LastReplyKey { get; private set; }

        /// <summary>Raised whenever the panel needs redrawing for a reason other than a tick.</summary>
        public event Action OnChanged;

        // -----------------------------------------------------------------
        // session
        // -----------------------------------------------------------------

        public void Begin(VisitorDefinition visitor)
        {
            _visitor = visitor;
            LastDecision = VisitorAccessLevel.Pending;
            _usedTactics.Clear();
            _revealed.Clear();
            _noted.Clear();
            _applied = 0;
            Agitation = 0;
            Cracked = false;
            WalkedOff = false;
            LastReplyKey = null;

            CarryOverFromLastTime();
            Raise();
        }

        /// <summary>
        /// What this caller remembers about the last time they were here.
        ///
        /// Only the ones who come back have anything to carry, which is a property of the
        /// roster rather than of this code: five callers a night is what made room for the
        /// same four people to appear three times each across the week, and this is what that
        /// room is for. A caretaker who refuses Oh Mi-ran on night 1 meets a harder woman on
        /// night 3, and a much harder one on night 5 when she is the one with something to
        /// lose.
        /// </summary>
        void CarryOverFromLastTime()
        {
            if (_visitor == null || string.IsNullOrEmpty(_visitor.remembersVisitorId)) return;
            if (ServiceHub.Interphone == null) return;

            LastDecision = ServiceHub.Interphone.DecisionFor(_visitor.remembersVisitorId);

            int share;
            switch (LastDecision)
            {
                case VisitorAccessLevel.Reject: share = RefusedBeforeAgitation; break;
                case VisitorAccessLevel.Hold:   share = HeldBeforeAgitation; break;

                // Being kept in the vestibule is not an insult - it is somebody doing their
                // job carefully, and most people can tell the difference.
                default: return;
            }

            int capacity = _visitor.PressureCapacity;
            if (capacity <= 0) return;

            _applied = Clamp(RoundToInt(capacity * share / 100f), 0, capacity);
            Agitation = Clamp(RoundToInt(100f * _applied / capacity), 0, 100);
        }

        /// <summary>
        /// How this caller was dealt with last time, or Pending when they have never been here
        /// before. Drives the one line on the panel that says so.
        /// </summary>
        public VisitorAccessLevel LastDecision { get; private set; }

        public void Clear() { Begin(null); }

        // -----------------------------------------------------------------
        // what can be seen right now
        // -----------------------------------------------------------------

        /// <summary>
        /// True when the caretaker is standing in the lobby rather than watching the feed.
        ///
        /// Glass tells are the reason to get out of the chair. A 240p camera pointed at a
        /// doorway cannot show that a pair of hands is shaking, or that the van behind them
        /// still has its engine running, and the walk down costs the desk.
        /// </summary>
        public static bool AtTheGlass
        {
            get { return ServiceHub.Presence != null && ServiceHub.Presence.AnyoneIn(ZoneIds.Lobby); }
        }

        public bool IsVisible(VisitorTell tell)
        {
            if (tell == null || _visitor == null) return false;
            if (tell.channel == ReadChannel.Glass && !AtTheGlass) return false;
            if (tell.unlockedBy != PressureTactic.None && !_revealed.Contains(tell.tellId)) return false;
            if (tell.fromAgitation > Agitation && !_revealed.Contains(tell.tellId)) return false;
            return true;
        }

        /// <summary>Everything observable at this instant, in authored order.</summary>
        public List<VisitorTell> VisibleTells()
        {
            var list = new List<VisitorTell>();
            if (_visitor == null || _visitor.tells == null) return list;

            for (int i = 0; i < _visitor.tells.Length; i++)
                if (IsVisible(_visitor.tells[i])) list.Add(_visitor.tells[i]);

            return list;
        }

        /// <summary>
        /// True when a tactic has already surfaced this observation, regardless of whether
        /// anybody is currently standing where they could see it.
        ///
        /// Split out from <see cref="IsVisible"/> because the two answers belong to different
        /// machines: what has been revealed is a fact about the caller and travels over the
        /// wire, while what is visible depends on where each caretaker happens to be standing
        /// and is worked out locally from that copy's own presence roster.
        /// </summary>
        public bool IsRevealed(string tellId)
        {
            return !string.IsNullOrEmpty(tellId) && _revealed.Contains(tellId);
        }

        public bool HasNoted(string tellId)
        {
            return !string.IsNullOrEmpty(tellId) && _noted.Contains(tellId);
        }

        /// <summary>
        /// Write an observation into the shift notes.
        ///
        /// The panel does not say whether it was worth writing down. A caretaker who logs
        /// three pieces of noise has three notes and knows nothing, and finds that out when
        /// the judgement comes back ungraded.
        /// </summary>
        public void Note(string tellId)
        {
            var tell = _visitor != null ? _visitor.FindTell(tellId) : null;
            if (tell == null || !IsVisible(tell)) return;
            if (!_noted.Add(tellId)) return;

            if (ServiceHub.Analytics != null)
                ServiceHub.Analytics.Track("door_tell_noted", _visitor.visitorId + ":" + tellId);
            Raise();
        }

        /// <summary>
        /// Observations that actually establish something, noise excluded. These count towards
        /// the two independent facts GDD 13.2 asks for, exactly as opening a record does -
        /// watching a courier refuse to hold the invoice up to the lens is a fact about the
        /// world, not a hunch.
        /// </summary>
        public int EstablishedFactCount
        {
            get
            {
                if (_visitor == null) return 0;
                int n = 0;
                foreach (var id in _noted)
                {
                    var tell = _visitor.FindTell(id);
                    if (tell != null && tell.weight != TellWeight.Noise) n++;
                }
                return n;
            }
        }

        /// <summary>Notes that prove nothing. Kept so the debrief can say where the time went.</summary>
        public int NoiseNoteCount
        {
            get
            {
                if (_visitor == null) return 0;
                int n = 0;
                foreach (var id in _noted)
                {
                    var tell = _visitor.FindTell(id);
                    if (tell != null && tell.weight == TellWeight.Noise) n++;
                }
                return n;
            }
        }

        // -----------------------------------------------------------------
        // pressure
        // -----------------------------------------------------------------

        public bool HasUsed(PressureTactic tactic) { return _usedTactics.Contains(tactic.ToString()); }

        /// <summary>
        /// Why a tactic is unavailable, or null when it can be used.
        ///
        /// Every refusal names itself. A button that highlights and then does nothing is
        /// indistinguishable from a broken one, and this panel has five of them.
        /// </summary>
        public string BlockedReasonKey(PressureTactic tactic)
        {
            if (_visitor == null) return "ui.door.blocked.nobody";
            if (WalkedOff) return "ui.door.blocked.walked_off";
            if (HasUsed(tactic)) return "ui.door.blocked.already_tried";

            var response = _visitor.ResponseFor(tactic);
            if (response != null && !string.IsNullOrEmpty(response.requiresAppId) &&
                (ServiceHub.Interphone == null || !ServiceHub.Interphone.HasConsulted(response.requiresAppId)))
                return "ui.door.blocked.no_record";

            return null;
        }

        public static int CostOf(PressureTactic tactic) { return DoorRead.CostOf(tactic); }

        /// <summary>
        /// Push, and pay for it.
        ///
        /// The clock moves first, because the minute is spent whether or not the caller gives
        /// anything back, and a caretaker who learns nothing has still lost the minute.
        /// </summary>
        public void Use(PressureTactic tactic)
        {
            if (tactic == PressureTactic.None) return;

            var blocked = BlockedReasonKey(tactic);
            if (blocked != null)
            {
                EventBus.Publish(new NotificationEvent(blocked, NotificationSeverity.Warning));
                return;
            }

            _usedTactics.Add(tactic.ToString());
            if (ServiceHub.Clock != null) ServiceHub.Clock.AdvanceSeconds(CostOf(tactic));

            var response = _visitor.ResponseFor(tactic);
            int delta = response != null ? response.agitationDelta : DoorRead.DefaultDelta(tactic);

            LastReplyKey = response != null && !string.IsNullOrEmpty(response.replyKey)
                ? response.replyKey
                : DefaultReplyKey(tactic);

            if (response != null && !string.IsNullOrEmpty(response.revealsTellId))
                _revealed.Add(response.revealsTellId);

            // Tells whose unlock is this tactic surface whether or not the response names them.
            if (_visitor.tells != null)
                for (int i = 0; i < _visitor.tells.Length; i++)
                    if (_visitor.tells[i] != null && _visitor.tells[i].unlockedBy == tactic)
                        _revealed.Add(_visitor.tells[i].tellId);

            ApplyAgitation(delta);

            if (ServiceHub.Analytics != null)
                ServiceHub.Analytics.Track("door_tactic", _visitor.visitorId + ":" + tactic);
            Raise();
        }

        /// <summary>
        /// How far one push moves this particular person.
        ///
        /// Against their own capacity, not against an absolute. Two callers who each give up
        /// everything they have to give both read as 100, and what differs between them is how
        /// many minutes of the night it took to get there and how far along that road they
        /// broke. A frightened neighbour is at their limit after two questions; a man running
        /// a story about a work order he does not have has four tactics' worth of composure to
        /// spend before he runs out.
        ///
        /// Measured off the running total rather than the displayed percentage, so rounding
        /// cannot leave a caller stranded at 97 with nothing left to try.
        /// </summary>
        void ApplyAgitation(int delta)
        {
            if (delta == 0) return;

            int capacity = _visitor.PressureCapacity;
            if (capacity <= 0) return;

            _applied = Clamp(_applied + delta, 0, capacity);
            Agitation = Clamp(RoundToInt(100f * _applied / capacity), 0, 100);

            if (!Cracked && Agitation >= _visitor.breakingPoint) Crack();
            if (Agitation >= WalkAwayAgitation) WalkOff();
        }

        static string DefaultReplyKey(PressureTactic tactic)
        {
            switch (tactic)
            {
                case PressureTactic.AskAgain: return "door.default.ask_again";
                case PressureTactic.Confront: return "door.default.confront";
                case PressureTactic.Silence:  return "door.default.silence";
                case PressureTactic.ShowMe:   return "door.default.show_me";
                case PressureTactic.Reassure: return "door.default.reassure";
            }
            return null;
        }

        /// <summary>
        /// The moment the story gives way - and the moment this stops being a lie detector.
        ///
        /// All three kinds of caller crack. What comes out is different, and nothing on screen
        /// labels which one just happened.
        /// </summary>
        void Crack()
        {
            Cracked = true;

            if (_visitor.tells != null)
                for (int i = 0; i < _visitor.tells.Length; i++)
                {
                    var tell = _visitor.tells[i];
                    if (tell == null || tell.weight == TellWeight.Noise) continue;
                    if (tell.fromAgitation > 0 && tell.fromAgitation <= Agitation)
                        _revealed.Add(tell.tellId);
                }

            switch (_visitor.truth)
            {
                case VisitorTruth.Deceptive:
                    LastReplyKey = "door.crack.deceptive";
                    EventBus.Publish(new NotificationEvent("ui.door.notify.story_broke",
                                                            NotificationSeverity.Urgent));
                    break;

                case VisitorTruth.Shaken:
                    LastReplyKey = !string.IsNullOrEmpty(_visitor.hiddenReasonKey)
                        ? _visitor.hiddenReasonKey
                        : "door.crack.shaken";
                    ChargeTrust(InnocentBrokenTrustCost, "reason.broke_innocent");
                    EventBus.Publish(new NotificationEvent("ui.door.notify.private_matter",
                                                            NotificationSeverity.Warning));
                    break;

                default:
                    LastReplyKey = "door.crack.legitimate";
                    ChargeTrust(InnocentBrokenTrustCost, "reason.broke_innocent");
                    EventBus.Publish(new NotificationEvent("ui.door.notify.took_offence",
                                                            NotificationSeverity.Warning));
                    break;
            }
        }

        /// <summary>
        /// They stop answering. The caretaker can still open the door or refuse it - they have
        /// simply run out of person to read, having spent a good part of the night's goodwill
        /// finding that out.
        /// </summary>
        void WalkOff()
        {
            if (WalkedOff) return;
            WalkedOff = true;
            LastReplyKey = "door.walked_off";

            if (_visitor.truth != VisitorTruth.Deceptive)
                ChargeTrust(InnocentWalkedTrustCost, "reason.pushed_too_far");

            EventBus.Publish(new NotificationEvent("ui.door.notify.walked_off",
                                                    NotificationSeverity.Warning));
        }

        static void ChargeTrust(int delta, string reasonKey)
        {
            if (ServiceHub.State == null) return;
            ServiceHub.State.AddStat(StatIds.CommunityTrust, delta, reasonKey);
        }

        // -----------------------------------------------------------------
        // how the panel describes the state (GDD 13.5: never a number)
        // -----------------------------------------------------------------

        public string AgitationKey
        {
            get
            {
                if (WalkedOff) return "ui.door.mood.silent";
                if (Agitation >= 80) return "ui.door.mood.breaking";
                if (Agitation >= 55) return "ui.door.mood.rattled";
                if (Agitation >= 30) return "ui.door.mood.uneasy";
                if (Agitation >= 12) return "ui.door.mood.guarded";
                return "ui.door.mood.calm";
            }
        }

        /// <summary>
        /// Take the host's version of this caller's state (v3.0 46.1).
        ///
        /// The masks are indexed against the visitor's own tell array, which both machines
        /// have from the same content, so nothing about the caller travels - only what has
        /// happened to them. A client never decides any of this; it is told.
        /// </summary>
        public void ApplyMirror(int agitation, bool cracked, bool walkedOff, string lastReplyKey,
                                int revealedMask, int notedMask, int usedTacticsMask)
        {
            Agitation = Clamp(agitation, 0, 100);
            Cracked = cracked;
            WalkedOff = walkedOff;
            LastReplyKey = string.IsNullOrEmpty(lastReplyKey) ? null : lastReplyKey;

            _revealed.Clear();
            _noted.Clear();
            _usedTactics.Clear();

            if (_visitor != null && _visitor.tells != null)
            {
                for (int i = 0; i < _visitor.tells.Length && i < 32; i++)
                {
                    var tell = _visitor.tells[i];
                    if (tell == null) continue;

                    if ((revealedMask & (1 << i)) != 0) _revealed.Add(tell.tellId);
                    if ((notedMask & (1 << i)) != 0) _noted.Add(tell.tellId);
                }
            }

            for (int i = 0; i < DoorRead.All.Length; i++)
                if ((usedTacticsMask & (1 << i)) != 0) _usedTactics.Add(DoorRead.All[i].ToString());

            Raise();
        }

        void Raise() { var cb = OnChanged; if (cb != null) cb(); }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        static int RoundToInt(float v)
        {
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }
    }
}
