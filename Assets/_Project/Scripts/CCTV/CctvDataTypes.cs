using System;
using UnityEngine;

namespace NO404.CCTV
{
    /// <summary>
    /// What the feed is actually showing. GDD 12.2 forbids labelling these states in the UI -
    /// the player has to notice a delayed or archival feed from its content, not from a badge.
    /// </summary>
    public enum FeedState
    {
        Live = 0,
        Delayed = 1,
        Archive = 2,
        SignalLost = 3,
        /// <summary>Real streaming/loading, drawn with a spinner rather than horror noise.</summary>
        Loading = 4
    }

    /// <summary>Anomaly families from GDD 12.3.</summary>
    public enum AnomalyCategory
    {
        ObjectChange = 0,
        PersonContradiction = 1,
        TimeEnvironment = 2,
        SpaceContradiction = 3,
        DirectThreat = 4
    }

    public static class AnomalyCatalogue
    {
        /// <summary>GDD 12.3 defines exactly 36 anomaly types.</summary>
        public const int TypeCount = 36;

        /// <summary>Localization key for a catalogue entry.</summary>
        public static string DescriptionKey(int typeNumber)
        {
            return "cctv.anomaly.type_" + typeNumber.ToString("00") + ".desc";
        }

        /// <summary>The family a catalogue number belongs to (GDD 12.3 groupings).</summary>
        public static AnomalyCategory CategoryOf(int typeNumber)
        {
            if (typeNumber <= 8) return AnomalyCategory.ObjectChange;
            if (typeNumber <= 16) return AnomalyCategory.PersonContradiction;
            if (typeNumber <= 24) return AnomalyCategory.TimeEnvironment;
            if (typeNumber <= 30) return AnomalyCategory.SpaceContradiction;
            return AnomalyCategory.DirectThreat;
        }
    }

    [Serializable]
    public sealed class CctvChannelDefinition
    {
        public string cameraId;        // "CAM-01"
        public string labelKey;        // "cctv.cam01.label"
        public string zoneId;
        public bool hasAudio;
        [Tooltip("Local position/rotation inside the zone, applied by the greybox builder.")]
        public Vector3 localPosition;
        public Vector3 localEuler;
        public float fieldOfView = 70f;
    }

    [Serializable]
    public sealed class AnomalyDefinition
    {
        [Tooltip("Unique instance id. The same type may be scheduled on several nights.")]
        public string anomalyId;       // "ANOMALY_04_N3"
        [Tooltip("The GDD 12.3 catalogue number, 1-36. Drives the description key.")]
        public int typeNumber;
        public string cameraId;
        public AnomalyCategory category;
        public string descriptionKey;
        [Tooltip("Core anomalies stay visible at least 4 seconds (GDD 12.4).")]
        public float durationSeconds = 6f;
        [Tooltip("Core anomalies can be re-checked through rewind or the log (GDD 12.4).")]
        public bool reviewable = true;
        [Tooltip("Evidence granted when the player snapshots this anomaly.")]
        public string evidenceId;
        [Tooltip("Objective completed when the player observes this anomaly.")]
        public string objectiveId;
        [Tooltip("Flag set when this anomaly fires - how an anomaly leaves something behind in the world.")]
        public string setsFlagId;
        public string caseId;
        public int nightIndex;
        [Tooltip("Earliest game second this anomaly may fire.")]
        public int windowBegin;
        public int windowEnd;
        [Tooltip("Feed state forced while the anomaly runs.")]
        public FeedState feedState = FeedState.Live;
    }

    /// <summary>Result of the player naming what they think they just saw on a channel.</summary>
    public enum ReportOutcome
    {
        /// <summary>Nothing was wrong on that channel. Crying wolf has a price.</summary>
        NothingThere = 0,
        /// <summary>Something was wrong, but not the family the player named.</summary>
        WrongCategory = 1,
        Correct = 2
    }

    /// <summary>
    /// Something on a channel that the player is entitled to report.
    ///
    /// Watching an anomaly used to be entirely passive - the picture changed and, unless the
    /// player happened to take a snapshot, nothing came of it. A sighting turns every anomaly
    /// and every contradictory resident crossing into a claim the player can stake and be
    /// right or wrong about, which is the verb the whole CCTV half of the game was missing.
    /// </summary>
    /// <summary>
    /// Where a sighting came from. GDD 15.4 charges the two very differently: a night
    /// schedules a handful of authored anomalies and up to fifty contradictory crossings, so
    /// pricing them the same makes the crossings the only thing the pressure ladder measures.
    /// </summary>
    public enum SightingOrigin
    {
        /// <summary>An authored anomaly from the GDD 12.3 catalogue.</summary>
        Anomaly = 0,
        /// <summary>Procedural resident traffic that does not match the records (GDD 12.6).</summary>
        Traffic = 1,
        /// <summary>A wrongly admitted visitor walking the building (GDD 15.4).</summary>
        Intruder = 2
    }

    public sealed class Sighting
    {
        public string CameraId;
        public AnomalyCategory Category;
        public SightingOrigin Origin = SightingOrigin.Anomaly;
        /// <summary>Last game second this may still be reported. Outlives the event itself.</summary>
        public int ExpiresAtGameSecond;
        /// <summary>Anomaly instance id, or a traffic key. Only used for logging.</summary>
        public string SourceId;
        /// <summary>Granted on a correct report, if any.</summary>
        public string EvidenceId;
        public int Resonance;
        public bool Reported;
    }

    /// <summary>One entry of the 60 second rewind ring buffer (GDD 12.2).</summary>
    public readonly struct FeedSample
    {
        public readonly int GameSecond;
        public readonly FeedState State;
        public readonly string AnomalyId;
        public readonly bool Motion;

        public FeedSample(int gameSecond, FeedState state, string anomalyId, bool motion)
        {
            GameSecond = gameSecond; State = state; AnomalyId = anomalyId; Motion = motion;
        }
    }

    /// <summary>
    /// Snapshot record. GDD 20.13 forbids persisting real PNGs, so a snapshot is just the
    /// camera id, the frame it points at and the anomaly that was on screen.
    /// </summary>
    [Serializable]
    public sealed class CctvSnapshot
    {
        public string snapshotId;
        public string cameraId;
        public int gameSecond;
        public string anomalyId;
        public string attachedCaseId;
    }
}
