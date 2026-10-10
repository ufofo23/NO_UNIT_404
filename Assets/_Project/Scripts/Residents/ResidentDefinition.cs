using System;
using UnityEngine;

namespace NO404.Residents
{
    public enum ResidentStatus
    {
        Resident = 0,
        Vacant = 1,
        MovedOut = 2,
        Staff = 3,
        /// <summary>Records that must not exist. Used only by the 404 sync (GDD 16.10).</summary>
        Unregistered = 4
    }

    public enum AccessCardStatus { Active = 0, Suspended = 1, Lost = 2, None = 3 }

    [Serializable]
    public sealed class VehicleRecord
    {
        public string plate;
        public string modelKey;
        public bool registered = true;
    }

    [Serializable]
    public sealed class VisitorRecord
    {
        public string visitorId;
        public string nameKey;
        public string purposeKey;
        [Tooltip("Pre-registered by the resident. One of the two independent checks in GDD 13.2.")]
        public bool preRegistered;
    }

    [Serializable]
    public sealed class ResidentNote
    {
        public string noteKey;
        public bool safetyCritical;
    }

    /// <summary>
    /// Something the caretaker can do to a record from the database screen (v5.1 13).
    ///
    /// Printing, exporting and deleting a row are things done to the row, at the row, and
    /// they are done once: the report filed afterwards is the log entry for which of them
    /// happened, not the place it is decided.
    /// </summary>
    [Serializable]
    public sealed class ResidentRecordAction
    {
        public string actionId;
        public string labelKey;
        [Tooltip("All must hold for the button to be on the screen at all.")]
        public Cases.ConditionDefinition[] availability = new Cases.ConditionDefinition[0];
        public Cases.ConsequenceDefinition[] consequences = new Cases.ConsequenceDefinition[0];
    }

    [CreateAssetMenu(menuName = "NO404/Residents/Resident", fileName = "RES_")]
    public sealed class ResidentDefinition : ScriptableObject
    {
        public string residentId;
        public string unitNumber;          // e.g. "803"
        public string nameKey;
        public string householdKey;
        public string phoneLast4;
        public ResidentStatus initialStatus = ResidentStatus.Resident;
        public AccessCardStatus cardStatus = AccessCardStatus.Active;
        public string cardId;
        public VehicleRecord[] vehicles = new VehicleRecord[0];
        public VisitorRecord[] recurringVisitors = new VisitorRecord[0];
        public ResidentNote[] notes = new ResidentNote[0];

        [Header("404 presentation (GDD 16.10)")]
        [Tooltip("Displayed as-is. For 404 this is 2009-11-07 which is the only abnormal field.")]
        public string lastSyncDate = "2026-11-02";
        [Tooltip("Hidden from search until the night-4 sync flag is set.")]
        public bool hiddenUntilSync;
        public string revealFlagId;
        [Tooltip("Evidence granted the first time this record is opened (e.g. the 404 row).")]
        public string evidenceOnView;

        [Tooltip("Shown in place of the name while the row is only a glimpse and has not been " +
                 "revealed. Empty = the name itself.")]
        public string glimpseNameKey;
        [Tooltip("Once this flag is set the row is gone from the database for good.")]
        public string hideFlagId;
        [Tooltip("What can be done to this row from the database screen.")]
        public ResidentRecordAction[] actions = new ResidentRecordAction[0];

        public ResidentRecordAction FindAction(string actionId)
        {
            if (actions == null) return null;
            for (int i = 0; i < actions.Length; i++)
                if (actions[i] != null && actions[i].actionId == actionId) return actions[i];
            return null;
        }
    }
}
