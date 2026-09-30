using System;
using UnityEngine;
using NO404.Cases;

namespace NO404.Anomalies
{
    /// <summary>
    /// What a tool does to the caretaker after it has given them something (spec 23).
    ///
    /// Only two of the five keep hold of the player once the transaction is done, and the two
    /// are not the same shape, so they are named rather than generalised: the locker wants a
    /// door shut in the next few seconds and gets worse until it is, while the toolbox wants
    /// its tool back within the hour and simply takes it if it does not come.
    /// </summary>
    public enum AnomalyToolHold
    {
        None = 0,
        /// <summary>A02: the locker door. Left open, something comes the other way.</summary>
        CloseDoor = 1,
        /// <summary>A04: the borrowed tool. Not returned in time and the weight goes with it.</summary>
        ReturnTool = 2
    }

    /// <summary>
    /// One line on a machine's menu.
    ///
    /// A tool is never free and never automatic: spec 23 opens by saying these are not cheats
    /// that solve a puzzle but a way to buy information or an object at a running cost. So
    /// every option carries its price in <see cref="onChosen"/> alongside whatever it gives,
    /// and the two are applied together or not at all.
    /// </summary>
    [Serializable]
    public sealed class AnomalyToolOptionDefinition
    {
        public string optionId;
        [Tooltip("Menu line. What the caretaker is choosing.")]
        public string labelKey;
        [Tooltip("What the machine prints, plays or says back. Empty when the option only navigates.")]
        public string resultKey;
        [Tooltip("Menu to open instead of finishing. Empty ends the transaction.")]
        public string nextMenuId;
        [Tooltip("When this option is on the menu at all. A machine only offers what the caretaker could ask for.")]
        public ConditionDefinition[] conditions = new ConditionDefinition[0];
        [Tooltip("The price and the gain, applied together the moment the option is taken.")]
        public ConsequenceDefinition[] onChosen = new ConsequenceDefinition[0];
        [Tooltip("Flag standing for the object that comes out, e.g. item.stethoscope.")]
        public string grantsFlagId;
        [Tooltip("Flag spent to take this option - the token the machine wants (spec 23 A05).")]
        public string consumesFlagId;
        [Tooltip("Start the tool's hold window (spec 23 A02 door, A04 return).")]
        public bool opensHold;
        [Tooltip("Release the tool's hold - A04's return slot. Ignored by tools with no hold.")]
        public bool releasesHold;
        [Tooltip("Game seconds of movement pull toward the parapet afterwards (spec 23 A05).")]
        public int driftGameSeconds;

        public static AnomalyToolOptionDefinition Option(string optionId, string labelKey,
                                                         string resultKey = null)
        {
            return new AnomalyToolOptionDefinition
            {
                optionId = optionId, labelKey = labelKey, resultKey = resultKey
            };
        }

        public AnomalyToolOptionDefinition Opens(string menuId) { nextMenuId = menuId; return this; }

        public AnomalyToolOptionDefinition Costs(params ConsequenceDefinition[] consequences)
        {
            onChosen = consequences ?? new ConsequenceDefinition[0];
            return this;
        }

        public AnomalyToolOptionDefinition Grants(string flagId, bool opensHoldWindow = false)
        {
            grantsFlagId = flagId;
            opensHold = opensHoldWindow;
            return this;
        }

        public AnomalyToolOptionDefinition Releases() { releasesHold = true; return this; }

        public AnomalyToolOptionDefinition Spends(string flagId) { consumesFlagId = flagId; return this; }

        public AnomalyToolOptionDefinition Drifts(int gameSeconds)
        {
            driftGameSeconds = gameSeconds;
            return this;
        }

        public AnomalyToolOptionDefinition When(params ConditionDefinition[] required)
        {
            conditions = required ?? new ConditionDefinition[0];
            return this;
        }
    }

    /// <summary>One screen of a machine's menu. Tools with a two-step ask have several.</summary>
    [Serializable]
    public sealed class AnomalyToolMenuDefinition
    {
        public string menuId;
        public string titleKey;
        [Tooltip("Standing text above the options - the machine's own wording.")]
        public string bodyKey;
        public AnomalyToolOptionDefinition[] options = new AnomalyToolOptionDefinition[0];

        public AnomalyToolOptionDefinition Find(string optionId)
        {
            if (options == null || string.IsNullOrEmpty(optionId)) return null;
            for (int i = 0; i < options.Length; i++)
                if (options[i] != null && options[i].optionId == optionId) return options[i];
            return null;
        }
    }

