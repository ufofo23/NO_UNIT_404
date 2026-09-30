using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NO404.Core;
using NO404.Visitors;

namespace NO404.Net
{
    /// <summary>
    /// The shift itself, shared (v3.0 46.1).
    ///
    /// The host simulates; this carries the parts of that simulation a second caretaker has to
    /// be able to see and act on, and carries their actions back. Two things made the design
    /// obvious once they were noticed.
    ///
    /// First, both machines already have identical content - the same visitors, the same
    /// tells, the same strings. So nothing about *what a caller is* needs sending. Only what
    /// has happened to them does, and that compresses to a handful of masks: which records
    /// have been opened, which observations have surfaced, which have been written down, which
    /// ways of pushing have been spent. Eight bits each, and the client rebuilds the whole
    /// interphone panel from its own copy of the content.
    ///
    /// Second, the client's UI should not know it is a client. So the snapshot is applied
    /// *into* the client's own services rather than parked in a parallel structure - the
    /// panel goes on reading <c>ServiceHub.Interphone</c> exactly as it does in single player,
    /// and the mirror is the only code that knows the difference.
    /// </summary>
    public sealed partial class NetShift : NetworkBehaviour
    {
        public static NetShift Instance { get; private set; }

        /// <summary>What the door looks like right now, in about forty bytes.</summary>
        public struct DoorSnapshot : INetworkSerializable, System.IEquatable<DoorSnapshot>
        {
            public FixedString64Bytes VisitorId;
            public FixedString64Bytes LastReplyKey;
            public int ConsultedApps;      // bit per AppIds.Order index
            public int RevealedTells;      // bit per index into the visitor's tells
            public int NotedTells;
            public int UsedTactics;        // bit per PressureTactic
            public short Agitation;
            public byte Flags;             // 1 cracked, 2 walked off

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref VisitorId);
                s.SerializeValue(ref LastReplyKey);
                s.SerializeValue(ref ConsultedApps);
                s.SerializeValue(ref RevealedTells);
                s.SerializeValue(ref NotedTells);
                s.SerializeValue(ref UsedTactics);
                s.SerializeValue(ref Agitation);
                s.SerializeValue(ref Flags);
            }

