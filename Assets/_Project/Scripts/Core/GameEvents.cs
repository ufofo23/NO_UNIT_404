namespace NO404.Core
{
    // Unity 6 compiles as C# 9, so `record struct` from the GDD sample is written as
    // `readonly struct` here. Semantics (immutable value payload) are unchanged.

    public readonly struct GameMinuteChangedEvent
    {
        public readonly int GameSecond;
        public readonly int Hour;
        public readonly int Minute;
        public GameMinuteChangedEvent(int gameSecond, int hour, int minute)
        {
            GameSecond = gameSecond; Hour = hour; Minute = minute;
        }
    }

    public readonly struct NightStartedEvent
    {
        public readonly int NightIndex;
        public NightStartedEvent(int nightIndex) { NightIndex = nightIndex; }
    }

    public readonly struct NightEndedEvent
    {
        public readonly int NightIndex;
        public NightEndedEvent(int nightIndex) { NightIndex = nightIndex; }
    }

    /// <summary>
    /// A story sequence has been asked for (v5.1 8.5). Carries the id and nothing else: the
    /// game state it follows from has already been applied, so a listener that is not there
    /// - a test, a client - loses the picture and none of the consequences.
    /// </summary>
    public readonly struct CinematicRequestedEvent
    {
        public readonly string CinematicId;
        public CinematicRequestedEvent(string cinematicId) { CinematicId = cinematicId; }
    }

    public readonly struct CaseStartedEvent
    {
        public readonly string CaseId;
        public CaseStartedEvent(string caseId) { CaseId = caseId; }
    }

    public readonly struct CaseStateChangedEvent
    {
        public readonly string CaseId;
        public readonly Cases.CaseState Previous;
        public readonly Cases.CaseState Current;
        public CaseStateChangedEvent(string caseId, Cases.CaseState previous, Cases.CaseState current)
        {
            CaseId = caseId; Previous = previous; Current = current;
        }
    }

    public readonly struct ObjectiveChangedEvent
    {
        public readonly string CaseId;
        public readonly string ObjectiveId;
        public readonly bool Completed;
        public ObjectiveChangedEvent(string caseId, string objectiveId, bool completed)
        {
            CaseId = caseId; ObjectiveId = objectiveId; Completed = completed;
        }
    }

    public readonly struct EvidenceAcquiredEvent
    {
        public readonly string EvidenceId;
        public EvidenceAcquiredEvent(string evidenceId) { EvidenceId = evidenceId; }
    }

    public readonly struct EvidenceLinkedEvent
    {
        public readonly string EvidenceA;
        public readonly string EvidenceB;
        public EvidenceLinkedEvent(string a, string b) { EvidenceA = a; EvidenceB = b; }
    }

    public readonly struct StatChangedEvent
    {
        public readonly string StatId;
        public readonly int Previous;
        public readonly int Current;
        public readonly string Reason;
        public StatChangedEvent(string statId, int previous, int current, string reason)
        {
            StatId = statId; Previous = previous; Current = current; Reason = reason;
        }
    }

    /// <summary>A choice that has a value rather than a yes/no was decided (v5.0 5.4).</summary>
    public readonly struct ChoiceRecordedEvent
    {
        public readonly string ChoiceId;
        public readonly string Value;
        public ChoiceRecordedEvent(string choiceId, string value)
        {
            ChoiceId = choiceId; Value = value;
        }
    }

    /// <summary>
    /// The caretaker's body or nerve moved (v5.0 6, 7).
    ///
    /// Carries the reason key, because v5.0 6.1 and 7.1 both forbid a change the player could
    /// not have seen coming - and a HUD that can say what just cost them ten points is the
    /// difference between a resource and a punishment.
    /// </summary>
    public readonly struct VitalChangedEvent
    {
        public readonly bool IsHp;
        public readonly int Previous;
        public readonly int Current;
        public readonly string ReasonKey;
        public VitalChangedEvent(bool isHp, int previous, int current, string reasonKey)
        {
            IsHp = isHp; Previous = previous; Current = current; ReasonKey = reasonKey;
        }
    }

    /// <summary>HP or SAN reached zero. The night is lost and no ending is reachable (v5.0 6.1 / 7.2).</summary>
    public readonly struct VitalCollapseEvent
    {
        public readonly bool Physical;
        public VitalCollapseEvent(bool physical) { Physical = physical; }
    }

    public readonly struct FlagChangedEvent
    {
        public readonly string FlagId;
        public readonly bool Value;
        public FlagChangedEvent(string flagId, bool value) { FlagId = flagId; Value = value; }
    }

    public readonly struct CctvSnapshotCreatedEvent
    {
        public readonly string CameraId;
        public readonly int GameSecond;
        public readonly string SnapshotId;
        public CctvSnapshotCreatedEvent(string cameraId, int gameSecond, string snapshotId)
        {
            CameraId = cameraId; GameSecond = gameSecond; SnapshotId = snapshotId;
        }
    }

    public readonly struct CctvAnomalyEvent
    {
        public readonly string CameraId;
        public readonly string AnomalyId;
        public readonly bool Started;
        public CctvAnomalyEvent(string cameraId, string anomalyId, bool started)
        {
            CameraId = cameraId; AnomalyId = anomalyId; Started = started;
        }
    }

    public readonly struct ZoneChangedEvent
    {
        public readonly string ZoneId;
        public ZoneChangedEvent(string zoneId) { ZoneId = zoneId; }
    }

    public readonly struct PcModeChangedEvent
    {
        public readonly bool Active;
        public PcModeChangedEvent(bool active) { Active = active; }
    }

    public readonly struct AppOpenedEvent
    {
        public readonly string AppId;
        public AppOpenedEvent(string appId) { AppId = appId; }
    }

    public readonly struct ResidentRecordViewedEvent
    {
        public readonly string ResidentId;
        public ResidentRecordViewedEvent(string residentId) { ResidentId = residentId; }
    }

    /// <summary>
    /// The player brought a camera up on the wall.
    ///
    /// Two v2.1 rules turn on this and nothing else: M01 forbids enlarging the lift camera
    /// while the floor sensor is lit, and M02 forbids zooming on the figure in the car park.
    /// Both are things the caretaker does at the desk with no object involved, so the anomaly
    /// props have no other way to hear about them.
    /// </summary>
    public readonly struct CctvChannelViewedEvent
    {
        public readonly string CameraId;
        public CctvChannelViewedEvent(string cameraId) { CameraId = cameraId; }
    }

    public readonly struct DialogueChoiceSelectedEvent
    {
        public readonly string ConversationId;
        public readonly string ChoiceId;
        public DialogueChoiceSelectedEvent(string conversationId, string choiceId)
        {
            ConversationId = conversationId; ChoiceId = choiceId;
        }
    }

    public readonly struct VisitorDecidedEvent
    {
        public readonly string VisitorId;
        public readonly int Decision;
        public VisitorDecidedEvent(string visitorId, int decision)
        {
            VisitorId = visitorId; Decision = decision;
        }
    }

    /// <summary>
    /// Published once by ServiceHub.ResetPlaythrough. World objects that latch one-shot state
    /// (EvidencePickup, DialogueInteractable, FlagInteractable) listen for this to clear it,
    /// because the office/lobby zones stay resident across a New Game inside the same running
    /// session and are never rebuilt from scratch.
    /// </summary>
    public readonly struct PlaythroughResetEvent
    {
    }

    /// <summary>
    /// Published by SaveService.Restore once every service has finished loading. EvidencePickup
    /// listens for this to hide itself again when the loaded save already owns its evidence -
    /// PlaythroughResetEvent fires earlier in the same restore, before evidence is loaded, so it
    /// cannot know that yet.
    /// </summary>
    public readonly struct SaveRestoredEvent
    {
    }

    public readonly struct GameSavedEvent
    {
        public readonly Save.SaveReason Reason;
        public readonly int Slot;
        public GameSavedEvent(Save.SaveReason reason, int slot) { Reason = reason; Slot = slot; }
    }

    /// <summary>
    /// Somebody was let in, and how far (v3.0 38.2). Carries the level as an int so the bus
    /// keeps its no-dependencies rule: Core does not reference the Visitors namespace.
    /// </summary>
    public readonly struct VisitorAccessGrantedEvent
    {
        public readonly string VisitorId;
        public readonly int Level;
        public VisitorAccessGrantedEvent(string visitorId, int level)
        {
            VisitorId = visitorId; Level = level;
        }
    }

    /// <summary>
    /// A visitor has been observed somewhere their pass does not reach (v3.0 38.5).
    ///
    /// Deliberately not a verdict and deliberately not a popup. Whatever listens to this makes
    /// the world say it - a camera, a log line, a door that was open - and the caretaker
    /// decides what it means.
    /// </summary>
    public readonly struct VisitorOffRouteEvent
    {
        public readonly string VisitorId;
        public readonly string ZoneId;
        public VisitorOffRouteEvent(string visitorId, string zoneId)
        {
            VisitorId = visitorId; ZoneId = zoneId;
        }
    }

    public readonly struct NotificationEvent
    {
        public readonly string BodyKey;
        public readonly NotificationSeverity Severity;
        public NotificationEvent(string bodyKey, NotificationSeverity severity)
        {
            BodyKey = bodyKey; Severity = severity;
        }
    }

    /// <summary>
    /// The night's counter-pressure moved (GDD 15.4). Carries the reason so the analytics
    /// pass can tell drift apart from a door left open.
    /// </summary>
    public readonly struct PressureChangedEvent
    {
        public readonly int Previous;
        public readonly int Current;
        public readonly string ReasonKey;
        public PressureChangedEvent(int previous, int current, string reasonKey)
        {
            Previous = previous; Current = current; ReasonKey = reasonKey;
        }
    }

    /// <summary>The pressure ladder crossed a rung (GDD 15.4). Drives sound, light and the door.</summary>
    public readonly struct PressureStageChangedEvent
    {
        public readonly Pressure.PressureStage Previous;
        public readonly Pressure.PressureStage Current;
        public PressureStageChangedEvent(Pressure.PressureStage previous, Pressure.PressureStage current)
        {
            Previous = previous; Current = current;
        }
    }

    /// <summary>Night reserve changed (GDD 15.5). Percent, plus whether it is under the line.</summary>
    public readonly struct PowerReserveChangedEvent
    {
        public readonly int Percent;
        public readonly bool Low;
        public PowerReserveChangedEvent(int percent, bool low) { Percent = percent; Low = low; }
    }

    /// <summary>The office door changed state or bolt (GDD 15.6).</summary>
    public readonly struct OfficeDoorChangedEvent
    {
        public readonly Interaction.OfficeDoorState State;
        public readonly bool Locked;
        public OfficeDoorChangedEvent(Interaction.OfficeDoorState state, bool locked)
        {
            State = state; Locked = locked;
        }
    }

    // ---- v2.1: stairs, risk and the response manual -------------------------

    /// <summary>
    /// The player reached a stairwell landing (spec 0.8.2).
    ///
    /// <see cref="From"/> and <see cref="To"/> are equal on entry, and equal again whenever an
    /// anomaly has bent the shaft back onto the floor the player just left - which is the one
    /// signal a listener needs to dress a loop.
    /// </summary>
    public readonly struct StairLandingChangedEvent
    {
        public readonly string From;
        public readonly string To;
        public readonly bool Entered;
        public StairLandingChangedEvent(string from, string to, bool entered)
        {
            From = from; To = to; Entered = entered;
        }
    }

    /// <summary>
    /// A floor's risk level changed (spec 0.10.3). Never rendered as a number: listeners turn
    /// it into light, sound and blocked routes.
    /// </summary>
    public readonly struct FloorRiskChangedEvent
    {
        public readonly string FloorId;
        public readonly int Previous;
        public readonly int Current;
        public FloorRiskChangedEvent(string floorId, int previous, int current)
        {
            FloorId = floorId; Previous = previous; Current = current;
        }
    }

    /// <summary>
    /// DistortionExposure crossed into a new band (spec 0.10.4). Fires on the band, not on
    /// every point, because the bands are what change the presentation.
    /// </summary>
    public readonly struct DistortionBandChangedEvent
    {
        public readonly int Previous;
        public readonly int Current;
        public readonly int Exposure;
        public DistortionBandChangedEvent(int previous, int current, int exposure)
        {
            Previous = previous; Current = current; Exposure = exposure;
        }
    }

    /// <summary>The player broke a manual rule they had been given (spec 0.10.2 B).</summary>
    public readonly struct ManualViolationEvent
    {
        public readonly string EventId;
        public readonly string RuleKey;
        public readonly int TotalViolations;
        public ManualViolationEvent(string eventId, string ruleKey, int totalViolations)
        {
            EventId = eventId; RuleKey = ruleKey; TotalViolations = totalViolations;
        }
    }

    /// <summary>A manual page became readable (spec 0.9.2).</summary>
    public readonly struct ManualPageUnlockedEvent
    {
        public readonly string PageId;
        public readonly string EventId;
        public ManualPageUnlockedEvent(string pageId, string eventId)
        {
            PageId = pageId; EventId = eventId;
        }
    }

    /// <summary>A manual anomaly event changed phase (spec 0.2 lifecycle).</summary>
    public readonly struct ManualEventStateChangedEvent
    {
        public readonly string EventId;
        public readonly Anomalies.ManualEventState Previous;
        public readonly Anomalies.ManualEventState Current;
        public ManualEventStateChangedEvent(string eventId,
                                            Anomalies.ManualEventState previous,
                                            Anomalies.ManualEventState current)
        {
            EventId = eventId; Previous = previous; Current = current;
        }
    }

    // ---- anomalous tools A01..A05 (spec 23) ---------------------------------

    /// <summary>A machine that was ordinary until tonight has started answering.</summary>
    public readonly struct AnomalyToolAwakeEvent
    {
        public readonly string ToolId;
        public AnomalyToolAwakeEvent(string toolId) { ToolId = toolId; }
    }

    /// <summary>The caretaker stepped up to a machine, or away from it.</summary>
    public readonly struct AnomalyToolSessionEvent
    {
        public readonly string ToolId;
        public readonly bool Open;
        public AnomalyToolSessionEvent(string toolId, bool open) { ToolId = toolId; Open = open; }
    }

    /// <summary>One transaction: what was asked for and what the machine gave back.</summary>
    public readonly struct AnomalyToolUsedEvent
    {
        public readonly string ToolId;
        public readonly string OptionId;
        public readonly string ResultKey;
        public AnomalyToolUsedEvent(string toolId, string optionId, string resultKey)
        {
            ToolId = toolId; OptionId = optionId; ResultKey = resultKey;
        }
    }

    /// <summary>
    /// A machine is waiting on the caretaker (spec 23 A02 door, A04 return).
    ///
    /// <c>Open</c> is what the hold is after this change rather than what happened, so a
    /// listener that dresses an open locker door only has one field to read. An expiry that
    /// leaves the door open publishes Open true and Expired true together.
    /// </summary>
    public readonly struct AnomalyToolHoldEvent
    {
        public readonly string ToolId;
        public readonly bool Open;
        public readonly bool Expired;
        public AnomalyToolHoldEvent(string toolId, bool open, bool expired)
        {
            ToolId = toolId; Open = open; Expired = expired;
        }
    }

    /// <summary>
    /// Movement is being pulled somewhere until the given game second (spec 23 A05).
    ///
    /// The direction is not in the event on purpose: what the parapet is, is a fact about the
    /// roof rather than about the machine, so the prop that knows where it stands supplies it
    /// and the service only says for how long.
    /// </summary>
    public readonly struct AnomalyDriftEvent
    {
        public readonly string ToolId;
        public readonly int EndsAtGameSecond;
        public AnomalyDriftEvent(string toolId, int endsAtGameSecond)
        {
            ToolId = toolId; EndsAtGameSecond = endsAtGameSecond;
        }
    }

    /// <summary>
    /// Movement is being pulled toward a place in the world until the given game second.
    ///
    /// Published by whatever knows where the pull is - the parapet anchor on the roof - and
    /// consumed by the player controller. Spec 23 A05 rules out a forced fall, so this is a
    /// bias the caretaker can walk out of by walking the other way, and it ends on its own.
    /// </summary>
    public readonly struct PlayerDriftEvent
    {
        public readonly UnityEngine.Vector3 Target;
        public readonly int EndsAtGameSecond;
        public PlayerDriftEvent(UnityEngine.Vector3 target, int endsAtGameSecond)
        {
            Target = target; EndsAtGameSecond = endsAtGameSecond;
        }
    }

    public enum NotificationSeverity { Info, Task, Warning, Urgent }
}
