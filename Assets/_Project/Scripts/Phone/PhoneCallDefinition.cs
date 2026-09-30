using UnityEngine;
using NO404.Cases;

namespace NO404.Phone
{
    /// <summary>
    /// How hard a call pushes to the front of the queue when several ring at once
    /// (GDD 20.16 "같은 시간에 전화가 여러 개 오면 우선순위 큐").
    /// </summary>
    public enum CallPriority
    {
        Routine = 0,
        Resident = 1,
        Management = 2,
        Emergency = 3,
        /// <summary>Calls that should not be possible at all. Always jump the queue.</summary>
        Impossible = 4
    }

    [CreateAssetMenu(menuName = "NO404/Phone/Call", fileName = "CALL_")]
    public sealed class PhoneCallDefinition : ScriptableObject
    {
        public string callId;
        public string callerNameKey;
        [Tooltip("Number shown on the handset. '404' for the call that cannot exist.")]
        public string callerNumber;
        public string conversationId;
        public CallPriority priority = CallPriority.Resident;

        [Header("Schedule")]
        public int nightIndex;
        [Tooltip("Game second the handset starts ringing.")]
        public int gameSecond;
        [Tooltip("How long it rings before it counts as missed, in game seconds.")]
        public int ringSeconds = 180;
        public ConditionDefinition[] conditions = new ConditionDefinition[0];

        [Header("Outcome")]
        public string caseId;

        /// <summary>
        /// Game seconds to wait after that case opens before this rings (v5.0 4.2).
        ///
        /// Zero, and almost always zero: a call belongs to a beat and arrives with it. It
        /// exists because one case can carry more than one beat - night 4's main is both the
        /// chairman ordering a record deleted and, later in the same job, the record's own
        /// household calling back. Both hang off the case, because a call owed to the clock is
        /// the thing this design refuses; they simply do not arrive in the same second.
        /// </summary>
        public int releaseDelaySeconds;
        public ConsequenceDefinition[] onAnswered = new ConsequenceDefinition[0];
        public ConsequenceDefinition[] onDeclined = new ConsequenceDefinition[0];
        [Tooltip("Applied when the player never picks up. Missing a call is a cost, not a dead end.")]
        public ConsequenceDefinition[] onMissed = new ConsequenceDefinition[0];
        [Tooltip("Re-rings once after this many game seconds if it was missed. 0 = never.")]
        public int retryAfterSeconds;
    }
}
