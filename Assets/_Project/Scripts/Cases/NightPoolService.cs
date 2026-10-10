using System.Collections.Generic;
using NO404.Core;

namespace NO404.Cases
{
    /// <summary>
    /// Which of tonight's fifteen quests actually happen (v5.0 4).
    ///
    /// Every night has a pool of fifteen. One of them is the night's spine and always runs;
    /// four or five of the remaining fourteen are drawn. That is the whole of what makes a
    /// second playthrough a different shift while the story stays the story - v5.0 0.2 and 0.3
    /// are explicit that 404, the 2009 fire and the Yoon family are never shuffled, and that
    /// what varies is which ordinary jobs and which after-effects land on top of them.
    ///
    /// Two properties matter more than the shape of the draw.
    ///
    /// <b>It is deterministic.</b> The campaign carries one seed and every night derives its
    /// own from that seed and the night index, so the same save always produces the same
    /// shift. v5.0 4.4 step 11 forbids re-drawing on load and it is not a performance note:
    /// this is an investigation game, and a contradiction the player is halfway through
    /// pinning down is worthless if reloading can replace it with a different one.
    ///
    /// <b>It is composed, not sampled.</b> v5.0 4.3 gives each night a floor for how much
    /// ordinary caretaking it must contain. Night 1 owes the player two genuine jobs, because
    /// the anomalies of night 4 only land on somebody who has learned what a normal night
    /// looks like. A weighted shuffle would satisfy that on average and fail it on the run
    /// that matters, so the quota is filled first and the remaining slots are drawn afterwards.
    /// </summary>
    public sealed class NightPoolService
    {
        /// <summary>v5.1 4.1. Every night is authored to this size.</summary>
        public const int PoolPerNight = 15;
        /// <summary>v5.1 4.1: main and story subquest are placed; thirteen compete.</summary>
        public const int RandomCandidates = 13;
        public const int RandomFloor = 3;
        public const int RandomCeiling = 4;

        // ---- the optional fourth slot (v5.1 4.4 step 10) --------------------
        public const int FourthSlotMinHp = 45;
        public const int FourthSlotMinSan = 40;
        public const float FourthSlotMinutesBudget = 48f;
        public const int FourthSlotPercent = 60;

        /// <summary>How much a quest is discounted for repeating last night (v5.0 4.4 step 5).</summary>
        public const int AlreadySeenPenaltyPercent = 60;

        /// <summary>What a night owes the player before anything is drawn for fun (v5.0 4.3).</summary>
        public struct Quota
        {
            public int Normal;
            public int Mixed;
            public int Heavy;              // ANOMALY or CONSEQUENCE together
            /// <summary>What the fourth slot looks for first. Main means "any".</summary>
            public QuestType FourthPreference;
            /// <summary>What it looks for second, before taking anything at all.</summary>
            public QuestType FourthFallback;

            public Quota(int normal, int mixed, int heavy, QuestType fourth, QuestType fallback = QuestType.Main)
            {
                Normal = normal; Mixed = mixed; Heavy = heavy;
                FourthPreference = fourth; FourthFallback = fallback;
            }
        }

        /// <summary>
        /// v5.1 4.3, exactly as tabled. Night 1 still owes one ordinary job and night 5 owes
        /// none - by then the night is made of what the player left behind.
        /// </summary>
        public static Quota QuotaFor(int nightIndex)
        {
            switch (nightIndex)
            {
                case 1:  return new Quota(1, 1, 1, QuestType.Normal, QuestType.Mixed);
                case 2:  return new Quota(1, 1, 1, QuestType.Main);      // Main here means "any"
                case 3:  return new Quota(0, 1, 2, QuestType.Anomaly);
                case 4:  return new Quota(0, 1, 2, QuestType.Anomaly);
                case 5:  return new Quota(0, 0, 3, QuestType.Consequence);
                default: return new Quota(0, 0, 3, QuestType.FinalPressure);
            }
        }

