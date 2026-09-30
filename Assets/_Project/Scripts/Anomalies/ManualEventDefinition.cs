using System;
using UnityEngine;
using NO404.Cases;

namespace NO404.Anomalies
{
    /// <summary>
    /// Lifecycle of a manual anomaly event (spec 0.2).
    ///
    /// The same ladder the main cases use, minus the investigation rungs an M event has no
    /// use for: an M event is survived by procedure, not solved by cross-referencing, so it
    /// goes from being noticed to being handled without an evidence bar in between (spec 0.9.4).
    /// </summary>
    public enum ManualEventState
    {
        Dormant = 0,
        /// <summary>The player has seen something is wrong. No task card yet.</summary>
        Teased = 1,
        /// <summary>Conditions are met and the event can begin.</summary>
        Available = 2,
        /// <summary>The player is in it, working the procedure.</summary>
        Active = 3,
        ResolvedCorrect = 4,
        ResolvedPartial = 5,
        ResolvedWrong = 6,
        ConsequenceQueued = 7,
        ConsequenceApplied = 8
    }

    public static class ManualEventStateExtensions
    {
        public static bool IsResolved(this ManualEventState state)
        {
            return state >= ManualEventState.ResolvedCorrect;
        }
    }

    /// <summary>How an event starts (spec 26 setupTrigger).</summary>
    /// <summary>
    /// How an event starts (spec 26 setupTrigger).
    ///
    /// The night is a chain rather than a timetable. Each night has one head that opens with
    /// the shift, and every other event is handed to the player the moment the one before it
    /// is closed out - so the pace is set by how fast they work, not by the clock.
    ///
    /// The clock-driven variant is deliberately gone. It made the night a list of alarms: an
    /// event whose hour had not come could not be reached however well the player was doing,
    /// and one whose hour had passed while they were three floors down arrived with no
    /// connection to anything they had just been doing.
    /// </summary>
    public enum ManualEventTrigger
    {
        /// <summary>The head of a night. Opens when the shift starts.</summary>
        NightStart = 0,
        /// <summary>PLAYER_ENTER_ZONE:&lt;zone&gt;</summary>
        ZoneEntered = 1,
        /// <summary>A case reaching a resolved state.</summary>
        CaseResolved = 2,
        PhoneCall = 3,
        Interphone = 4,
        /// <summary>Started explicitly by another event or by a scripted beat.</summary>
        Manual = 5,
        /// <summary>
        /// triggerTarget names the M event this one follows. Fires as soon as that event is
        /// resolved - on any outcome, because a wrong answer must not take the rest of the
        /// night with it (spec 0.3 / 0.5).
        /// </summary>
        AfterEvent = 6
    }

    public enum ManualCompare
    {
        Equals = 0,
        NotEquals = 1,
        AtLeast = 2,
        AtMost = 3
    }

    /// <summary>
    /// One clause of spec 26's correctConditions, e.g. "replyCount == sourceCount" or
    /// "playerInitiatedEarly == false".
    ///
    /// Both sides are counters on the running event rather than global state. An M event is
    /// judged entirely on what the player did during it, which is what lets the same event be
    /// replayed after a wrong answer without unpicking anything (spec 30.2 case 3).
    /// </summary>
    [Serializable]
    public sealed class ManualCheckDefinition
    {
        [Tooltip("Left-hand counter, e.g. replyCount.")]
        public string counterId;
        public ManualCompare compare = ManualCompare.Equals;
        [Tooltip("Right-hand counter. Leave empty to compare against value instead.")]
        public string otherCounterId;
        [Tooltip("Right-hand literal, used when otherCounterId is empty. Booleans are 0 and 1.")]
        public int value;

        /// <summary>
        /// The manual rule this clause is the test of, e.g. manual.m02.forbid.1.
        ///
        /// Set it on the clauses that correspond to a written prohibition, and leave it empty
        /// on the ones that are simply "the job is not finished". That difference is what lets
        /// ManualEventService tell a rule the player broke from a step they had not got to
        /// yet - only the first counts as a violation (spec 0.10.2 B).
        /// </summary>
        public string ruleKey;

        public ManualCheckDefinition Breaking(string rule)
        {
            ruleKey = rule;
            return this;
        }

        public static ManualCheckDefinition Counters(string a, ManualCompare compare, string b)
        {
            return new ManualCheckDefinition { counterId = a, compare = compare, otherCounterId = b };
        }

        public static ManualCheckDefinition Literal(string counterId, ManualCompare compare, int value)
        {
            return new ManualCheckDefinition { counterId = counterId, compare = compare, value = value };
        }

        public static ManualCheckDefinition IsFalse(string counterId)
        {
            return Literal(counterId, ManualCompare.Equals, 0);
        }

        public static ManualCheckDefinition IsTrue(string counterId)
        {
            return Literal(counterId, ManualCompare.Equals, 1);
        }
    }

