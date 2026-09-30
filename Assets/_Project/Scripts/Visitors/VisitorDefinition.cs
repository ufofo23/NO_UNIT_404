using System;
using UnityEngine;
using NO404.Cases;

namespace NO404.Visitors
{
    /// <summary>
    /// A single verifiable fact shown on the interphone. GDD 13.2 requires at least two
    /// independent checks to identify a fake visitor, so a visitor is only "correctly
    /// judged" when the player has consulted enough of these.
    /// </summary>
    [Serializable]
    public sealed class VisitorCheck
    {
        public string labelKey;
        public string valueKey;
        [Tooltip("True when this specific fact contradicts the visitor's story.")]
        public bool contradicts;
        [Tooltip("Where the player can independently confirm this: resident DB, access log, CCTV...")]
        public string crossReferenceAppId;
        [Tooltip("Earliest night this check is on the panel at all. The nightly briefing " +
                 "is what teaches it, so the player's checklist grows with the threat.")]
        public int fromNight;
    }

    [CreateAssetMenu(menuName = "NO404/Visitors/Visitor", fileName = "VIS_")]
    public sealed class VisitorDefinition : ScriptableObject
    {
        public string visitorId;
        public string nameKey;
        public string purposeKey;
        public string idCardNameKey;
        public string targetUnit;
        public string cameraId = "CAM-01";
        public string conversationId;

        [Header("Schedule")]
        [Tooltip("Night this visitor turns up on. -1 = never scheduled automatically.")]
        public int nightIndex = -1;
        [Tooltip("Game second they press the buzzer.")]
        public int arrivalGameSecond;

        [Header("Truth")]
        [Tooltip("The furthest a fully informed caretaker should let this person in. " +
                 "Granting more than this is a safety failure; granting less is over-caution, " +
                 "and both cost something different (v3.0 38.2).")]
        public VisitorAccessLevel correctAccess = VisitorAccessLevel.LobbyOnly;

        [Tooltip("Which levels the panel offers for this caller at all. Empty = whatever the " +
                 "night has taught. Use it to keep a courier off the floor buttons entirely.")]
        public VisitorAccessLevel[] allowedAccess = new VisitorAccessLevel[0];

        public VisitorCheck[] checks = new VisitorCheck[0];

        [Header("Where they go (v3.0 38.5)")]
        [Tooltip("The zone they say they are here for. FloorPass resolves against this.")]
        public string destinationZone;

        [Tooltip("Zones they walk through, in order, if nothing goes wrong. The route is what " +
                 "makes leaving it visible - a caller with no route can never be off it.")]
        public string[] expectedRoute = new string[0];

        [Tooltip("True for callers who will leave the route once they have the run of a floor.")]
        public bool canDeviate;

        [Tooltip("Where they go instead. Empty falls back to the fourth floor.")]
        public string deviationZone;

        [Tooltip("How many legs they walk honestly first. Deviating on the first step reads " +
                 "as a scripted trap rather than as somebody taking their chance.")]
        public int deviateAfterStep = 1;

        [Tooltip("Escorting is refused for some callers - a delivery has no reason to need one.")]
        public bool canBeEscorted = true;

        [Tooltip("Not happening now: a recording of something that already did (v3.0 38.1).")]
        public bool isHistoricalReplay;

        [Tooltip("Impossible, and not a threat. Turning them away is not the safe answer.")]
        public bool anomalousButSafe;

        [Header("The person (GDD 13.5)")]
        [Tooltip("What they actually are. Shaken callers read as liars and must still be let in.")]
        public VisitorTruth truth = VisitorTruth.Legitimate;

        [Tooltip("How much of the pressure available against them it takes before they give " +
                 "way, as a percentage. 45 is somebody barely holding on; 90 will not be moved " +
                 "by anything short of the entire night. A Deceptive caller drops their " +
                 "decisive tell here; a Shaken one finally says what is actually wrong.")]
        [Range(1, 100)] public int breakingPoint = 70;