        /// <summary>
        /// Whether a night has its full v5.1 pool authored, rather than just its main.
        ///
        /// The old night-response events (M01-M18) were written for nights with nothing else
        /// in them. A night with its fifteen quests is already five or six jobs long (v5.1
        /// 4.1), and running the old chain on top of it would double the shift.
        /// </summary>
        public static bool HasAuthoredPool(int nightIndex)
        {
            var content = ServiceHub.Content;
            if (content == null) return false;

            int count = 0;
            foreach (var def in content.Cases)
                if (def != null && def.nightIndex == nightIndex) count++;

            return count >= PoolPerNight;
        }

        /// <summary>What the draw is allowed to know about the caretaker (v5.0 4.4 step 9).</summary>
        public struct Conditions
        {
            public int Hp;
            public int San;
        }

        /// <summary>The campaign's seed. Every night's draw is derived from it (v5.0 4.4 step 10).</summary>
        public int CampaignSeed { get; private set; }

        readonly List<string> _selected = new List<string>(6);
        readonly List<string> _previousNight = new List<string>(6);

        public IReadOnlyList<string> Selected { get { return _selected; } }

        /// <summary>
        /// Starts a campaign's worth of draws.
        ///
        /// A zero seed means this campaign has never drawn anything - a new game, or a save
        /// written before the pool existed - so one is minted. Anything else is replayed.
        /// </summary>
        public void BeginCampaign(int seed)
        {
            CampaignSeed = seed != 0 ? seed : NewSeed();
            _selected.Clear();
            _previousNight.Clear();
            Log.Info("Pool", "campaign seed " + CampaignSeed);
        }

        static int NewSeed()
        {
            int seed = System.Environment.TickCount ^ (int)System.DateTime.Now.Ticks;
            return seed == 0 ? 1 : seed;
        }

        /// <summary>
        /// Replays a night that has already been drawn (v5.0 4.4 step 11).
        ///
        /// Called on load. Nothing is re-evaluated: the ids in the save are the night, even if
        /// the world state has since moved somewhere that would have made one of them
        /// ineligible. A save that re-derives its own contents is a save that can disagree
        /// with the screenshot the player took of it.
        /// </summary>
        public void Restore(IEnumerable<string> selected, IEnumerable<string> previousNight)
        {
            _selected.Clear();
            if (selected != null) foreach (var id in selected) _selected.Add(id);

            _previousNight.Clear();
            if (previousNight != null) foreach (var id in previousNight) _previousNight.Add(id);
        }

        public IReadOnlyList<string> PreviousNight { get { return _previousNight; } }

        // -----------------------------------------------------------------
        // the draw
        // -----------------------------------------------------------------

