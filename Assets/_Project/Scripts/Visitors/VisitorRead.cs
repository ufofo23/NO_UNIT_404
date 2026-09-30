using System;
using UnityEngine;

namespace NO404.Visitors
{
    /// <summary>
    /// What the caller actually is, underneath what they say.
    ///
    /// The door used to be a two-way sort: the records either contradicted the caller or they
    /// did not, so "nervous" meant "guilty" and the whole job was arithmetic on a database.
    /// Nobody plays a lie detector that is never wrong.
    ///
    /// <see cref="Shaken"/> is the category that makes reading a person worth doing. They are
    /// telling the truth about why they are at the door and lying about something else - a
    /// debt, an affair, a diagnosis, the fact that they are frightened of this building - and
    /// every deception signal a <see cref="Deceptive"/> caller gives, they give too. The
    /// correct call is still to let them in. A caretaker who treats agitation as proof turns
    /// the neighbours against them (GDD 13.6).
    /// </summary>
    public enum VisitorTruth
    {
        /// <summary>Nothing to hide. Stays composed under pressure and gives nothing up.</summary>
        Legitimate = 0,
        /// <summary>The story is false. Pressure breaks it, if the caretaker applies the right kind.</summary>
        Deceptive = 1,
        /// <summary>Here for a legitimate reason and hiding a private one. Reads exactly like a liar.</summary>
        Shaken = 2
    }

    /// <summary>Where a behaviour can be observed. The channel decides what it costs to see it.</summary>
    public enum ReadChannel
    {
        /// <summary>Audible over the interphone. Free - the caretaker is already listening.</summary>
        Voice = 0,
        /// <summary>Visible on the caller's camera. Free, but the feed is 240p and grainy.</summary>
        Camera = 1,
        /// <summary>
        /// Only visible through the lobby glass, in person (GDD 13.7). The caretaker has to
        /// leave the desk and walk down, which is the one thing the office chair cannot buy.
        /// </summary>
        Glass = 2
    }

    /// <summary>What a behaviour is worth once it has been noticed.</summary>
    public enum TellWeight
    {
        /// <summary>Noise. True of nervous honest people and liars alike; proves nothing.</summary>
        Noise = 0,
        /// <summary>Real evidence that the story is false.</summary>
        Deception = 1,
        /// <summary>Real evidence that the fear is about something other than this door.</summary>
        Innocent = 2
    }

    /// <summary>
    /// A way of pushing on the person rather than on the records (GDD 13.5).
    ///
    /// Every one of these costs game time and moves the caller's agitation, and the two
    /// directions are not symmetric: pressure that cracks a liar also cracks somebody who is
    /// simply having the worst night of their life.
    /// </summary>
    public enum PressureTactic
    {
        None = 0,
        /// <summary>Ask the same question again. A rehearsed story repeats too exactly.</summary>
        AskAgain = 1,
        /// <summary>Read the record back at them. Needs the record to have been opened first.</summary>
        Confront = 2,
        /// <summary>Say nothing and let the line sit open. Guilt fills a silence; innocence waits.</summary>
        Silence = 3,
        /// <summary>Tell them to hold it up to the lens. Paperwork that does not exist cannot be shown.</summary>
        ShowMe = 4,
        /// <summary>Take the pressure off. Costs the least and is the only way to get a Shaken caller to explain.</summary>
        Reassure = 5
    }

    /// <summary>
    /// One observable behaviour. The panel never says what it means - a tell is a thing the
    /// caretaker saw, and deciding what it was worth is the job.
    /// </summary>
    [Serializable]
    public sealed class VisitorTell
    {
        public string tellId;
        [Tooltip("What the caretaker observes, in plain description. Never a verdict.")]
        public string labelKey;
        public ReadChannel channel = ReadChannel.Voice;
        public TellWeight weight = TellWeight.Noise;
        [Tooltip("None = on show from the first second. Otherwise it only surfaces once this tactic has been used.")]
        public PressureTactic unlockedBy = PressureTactic.None;
        [Tooltip("Only surfaces once the caller is at least this agitated. 0 = immediately.")]
        [Range(0, 100)] public int fromAgitation;
    }

    /// <summary>
    /// The pressure model's shared constants.
    ///
    /// Both the runtime and the content validator read the same table, because a validator
    /// that computes a caller's capacity differently from the service that spends it is a
    /// validator that certifies broken content.
    /// </summary>
    public static class DoorRead
    {
        /// <summary>The four tactics that raise agitation. Reassure is not one of them.</summary>
        public static readonly PressureTactic[] PushingTactics =
        {
            PressureTactic.AskAgain, PressureTactic.Confront,
            PressureTactic.Silence, PressureTactic.ShowMe
        };

        /// <summary>Every tactic, in the order the panel lays them out.</summary>
        public static readonly PressureTactic[] All =
        {
            PressureTactic.AskAgain, PressureTactic.Confront, PressureTactic.Silence,
            PressureTactic.ShowMe, PressureTactic.Reassure
        };

        /// <summary>What a tactic is worth against a caller who has nothing scripted for it.</summary>
        public static int DefaultDelta(PressureTactic tactic)
        {
            switch (tactic)
            {
                case PressureTactic.AskAgain: return 10;
                case PressureTactic.Confront: return 25;
                case PressureTactic.Silence:  return 18;
                case PressureTactic.ShowMe:   return 20;
                case PressureTactic.Reassure: return -20;
            }
            return 0;
        }

        /// <summary>Game seconds a tactic costs. Backing off is the cheap one.</summary>
        public static int CostOf(PressureTactic tactic)
        {
            switch (tactic)
            {
                case PressureTactic.AskAgain: return 45;
                case PressureTactic.Confront: return 90;
                case PressureTactic.Silence:  return 75;
                case PressureTactic.ShowMe:   return 60;
                case PressureTactic.Reassure: return 40;
            }
            return 0;
        }
    }

    /// <summary>How one caller answers one kind of pressure.</summary>
    [Serializable]
    public sealed class TacticResponse
    {
        public PressureTactic tactic = PressureTactic.AskAgain;
        [Tooltip("The line they give back.")]
        public string replyKey;
        [Tooltip("Change to agitation, before the caller's composure damps it.")]
        public int agitationDelta = 10;
        [Tooltip("Optional: the tell this answer puts on the board.")]
        public string revealsTellId;
        [Tooltip("Confront only: which record must already have been opened for this to be usable.")]
        public string requiresAppId;
    }
}
