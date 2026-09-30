using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NO404.Cases;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// v5.0 30.1 — the acceptance tests for the nightly draw.
    ///
    /// A random system is the one kind of code that cannot be checked by playing it. A shift
    /// that quietly skips its main quest, or draws six events instead of five, or shuffles
    /// itself when the player reloads, looks exactly like a shift that worked - and only looks
    /// wrong on the run nobody was watching. So the properties v5.0 4 promises are asserted
    /// over enough seeds that a violation cannot hide in the tail.
    /// </summary>
    public sealed class NightPoolTests
    {
        const int Seeds = 100;

        readonly List<CaseDefinition> _made = new List<CaseDefinition>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _made.Count; i++)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);

            _made.Clear();
        }

        CaseDefinition Quest(string id, int night, QuestType type, bool main = false,
                             AnomalyFamily family = AnomalyFamily.None, string mutex = null)
        {
            var def = ScriptableObject.CreateInstance<CaseDefinition>();
            def.caseId = id;
            def.nightIndex = night;
            def.questType = type;
            def.isFixedMain = main;
            def.family = family;
            def.mutexGroup = mutex;
            def.baseWeight = 100;
            def.estimatedMinutes = 6f;
            _made.Add(def);
            return def;
        }

        /// <summary>
        /// A night authored the way v5.0 4.1 asks: one main and fourteen candidates, with
        /// enough of each type that the quota is payable.
        /// </summary>
        List<CaseDefinition> BuildPool(int night)
        {
            var pool = new List<CaseDefinition>
            {
                Quest("N" + night + "-M01", night, QuestType.Main, main: true)
            };

            for (int i = 0; i < 5; i++) pool.Add(Quest("N" + night + "-NRM" + i, night, QuestType.Normal));
            for (int i = 0; i < 4; i++) pool.Add(Quest("N" + night + "-MIX" + i, night, QuestType.Mixed));
            for (int i = 0; i < 3; i++)
                pool.Add(Quest("N" + night + "-ANM" + i, night, QuestType.Anomaly,
                               family: AnomalyFamily.Echo));
            pool.Add(Quest("N" + night + "-CON0", night, QuestType.Consequence));
            pool.Add(Quest("N" + night + "-FIN0", night, QuestType.FinalPressure));

            Assert.AreEqual(NightPoolService.PoolPerNight, pool.Count, "a night is authored to fifteen");
            return pool;
        }

        static NightPoolService.Conditions Healthy
        {
            get { return new NightPoolService.Conditions { Hp = 100, San = 100 }; }
        }

        [Test]
        public void TheMainQuestIsAlwaysDrawn()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var pool = new NightPoolService();
                pool.BeginCampaign(seed);

                var drawn = pool.SelectForNight(1, BuildPool(1), Healthy);
                CollectionAssert.Contains(drawn, "N1-M01", "seed " + seed + " lost the main quest");

                TearDown();
            }
        }

        [Test]
        public void EveryNightDrawsFourOrFiveRandomsOnTopOfTheMain()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int night = 1; night <= 6; night++)
                {
                    var pool = new NightPoolService();
                    pool.BeginCampaign(seed);

                    var drawn = pool.SelectForNight(night, BuildPool(night), Healthy);
                    int randoms = drawn.Count - 1;

                    Assert.GreaterOrEqual(randoms, NightPoolService.RandomFloor,
                        "seed " + seed + " night " + night + " drew too few");
                    Assert.LessOrEqual(randoms, NightPoolService.RandomCeiling,
                        "seed " + seed + " night " + night + " drew too many");

                    TearDown();
                }
            }
        }

        [Test]
        public void TheSameSeedAlwaysDealsTheSameNight()
        {
            // v5.0 4.4 step 11. Ten loads of one save must be one shift.
            const int seed = 4242;
            List<string> first = null;

            for (int load = 0; load < 10; load++)
            {
                var pool = new NightPoolService();
                pool.BeginCampaign(seed);

                var drawn = new List<string>(pool.SelectForNight(3, BuildPool(3), Healthy));

                if (first == null) first = drawn;
                else CollectionAssert.AreEqual(first, drawn, "load " + load + " dealt a different night");

                TearDown();
            }
        }

        [Test]
        public void RestoringASaveDoesNotRedraw()
        {
            var pool = new NightPoolService();
            pool.BeginCampaign(99);
            pool.SelectForNight(2, BuildPool(2), Healthy);

            var saved = new List<string>(pool.Selected);

            var reloaded = new NightPoolService();
            reloaded.BeginCampaign(99);
            reloaded.Restore(saved, new List<string>());

            CollectionAssert.AreEqual(saved, reloaded.Selected);
        }

        [Test]
        public void EveryNightMeetsItsCompositionQuota()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int night = 1; night <= 6; night++)
                {
                    var made = BuildPool(night);
                    var byId = new Dictionary<string, CaseDefinition>();
                    for (int i = 0; i < made.Count; i++) byId[made[i].caseId] = made[i];

                    var pool = new NightPoolService();
                    pool.BeginCampaign(seed);
                    var drawn = pool.SelectForNight(night, made, Healthy);

                    int normal = 0, mixed = 0, heavy = 0;
                    for (int i = 0; i < drawn.Count; i++)
                    {
                        var def = byId[drawn[i]];
                        if (def.isFixedMain) continue;

                        if (def.questType == QuestType.Normal) normal++;
                        else if (def.questType == QuestType.Mixed) mixed++;
                        else if (def.questType == QuestType.Anomaly
                              || def.questType == QuestType.Consequence
                              || def.questType == QuestType.FinalPressure) heavy++;
                    }

                    var quota = NightPoolService.QuotaFor(night);
                    var where = "seed " + seed + " night " + night;

                    Assert.GreaterOrEqual(normal, quota.Normal, where + " owed ordinary work");
                    Assert.GreaterOrEqual(mixed, quota.Mixed, where + " owed a mixed job");
                    Assert.GreaterOrEqual(heavy, quota.Heavy, where + " owed anomaly/consequence");

                    TearDown();
                }
            }
        }

        [Test]
        public void NothingIsDrawnTwiceInOneNight()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var pool = new NightPoolService();
                pool.BeginCampaign(seed);

                var drawn = pool.SelectForNight(4, BuildPool(4), Healthy);
                CollectionAssert.AllItemsAreUnique(drawn, "seed " + seed);

                TearDown();
            }
        }

        [Test]
        public void TwoQuestsFromOneMutexGroupNeverShareANight()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var made = BuildPool(2);
                // Every anomaly tonight fights over the same room.
                for (int i = 0; i < made.Count; i++)
                    if (made[i].questType == QuestType.Anomaly) made[i].mutexGroup = "pump_room";

                var byId = new Dictionary<string, CaseDefinition>();
                for (int i = 0; i < made.Count; i++) byId[made[i].caseId] = made[i];

                var pool = new NightPoolService();
                pool.BeginCampaign(seed);
                var drawn = pool.SelectForNight(2, made, Healthy);

                int pumpRoom = 0;
                for (int i = 0; i < drawn.Count; i++)
                    if (byId[drawn[i]].mutexGroup == "pump_room") pumpRoom++;

                Assert.LessOrEqual(pumpRoom, 1, "seed " + seed + " ran two mutually exclusive quests");
                TearDown();
            }
        }

        [Test]
        public void AHurtCaretakerIsNeverGivenTheFifthSlot()
        {
            // v5.0 4.4 step 9: below either floor, the night stops at four.
            var broken = new NightPoolService.Conditions
            {
                Hp = NightPoolService.FifthSlotMinHp - 1,
                San = 100
            };

            for (int seed = 1; seed <= Seeds; seed++)
            {
                var pool = new NightPoolService();
                pool.BeginCampaign(seed);

                var drawn = pool.SelectForNight(1, BuildPool(1), broken);
                Assert.AreEqual(NightPoolService.RandomFloor, drawn.Count - 1,
                    "seed " + seed + " handed a fifth job to somebody at " + broken.Hp + " HP");

                TearDown();
            }
        }

        [Test]
        public void ANightWithNoEligibleCandidatesStillRunsItsMain()
        {
            // The campaign has to be finishable from any world state (v5.0 4.4 step 1).
            var pool = new NightPoolService();
            pool.BeginCampaign(7);

            var onlyMain = new List<CaseDefinition> { Quest("N6-M01", 6, QuestType.Main, main: true) };
            var drawn = pool.SelectForNight(6, onlyMain, Healthy);

            CollectionAssert.AreEqual(new[] { "N6-M01" }, drawn);
        }

        [Test]
        public void LastNightsFamilyIsDiscouragedButNeverImpossible()
        {
            // v5.0 4.4 step 5 is a penalty, not a ban: a pool with one CONSEQUENCE left must
            // still be able to pay a quota that demands one.
            var pool = new NightPoolService();
            pool.BeginCampaign(11);

            pool.SelectForNight(3, BuildPool(3), Healthy);
            var second = pool.SelectForNight(4, BuildPool(4), Healthy);

            Assert.GreaterOrEqual(second.Count - 1, NightPoolService.RandomFloor,
                "the repeat penalty starved the following night");
        }
    }
}