        /// <summary>
        /// Chooses tonight's shift out of the night's pool, following v5.1 4.4 step by step.
        ///
        /// The main quest is placed rather than drawn, so a pool whose fourteen candidates are
        /// all ineligible still produces a playable night. That is deliberate: the campaign has
        /// to be finishable from any world state, and 4.4 step 1 fixes the main before
        /// anything else is even evaluated.
        /// </summary>
        public IReadOnlyList<string> SelectForNight(int nightIndex,
                                                    IReadOnlyList<CaseDefinition> pool,
                                                    Conditions conditions)
        {
            // Last night's shift becomes this night's repeat penalty, and then this night
            // overwrites it. Rolled here rather than handed in, because the two lists are this
            // service's own memory and a caller passing one of them back in would have been
            // relying on the order these two statements happen to be written in.
            _previousNight.Clear();
            _previousNight.AddRange(_selected);

            _selected.Clear();
            if (pool == null || pool.Count == 0) return _selected;

            // Derived rather than stored per night, so night 4 of a campaign always draws the
            // same way whether it is reached in one sitting or six.
            var random = new System.Random(unchecked(CampaignSeed * 397 + nightIndex * 7919));

            var candidates = new List<CaseDefinition>();
            CaseDefinition main = null;
            CaseDefinition story = null;

            for (int i = 0; i < pool.Count; i++)
            {
                var def = pool[i];
                if (def == null) continue;

                if (def.isFixedMain) { if (main == null) main = def; continue; }
                if (def.isFixedStory) { if (story == null) story = def; continue; }
                if (IsEligible(def)) candidates.Add(def);
            }

            // Step 1. The spine goes in first and is never competed for.
            if (main != null) _selected.Add(main.caseId);

            var takenMutex = new HashSet<string>(System.StringComparer.Ordinal);
            if (main != null && !string.IsNullOrEmpty(main.mutexGroup)) takenMutex.Add(main.mutexGroup);

            // dev.mainonly stops here, with the spine placed and nothing drawn around it.
            //
            // It reads as the pool's business rather than the caller's because this is the one
            // place that knows the difference between the night's spine and the fourteen
            // things it is hidden inside - a caller filtering the result afterwards would have
            // to re-derive isFixedMain to do it. Nothing else about the draw changes: the seed
            // is untouched, so switching back off deals the same night it would have dealt.
            if (MainOnlyMode.Active)
            {
                Log.Info("Pool", "night " + nightIndex + " is main-only (dev.mainonly)");
                return _selected;
            }

            // Step 2. The story subquest is placed regardless of seed: what it decides is
            // part of the ending, and an ending cannot hang on a coin toss.
            if (story != null)
            {
                _selected.Add(story.caseId);
                if (!string.IsNullOrEmpty(story.mutexGroup)) takenMutex.Add(story.mutexGroup);
            }

            int placed = _selected.Count;
            var quota = QuotaFor(nightIndex);
            int target = RandomFloor;

            // Steps 4 and 6. What the night owes comes before what it fancies, and an
            // after-effect of an earlier choice outranks an ordinary draw within each band.
            TakeQuota(candidates, takenMutex, random, quota.Normal, QuestType.Normal);
            TakeQuota(candidates, takenMutex, random, quota.Mixed, QuestType.Mixed);
            TakeHeavyQuota(candidates, takenMutex, random, quota.Heavy);

            // Step 9. Whatever the quota did not already cover, up to three.
            while (_selected.Count - placed < target)
            {
                var pick = Draw(candidates, takenMutex, random, QuestType.Main /* any */);
                if (pick == null) break;
                Accept(pick, candidates, takenMutex);
            }

            // Step 10. A fourth only for somebody who can still carry one.
            if (_selected.Count - placed >= target && WantsFourth(conditions, random))
            {
                var pick = Draw(candidates, takenMutex, random, quota.FourthPreference)
                        ?? Draw(candidates, takenMutex, random, quota.FourthFallback)
                        ?? Draw(candidates, takenMutex, random, QuestType.Main /* any */);
                if (pick != null) Accept(pick, candidates, takenMutex);
            }

            Log.Info("Pool", "night " + nightIndex + " drew " + _selected.Count + ": " +
                             string.Join(", ", new List<string>(_selected).ToArray()));
            return _selected;
        }

        /// <summary>
        /// Whether the night has room for a fourth random event.
        ///
        /// The three gates are the whole of v5.1 4.4 step 10 and they are all about the person
        /// rather than the pool: somebody at 30 HP does not need one more errand, they need
        /// the shift to end so they can start the next one able to walk.
        /// </summary>
        bool WantsFourth(Conditions conditions, System.Random random)
        {
            if (conditions.Hp < FourthSlotMinHp || conditions.San < FourthSlotMinSan) return false;
            if (EstimatedMinutes() > FourthSlotMinutesBudget) return false;

            return random.Next(100) < FourthSlotPercent;
        }

        float EstimatedMinutes()
        {
            float total = 0f;
            for (int i = 0; i < _selected.Count; i++)
            {
                var def = ServiceHub.Content != null ? ServiceHub.Content.FindCase(_selected[i]) : null;
                total += def != null ? def.estimatedMinutes : 6f;
            }
            return total;
        }

        // -----------------------------------------------------------------

        void TakeQuota(List<CaseDefinition> candidates, HashSet<string> takenMutex,
                       System.Random random, int count, QuestType type)
        {
            for (int i = 0; i < count; i++)
            {
                var pick = Draw(candidates, takenMutex, random, type);
                if (pick == null) return;      // the pool cannot pay this quota tonight
                Accept(pick, candidates, takenMutex);
            }
        }

