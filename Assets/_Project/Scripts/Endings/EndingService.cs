using System;
using System.Collections.Generic;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Endings
{
    /// <summary>
    /// Decides which ending the playthrough earned (GDD 10) and keeps the gallery of the
    /// ones the player has seen.
    ///
    /// Endings are ranked by evaluationOrder and the first match wins, so the secret and
    /// failure endings can pre-empt the "good" ones exactly the way GDD 10 describes.
    /// The last ending in the list has no conditions and acts as the guaranteed fallback -
    /// there is no state in which the game can refuse to end.
    /// </summary>
    public sealed class EndingService
    {
        readonly ContentDatabase _content;
        readonly List<EndingDefinition> _ordered = new List<EndingDefinition>();

        public EndingDefinition Reached { get; private set; }
        public event Action<EndingDefinition> OnEndingReached;

        public EndingService(ContentDatabase content)
        {
            _content = content;
            Rebuild();
        }

        void Rebuild()
        {
            _ordered.Clear();
            foreach (var ending in _content.Endings)
                if (ending != null) _ordered.Add(ending);

            _ordered.Sort((a, b) => a.evaluationOrder.CompareTo(b.evaluationOrder));
        }

        public IReadOnlyList<EndingDefinition> AllOrdered { get { return _ordered; } }

        /// <summary>
        /// Which ending the campaign earned (v5.0 8.1, 8.2).
        ///
        /// Two steps, in this order and never folded together. First the gate: below thirty
        /// HP or thirty SAN there is no ending at all, only a failure result - v5.0 8.1 is
        /// explicit that this is not a fifth grade but the campaign having been pushed to its
        /// last scene in a state that cannot carry it. Then the grade, from a score that
        /// weights the body slightly above the nerve.
        ///
        /// The floors below the score are not decoration. Without them a caretaker at 100 HP
        /// and 44 SAN scores 74.8 and would read as a B - somebody physically untouched who
        /// can no longer tell what they saw. v5.0 8.2's extra restrictions exist to stop
        /// exactly that trade, so they are applied after the score rather than folded into it.
        /// </summary>
        public EndingDefinition Evaluate()
        {
            var vitals = ServiceHub.Vitals;
            if (vitals == null) return Find(EndingIds.D_CommunityCollapse);

            if (vitals.Gate != EndingGate.Eligible)
            {
                Log.Info("Ending", "no ending: " + vitals.Gate +
                                   " (HP " + vitals.Hp + ", SAN " + vitals.San + ")");
                return Find(EndingIds.NoEnding);
            }

            var grade = GradeFor(vitals.Hp, vitals.San);
            Log.Info("Ending", "grade " + grade + " from HP " + vitals.Hp + " / SAN " + vitals.San +
                               " (score " + vitals.ConditionScore.ToString("0.0") + ")");

            return Find(grade);
        }

        /// <summary>
        /// v5.0 8.2, as written. Public and static so the acceptance tests can walk the
        /// thresholds without standing a whole campaign up first.
        /// </summary>
        public static string GradeFor(int hp, int san)
        {
            float score = hp * 0.55f + san * 0.45f;

            // The two restrictions v5.0 8.2 adds under the table.
            bool aAllowed = hp >= 55 && san >= 50;
            bool bAllowed = hp >= 40 && san >= 40;

            if (aAllowed && score >= 85f && hp >= 75 && san >= 75) return EndingIds.A_RecordedPeople;
            if (bAllowed && score >= 70f && hp >= 60 && san >= 55) return EndingIds.B_SafeSilence;
            if (score >= 55f && hp >= 45 && san >= 40) return EndingIds.C_Erased404;

            return EndingIds.D_CommunityCollapse;
        }

        EndingDefinition Find(string endingId)
        {
            for (int i = 0; i < _ordered.Count; i++)
                if (_ordered[i].endingId == endingId) return _ordered[i];

            Log.Error("Ending", "content has no ending " + endingId);
            return null;
        }

        /// <summary>
        /// What the campaign's choices changed about the ending it did not choose (v5.0 8.4).
        ///
        /// The grade is two numbers, which is what makes it legible. This is where six nights
        /// of decisions get their say: who is named in the epilogue, how much of the record
        /// survives to be verified, whether anybody corroborates it. Returned as keys rather
        /// than prose so the ending screen can lay them out and the string table can hold them.
        /// </summary>
        public List<string> EpilogueModifiers()
        {
            var keys = new List<string>();
            var state = ServiceHub.State;
            if (state == null) return keys;

            if (state.GetFlag(FlagIds.DongsikRescued)) keys.Add("ending.epilogue.dongsik_survived");
            if (state.GetFlag(FlagIds.HarinRecordPreserved)) keys.Add("ending.epilogue.harin_named");
            if (state.GetStat(StatIds.ArchiveIntegrity) >= 70) keys.Add("ending.epilogue.records_verified");
            if (state.GetStat(StatIds.CommunityTrust) >= 70) keys.Add("ending.epilogue.residents_testify");
            if (state.GetStat(StatIds.ChairmanAlert) >= 80) keys.Add("ending.epilogue.chairman_fights");
            if (state.GetStat(StatIds.BuildingSafety) < 40) keys.Add("ending.epilogue.residents_hurt");

            return keys;
        }

        /// <summary>Evaluates, records the result and fires the event that shows the screen.</summary>
        public EndingDefinition Resolve()
        {
            var ending = Evaluate();
            if (ending == null) return null;

            Reached = ending;

            // A failure result is not a collectible.
            if (ending.endingId != EndingIds.NoEnding) Unlock(ending.endingId);

            if (!string.IsNullOrEmpty(ending.achievementId)) ServiceHub.Steam.Unlock(ending.achievementId);

            // GDD 24.2's hardest tier, rewarded regardless of which ending it was reached on.
            if (DifficultyProfile.Current == Difficulty.NightSupervisor)
                ServiceHub.Steam.Unlock(AchievementIds.NightChief);

            ServiceHub.Analytics.Track(AnalyticsService.Events.EndingReached, ending.endingId);

            var cb = OnEndingReached;
            if (cb != null) cb(ending);

            return ending;
        }

        // ---- gallery -------------------------------------------------------

        /// <summary>
        /// Unlocked endings live in the settings file rather than a save slot: the gallery is
        /// meta-progression and must survive deleting a playthrough (GDD 6.5 / 16.15).
        /// </summary>
        public void Unlock(string endingId)
        {
            var settings = ServiceHub.Settings.Current;
            if (settings.unlockedEndings.Contains(endingId)) return;

            settings.unlockedEndings.Add(endingId);
            ServiceHub.Settings.Save();
            Log.Info("Ending", "gallery unlocked " + endingId);

            // Only the four grades count towards the collection. NoEnding is authored and
            // reachable but it is a failure result, and "seen them all" must not require
            // somebody to have collapsed once (v5.0 8.1).
            if (settings.unlockedEndings.Count >= EndingIds.All.Length)
                ServiceHub.Steam.Unlock(AchievementIds.Completionist);
        }

        public bool IsUnlocked(string endingId)
        {
            return ServiceHub.Settings.Current.unlockedEndings.Contains(endingId);
        }

        public int UnlockedCount { get { return ServiceHub.Settings.Current.unlockedEndings.Count; } }

        /// <summary>Aftermath of the most recently reached ending, used by the main menu.</summary>
        public EndingAftermath LastAftermath
        {
            get
            {
                var settings = ServiceHub.Settings.Current;
                if (string.IsNullOrEmpty(settings.lastEndingId)) return EndingAftermath.None;

                var ending = _content.FindEnding(settings.lastEndingId);
                return ending != null ? ending.aftermath : EndingAftermath.None;
            }
        }

        public void RecordLastEnding(string endingId)
        {
            ServiceHub.Settings.Current.lastEndingId = endingId;
            ServiceHub.Settings.Save();
        }

        public void Reset() { Reached = null; }
    }
}
