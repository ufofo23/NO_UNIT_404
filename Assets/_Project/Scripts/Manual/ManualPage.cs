using UnityEngine;

namespace NO404.Manual
{
    /// <summary>
    /// How a page reaches the player (spec 0.9.2).
    ///
    /// Every M event must have one of these. The rule the enum exists to enforce is spec
    /// 0.9.2's last line: an answer the game never hinted at is not a puzzle, so a page with
    /// no source is a content bug rather than a difficulty choice.
    /// </summary>
    public enum ManualUnlockSource
    {
        /// <summary>Already in the binder in the desk drawer at the start of the prologue.</summary>
        Preloaded = 0,
        /// <summary>A note Dongsik left, found shortly before the anomaly.</summary>
        DongsikNote = 1,
        /// <summary>A broadcast or radio spot that forecasts tomorrow's response.</summary>
        Broadcast = 2,
        /// <summary>Half the rule was learned from a different case on an earlier night.</summary>
        PriorCase = 3,
        /// <summary>Signage, numbers or a repeated motion visible at the site itself.</summary>
        FieldHint = 4
    }

    /// <summary>
    /// One page of 야간 특이상황 대응 지침 (spec 0.9).
    ///
    /// The four sections are the spec 0.9.3 layout and are kept as separate arrays rather
    /// than one block of prose, because the UI has to be able to show the prohibition on its
    /// own: 금지 is the part that gets someone hurt when it is skimmed.
    ///
    /// Spec 0.9.4 draws the line this data sits on. A page says what to do and never says why
    /// the anomaly is happening - the why is what CCTV, the databases and the site are for.
    /// </summary>
    [CreateAssetMenu(menuName = "NO404/Manual/Manual Page", fileName = "MANUAL_")]
    public sealed class ManualPage : ScriptableObject
    {
        [Header("Identity")]
        public string pageId;
        [Tooltip("The M or A event this page answers, e.g. M16.")]
        public string eventId;
        [Tooltip("Page heading, e.g. '표시되지 않는 층'.")]
        public string titleKey;

        [Header("Sections (spec 0.9.3)")]
        [Tooltip("관찰 - what the caretaker may see. Never why.")]
        public string[] observationKeys = new string[0];
        [Tooltip("금지 - what must not be done. The lethal ones are marked below.")]
        public string[] prohibitionKeys = new string[0];
        [Tooltip("조치 - the numbered procedure, in order.")]
        public string[] stepKeys = new string[0];
        [Tooltip("비고 - qualifications that make the procedure exact.")]
        public string[] noteKeys = new string[0];

        [Header("Availability")]
        public ManualUnlockSource source = ManualUnlockSource.Preloaded;
        [Tooltip("Night the page can first be obtained. 0 = the prologue.")]
        public int nightIndex;
        [Tooltip("Flag that unlocks the page, for sources that depend on an event happening.")]
        public string unlockFlagId;

        /// <summary>
        /// Spec 0.10.5: only a prohibition the manual marks as lethal may ever kill, and only
        /// on a repeat. A page with no lethal prohibition can cost the player a great deal and
        /// cannot cost them the run.
        /// </summary>
        [Tooltip("Indices into prohibitionKeys that the manual marks as lethal.")]
        public int[] lethalProhibitions = new int[0];

        public bool IsLethal(int prohibitionIndex)
        {
            for (int i = 0; i < lethalProhibitions.Length; i++)
                if (lethalProhibitions[i] == prohibitionIndex) return true;
            return false;
        }

        public bool HasLethalProhibition { get { return lethalProhibitions.Length > 0; } }
    }
}
