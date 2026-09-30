using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NO404.CCTV;
using NO404.Core;
using NO404.Residents;

namespace NO404.Net
{
    /// <summary>
    /// The rest of the shift: everything a three-handed night 1 needs that the door and the
    /// tracking board do not already carry (v3.0 34-37, 46).
    ///
    /// Three layers, and the split between them is the whole design.
    ///
    /// <b>State</b> travels as a mirror of the host's save file (<see cref="ShiftMirror"/>) -
    /// cases, objectives, evidence, reserve, pressure, the access log, the manual events.
    /// Wide, slow, and impossible to forget to extend, because the day a service starts being
    /// saved is the day it starts being mirrored.
    ///
    /// <b>Moments</b> travel as RPCs, because a mirror half a second later is not a knock at
    /// the door. Anything that is heard, staged or announced comes through here and lands as
    /// the same event or the same service call a single-player machine would have made, so
    /// the caption system, the anomaly stager and the props never learn that networking
    /// exists.
    ///
    /// <b>Actions</b> travel the other way. Every verb a caretaker has that changes the shift
    /// - picking up a document, throwing the breaker, opening a record, filing a report - goes
    /// through <see cref="Request"/>, which runs it locally on the host and asks for it from a
    /// client. The host re-runs the same service call singleplayer runs, so nothing here has
    /// to trust the sender.
    /// </summary>
    public sealed partial class NetShift
    {
        // =================================================================
        // camera wall
        // =================================================================

        /// <summary>
        /// One channel's live state.
        ///
        /// Not in the mirror because none of it is in the save file, and rightly so: what a
        /// feed is showing right now is not something a reload should restore. It is however
        /// exactly what the cold open is about - twelve dead channels and a basement board -
        /// so a second caretaker who sees a live wall while the first is standing in the dark
        /// is not playing the same night.
        /// </summary>
        public struct CameraSync : INetworkSerializable, IEquatable<CameraSync>
        {
            public FixedString32Bytes CameraId;
            public FixedString64Bytes AnomalyId;
            public int AnomalyEndsAt;
            public byte State;
            public bool Motion;
            public bool Unreviewed;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref CameraId);
                s.SerializeValue(ref AnomalyId);
                s.SerializeValue(ref AnomalyEndsAt);
                s.SerializeValue(ref State);
                s.SerializeValue(ref Motion);
                s.SerializeValue(ref Unreviewed);
            }