    /// <summary>
    /// One of the five anomalous tools A01..A05 (v2.1 spec 23).
    ///
    /// The division of labour with <see cref="ManualEventDefinition"/> is the whole point of
    /// having a separate type. An M event is something that happens to the caretaker and is
    /// judged; a tool is something the caretaker chooses to use and is never judged at all.
    /// There is no correct answer here, no violation and no failsafe - only a price, paid
    /// every time, in a currency the ending is forbidden to read (spec 31 C14, spec 32).
    ///
    /// That last part is why the debts are MemoryDebt and ToolDebt rather than anything the
    /// ending algorithm consults: a caretaker who leaned on these machines to get through a
    /// night has a harder last night and exactly the same endings open to them.
    /// </summary>
    [CreateAssetMenu(menuName = "NO404/Manual/Anomaly Tool", fileName = "A")]
    public sealed class AnomalyToolDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string toolId;
        public string nameKey;
        [Tooltip("Prompt while the machine is still ordinary. Spec 23: it looks like a POS, a locker, a TV.")]
        public string dormantKey;

        [Header("Placement")]
        public string zoneId;
        [Tooltip("Floor id from FloorPlan. Risk a tool raises lands here.")]
        public string floorId;

        [Header("Availability (spec 21)")]
        [Tooltip("Earliest night the machine wakes up. 0 means it never does on its own.")]
        public int unlockNight;
        [Tooltip("All must hold on that night before the machine wakes.")]
        public ConditionDefinition[] unlockConditions = new ConditionDefinition[0];
        public string unlockNotifyKey;

        [Header("Menu")]
        public string rootMenuId;
        public AnomalyToolMenuDefinition[] menus = new AnomalyToolMenuDefinition[0];

        [Header("Hold (spec 23 A02 / A04)")]
        public AnomalyToolHold hold;
        [Tooltip("How long the caretaker has, in game seconds.")]
        public int holdGameSeconds;
        [Tooltip("The line that stays on screen while the hold is open.")]
        public string holdPromptKey;
        [Tooltip("Escalation, in order. Spec 23 A02 wants three, after the clean window has gone.")]
        public string[] holdWarnKeys = new string[0];
        /// <summary>
        /// Game seconds since the hold opened at which each warning lands, parallel to
        /// <see cref="holdWarnKeys"/>.
        ///
        /// Written out rather than derived from the window because spec 23 A02 gives the
        /// three beats as six, eight and ten seconds against a five-second window - they are
        /// not spaced through it, they are what happens after it has already gone.
        /// </summary>
        [Tooltip("Game seconds since the hold opened for each warning, parallel to holdWarnKeys.")]
        public int[] holdWarnAtGameSeconds = new int[0];
        [Tooltip("What the hold running out costs. A02 charges nothing here - it charges on the way out.")]
        public ConsequenceDefinition[] onHoldExpired = new ConsequenceDefinition[0];
        [Tooltip("What releasing the hold after it expired costs (spec 23 A02).")]
        public ConsequenceDefinition[] onLateRelease = new ConsequenceDefinition[0];
        [Tooltip("Prompt on the release interaction - shutting the door, putting the tool back.")]
        public string holdReleaseKey;
        [Tooltip("Notification when the hold is released in time.")]
        public string holdReleasedKey;
        [Tooltip("Notification when the hold is released after it expired.")]
        public string holdLateKey;

        /// <summary>
        /// A02: the price is paid when the window runs out, and the door is still open.
        ///
        /// Spec 23 is specific that hammering the interact key is not the way out - one
        /// correct close interaction is - so the hold cannot simply end when the timer does.
        /// A04 is the opposite: when the hour is up the tool is gone and there is nothing
        /// left to hand back.
        /// </summary>
        [Tooltip("The hold continues after it expires until the caretaker releases it (A02).")]
        public bool holdSurvivesExpiry;

        public AnomalyToolMenuDefinition FindMenu(string menuId)
        {
            if (menus == null || string.IsNullOrEmpty(menuId)) return null;
            for (int i = 0; i < menus.Length; i++)
                if (menus[i] != null && menus[i].menuId == menuId) return menus[i];
            return null;
        }

        public AnomalyToolMenuDefinition RootMenu { get { return FindMenu(rootMenuId); } }

        public AnomalyToolOptionDefinition FindOption(string menuId, string optionId)
        {
            var menu = FindMenu(menuId);
            return menu == null ? null : menu.Find(optionId);
        }
    }
}
