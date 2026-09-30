using System;
using UnityEngine;

namespace NO404.Evidence
{
    /// <summary>Evidence categories from GDD 14.1.</summary>
    public enum EvidenceType
    {
        Document = 0,
        Photo = 1,
        CctvSnapshot = 2,
        AudioRecording = 3,
        AccessLog = 4,
        MeterGraph = 5,
        PhysicalObject = 6,
        Testimony = 7
    }

    /// <summary>Link types the player picks when connecting two cards (GDD 16.13).</summary>
    public enum EvidenceRelation
    {
        SameTime = 0,
        SamePerson = 1,
        LocationContradiction = 2,
        Cause = 3,
        TestimonySupport = 4
    }

    public enum EvidenceSource
    {
        Unknown = 0,
        WorldPickup = 1,
        Cctv = 2,
        Database = 3,
        Dialogue = 4,
        Facility = 5,
        FailSafe = 6
    }

    [Serializable]
    public sealed class EvidenceRelationRule
    {
        public string otherEvidenceId;
        public EvidenceRelation relation;
        [Tooltip("A meaningful link. Wrong links are allowed and are not flagged immediately.")]
        public bool meaningful = true;
    }

    [CreateAssetMenu(menuName = "NO404/Evidence/Evidence Definition", fileName = "EV_")]
    public sealed class EvidenceDefinition : ScriptableObject
    {
        public string evidenceId;
        public string displayNameKey;
        public string descriptionKey;
        public EvidenceType type;
        [Tooltip("Placeholder art hook. The art pass replaces this with a real thumbnail.")]
        public Sprite thumbnail;
        public string[] tags = new string[0];
        public EvidenceRelationRule[] validRelations = new EvidenceRelationRule[0];
        [Tooltip("Case this evidence is grouped under on the board.")]
        public string ownerCaseId;
        [Tooltip("Marks evidence that must be preserved for the truth ending (GDD 7.2).")]
        public bool archiveCritical;
    }
}