            public bool Equals(CameraSync o) { return CameraId.Equals(o.CameraId); }
        }

        readonly NetworkList<CameraSync> _cameras = new NetworkList<CameraSync>();
        readonly NetworkVariable<bool> _feedsCut = new NetworkVariable<bool>();

        /// <summary>
        /// The office handset (GDD 16.12).
        ///
        /// There is one phone in the building and any caretaker may pick it up, so which call
        /// is ringing has to be on everybody's screen the moment it starts. It is a
        /// NetworkVariable rather than part of the mirror because a ringing phone is one of
        /// the few things in this game with a deadline on it - a call that reaches a client
        /// half a second late is a call they had half a second less to answer.
        /// </summary>
        public struct PhoneSync : INetworkSerializable, IEquatable<PhoneSync>
        {
            public FixedString64Bytes CallId;
            public bool InCall;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref CallId);
                s.SerializeValue(ref InCall);
            }

            public bool Equals(PhoneSync o) { return CallId.Equals(o.CallId) && InCall == o.InCall; }
        }

        readonly NetworkVariable<PhoneSync> _phone = new NetworkVariable<PhoneSync>();

        /// <summary>
        /// The office door (GDD 15.6): what is on the other side of it, and whether the bolt
        /// is thrown. Two bytes, and both of them decide what the room is worth.
        /// </summary>
        public struct OfficeDoorSync : INetworkSerializable, IEquatable<OfficeDoorSync>
        {
            public byte State;
            public bool Locked;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref State);
                s.SerializeValue(ref Locked);
            }

            public bool Equals(OfficeDoorSync o) { return State == o.State && Locked == o.Locked; }
        }

        readonly NetworkVariable<OfficeDoorSync> _officeDoor = new NetworkVariable<OfficeDoorSync>();

        string _cameraSignature;

        /// <summary>Signature-gated like the doors: a still wall costs nothing.</summary>
        void CaptureCameras()
        {
            _feedsCut.Value = ServiceHub.Cctv.FeedsCut;

            var channels = ServiceHub.Cctv.Channels;

            var signature = string.Empty;
            for (int i = 0; i < channels.Count; i++)
            {
                var c = channels[i];
                signature += c.CameraId + ":" + (int)c.State + ":" + c.ActiveAnomalyId + ":" +
                             c.AnomalyEndsAtGameSecond + ":" + (c.Motion ? 1 : 0) + ":" +
                             (c.UnreviewedMotion ? 1 : 0) + ";";
            }

            if (signature == _cameraSignature) return;
            _cameraSignature = signature;

            _cameras.Clear();
            for (int i = 0; i < channels.Count; i++)
            {
                var c = channels[i];
                _cameras.Add(new CameraSync
                {
                    CameraId = c.CameraId ?? string.Empty,
                    AnomalyId = c.ActiveAnomalyId ?? string.Empty,
                    AnomalyEndsAt = c.AnomalyEndsAtGameSecond,
                    State = (byte)c.State,
                    Motion = c.Motion,
                    Unreviewed = c.UnreviewedMotion
                });
            }
        }

        void CaptureOfficeDoor()
        {
            var door = Interaction.OfficeDoorController.Instance;
            if (door == null) return;

            _officeDoor.Value = new OfficeDoorSync
            {
                State = (byte)door.State,
                Locked = door.Locked
            };
        }

        void ApplyOfficeDoor()
        {
            var door = Interaction.OfficeDoorController.Instance;
            if (door == null) return;

            door.ApplyNetworkState((Interaction.OfficeDoorState)_officeDoor.Value.State,
                                   _officeDoor.Value.Locked);
        }

        void CapturePhone()
        {
            var active = ServiceHub.Phone.Active;

            _phone.Value = new PhoneSync
            {
                CallId = active != null ? active.callId : string.Empty,
                InCall = ServiceHub.Phone.InCall
            };
        }

        void ApplyPhone()
        {
            ServiceHub.Phone.ApplyMirror(_phone.Value.CallId.ToString(), _phone.Value.InCall);
        }

        void ApplyCameras()
        {
            ServiceHub.Cctv.ApplyNetworkFeedCut(_feedsCut.Value);

            for (int i = 0; i < _cameras.Count; i++)
            {
                var row = _cameras[i];
                ServiceHub.Cctv.ApplyNetworkChannel(row.CameraId.ToString(), (FeedState)row.State,
                                                    row.AnomalyId.ToString(), row.AnomalyEndsAt,
                                                    row.Motion, row.Unreviewed);
            }
        }

        // =================================================================
        // the mirror
        // =================================================================

        float _nextMirrorTime;
        string _lastMirrorJson;

        /// <summary>
        /// Makes the next push happen whatever the shift has or has not done.
        ///
        /// The mirror is sent on change, which is what makes it affordable - and which is
        /// exactly wrong for somebody who has just walked in. A caretaker joining at 01:00
        /// would have had no cases, no evidence and no reserve until the next thing in the
        /// building happened to move, because the state they were missing had not changed
        /// since long before they arrived.
        /// </summary>
        /// <summary>Says the host is no longer running a night, so nobody joins a dead one.</summary>
        public static void ClearNight()
        {
            if (Instance == null || !Instance.IsServer) return;
            Instance._nightIndex.Value = 0;
        }

        public static void ForceMirror()
        {
            if (Instance == null || !Instance.IsServer) return;

            Instance._lastMirrorJson = null;
            Instance._nextMirrorTime = 0f;
        }

        /// <summary>
        /// Opens the named-message channel the mirror travels on, and - on the host - starts
        /// listening for the handful of published events that have to reach everybody as
        /// events rather than as state.
        /// </summary>
        void OpenNightChannel()
        {
            var messaging = NetworkManager != null ? NetworkManager.CustomMessagingManager : null;
            if (messaging != null && !IsServer)
                messaging.RegisterNamedMessageHandler(ShiftMirror.MessageName, OnMirrorMessage);

            if (IsServer)
            {
                EventBus.Subscribe<NotificationEvent>(ForwardNotification);
                EventBus.Subscribe<CctvAnomalyEvent>(ForwardAnomaly);
                EventBus.Subscribe<ManualEventStateChangedEvent>(ForwardManualEvent);
                EventBus.Subscribe<EvidenceAcquiredEvent>(ForwardEvidence);

                ServiceHub.Dialogue.OnLineChanged += ForwardDialogueLine;
                ServiceHub.Dialogue.OnConversationEnded += ForwardDialogueEnd;
                return;
            }

            ApplyCameras();
            ApplyPhone();
            ApplyOfficeDoor();

            _cameras.OnListChanged += _ => ApplyCameras();
            _feedsCut.OnValueChanged += (_, __) => ApplyCameras();
            _phone.OnValueChanged += (_, __) => ApplyPhone();
            _officeDoor.OnValueChanged += (_, __) => ApplyOfficeDoor();
        }

        void CloseNightChannel()
        {
            var messaging = NetworkManager != null ? NetworkManager.CustomMessagingManager : null;
            if (messaging != null && !IsServer)
                messaging.UnregisterNamedMessageHandler(ShiftMirror.MessageName);

            if (!IsServer) return;

            EventBus.Unsubscribe<NotificationEvent>(ForwardNotification);
            EventBus.Unsubscribe<CctvAnomalyEvent>(ForwardAnomaly);
            EventBus.Unsubscribe<ManualEventStateChangedEvent>(ForwardManualEvent);
            EventBus.Unsubscribe<EvidenceAcquiredEvent>(ForwardEvidence);

            ServiceHub.Dialogue.OnLineChanged -= ForwardDialogueLine;
            ServiceHub.Dialogue.OnConversationEnded -= ForwardDialogueEnd;
        }

        /// <summary>
        /// Sends the shift, if it has moved and if it is time.
        ///
        /// The change test runs on the uncompressed JSON: a night in which nothing has
        /// happened for a minute produces the identical string every time and sends nothing at
        /// all, which is what makes a half-second interval affordable.
        /// </summary>
        void PushMirror()
        {
            if (!IsServer) return;
            if (NetworkManager == null || NetworkManager.ConnectedClientsIds.Count <= 1) return;
            if (Time.unscaledTime < _nextMirrorTime) return;

            _nextMirrorTime = Time.unscaledTime + ShiftMirror.PushInterval;

            var json = ShiftMirror.CaptureJson();
            if (string.IsNullOrEmpty(json) || json == _lastMirrorJson) return;
            _lastMirrorJson = json;

            var payload = ShiftMirror.Compress(json);
            if (payload == null) return;

            if (payload.Length > ShiftMirror.WarnBytes)
                Log.Warn("Multiplayer", "the shift mirror is " + payload.Length + " bytes");

            // Everybody except the host, who is the one holding the original. Sending to all
            // would deliver it back to a machine with no handler registered for it.
            _mirrorTargets.Clear();
            var ids = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] != NetworkManager.LocalClientId) _mirrorTargets.Add(ids[i]);

            if (_mirrorTargets.Count == 0) return;

            using (var writer = new FastBufferWriter(payload.Length + sizeof(int), Allocator.Temp,
                                                     payload.Length + sizeof(int)))
            {
                writer.WriteValueSafe(payload.Length);
                writer.WriteBytesSafe(payload, payload.Length);

                NetworkManager.CustomMessagingManager.SendNamedMessage(
                    ShiftMirror.MessageName, _mirrorTargets, writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        readonly System.Collections.Generic.List<ulong> _mirrorTargets =
            new System.Collections.Generic.List<ulong>(4);

        void OnMirrorMessage(ulong sender, FastBufferReader reader)
        {
            if (IsServer) return;

            int length;
            reader.ReadValueSafe(out length);
            if (length <= 0) return;

            var payload = new byte[length];
            reader.ReadBytesSafe(ref payload, length);

            var data = ShiftMirror.Decompress(payload);
            if (data == null) return;

            ServiceHub.Save.ApplyShiftMirror(data);
        }

        // =================================================================
        // moments
        // =================================================================

        /// <summary>
        /// Things that have to be heard rather than inferred.
        ///
        /// One RPC with a kind rather than a dozen: every payload here is at most two ids and
        /// a number, and a dozen near-identical RPCs is a dozen chances to forget the
        /// <c>IsServer</c> guard that stops the host doing everything twice.
        /// </summary>
        public enum Moment : byte
        {
            Notify = 0,           // a = key, n = severity
            CaptionAmbient = 1,   // a = key
            OfficeKnock = 3,      // n = knocks
            AnomalyStart = 4,     // a = anomalyId, b = cameraId
            AnomalyStop = 5,      // a = anomalyId, b = cameraId
            ManualEvent = 6,      // a = eventId, n = (previous << 8) | current
            Evidence = 7,         // a = evidenceId
            DialogueNode = 8,     // a = conversationId, b = nodeId
            DialogueEnd = 9,      // a = conversationId
            NightEnded = 10,      // n = nightIndex
            QueueGlimpse = 12     // a = residentId
        }

        public static void Broadcast(Moment moment, string a = null, string b = null, int n = 0)
        {
            if (Instance == null || !Instance.IsServer) return;
            if (Instance.NetworkManager == null || Instance.NetworkManager.ConnectedClientsIds.Count <= 1) return;

            Instance.MomentClientRpc((byte)moment, a ?? string.Empty, b ?? string.Empty, n);
        }

        [ClientRpc]
        void MomentClientRpc(byte kind, FixedString64Bytes a, FixedString64Bytes b, int n)
        {
            // The host is a client too, and it has already done all of this itself.
            if (IsServer) return;

            var first = a.ToString();
            var second = b.ToString();

            switch ((Moment)kind)
            {
                case Moment.Notify:
                    EventBus.Publish(new NotificationEvent(first, (NotificationSeverity)n));
                    break;

                case Moment.CaptionAmbient:
                    ServiceHub.Captions.Ambient(first);
                    break;

                case Moment.OfficeKnock:
                    if (Interaction.OfficeDoorController.Instance != null)
                        Interaction.OfficeDoorController.Instance.ScriptedKnock(n);
                    break;

                case Moment.AnomalyStart:
                    EventBus.Publish(new CctvAnomalyEvent(second, first, true));
                    break;

                case Moment.AnomalyStop:
                    EventBus.Publish(new CctvAnomalyEvent(second, first, false));
                    break;

                case Moment.ManualEvent:
                    // Both halves of the transition matter to the props, so they ride in one
                    // int: the previous state in the high byte, the new one in the low.
                    EventBus.Publish(new ManualEventStateChangedEvent(
                        first,
                        (Anomalies.ManualEventState)((n >> 8) & 0xFF),
                        (Anomalies.ManualEventState)(n & 0xFF)));
                    break;

                case Moment.Evidence:
                    ServiceHub.Audio.PlayCue(AudioCue.EvidenceTaken);
                    break;

                case Moment.DialogueNode:
                    ServiceHub.Dialogue.Resume(first, second);
                    break;

                case Moment.DialogueEnd:
                    ServiceHub.Dialogue.End();
                    break;

                case Moment.NightEnded:
                    if (GameLoop.Instance != null) GameLoop.Instance.FollowNightEnded();
                    break;

                case Moment.QueueGlimpse:
                    // GDD 16.10 gives the 404 record three seconds in the list and then takes
                    // it away. It is armed rather than shown, so each caretaker spends their
                    // own three seconds whenever they next open the app - the beat is meant to
                    // be witnessed, and a shift where only the host could witness it would
                    // make the game's own cover image unreachable for two players in three.
                    ServiceHub.Residents.QueueGlimpse(first);
                    break;
            }
        }

        void ForwardNotification(NotificationEvent evt)
        {
            Broadcast(Moment.Notify, evt.BodyKey, null, (int)evt.Severity);
        }

        void ForwardAnomaly(CctvAnomalyEvent evt)
        {
            Broadcast(evt.Started ? Moment.AnomalyStart : Moment.AnomalyStop,
                      evt.AnomalyId, evt.CameraId);
        }

        void ForwardManualEvent(ManualEventStateChangedEvent evt)
        {
            Broadcast(Moment.ManualEvent, evt.EventId, null,
                      (((int)evt.Previous & 0xFF) << 8) | ((int)evt.Current & 0xFF));
        }

        void ForwardEvidence(EvidenceAcquiredEvent evt)
        {
            Broadcast(Moment.Evidence, evt.EvidenceId);
        }

        void ForwardDialogueLine(Dialogue.DialogueLine line)
        {
            if (line == null || ServiceHub.Dialogue.Current == null) return;
            Broadcast(Moment.DialogueNode, ServiceHub.Dialogue.Current.conversationId, line.NodeId);
        }

        void ForwardDialogueEnd(string conversationId)
        {
            Broadcast(Moment.DialogueEnd, conversationId);
        }

        // =================================================================
        // resident traffic
        // =================================================================

        /// <summary>
        /// Somebody crossing a camera, on everybody's screens (GDD 12.3).
        ///
        /// The schedule is the host's - it has to be, because half of these movements are
        /// contradictions the access log is deliberately not carrying, and two machines
        /// rolling their own would disagree about which. So the host decides, and every copy
        /// puts the same person in the same corridor walking the same way.
        /// </summary>
        public static void BroadcastTraffic(string nameKey, string zoneId, int kind, bool inbound, float speed)
        {
            if (Instance == null || !Instance.IsServer) return;
            if (Instance.NetworkManager == null || Instance.NetworkManager.ConnectedClientsIds.Count <= 1) return;

            Instance.TrafficClientRpc(nameKey ?? string.Empty, zoneId ?? string.Empty,
                                      (byte)kind, inbound, speed);
        }

        [ClientRpc]
        void TrafficClientRpc(FixedString64Bytes nameKey, FixedString32Bytes zoneId,
                              byte kind, bool inbound, float speed)
        {
            if (IsServer) return;

            ServiceHub.Traffic.SpawnMirroredActor(nameKey.ToString(), zoneId.ToString(),
                                                  (TrafficKind)kind, inbound, speed);
        }

        // =================================================================
        // actions
        // =================================================================

        /// <summary>
        /// Every verb a caretaker has that changes the shift rather than only their own night.
        ///
        /// Deliberately coarse. The host does not receive "the client thinks it now owns
        /// EV_KEY404"; it receives "somebody picked up EV_KEY404" and runs its own
        /// EvidenceService, which is the same call the office PC makes in single player and
        /// refuses the same things for the same reasons.
        /// </summary>
        public enum ShiftAct : byte
        {
            AcquireEvidence = 0,   // a = evidenceId, n = EvidenceSource
            ResetBreaker = 1,
            EnterZone = 2,         // a = zoneId
            OpenApp = 3,           // a = appId
            ViewRecord = 4,        // a = residentId
            ViewChannel = 5,       // a = cameraId
            TakeSnapshot = 6,      // a = cameraId, n = rewind offset
            SetFlag = 7,           // a = flagId, n = 0/1
            StartDialogue = 8,     // a = conversationId
            AdvanceDialogue = 9,
            AnswerPhone = 10,
            DeclinePhone = 11,
            ReportAnomaly = 13,    // a = cameraId, n = AnomalyCategory
            CorridorLights = 14,   // n = 0/1
            AdvanceNight = 15,
            OfficeBolt = 16,       // n = 0/1
            ManualCounterSet = 17, // a = eventId, b = counterId, n = value
            ManualCounterAdd = 18, // a = eventId, b = counterId, n = delta
            ManualObjective = 19,  // a = eventId, b = objectiveId
            ManualResolve = 20,    // a = eventId
            ElevatorRide = 21,
            Peephole = 22,
            LinkEvidence = 23,     // a = evidenceId, b = evidenceId, n = EvidenceRelation
            EndShift = 24
        }

        /// <summary>
        /// Do it, or ask the host to. Single player and the host take the first branch, which
        /// is the line that keeps every call site in the game free of an "am I networked" test.
        /// </summary>
        public static void Request(ShiftAct act, string a = null, string b = null, int n = 0)
        {
            if (NetSession.Authoritative)
            {
                Apply(act, a, b, n,
                      ServiceHub.Presence != null ? ServiceHub.Presence.LocalPlayerId
                                                  : PlayerPresence.SoloPlayerId);
                return;
            }

            if (Instance != null) Instance.ActServerRpc((byte)act, a ?? string.Empty, b ?? string.Empty, n);
        }

        [ServerRpc(RequireOwnership = false)]
        void ActServerRpc(byte act, FixedString64Bytes a, FixedString64Bytes b, int n,
                          ServerRpcParams p = default)
        {
            Apply((ShiftAct)act, a.ToString(), b.ToString(), n,
                  NetSession.PlayerIdFor(p.Receive.SenderClientId));
        }

        static void Apply(ShiftAct act, string a, string b, int n, string playerId)
        {
            switch (act)
            {
                case ShiftAct.AcquireEvidence:
                    // The source is carried rather than assumed: it is written into the save,
                    // and a database record that arrives labelled as something picked up off a
                    // desk is a different piece of paper in the tray.
                    ServiceHub.Evidence.Acquire(a, (Evidence.EvidenceSource)n);
                    break;

                case ShiftAct.ResetBreaker:
                    ServiceHub.Power.TryResetBreaker();
                    break;

                case ShiftAct.EnterZone:
                    // Presence is what says where somebody is; this is what the building does
                    // about it. Both have to happen on the host or a case whose objective is
                    // "be on the third floor" is only ever satisfied by whoever is hosting.
                    ServiceHub.Presence.Report(playerId, a);
                    ServiceHub.Cases.NotifyZoneEntered(a);
                    ServiceHub.ManualEvents.NotifyZoneEntered(a);
                    ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.EnterZone, a);
                    if (GameLoop.Instance != null) GameLoop.Instance.NoteShiftZoneEntered(a);
                    break;

                case ShiftAct.OpenApp:
                    ServiceHub.Cases.NotifyAppOpened(a);
                    ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.OpenApp, a);
                    break;

                case ShiftAct.ViewRecord:
                    ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.ViewRecord, a);
                    break;

                case ShiftAct.ViewChannel:
                    ServiceHub.Cctv.NoteChannelViewed(a);
                    break;

                case ShiftAct.TakeSnapshot:
                    ServiceHub.Cctv.TakeSnapshot(a, n);
                    break;

                case ShiftAct.SetFlag:
                    ServiceHub.State.SetFlag(a, n != 0);
                    break;

                case ShiftAct.StartDialogue:
                    ServiceHub.Dialogue.Start(a);
                    break;

                case ShiftAct.AdvanceDialogue:
                    ServiceHub.Dialogue.Advance();
                    break;

                case ShiftAct.AnswerPhone:
                    ServiceHub.Phone.Answer();
                    break;

                case ShiftAct.DeclinePhone:
                    ServiceHub.Phone.Decline();
                    break;

                case ShiftAct.ReportAnomaly:
                    ServiceHub.Cctv.Report(a, (AnomalyCategory)n);
                    break;

                case ShiftAct.CorridorLights:
                    ServiceHub.Power.SetCorridorLights(n != 0);
                    break;

                case ShiftAct.AdvanceNight:
                    if (GameLoop.Instance != null) GameLoop.Instance.AdvanceNightForShift();
                    break;

                case ShiftAct.OfficeBolt:
                    if (Interaction.OfficeDoorController.Instance != null)
                        Interaction.OfficeDoorController.Instance.SetLocked(n != 0);
                    break;

                case ShiftAct.ManualCounterSet:
                    ServiceHub.ManualEvents.SetCounter(a, b, n);
                    break;

                case ShiftAct.ManualCounterAdd:
                    ServiceHub.ManualEvents.AddCounter(a, b, n);
                    break;

                case ShiftAct.ManualObjective:
                    ServiceHub.ManualEvents.CompleteObjective(a, b);
                    break;

                case ShiftAct.ManualResolve:
                    ServiceHub.ManualEvents.Resolve(a);
                    break;

                case ShiftAct.ElevatorRide:
                    // GDD 15.5: the car is the most expensive thing on the night circuit, and
                    // it is one meter for one building. A client that charged its own mirrored
                    // reserve would have the number wiped by the next push and ride for free.
                    ServiceHub.Power.ChargeElevatorRide();
                    break;

                case ShiftAct.Peephole:
                    if (Interaction.OfficeDoorController.Instance != null)
                        Interaction.OfficeDoorController.Instance.UsePeephole();
                    break;

                case ShiftAct.LinkEvidence:
                    ServiceHub.Evidence.Link(a, b, (Evidence.EvidenceRelation)n);
                    break;

                case ShiftAct.EndShift:
                    if (GameLoop.Instance != null) GameLoop.Instance.RequestEndShift();
                    break;
            }
        }

        // =================================================================
        // filing a report
        // =================================================================

        /// <summary>
        /// Told what the office PC should print after a submission.
        ///
        /// A report is the one action whose answer the person who made it has to read, and it
        /// is an answer the host computes - whether the evidence attached was enough, which
        /// result line the case gives back. So it comes back to that caretaker only.
        /// </summary>
        public static Action<bool, string> OnDecisionAnswered;

        public static void RequestDecision(string caseId, string decisionId,
                                           System.Collections.Generic.List<string> attached)
        {
            if (NetSession.Authoritative)
            {
                var result = ServiceHub.Cases.SubmitDecision(caseId, decisionId, attached);
                var cb = OnDecisionAnswered;
                if (cb != null)
                    cb(result.Accepted, result.Accepted ? result.ResultKey : result.RejectReason);
                return;
            }

            if (Instance != null) Instance.DecisionServerRpc(caseId, decisionId);
        }

        [ServerRpc(RequireOwnership = false)]
        void DecisionServerRpc(FixedString64Bytes caseId, FixedString64Bytes decisionId,
                               ServerRpcParams p = default)
        {
            // The evidence list is rebuilt here rather than sent. Everything the shift owns is
            // owned by all of it - the tray is not per-caretaker - so the host already holds
            // exactly the list the client would have written down, and a client that sent one
            // could send a longer one.
            var attached = new System.Collections.Generic.List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);

            var result = ServiceHub.Cases.SubmitDecision(caseId.ToString(), decisionId.ToString(), attached);

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { p.Receive.SenderClientId } }
            };

            DecisionAnsweredClientRpc(result.Accepted,
                                      result.Accepted ? result.ResultKey : result.RejectReason,
                                      target);
        }

        [ClientRpc]
        void DecisionAnsweredClientRpc(bool accepted, FixedString64Bytes key, ClientRpcParams p = default)
        {
            var cb = OnDecisionAnswered;
            if (cb != null) cb(accepted, key.ToString());
        }

        // =================================================================
        // naming what was on a camera
        // =================================================================

        /// <summary>What the camera wall said back to whoever filed the claim.</summary>
        public static Action<CCTV.ReportOutcome> OnReportAnswered;

        public static void RequestReport(string cameraId, AnomalyCategory category)
        {
            if (NetSession.Authoritative)
            {
                var outcome = ServiceHub.Cctv.Report(cameraId, category);
                var cb = OnReportAnswered;
                if (cb != null) cb(outcome);
                return;
            }

            if (Instance != null) Instance.ReportServerRpc(cameraId, (byte)category);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportServerRpc(FixedString64Bytes cameraId, byte category, ServerRpcParams p = default)
        {
            // The sighting list lives on the host, so only the host can say whether there was
            // anything on that camera to name. A client that decided for itself would be
            // scoring its own guess (GDD 12.4).
            var outcome = ServiceHub.Cctv.Report(cameraId.ToString(), (AnomalyCategory)category);

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { p.Receive.SenderClientId } }
            };

            ReportAnsweredClientRpc((byte)outcome, target);
        }

        [ClientRpc]
        void ReportAnsweredClientRpc(byte outcome, ClientRpcParams p = default)
        {
            var cb = OnReportAnswered;
            if (cb != null) cb((CCTV.ReportOutcome)outcome);
        }
    }
}