        /// <summary>
        /// Everything the caretaker could do to this person, added up.
        ///
        /// Agitation is measured against this rather than against an absolute, which is what
        /// makes a breaking point mean the same thing on every caller and makes it impossible
        /// to author one that no amount of pressure can reach.
        /// </summary>
        public int PressureCapacity
        {
            get
            {
                int total = 0;
                for (int i = 0; i < DoorRead.PushingTactics.Length; i++)
                {
                    var tactic = DoorRead.PushingTactics[i];
                    var response = ResponseFor(tactic);
                    int delta = response != null ? response.agitationDelta : DoorRead.DefaultDelta(tactic);
                    if (delta > 0) total += delta;
                }
                return total;
            }
        }

        [Tooltip("What a Shaken caller is really hiding, said once they crack. Unused otherwise.")]
        public string hiddenReasonKey;

        [Tooltip("Everything that can be noticed about them, on any channel.")]
        public VisitorTell[] tells = new VisitorTell[0];

        [Tooltip("How they answer each kind of pressure. A tactic with no entry gets the archetype's default.")]
        public TacticResponse[] responses = new TacticResponse[0];

        [Tooltip("An earlier caller this person remembers being judged. Drives the callback " +
                 "lines on a later night; empty for a first appearance.")]
        public string remembersVisitorId;

        [Header("Outcome")]
        public ConsequenceDefinition[] onCorrect = new ConsequenceDefinition[0];
        public ConsequenceDefinition[] onWrong = new ConsequenceDefinition[0];
        [Tooltip("Holding costs time but keeps the investigation open (GDD 13.3).")]
        public ConsequenceDefinition[] onHold = new ConsequenceDefinition[0];

        /// <summary>The response for a tactic, or null when this caller has nothing scripted for it.</summary>
        public TacticResponse ResponseFor(PressureTactic tactic)
        {
            if (responses == null) return null;
            for (int i = 0; i < responses.Length; i++)
                if (responses[i] != null && responses[i].tactic == tactic) return responses[i];
            return null;
        }

        public VisitorTell FindTell(string tellId)
        {
            if (tells == null || string.IsNullOrEmpty(tellId)) return null;
            for (int i = 0; i < tells.Length; i++)
                if (tells[i] != null && tells[i].tellId == tellId) return tells[i];
            return null;
        }

        /// <summary>
        /// Tells that are worth something once found. A caller with none of these cannot be
        /// read at all, which is a content error rather than a difficulty setting.
        /// </summary>
        public int RealTellCount
        {
            get
            {
                if (tells == null) return 0;
                int n = 0;
                for (int i = 0; i < tells.Length; i++)
                    if (tells[i] != null && tells[i].weight != TellWeight.Noise) n++;
                return n;
            }
        }

        /// <summary>True when the informed answer is to keep this person outside entirely.</summary>
        public bool ShouldBeRefused { get { return correctAccess == VisitorAccessLevel.Reject; } }

        /// <summary>
        /// The levels this caller's panel offers on a given night: what the night has taught,
        /// narrowed by whatever the caller's own definition restricts.
        /// </summary>
        public VisitorAccessLevel[] OfferedAccess(int nightIndex)
        {
            var taught = VisitorAccess.TaughtBy(nightIndex);
            if (allowedAccess == null || allowedAccess.Length == 0) return taught;

            var list = new System.Collections.Generic.List<VisitorAccessLevel>();
            for (int i = 0; i < taught.Length; i++)
            {
                for (int k = 0; k < allowedAccess.Length; k++)
                    if (allowedAccess[k] == taught[i]) { list.Add(taught[i]); break; }
            }

            // Turning somebody away is never taken off the table.
            if (!list.Contains(VisitorAccessLevel.Reject)) list.Insert(0, VisitorAccessLevel.Reject);
            return list.ToArray();
        }

        public int ContradictionCount
        {
            get
            {
                if (checks == null) return 0;
                int n = 0;
                for (int i = 0; i < checks.Length; i++) if (checks[i] != null && checks[i].contradicts) n++;
                return n;
            }
        }
    }
}
