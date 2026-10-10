using System;
using System.Collections.Generic;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Visitors
{
    /// <summary>
    /// Front door interphone (GDD 13). The correctness of a judgement depends on how many
    /// independent checks the player actually consulted, not on the visitor's appearance.
    /// </summary>
    public sealed class InterphoneService
    {
        readonly ContentDatabase _content;
        readonly Queue<VisitorDefinition> _queue = new Queue<VisitorDefinition>();
        readonly Dictionary<string, VisitorAccessLevel> _decisions = new Dictionary<string, VisitorAccessLevel>();
        readonly HashSet<string> _consultedChecks = new HashSet<string>();

        /// <summary>
        /// Which records the caretaker has actually opened for this caller.
        ///
        /// This is the change that turned the door from a reading exercise into the game it
        /// was supposed to be. The interphone panel used to print every fact about a caller
        /// including the ones that convicted them - "804호 : 2010년부터 공실" - so the player
        /// was not detecting anything, they were reading a verdict someone else had written.
        ///
        /// Now the panel prints what the caller *claims* and nothing else. A fact appears only
        /// once the player has gone and looked it up in the record that proves it, and the
        /// contradiction is theirs to find.
        /// </summary>
        readonly HashSet<string> _consultedSources = new HashSet<string>();

        /// <summary>Sources that are records rather than the caller's own word.</summary>
        public const string DialogueSource = "dialogue";

        public InterphoneService(ContentDatabase content)
        {
            _content = content;
            Read = new DoorReadService();
        }

        /// <summary>
        /// The half of the door that is about the person rather than the paperwork (GDD 13.5).
        /// One session per caller, opened by <see cref="PullNext"/> and closed with them.
        /// </summary>
        public DoorReadService Read { get; private set; }

        public VisitorDefinition Active { get; private set; }
        public bool HasWaitingVisitor { get { return Active != null || _queue.Count > 0; } }
        public int QueueLength { get { return _queue.Count; } }

        /// <summary>
        /// True while the office door is bolted (GDD 15.6). The front-door release runs off
        /// the same night circuit as the office bolt, so hiding stops the queue moving - which
        /// is the entire cost of hiding.
        /// </summary>
        public bool DoorBlocked { get; private set; }

        public void SetDoorBlocked(bool blocked) { DoorBlocked = blocked; }

        public event Action<VisitorDefinition> OnVisitorArrived;
        public event Action<VisitorDefinition, VisitorAccessLevel> OnVisitorResolved;

        public void Enqueue(string visitorId)
        {
            var definition = _content.FindVisitor(visitorId);
            if (definition == null) { Log.Error("Interphone", "unknown visitor " + visitorId); return; }
            if (_decisions.ContainsKey(visitorId)) return;

            _queue.Enqueue(definition);
            if (Active == null) PullNext();
        }

        void PullNext()
        {
            if (_queue.Count == 0) { Active = null; return; }

            Active = _queue.Dequeue();
            _consultedChecks.Clear();
            _consultedSources.Clear();
            Read.Begin(Active);

            EventBus.Publish(new NotificationEvent("ui.notify.interphone", NotificationSeverity.Urgent));
            ServiceHub.Cases.NotifyInterphone(Active.visitorId);

            var cb = OnVisitorArrived;
            if (cb != null) cb(Active);
            Log.Info("Interphone", "visitor waiting: " + Active.visitorId);
        }

        /// <summary>Called when the player looks up a cross-reference for the current visitor.</summary>
        /// <summary>
        /// Called when the player consults something about the current caller.
        ///
        /// The key carries its own source: "residents.803", "access.compare", "cctv.CAM-01",
        /// or a bare dialogue choice id. Everything before the first dot is the record that
        /// was opened; a key with no dot is the caller answering a question, which is not a
        /// record and is treated as one source no matter how many questions are asked.
        /// </summary>
        public void MarkChecked(string checkLabelKey)
        {
            if (Active == null || string.IsNullOrEmpty(checkLabelKey)) return;

            _consultedChecks.Add(checkLabelKey);
            _consultedSources.Add(SourceOf(checkLabelKey));
        }

        static string SourceOf(string checkLabelKey)
        {
            int dot = checkLabelKey.IndexOf('.');
            return dot > 0 ? checkLabelKey.Substring(0, dot) : DialogueSource;
        }

        /// <summary>True once the player has opened this record for the current caller.</summary>
        public bool HasConsulted(string sourceId)
        {
            return !string.IsNullOrEmpty(sourceId) && _consultedSources.Contains(sourceId);
        }

        public int ConsultedCheckCount { get { return _consultedSources.Count; } }

        /// <summary>
        /// GDD 13.2: at least two independent facts are needed. Below that the player is
        /// guessing - the decision still stands but is never graded as "correct".
        /// </summary>
        ///
        /// Counted per source rather than per question. Asking the courier three questions
        /// used to clear this bar on its own, which made "two independent facts" mean "let the
        /// suspect talk for a while". Two different records clears it; one record plus the
        /// caller's own account clears it; the caller's account alone never does.
        public bool HasEnoughInformation { get { return EstablishedFactCount >= 2; } }

        /// <summary>
        /// Everything the caretaker can actually point at: records they opened, plus
        /// observations they wrote down that were worth writing down (GDD 13.5).
        ///
        /// Behaviour counts as a fact on purpose. Watching a contractor fail to produce a work
        /// order they claim to be holding is not a feeling about them - it is the same class of
        /// evidence as the work order not being in the system, and it is available to a
        /// caretaker who is standing in their own lobby with no computer at all.
        /// </summary>
        public int EstablishedFactCount
        {
            get { return _consultedSources.Count + Read.EstablishedFactCount; }
        }

        public VisitorAccessLevel DecisionFor(string visitorId)
        {
            VisitorAccessLevel level;
            return _decisions.TryGetValue(visitorId, out level) ? level : VisitorAccessLevel.Pending;
        }

        /// <summary>
        /// Judge the caller at the door.
        ///
        /// Every refusal path here says so out loud. It used to return silently when there was
        /// no active caller or the office door was bolted, and a playtest found the result
        /// indistinguishable from a broken button: four controls that highlight when pressed
        /// and then do nothing at all, with nothing on screen and nothing in the log.
        ///
        /// A player is allowed to be told no. They are not allowed to be ignored.
        /// </summary>
        public void Grant(VisitorAccessLevel level)
        {
            if (level == VisitorAccessLevel.Pending) return;

            if (Active == null)
            {
                EventBus.Publish(new NotificationEvent("ui.notify.no_visitor_to_judge",
                                                       NotificationSeverity.Warning));
                Log.Warn("Interphone", "grant " + level + " with nobody at the door");
                return;
            }

            if (DoorBlocked && VisitorAccess.LetsThemIn(level))
            {
                EventBus.Publish(new NotificationEvent("ui.notify.front_door_blocked",
                                                       NotificationSeverity.Warning));
                Log.Warn("Interphone", "grant " + level + " refused: the office door is bolted");
                return;
            }

            var visitor = Active;

            if (level == VisitorAccessLevel.Escorted && !visitor.canBeEscorted)
            {
                EventBus.Publish(new NotificationEvent("ui.access.blocked.no_escort",
                                                       NotificationSeverity.Warning));
                return;
            }

            _decisions[visitor.visitorId] = level;

            if (level == VisitorAccessLevel.Hold)
            {
                // Holding costs time and keeps them standing there (GDD 13.3). It is not a
                // resolution: the caller stays at the panel and the queue does not move.
                ServiceHub.Cases.ApplyConsequences(visitor.onHold);
                ServiceHub.Clock.AdvanceSeconds(120);
                Log.Info("Interphone", visitor.visitorId + " held outside");

                var holdCb = OnVisitorResolved;
                if (holdCb != null) holdCb(visitor, level);
                return;
            }

            var judgement = Judge(visitor, level);
            ServiceHub.State.NoteVisitor(judgement == GrantJudgement.Correct);
            ServiceHub.Cases.ApplyConsequences(
                judgement == GrantJudgement.Correct ? visitor.onCorrect : visitor.onWrong);
            NotePressure(visitor, level, judgement);

            ServiceHub.AccessLog.Add(
                ServiceHub.Clock.GameSecond,
                Residents.AccessSubject.Visitor,
                visitor.nameKey,
                string.Empty,
                "log.location.lobby",
                visitor.cameraId,
                VisitorAccess.LetsThemIn(level));

            // And this is where the door stops being the end of it (v3.0 B-02). Anybody who
            // was let in becomes somebody who is now walking around the building.
            if (VisitorAccess.LetsThemIn(level) && ServiceHub.ActiveVisitors != null)
                ServiceHub.ActiveVisitors.Admit(visitor, level, GrantingPlayerId);

            EventBus.Publish(new VisitorDecidedEvent(visitor.visitorId, (int)level));
            Cases.SubquestRules.VisitorDecided(visitor.visitorId, (int)level);
            ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.JudgeVisitor, visitor.visitorId);
            ServiceHub.Analytics.Track("visitor_access", visitor.visitorId + ":" + level);
            Log.Info("Interphone", visitor.visitorId + " -> " + level + " (" + judgement + ")");

            var cb = OnVisitorResolved;
            if (cb != null) cb(visitor, level);

            // The caller's script leaves with the caller.
            //
            // Every doorstep conversation ends on an "await" node - the questions have been
            // answered and the caller is standing there waiting to be let in or turned away -
            // and a terminal node only closes when the view's Continue button calls Advance().
            // The natural way to play is to press admit or refuse instead, which closed
            // nothing, so the dialogue service stayed busy on somebody who had already left
            // the door.
            //
            // Two things broke off that. TryStartVisitorTalk will not open a script while the
            // service is busy, so the next caller arrived at a door panel with no questions on
            // it and no route to the two independent facts GDD 13.2 requires. And the shift
            // could never be closed: the clock-off button reads Dialogue.IsActive, so a
            // caretaker who had finished every job on the night was told they were
            // mid-conversation until 06:00.
            Active = null;
            Read.Clear();
            EndTalkWith(visitor);
            PullNext();

            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);
        }

        /// <summary>
        /// Closes this caller's conversation, and only this caller's.
        ///
        /// The radio and the handset share the dialogue service, so ending whatever happens to
        /// be open would cut off a call the caretaker is in the middle of.
        /// </summary>
        static void EndTalkWith(VisitorDefinition visitor)
        {
            var dialogue = ServiceHub.Dialogue;
            if (dialogue == null || dialogue.Current == null) return;
            if (string.IsNullOrEmpty(visitor.conversationId)) return;
            if (dialogue.Current.conversationId != visitor.conversationId) return;

            dialogue.End();
        }

        /// <summary>How a grant compares with what an informed caretaker would have given.</summary>
        public enum GrantJudgement
        {
            /// <summary>Exactly right, and arrived at with enough to go on.</summary>
            Correct = 0,
            /// <summary>Right answer, reached by guessing. Not graded (GDD 13.2).</summary>
            Uninformed = 1,
            /// <summary>Tighter than it needed to be. Costs goodwill, costs nobody their safety.</summary>
            OverCautious = 2,
            /// <summary>Further in than they should have been. This is the one that costs.</summary>
            OverPermissive = 3
        }

        /// <summary>
        /// Grading a grant, which is a richer question than grading a yes or no.
        ///
        /// The old model had two answers and so had two outcomes. Seven levels have a
        /// direction: giving somebody less than they needed and giving them more than they
        /// should have had are both wrong, and they are wrong in opposite ways. A caretaker
        /// who refuses everybody is safe and unemployed; one who hands out floor passes has a
        /// building full of people nobody can find.
        /// </summary>
        public static GrantJudgement Judge(VisitorDefinition visitor, VisitorAccessLevel level)
        {
            int granted = VisitorAccess.RiskRank(level);
            int correct = VisitorAccess.RiskRank(visitor.correctAccess);

            if (granted > correct) return GrantJudgement.OverPermissive;
            if (granted < correct) return GrantJudgement.OverCautious;

            return ServiceHub.Interphone != null && ServiceHub.Interphone.HasEnoughInformation
                ? GrantJudgement.Correct
                : GrantJudgement.Uninformed;
        }

        /// <summary>
        /// GDD 15.4. A judgement used to end at a number on the summary screen; this is where
        /// it stops being a score.
        ///
        /// Over-permissive is the expensive one and it is expensive in proportion: letting
        /// somebody who should have been held at the lobby onto the fourth floor is not the
        /// same mistake as letting somebody who should have been refused into the building at
        /// all, and the pressure charged says so.
        /// </summary>
        void NotePressure(VisitorDefinition visitor, VisitorAccessLevel level, GrantJudgement judgement)
        {
            var pressure = ServiceHub.Pressure;
            if (pressure == null) return;

            switch (judgement)
            {
                case GrantJudgement.Correct:
                    pressure.NoteVisitorCorrect();
                    return;

                case GrantJudgement.OverCautious:
                    // Only turning away somebody who had a right to be here is a real cost.
                    // Holding a courier in the vestibule is just slow.
                    if (level == VisitorAccessLevel.Reject) pressure.NoteGenuineRefused();
                    return;

                case GrantJudgement.OverPermissive:
                    pressure.NoteWrongAdmit(visitor.visitorId);

                    // Somebody who should never have been through the front door at all is not
                    // walking a route - they are loose, and the traffic service is what gives
                    // the caretaker a way to find them again (GDD 15.4).
                    if (visitor.ShouldBeRefused && ServiceHub.Traffic != null)
                        ServiceHub.Traffic.InjectIntruder(visitor.visitorId, visitor.nameKey);
                    return;
            }
        }

        /// <summary>
        /// Who pressed the button. One caretaker today; in a four-handed shift the pass has to
        /// remember whose it was (v3.0 45.3), and the record is worth keeping either way.
        /// </summary>
        public string GrantingPlayerId = "P1";

        /// <summary>
        /// Put this copy's door where the host's door is (v3.0 46.1).
        ///
        /// Only ever called on a client. It sets the active caller and which records have been
        /// opened for them, and it deliberately does not touch the queue: a client has no
        /// business deciding who is next, and the host will tell it when that changes.
        /// </summary>
        public void ApplyMirror(string visitorId, int consultedAppsMask)
        {
            var next = string.IsNullOrEmpty(visitorId) ? null : _content.FindVisitor(visitorId);

            if (next != Active)
            {
                Active = next;
                _consultedChecks.Clear();
                _consultedSources.Clear();
                Read.Begin(next);

                // Somebody is at the door, and a caretaker three floors up has to be told the
                // same way the one at the desk was: the interphone is the only thing in this
                // game that demands attention from wherever you are standing (v3.0 37).
                if (next != null)
                {
                    var cb = OnVisitorArrived;
                    if (cb != null) cb(next);
                }
            }

            _consultedSources.Clear();
            for (int i = 0; i < AppIds.Order.Length; i++)
                if ((consultedAppsMask & (1 << i)) != 0) _consultedSources.Add(AppIds.Order[i]);
        }

        public void Reset()
        {
            _queue.Clear();
            _decisions.Clear();
            _consultedChecks.Clear();
            _consultedSources.Clear();
            Active = null;
            DoorBlocked = false;
            Read.Clear();
        }

        public void LoadFrom(IEnumerable<Save.VisitorSaveEntry> entries)
        {
            _decisions.Clear();
            if (entries == null) return;
            foreach (var e in entries) _decisions[e.visitorId] = (VisitorAccessLevel)e.decision;
        }

        public IEnumerable<KeyValuePair<string, VisitorAccessLevel>> Decisions { get { return _decisions; } }
    }
}