    /// <summary>One numbered step of the procedure the player has to carry out.</summary>
    [Serializable]
    public sealed class ManualObjectiveDefinition
    {
        public string objectiveId;
        [Tooltip("HUD text. Must not name the answer the player has not been given yet.")]
        public string titleKey;
        [Tooltip("Optional steps do not gate the decision.")]
        public bool optional;
        [Tooltip("Tracked without appearing in the HUD.")]
        public bool hidden;
    }

    /// <summary>
    /// Spec 26's failsafe block, and the promise spec 0.5 makes: no M event can leave the
    /// player with nothing left to try.
    /// </summary>
    [Serializable]
    public sealed class ManualFailSafeDefinition
    {
        [Tooltip("Wrong attempts after which the recovery route opens. 0 disables it.")]
        public int afterWrongAttempts = 2;
        [Tooltip("What opens up, e.g. ENABLE_KNOCK_REPLAY.")]
        public string action;
        [Tooltip("Hint shown when the route opens.")]
        public string notifyKey;
        [Tooltip("Manual page handed over as a last resort. Often the event's own page.")]
        public string revealPageId;
    }

    /// <summary>
    /// One of the eighteen night-anomaly events M01..M18 (spec 22), in the common shape spec
    /// 26 defines.
    ///
    /// The division of labour with <see cref="CaseDefinition"/> is deliberate. A case asks the
    /// player to work out what is true; an M event asks them to do the right thing while it is
    /// happening. So this carries a procedure, a set of counters to judge it by, and two sets
    /// of consequences - and no evidence board, no report, no decision list.
    /// </summary>
    [CreateAssetMenu(menuName = "NO404/Manual/Manual Event", fileName = "M")]
    public sealed class ManualEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string eventId;
        public string nameKey;
        [Tooltip("Task-card summary shown once the event is registered.")]
        public string summaryKey;

        [Header("Placement")]
        public int nightIndex;
        [Tooltip("Floor id from FloorPlan. Risk from this event lands here.")]
        public string floorId;
        [Tooltip("Zone the event takes place in.")]
        public string zoneId;
        [Tooltip("Earliest game second (spec 26 earliestTime).")]
        public int earliestSecond;
        /// <summary>
        /// Re-arms on every night from <see cref="nightIndex"/> up to <see cref="lastNightIndex"/>.
        /// Only M13 uses this: spec 22 gives it one postcard a night from night 1 to night 5.
        /// </summary>
        public bool repeatsNightly;
        [Tooltip("Last night a repeating event appears. Ignored unless repeatsNightly.")]
        public int lastNightIndex;

        [Header("Availability")]
        public ManualEventTrigger trigger = ManualEventTrigger.ZoneEntered;
        [Tooltip("Zone / case / call id, depending on the trigger.")]
        public string triggerTarget;
        public ConditionDefinition[] startConditions = new ConditionDefinition[0];

        [Header("Manual (spec 0.9.2)")]
        [Tooltip("The page that tells the player how to handle this. Required for every event.")]
        public string manualPageId;

        [Header("Procedure")]
        public ManualObjectiveDefinition[] objectives = new ManualObjectiveDefinition[0];
        [Tooltip("All must hold for the outcome to be Correct (spec 26 correctConditions).")]
        public ManualCheckDefinition[] correctConditions = new ManualCheckDefinition[0];
        [Tooltip("If all of these hold the outcome is Partial rather than Wrong. May be empty.")]
        public ManualCheckDefinition[] partialConditions = new ManualCheckDefinition[0];

        [Header("Outcome")]
        public ConsequenceDefinition[] onCorrect = new ConsequenceDefinition[0];
        public ConsequenceDefinition[] onPartial = new ConsequenceDefinition[0];
        public ConsequenceDefinition[] onWrong = new ConsequenceDefinition[0];
        public ManualFailSafeDefinition failSafe = new ManualFailSafeDefinition();

        /// <summary>
        /// Spec 0.10.5: an event may only kill on a repeated breach of a prohibition the
        /// manual marks lethal. Off by default, and the runtime still requires the page to
        /// carry a lethal prohibition before honouring it.
        /// </summary>
        [Tooltip("Repeated lethal-rule breaches may end the shift. Requires a lethal prohibition on the page.")]
        public bool lethalOnRepeat;

        public ManualObjectiveDefinition FindObjective(string objectiveId)
        {
            if (objectives == null) return null;
            for (int i = 0; i < objectives.Length; i++)
                if (objectives[i] != null && objectives[i].objectiveId == objectiveId) return objectives[i];
            return null;
        }

        /// <summary>Required steps, in order. What the HUD counts.</summary>
        public int RequiredObjectiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < objectives.Length; i++)
                    if (objectives[i] != null && !objectives[i].optional) count++;
                return count;
            }
        }
    }
}