        /// <summary>
        /// The ANOMALY/CONSEQUENCE half of the quota, which v5.0 4.3 counts together.
        ///
        /// Consequence first, because 4.4 step 6 ranks a bill the player ran up above an
        /// event the night invented - the point of the whole state model is that choices come
        /// back, and a night that quietly skipped them in favour of a fresh anomaly would be
        /// hiding exactly the thing it exists to show.
        /// </summary>
        void TakeHeavyQuota(List<CaseDefinition> candidates, HashSet<string> takenMutex,
                            System.Random random, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var pick = Draw(candidates, takenMutex, random, QuestType.Consequence)
                        ?? Draw(candidates, takenMutex, random, QuestType.Anomaly)
                        ?? Draw(candidates, takenMutex, random, QuestType.FinalPressure);

                if (pick == null) return;
                Accept(pick, candidates, takenMutex);
            }
        }

        /// <summary>
        /// One weighted draw. <see cref="QuestType.Main"/> is passed to mean "any type",
        /// since a main is never in the candidate list to begin with.
        /// </summary>
        CaseDefinition Draw(List<CaseDefinition> candidates, HashSet<string> takenMutex,
                            System.Random random, QuestType type)
        {
            int total = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!Fits(candidates[i], takenMutex, type)) continue;
                total += WeightOf(candidates[i]);
            }

            if (total <= 0) return null;

            int roll = random.Next(total);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!Fits(candidates[i], takenMutex, type)) continue;

                roll -= WeightOf(candidates[i]);
                if (roll < 0) return candidates[i];
            }

            return null;
        }

        bool Fits(CaseDefinition def, HashSet<string> takenMutex, QuestType type)
        {
            if (def == null || WeightOf(def) <= 0) return false;
            if (type != QuestType.Main && def.questType != type) return false;

            // Step 7. Two quests from the same group would each be explaining a building the
            // other one has already changed.
            return string.IsNullOrEmpty(def.mutexGroup) || !takenMutex.Contains(def.mutexGroup);
        }

        /// <summary>
        /// A quest's chance, after the penalty for having just been seen (v5.0 4.4 step 5).
        ///
        /// Never reduced to zero. Repeating a family the night after is worse than repeating
        /// nothing at all, but a pool with only one CONSEQUENCE left in it still has to be
        /// able to pay the quota that demands one.
        /// </summary>
        int WeightOf(CaseDefinition def)
        {
            int weight = def.baseWeight;
            if (weight <= 0) return 0;
            if (def.nightIndex == 2 && def.family == AnomalyFamily.Record &&
                ServiceHub.State != null && ServiceHub.State.GetFlag("N1_404_BILL_DISCARDED"))
                weight += 50;
            weight += SubquestRules.WeightBonus(def);

            if (SeenLastNight(def)) weight = weight * (100 - AlreadySeenPenaltyPercent) / 100;

            return weight < 1 ? 1 : weight;
        }

        bool SeenLastNight(CaseDefinition def)
        {
            for (int i = 0; i < _previousNight.Count; i++)
            {
                if (_previousNight[i] == def.caseId) return true;

                var previous = ServiceHub.Content != null ? ServiceHub.Content.FindCase(_previousNight[i]) : null;
                if (previous == null) continue;

                if (previous.family != AnomalyFamily.None && previous.family == def.family) return true;
            }

            return false;
        }

        void Accept(CaseDefinition def, List<CaseDefinition> candidates, HashSet<string> takenMutex)
        {
            _selected.Add(def.caseId);
            candidates.Remove(def);
            if (!string.IsNullOrEmpty(def.mutexGroup)) takenMutex.Add(def.mutexGroup);
        }

        /// <summary>
        /// Step 2. Whether a candidate's own conditions allow it tonight.
        ///
        /// ASSUMPTION: v5.0 4.4 step 3 lets an ineligible quest be converted to a fallback
        /// variant instead of being dropped. There is no variant system yet, so an ineligible
        /// quest is simply not drawn. The step is left as the only piece of 4.4 not
        /// implemented rather than approximated with something that is not a variant.
        /// </summary>
        static bool IsEligible(CaseDefinition def)
        {
            string reason;
            if (!ConditionEvaluator.EvaluateAll(def.startConditions, out reason)) return false;

            return def.blockedBy == null || def.blockedBy.Length == 0
                || !ConditionEvaluator.EvaluateAny(def.blockedBy);
        }

        public void Reset()
        {
            CampaignSeed = 0;
            _selected.Clear();
            _previousNight.Clear();
        }
    }
}
