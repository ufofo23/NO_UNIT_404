using System;
using System.Collections.Generic;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Phone
{
    /// <summary>
    /// Incoming calls for the office handset (GDD 3.1 "전화·인터폰", 20.16 scheduler rules).
    ///
    /// Behaviour that matters:
    ///  - several calls can be due at once; the queue is ordered by priority, then by time
    ///  - a call never interrupts an important conversation - it waits and rings afterwards
    ///  - an unanswered call is a cost (missed information), never a dead end; important
    ///    calls re-ring once
    /// </summary>
    public sealed class PhoneService
    {
        sealed class PendingCall
        {
            public PhoneCallDefinition Definition;
            public int DueAtGameSecond;
            public bool IsRetry;

            /// <summary>The case whose opening rings this call, or null when it is on a clock.</summary>
            public string WaitingOnCase;
        }

        readonly ContentDatabase _content;
        readonly GameClock _clock;

        readonly List<PendingCall> _scheduled = new List<PendingCall>();
        readonly List<PhoneCallDefinition> _ringing = new List<PhoneCallDefinition>();
        readonly HashSet<string> _answered = new HashSet<string>();
        readonly HashSet<string> _missed = new HashSet<string>();

        int _ringingSinceGameSecond;

        public PhoneService(ContentDatabase content, GameClock clock)
        {
            _content = content;
            _clock = clock;
        }

        /// <summary>The call currently ringing, or null.</summary>
        public PhoneCallDefinition Active { get { return _ringing.Count > 0 ? _ringing[0] : null; } }

        public bool IsRinging { get { return _ringing.Count > 0 && !InCall; } }
        public bool InCall { get; private set; }
        public int WaitingCount { get { return _ringing.Count; } }

        public event Action<PhoneCallDefinition> OnCallStartedRinging;
        public event Action<PhoneCallDefinition> OnCallEnded;

        // ---- scheduling ----------------------------------------------------

        /// <summary>Queues every call authored for this night. Called at shift start.</summary>
        public void ScheduleNight(int nightIndex)
        {
            _scheduled.Clear();
            _ringing.Clear();
            InCall = false;

            foreach (var definition in _content.PhoneCalls)
            {
                if (definition == null || definition.nightIndex != nightIndex) continue;

                // dev.mainonly: every authored call already names the case it belongs to, so
                // the ones that belong to tonight's main still ring and the rest are never
                // queued. Skipped here rather than silenced in Tick, so nothing is left owed
                // and ReleaseCallsWaitingOnCases has nothing to let go of at the end.
                if (MainOnlyMode.Suppresses(definition.caseId)) continue;

                // A call that names a case belongs to that case's beat, not to the clock.
                //
                // Every one of them already carried the id and nothing read it, so the
                // handset rang at an authored hour while the case it was about started
                // whenever the caretaker got to it. On night 1 that is a neighbour ringing to
                // complain about the noise from 304 either well before or well after the
                // caretaker has been given the 304 job - the two halves of one beat, arriving
                // as two unrelated events.
                _scheduled.Add(new PendingCall
                {
                    Definition = definition,
                    DueAtGameSecond = definition.gameSecond,
                    WaitingOnCase = definition.caseId
                });
            }

            Log.Info("Phone", "scheduled " + _scheduled.Count + " calls for night " + nightIndex);
        }

        /// <summary>
        /// A case has opened, so the call that belongs to it rings.
        ///
        /// Called by GameLoop, which is the one place that already watches case beats. Keeping
        /// the subscription there rather than here leaves the phone with no opinion about
        /// cases beyond the id it was authored with.
        /// </summary>
        /// <summary>
        /// Ring everything still waiting, whatever its case did.
        ///
        /// The counterpart of GameLoop letting go of stranded callers: a call whose case never
        /// opened is a beat the player was owed and never got, and silently dropping it is
        /// worse than delivering it late.
        /// </summary>
        public void ReleaseCallsWaitingOnCases()
        {
            for (int i = 0; i < _scheduled.Count; i++)
            {
                var pending = _scheduled[i];
                if (string.IsNullOrEmpty(pending.WaitingOnCase)) continue;

                pending.WaitingOnCase = null;
                pending.DueAtGameSecond = _clock.GameSecond;
            }
        }

        public void NotifyCaseStarted(string caseId)
        {
            if (string.IsNullOrEmpty(caseId)) return;

            for (int i = 0; i < _scheduled.Count; i++)
            {
                var pending = _scheduled[i];
                if (pending.WaitingOnCase != caseId) continue;

                // Now, unless the call was authored to land later inside the same job. A
                // delay measured from the case is not the thing this design refuses - what it
                // refuses is a delay measured from the clock, which is what put the complaint
                // about 304 an hour away from the 304 job in the first place.
                pending.WaitingOnCase = null;
                pending.DueAtGameSecond = _clock.GameSecond + pending.Definition.releaseDelaySeconds;

                Log.Info("Phone", pending.Definition.callId + " rings with " + caseId);
            }
        }

        public void Tick()
        {
            int now = _clock.GameSecond;

            PromoteDueCalls(now);
            ExpireRingingCalls(now);
        }

        void PromoteDueCalls(int now)
        {
            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                var pending = _scheduled[i];

                // A call tied to a case waits for it and for nothing else. There is no hour
                // at which it gives up and rings anyway: the authored second is what sorted
                // the roster, not a promise about the clock. GameLoop lets go of anything
                // still waiting once the night has no work left to start, which is the only
                // moment a case beat can be said to have failed to arrive.
                if (!string.IsNullOrEmpty(pending.WaitingOnCase)) continue;

                if (now < pending.DueAtGameSecond) continue;

                var definition = pending.Definition;
                if (_answered.Contains(definition.callId)) { _scheduled.RemoveAt(i); continue; }

                string reason;
                if (!ConditionEvaluator.EvaluateAll(definition.conditions, out reason))
                {
                    // Conditions may still become true later in the shift; keep it queued
                    // until its window has clearly passed.
                    if (now > definition.gameSecond + definition.ringSeconds * 4) _scheduled.RemoveAt(i);
                    continue;
                }

                _scheduled.RemoveAt(i);
                Enqueue(definition, now);
            }
        }

        void Enqueue(PhoneCallDefinition definition, int now)
        {
            if (_ringing.Contains(definition)) return;

            _ringing.Add(definition);

            // Highest priority first; ties resolve to whichever was authored earlier.
            _ringing.Sort((a, b) =>
            {
                int byPriority = b.priority.CompareTo(a.priority);
                return byPriority != 0 ? byPriority : a.gameSecond.CompareTo(b.gameSecond);
            });

            if (_ringing.Count == 1) _ringingSinceGameSecond = now;

            EventBus.Publish(new NotificationEvent("ui.notify.incoming_call", NotificationSeverity.Urgent));
            Log.Info("Phone", "ringing: " + definition.callId + " (" + definition.priority + ")");

            var cb = OnCallStartedRinging;
            if (cb != null) cb(definition);
        }

        void ExpireRingingCalls(int now)
        {
            if (InCall || _ringing.Count == 0) return;

            // An important conversation holds the line - the handset keeps ringing instead of
            // cutting the player off mid-sentence (GDD 20.16).
            if (ServiceHub.Dialogue.IsBlocking) { _ringingSinceGameSecond = now; return; }

            var definition = _ringing[0];
            if (now - _ringingSinceGameSecond < definition.ringSeconds) return;

            _ringing.RemoveAt(0);
            _ringingSinceGameSecond = now;
            MarkMissed(definition, now);
        }

        void MarkMissed(PhoneCallDefinition definition, int now)
        {
            _missed.Add(definition.callId);
            ServiceHub.Cases.ApplyConsequences(definition.onMissed);
            ServiceHub.Analytics.Track("call_missed", definition.callId);
            Log.Warn("Phone", "missed call: " + definition.callId);

            if (definition.retryAfterSeconds > 0 && !HasRetryQueued(definition))
            {
                _scheduled.Add(new PendingCall
                {
                    Definition = definition,
                    DueAtGameSecond = now + definition.retryAfterSeconds,
                    IsRetry = true
                });
            }

            EventBus.Publish(new NotificationEvent("ui.notify.missed_call", NotificationSeverity.Warning));
        }

        bool HasRetryQueued(PhoneCallDefinition definition)
        {
            for (int i = 0; i < _scheduled.Count; i++)
                if (_scheduled[i].IsRetry && _scheduled[i].Definition == definition) return true;
            return false;
        }

        // ---- player actions ------------------------------------------------

        public void Answer()
        {
            var definition = Active;
            if (definition == null || InCall) return;

            InCall = true;
            _answered.Add(definition.callId);
            _missed.Remove(definition.callId);

            ServiceHub.Cases.ApplyConsequences(definition.onAnswered);
            ServiceHub.Cases.NotifyPhoneAnswered(definition.conversationId);
            ServiceHub.Analytics.Track("call_answered", definition.callId);
            Log.Info("Phone", "answered " + definition.callId);

            if (!string.IsNullOrEmpty(definition.conversationId))
            {
                ServiceHub.Dialogue.OnConversationEnded += HandleConversationEnded;
                if (!ServiceHub.Dialogue.Start(definition.conversationId))
                {
                    // Start failed, so HandleConversationEnded will never fire for this call -
                    // leaving it subscribed would fire it on the next unrelated conversation.
                    ServiceHub.Dialogue.OnConversationEnded -= HandleConversationEnded;
                    EndCall();
                }
            }
            else
            {
                EndCall();
            }
        }

        public void Decline()
        {
            var definition = Active;
            if (definition == null || InCall) return;

            _ringing.RemoveAt(0);
            _ringingSinceGameSecond = _clock.GameSecond;

            ServiceHub.Cases.ApplyConsequences(definition.onDeclined);
            ServiceHub.Analytics.Track("call_declined", definition.callId);
            Log.Info("Phone", "declined " + definition.callId);
        }

        void HandleConversationEnded(string conversationId)
        {
            ServiceHub.Dialogue.OnConversationEnded -= HandleConversationEnded;
            EndCall();
        }

        void EndCall()
        {
            var definition = Active;
            if (definition != null) _ringing.RemoveAt(0);

            InCall = false;
            _ringingSinceGameSecond = _clock.GameSecond;

            var cb = OnCallEnded;
            if (cb != null) cb(definition);

            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);
        }

        /// <summary>
        /// The host's handset, on this copy's desk (v3.0 46.1).
        ///
        /// The schedule stays the host's - a client that queued its own calls would ring a
        /// phone nobody else could hear - so all a client needs is which call is ringing and
        /// whether somebody has picked it up. Answering goes back the other way as a request,
        /// and the consequences of answering land on the host and return in the mirror.
        ///
        /// Ringing is rebuilt rather than merged: there is exactly one handset in the office
        /// and it is either ringing or it is not.
        /// </summary>
        public void ApplyMirror(string ringingCallId, bool inCall)
        {
            var definition = string.IsNullOrEmpty(ringingCallId) ? null : _content.FindPhoneCall(ringingCallId);
            var previous = Active;

            _ringing.Clear();
            if (definition != null) _ringing.Add(definition);

            InCall = inCall;

            if (definition == null || definition == previous) return;

            var cb = OnCallStartedRinging;
            if (cb != null) cb(definition);
        }

        public bool WasAnswered(string callId) { return _answered.Contains(callId); }
        public bool WasMissed(string callId) { return _missed.Contains(callId); }

        /// <summary>Manual trigger for scripted beats and the dev console.</summary>
        public bool ForceCall(string callId)
        {
            var definition = _content.FindPhoneCall(callId);
            if (definition == null) return false;

            Enqueue(definition, _clock.GameSecond);
            return true;
        }

        public void Reset()
        {
            _scheduled.Clear();
            _ringing.Clear();
            _answered.Clear();
            _missed.Clear();
            InCall = false;
            _ringingSinceGameSecond = 0;
        }

        public IEnumerable<string> AnsweredCalls { get { return _answered; } }
        public IEnumerable<string> MissedCalls { get { return _missed; } }

        public void LoadFrom(IEnumerable<string> answered, IEnumerable<string> missed)
        {
            _answered.Clear();
            _missed.Clear();
            if (answered != null) foreach (var id in answered) _answered.Add(id);
            if (missed != null) foreach (var id in missed) _missed.Add(id);
        }
    }
}
