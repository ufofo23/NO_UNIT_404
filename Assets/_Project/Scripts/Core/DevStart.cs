using System.Collections.Generic;
using NO404.Cases;

namespace NO404.Core
{
    /// <summary>How the nights before the chosen one are assumed to have gone.</summary>
    public enum DevPriorNights
    {
        /// <summary>Nothing happened: every flag, choice and stat is at its new-game value.</summary>
        Untouched = 0,
        /// <summary>Each earlier main was closed on its first Correct decision.</summary>
        HandledWell = 1,
        /// <summary>Each earlier main was closed on its first Wrong decision.</summary>
        HandledBadly = 2
    }

    /// <summary>What the developer start screen asks GameLoop for.</summary>
    public sealed class DevStartOptions
    {
        public int Night = 1;
        public bool MainOnly;
        /// <summary>Off by default: a test sitting must not write over somebody's campaign.</summary>
        public bool AllowSaves;
        public DevPriorNights PriorNights = DevPriorNights.Untouched;
    }

    /// <summary>
    /// The developer start: a fresh game opened on a chosen night (GDD 20.21).
    ///
    /// The console could already do this - New Game, then dev.mainonly, then night.start N -
    /// but a night opened that way arrives with none of the earlier nights behind it, and a
    /// main whose branches read N1_404_BILL_PRESERVED cannot be tested on a world where
    /// night 1 never happened. This is the part that puts those nights back.
    ///
    /// It replays what the earlier mains were authored to leave behind rather than keeping a
    /// list of flags per night here. A list would be a second copy of the campaign's
    /// consequences, and it would be wrong the first time somebody edited a decision.
    /// </summary>
    public static class DevStart
    {
        /// <summary>
        /// Leaves the world as it would stand after the mains of every night before
        /// <paramref name="night"/>, and returns how many of them were applied.
        ///
        /// Only what a later night can read back is carried: flags, choices, stats and debts.
        /// HP and SAN are not, because a night's toll on the caretaker depends on how it was
        /// played and not on which box was ticked at the end of it; evidence, achievements and
        /// anything deferred to "next night" are left out for the same reason they are not
        /// world state.
        /// </summary>
        public static int ApplyPriorNights(int night, DevPriorNights prior)
        {
            if (!DevConsole.Enabled || prior == DevPriorNights.Untouched) return 0;

            var wanted = prior == DevPriorNights.HandledWell ? DecisionQuality.Correct
                                                             : DecisionQuality.Wrong;

            var mains = new List<CaseDefinition>();
            foreach (var definition in ServiceHub.Content.Cases)
            {
                if (definition == null || !definition.isFixedMain) continue;
                if (definition.nightIndex < 1 || definition.nightIndex >= night) continue;
                mains.Add(definition);
            }

            // In the order they would have been played: a later night's write wins.
            mains.Sort((a, b) =>
            {
                int byNight = a.nightIndex.CompareTo(b.nightIndex);
                return byNight != 0 ? byNight : string.CompareOrdinal(a.caseId, b.caseId);
            });

            int applied = 0;
            for (int i = 0; i < mains.Count; i++)
            {
                var decision = FirstOfQuality(mains[i], wanted);
                if (decision == null) continue;

                ServiceHub.Cases.ApplyConsequences(WorldStateOnly(decision.consequences));
                Log.Info("Dev", mains[i].caseId + " assumed closed as " + decision.decisionId);
                applied++;
            }

            return applied;
        }

        static DecisionDefinition FirstOfQuality(CaseDefinition definition, DecisionQuality quality)
        {
            var decisions = definition.decisions;
            if (decisions == null) return null;

            for (int i = 0; i < decisions.Length; i++)
                if (decisions[i] != null && decisions[i].quality == quality) return decisions[i];

            return null;
        }

        static ConsequenceDefinition[] WorldStateOnly(ConsequenceDefinition[] consequences)
        {
            var kept = new List<ConsequenceDefinition>();
            if (consequences == null) return kept.ToArray();

            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c == null || c.nextNight) continue;

                if (c.type != ConsequenceType.SetFlag && c.type != ConsequenceType.SetChoice &&
                    c.type != ConsequenceType.StatDelta)
                    continue;

                // Without its conditions. "Handled well" means the night went as well as that
                // decision allows, and the things a condition would look for - what was in the
                // tray, who was let through the door - are exactly what this start skipped.
                kept.Add(new ConsequenceDefinition
                {
                    type = c.type, targetId = c.targetId, amount = c.amount, boolValue = c.boolValue,
                    stringValue = c.stringValue, reasonKey = c.reasonKey
                });
            }

            return kept.ToArray();
        }
    }
}
