using System;
using System.Collections.Generic;
using NO404.Cases;
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
        /// <summary>
        /// v5.1 8.1, in full:
        ///
        ///   HP > 0 AND FinalMainQuestCompleted AND ExternalEvidenceSent -> SINGLE_ENDING_404_EXISTED
        ///
        /// HP and SAN do not grade anything. A caretaker on one point of SAN who got the
        /// evidence out reaches the same ending as one who is untouched; what being worn
        /// down costs is how hard the last night was to get through (v5.1 8.1, 0.8). HP 0
        /// and SAN 0 are deaths and breakdowns, handled where they happen, not here.
        ///
        /// Anything short of that - the night ran out, nothing was sent - is not a worse
        /// ending. It is no ending: the campaign did not reach its last scene.
        /// </summary>
        public EndingDefinition Evaluate()
        {
            var vitals = ServiceHub.Vitals;
            var state = ServiceHub.State;

            if (vitals != null && vitals.Hp <= 0)
            {
                Log.Info("Ending", "no ending: HP 0");
                return Find(EndingIds.NoEnding);
            }

            var finalMain = ServiceHub.Cases != null ? ServiceHub.Cases.Find(FinalMainId) : null;
            bool finished = finalMain != null && finalMain.State.IsResolved();
            bool sent = state != null && state.GetFlag(FlagIds.ExternalEvidenceSent);

            if (!finished || !sent)
            {
                Log.Info("Ending", "no ending: final main " + (finished ? "done" : "open") +
                                   ", evidence " + (sent ? "sent" : "not sent"));
                return Find(EndingIds.NoEnding);
            }

            return Find(EndingIds.SingleEnding404Existed);
        }

        /// <summary>The main quest whose completion v5.1 8.1 calls FinalMainQuestCompleted.</summary>
        public const string FinalMainId = "N6-M01";

        EndingDefinition Find(string endingId)
        {
            for (int i = 0; i < _ordered.Count; i++)
                if (_ordered[i].endingId == endingId) return _ordered[i];

            Log.Error("Ending", "content has no ending " + endingId);
            return null;
        }

        /// <summary>
        /// CIN-END, as the ordered list of shots it is made of (v5.1 8.2, 8.3, 8.5.5).
        ///
        /// The frame never changes: the exit from 404, the evidence arriving outside, the
        /// dawn, the number written back onto the plan, the title. Between them sit the
        /// conditional shots, and every one of them has two forms - neither of which is the
        /// bad one. v5.1 8.3 is explicit that these are not good and bad endings, and the
        /// scenario book goes further: where a choice went the other way the shot is often
        /// the stronger of the two (her name written by hand rather than printed).
        ///
        /// Only flags MAIN and FIXED_STORY quests may set are read here (v5.1 8.4).
        /// </summary>
        public List<string> EndingShots()
        {
            var keys = new List<string>();
            var state = ServiceHub.State;
            if (state == null) return keys;

            // END_00 / END_01: out of 404, and the evidence is somewhere it cannot be deleted.
            keys.Add("ending.shot.exit_404");
            keys.Add(state.GetFlag(FlagIds.SubmittedToMedia) ? "ending.shot.sent.media"
                   : state.GetFlag(FlagIds.SubmittedToResidents) ? "ending.shot.sent.residents"
                   : "ending.shot.sent.police");

            // END_02: Seon-ja.
            if (state.GetFlag(FlagIds.EndSunjaTestimonyFull)) keys.Add("ending.shot.sunja.full");
            else if (state.GetFlag(FlagIds.EndSunjaTrusted)) keys.Add("ending.shot.sunja.trusted");
            else keys.Add("ending.shot.sunja.empty_chair");

            // The 2009 call has no "without" shot: v5.1 8.3 leaves it out.
            if (state.GetFlag(FlagIds.End2009CallPreserved)) keys.Add("ending.shot.call_2009");

            keys.Add(state.GetFlag(FlagIds.EndChildItemsPreserved) ? "ending.shot.child_items.kept"
                                                                   : "ending.shot.child_items.photo");

            // END_03: Park Dong-sik. Never alive (v5.1 0.13): what changes is how much of what
            // he did leaves the building with the evidence.
            keys.Add(state.GetFlag(FlagIds.EndDongsikRemainsRecovered) ? "ending.shot.dongsik.recovered"
                                                                       : "ending.shot.dongsik.located");
            keys.Add(state.GetFlag(FlagIds.EndDongsikBroadcastVerified) ? "ending.shot.broadcast.original"
                                                                        : "ending.shot.broadcast.other_record");

            keys.Add(state.GetFlag(FlagIds.EndHarinDoorOpened) ? "ending.shot.harin_door.memory"
                                                               : "ending.shot.harin_door.slipper");

            // END_05: the ledger.
            keys.Add(state.GetFlag(FlagIds.EndLedgerSecured) ? "ending.shot.ledger.opened"
                                                             : "ending.shot.ledger.digital");

            // "Days later" (scenario book, 제6기): what the investigation corrects.
            keys.Add("ending.shot.days_later");

            // END_04: her name. Printed, or written by the sister who remembered it.
            keys.Add(state.GetFlag(FlagIds.EndHarinRecordFirstRegistered) ? "ending.shot.harin_record.printed"
                                                                          : "ending.shot.harin_record.handwritten");

            // END_06 / END_07: dawn, and the number back on the plan.
            keys.Add("ending.shot.dawn");
            keys.Add("ending.shot.404_restored");
            return keys;
        }

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

            // Only real endings count towards the collection - there is one (v5.1 8.1) - and
            // a save from before v5.1 may still hold the four old letters, which must not
            // count either.
            int collected = 0;
            for (int i = 0; i < EndingIds.All.Length; i++)
                if (settings.unlockedEndings.Contains(EndingIds.All[i])) collected++;

            if (collected >= EndingIds.All.Length)
                ServiceHub.Steam.Unlock(AchievementIds.Completionist);
        }

        public bool IsUnlocked(string endingId)
        {
            return ServiceHub.Settings.Current.unlockedEndings.Contains(endingId);
        }

        public int UnlockedCount
        {
            get
            {
                int count = 0;
                var unlocked = ServiceHub.Settings.Current.unlockedEndings;
                for (int i = 0; i < EndingIds.All.Length; i++)
                    if (unlocked.Contains(EndingIds.All[i])) count++;
                return count;
            }
        }

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