            public bool Equals(DoorSnapshot o)
            {
                return VisitorId.Equals(o.VisitorId) && LastReplyKey.Equals(o.LastReplyKey) &&
                       ConsultedApps == o.ConsultedApps && RevealedTells == o.RevealedTells &&
                       NotedTells == o.NotedTells && UsedTactics == o.UsedTactics &&
                       Agitation == o.Agitation && Flags == o.Flags;
            }
        }

        /// <summary>
        /// One door, as everybody else needs to see it.
        ///
        /// Only the id and the state travel. What a door is, where it hangs and what it takes
        /// to open it is content both machines already have.
        /// </summary>
        public struct DoorSync : INetworkSerializable, System.IEquatable<DoorSync>
        {
            public FixedString64Bytes DoorId;
            public byte State;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref DoorId);
                s.SerializeValue(ref State);
            }

            public bool Equals(DoorSync o) { return DoorId.Equals(o.DoorId); }
        }

        /// <summary>One row of the tracking board (v3.0 38.4).</summary>
        public struct VisitorCard : INetworkSerializable, System.IEquatable<VisitorCard>
        {
            public FixedString64Bytes VisitorId;
            public FixedString32Bytes LastKnownZone;
            public int LastSeenSecond;
            public int EnteredSecond;
            public int Flags;
            public byte Level;
            public byte State;
            public bool Revoked;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref VisitorId);
                s.SerializeValue(ref LastKnownZone);
                s.SerializeValue(ref LastSeenSecond);
                s.SerializeValue(ref EnteredSecond);
                s.SerializeValue(ref Flags);
                s.SerializeValue(ref Level);
                s.SerializeValue(ref State);
                s.SerializeValue(ref Revoked);
            }

            public bool Equals(VisitorCard o) { return VisitorId.Equals(o.VisitorId); }
        }

        /// <summary>
        /// The night the host is running, or 0 when they are still in the menu. A client
        /// watches this rather than being pushed, so somebody who joins ten minutes late
        /// arrives on the right night without any extra handshake.
        /// </summary>
        readonly NetworkVariable<int> _nightIndex = new NetworkVariable<int>();

        readonly NetworkVariable<int> _clockSecond = new NetworkVariable<int>();
        readonly NetworkVariable<DoorSnapshot> _door = new NetworkVariable<DoorSnapshot>();
        readonly NetworkList<VisitorCard> _board = new NetworkList<VisitorCard>();
        readonly NetworkList<DoorSync> _doors = new NetworkList<DoorSync>();

        readonly List<KeyValuePair<string, Interaction.DoorState>> _doorScratch =
            new List<KeyValuePair<string, Interaction.DoorState>>();

        public override void OnNetworkSpawn()
        {
            Instance = this;

            OpenNightChannel();

            if (IsServer) return;

            // A joining caretaker starts from whatever the shift is already doing.
            ApplyClock(_clockSecond.Value);
            ApplyDoor(_door.Value);
            ApplyBoard();
            ApplyDoors();

            FollowNight(_nightIndex.Value);
            _nightIndex.OnValueChanged += (_, v) => FollowNight(v);
            _clockSecond.OnValueChanged += (_, v) => ApplyClock(v);
            _door.OnValueChanged += (_, v) => ApplyDoor(v);
            _board.OnListChanged += _ => ApplyBoard();
            _doors.OnListChanged += _ => ApplyDoors();
        }

        public override void OnNetworkDespawn()
        {
            CloseNightChannel();
            if (Instance == this) Instance = null;
        }

        // -----------------------------------------------------------------
        // host: publish
        // -----------------------------------------------------------------

        public void Publish()
        {
            if (!IsServer) return;

            _nightIndex.Value = GameLoop.Instance != null && GameLoop.Instance.Mode == GameMode.Playing
                ? ServiceHub.State.NightIndex
                : 0;

            _clockSecond.Value = ServiceHub.Clock.GameSecond;
            _door.Value = CaptureDoor();
            CaptureBoard();
            CaptureDoors();
            CaptureCameras();
            CapturePhone();
            CaptureOfficeDoor();
            PushMirror();
        }

        string _doorSignature;

        /// <summary>
        /// Publishes every door whose state has moved.
        ///
        /// Signature-gated like the visitor board, and for the same reason: Publish runs every
        /// frame, and any write dirties a NetworkList. A building full of doors that nobody is
        /// touching should cost nothing.
        /// </summary>
        void CaptureDoors()
        {
            Interaction.DoorController.CaptureStates(_doorScratch);

            var signature = string.Empty;
            for (int i = 0; i < _doorScratch.Count; i++)
                signature += _doorScratch[i].Key + ":" + (int)_doorScratch[i].Value + ";";

            if (signature == _doorSignature) return;
            _doorSignature = signature;

            _doors.Clear();
            for (int i = 0; i < _doorScratch.Count; i++)
                _doors.Add(new DoorSync
                {
                    DoorId = _doorScratch[i].Key,
                    State = (byte)_doorScratch[i].Value
                });
        }

        void ApplyDoors()
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                var row = _doors[i];
                var door = Interaction.DoorController.Find(row.DoorId.ToString());
                if (door != null) door.ApplyNetworkState((Interaction.DoorState)row.State);
            }
        }

        static DoorSnapshot CaptureDoor()
        {
            var interphone = ServiceHub.Interphone;
            var read = interphone.Read;
            var visitor = interphone.Active;

            var snapshot = new DoorSnapshot
            {
                VisitorId = visitor != null ? visitor.visitorId : string.Empty,
                LastReplyKey = read.LastReplyKey ?? string.Empty,
                Agitation = (short)read.Agitation,
                Flags = (byte)((read.Cracked ? 1 : 0) | (read.WalkedOff ? 2 : 0))
            };

            for (int i = 0; i < AppIds.Order.Length; i++)
                if (interphone.HasConsulted(AppIds.Order[i])) snapshot.ConsultedApps |= 1 << i;

            if (visitor != null && visitor.tells != null)
            {
                for (int i = 0; i < visitor.tells.Length && i < 32; i++)
                {
                    var tell = visitor.tells[i];
                    if (tell == null) continue;

                    // Revealed is asked without the glass condition on purpose: whether a tell
                    // is *visible* depends on where each caretaker is standing, and each
                    // client works that out for itself from its own presence roster.
                    if (read.IsRevealed(tell.tellId)) snapshot.RevealedTells |= 1 << i;
                    if (read.HasNoted(tell.tellId)) snapshot.NotedTells |= 1 << i;
                }
            }

            for (int i = 0; i < DoorRead.All.Length; i++)
                if (read.HasUsed(DoorRead.All[i])) snapshot.UsedTactics |= 1 << i;

            return snapshot;
        }

        string _boardSignature;

        void CaptureBoard()
        {
            var live = ServiceHub.ActiveVisitors.All;

            // Publish() runs every frame, and a NetworkList is dirtied by any write - so
            // rebuilding unconditionally meant sending the whole board thirty times a second
            // for a shift where nothing had happened. The signature is the cheap half of a
            // diff: it decides *whether* to rebuild, and the rebuild itself stays wholesale
            // because a wrong diff would leave a caretaker chasing somebody who already left.
            var signature = string.Empty;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                signature += t.VisitorId + ":" + t.LastKnownZone + ":" + t.LastKnownSecond + ":" +
                             (int)t.Flags + ":" + (int)t.State + ":" +
                             (t.Token != null && t.Token.revoked ? "R" : "-") + ";";
            }

            if (signature == _boardSignature) return;
            _boardSignature = signature;

            _board.Clear();

            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                _board.Add(new VisitorCard
                {
                    VisitorId = t.VisitorId ?? string.Empty,
                    LastKnownZone = t.LastKnownZone ?? string.Empty,
                    LastSeenSecond = t.LastKnownSecond,
                    EnteredSecond = t.EnteredSecond,
                    Flags = (int)t.Flags,
                    Level = (byte)(t.Token != null ? t.Token.level : VisitorAccessLevel.Pending),
                    State = (byte)t.State,
                    Revoked = t.Token != null && t.Token.revoked
                });
            }
        }

        // -----------------------------------------------------------------
        // client: apply
        // -----------------------------------------------------------------

        static void ApplyClock(int second)
        {
            // The clock is the one thing that must never disagree. A second caretaker whose
            // shift ends four minutes early is a bug report nobody can reproduce.
            ServiceHub.Clock.MirrorTo(second);
        }

        static void ApplyDoor(DoorSnapshot snapshot)
        {
            ServiceHub.Interphone.ApplyMirror(snapshot.VisitorId.ToString(), snapshot.ConsultedApps);
            ServiceHub.Interphone.Read.ApplyMirror(
                snapshot.Agitation,
                (snapshot.Flags & 1) != 0,
                (snapshot.Flags & 2) != 0,
                snapshot.LastReplyKey.ToString(),
                snapshot.RevealedTells,
                snapshot.NotedTells,
                snapshot.UsedTactics);
        }

        void ApplyBoard()
        {
            var rows = new System.Collections.Generic.List<ActiveVisitorService.MirrorRow>();

            for (int i = 0; i < _board.Count; i++)
            {
                var card = _board[i];
                rows.Add(new ActiveVisitorService.MirrorRow
                {
                    VisitorId = card.VisitorId.ToString(),
                    LastKnownZone = card.LastKnownZone.ToString(),
                    LastKnownSecond = card.LastSeenSecond,
                    EnteredSecond = card.EnteredSecond,
                    Flags = (VisitorFlags)card.Flags,
                    Level = (VisitorAccessLevel)card.Level,
                    State = (VisitorState)card.State,
                    Revoked = card.Revoked
                });
            }

            ServiceHub.ActiveVisitors.ApplyMirror(rows);
        }

        static void FollowNight(int nightIndex)
        {
            if (nightIndex <= 0 || GameLoop.Instance == null) return;
            GameLoop.Instance.JoinShiftInProgress(nightIndex);
        }

        // -----------------------------------------------------------------
        // client: ask the host to do something
        // -----------------------------------------------------------------
        //
        // Nothing here trusts the sender. Every one of these lands in the same service call a
        // single-player press would have made, so the host's own rules - the office door being
        // bolted, a tactic already spent, a record not yet opened - refuse a bad request the
        // same way they refuse a bad click (v3.0 46.1).

        [ServerRpc(RequireOwnership = false)]
        public void MarkCheckedServerRpc(FixedString64Bytes key)
        {
            ServiceHub.Interphone.MarkChecked(key.ToString());
        }

        [ServerRpc(RequireOwnership = false)]
        public void NoteTellServerRpc(FixedString64Bytes tellId)
        {
            ServiceHub.Interphone.Read.Note(tellId.ToString());
        }

        [ServerRpc(RequireOwnership = false)]
        public void UseTacticServerRpc(byte tactic)
        {
            ServiceHub.Interphone.Read.Use((PressureTactic)tactic);
        }

        [ServerRpc(RequireOwnership = false)]
        public void GrantServerRpc(byte level, ServerRpcParams p = default)
        {
            ServiceHub.Interphone.GrantingPlayerId = NetSession.PlayerIdFor(p.Receive.SenderClientId);
            ServiceHub.Interphone.Grant((VisitorAccessLevel)level);
            ServiceHub.Interphone.GrantingPlayerId = PlayerPresence.SoloPlayerId;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RevokeServerRpc(FixedString64Bytes visitorId)
        {
            ServiceHub.ActiveVisitors.Revoke(visitorId.ToString());
        }

        [ServerRpc(RequireOwnership = false)]
        public void DialogueChooseServerRpc(FixedString64Bytes choiceId)
        {
            ServiceHub.Dialogue.Choose(choiceId.ToString());
        }

        /// <summary>
        /// A caretaker somewhere else wants a door open.
        ///
        /// The host re-runs the door's own rules rather than trusting the request - the client
        /// could be lying about where it is standing, and more usefully, the door may have been
        /// locked by something that happened in the two hundred milliseconds the request spent
        /// in flight. CanInteract is the same check singleplayer runs.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void DoorServerRpc(FixedString64Bytes doorId, ServerRpcParams p = default)
        {
            var door = Interaction.DoorController.Find(doorId.ToString());
            if (door == null) return;

            var playerId = NetSession.PlayerIdFor(p.Receive.SenderClientId);
            var zone = ServiceHub.Presence.ZoneOf(playerId);

            var context = new Interaction.PlayerContext
            {
                ZoneId = zone ?? ZoneIds.Office,

                // Distance is the one thing the host cannot verify without the client's
                // position, and the position it has is a zone. Standing in the same zone is
                // the honest granularity, and it is the same one the escort rule uses.
                DistanceToTarget = 0f
            };

            string reason;
            if (!door.CanInteract(context, out reason))
            {
                Log.Info("Multiplayer", playerId + " was refused " + doorId + ": " + reason);
                return;
            }

            door.Interact(context);
        }

        // -----------------------------------------------------------------
        // one call site for the UI, whichever side it is on
        // -----------------------------------------------------------------

        /// <summary>
        /// Do it here, or ask the host to. Every interphone control routes through these so no
        /// button has to know whether a session exists.
        /// </summary>
        public static void RequestMarkChecked(string key)
        {
            if (NetSession.Authoritative) ServiceHub.Interphone.MarkChecked(key);
            else if (Instance != null) Instance.MarkCheckedServerRpc(key);
        }

        public static void RequestNote(string tellId)
        {
            if (NetSession.Authoritative) ServiceHub.Interphone.Read.Note(tellId);
            else if (Instance != null) Instance.NoteTellServerRpc(tellId);
        }

        public static void RequestTactic(PressureTactic tactic)
        {
            if (NetSession.Authoritative) ServiceHub.Interphone.Read.Use(tactic);
            else if (Instance != null) Instance.UseTacticServerRpc((byte)tactic);
        }

        public static void RequestGrant(VisitorAccessLevel level)
        {
            if (NetSession.Authoritative) ServiceHub.Interphone.Grant(level);
            else if (Instance != null) Instance.GrantServerRpc((byte)level);
        }

        public static void RequestRevoke(string visitorId)
        {
            if (NetSession.Authoritative) ServiceHub.ActiveVisitors.Revoke(visitorId);
            else if (Instance != null) Instance.RevokeServerRpc(visitorId);
        }

        public static void RequestDialogueChoice(string choiceId)
        {
            if (NetSession.Authoritative) ServiceHub.Dialogue.Choose(choiceId);
            else if (Instance != null) Instance.DialogueChooseServerRpc(choiceId);
        }

        /// <summary>
        /// Ask the host to work this door. Only ever called from a client - the host reaches
        /// DoorController.OnInteract directly, which is where this would have sent it anyway.
        /// </summary>
        public static void RequestDoor(string doorId)
        {
            if (Instance != null) Instance.DoorServerRpc(doorId);
        }
    }
}
