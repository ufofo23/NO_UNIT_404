using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// v5.1 4.1 / 30.1 against the content itself: nights 1, 3 and 5 are each a fifteen-quest
    /// pool of one main, one story subquest and thirteen candidates, and the draw over that
    /// real content composes the night 4.2 describes.
    /// </summary>
    public sealed class NightPoolContentTests
    {
        static readonly int[] AuthoredNights = { 1, 3, 5 };
        static readonly Dictionary<int, string> StoryOf = new Dictionary<int, string>
        {
            { 1, "N1-R01" }, { 3, "N3-R12" }, { 5, "N5-R13" }
        };

        List<CaseDefinition> _all;
        HashSet<string> _evidence;

        [SetUp]
        public void Setup()
        {
            _all = new List<CaseDefinition>(SeedContent.BuildV5Mains());
            _all.AddRange(SeedContent.BuildV51Subquests());

            _evidence = new HashSet<string>();
            foreach (var e in SeedContent.BuildV5Evidence()) _evidence.Add(e.evidenceId);
            foreach (var e in SeedContent.BuildV51Evidence()) _evidence.Add(e.evidenceId);
            foreach (var e in SeedContent.BuildEvidence()) _evidence.Add(e.evidenceId);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var c in _all) if (c != null) Object.DestroyImmediate(c);
        }

        List<CaseDefinition> Night(int night)
        {
            return _all.FindAll(c => c.nightIndex == night);
        }

        [Test]
        public void EachAuthoredNightIsOneMainOneStoryAndThirteenCandidates()
        {
            foreach (int night in AuthoredNights)
            {
                var pool = Night(night);
                Assert.AreEqual(NightPoolService.PoolPerNight, pool.Count, "night " + night);
                Assert.AreEqual(1, pool.FindAll(c => c.isFixedMain).Count, "night " + night + " main");

                var story = pool.FindAll(c => c.isFixedStory);
                Assert.AreEqual(1, story.Count, "night " + night + " story");
                Assert.AreEqual(StoryOf[night], story[0].caseId);
                Assert.AreEqual(0, story[0].baseWeight, "a story subquest is placed, never drawn");

                var random = pool.FindAll(c => !c.isFixedMain && !c.isFixedStory);
                Assert.AreEqual(NightPoolService.RandomCandidates, random.Count, "night " + night);
                foreach (var c in random) Assert.Greater(c.baseWeight, 0, c.caseId + " can never be drawn");
            }
        }

        [Test]
        public void EveryNightCanPayItsQuotaFromItsOwnCandidates()
        {
            foreach (int night in AuthoredNights)
            {
                var random = Night(night).FindAll(c => !c.isFixedMain && !c.isFixedStory);
                var quota = NightPoolService.QuotaFor(night);

                int normal = random.FindAll(c => c.questType == QuestType.Normal).Count;
                int mixed = random.FindAll(c => c.questType == QuestType.Mixed).Count;
                int heavy = random.FindAll(c => c.questType == QuestType.Anomaly || c.questType == QuestType.Consequence).Count;

                Assert.GreaterOrEqual(normal, quota.Normal, "night " + night + " normal");
                Assert.GreaterOrEqual(mixed, quota.Mixed, "night " + night + " mixed");
                Assert.GreaterOrEqual(heavy, quota.Heavy, "night " + night + " anomaly/consequence");
            }
        }

        [Test]
        public void OnlyMainsAndStorySubquestsWriteEndingPresentationFlags()
        {
            // v5.1 4.1.1: a random quest may change the night, never the ending's shots.
            var ending = new HashSet<string>
            {
                FlagIds.EndSunjaTrusted, FlagIds.EndChildItemsPreserved, FlagIds.EndDongsikBroadcastVerified
            };

            foreach (var c in _all)
            {
                if (c.isFixedMain || c.isFixedStory) continue;
                foreach (var d in c.decisions)
                    foreach (var q in d.consequences)
                        Assert.IsFalse(q.type == ConsequenceType.SetFlag && ending.Contains(q.targetId),
                                       c.caseId + "/" + d.decisionId + " writes " + q.targetId);
            }
        }

        [Test]
        public void EveryStorySubquestCanSetItsEndingFlag()
        {
            var expected = new Dictionary<string, string>
            {
                { "N1-R01", FlagIds.EndSunjaTrusted },
                { "N3-R12", FlagIds.EndChildItemsPreserved },
                { "N5-R13", FlagIds.EndDongsikBroadcastVerified }
            };

            foreach (var pair in expected)
            {
                var c = _all.Find(x => x.caseId == pair.Key);
                bool found = false;
                foreach (var d in c.decisions)
                    foreach (var q in d.consequences)
                        if (q.type == ConsequenceType.SetFlag && q.targetId == pair.Value && q.boolValue) found = true;
                Assert.IsTrue(found, pair.Key + " never sets " + pair.Value);
            }
        }

        [Test]
        public void EveryEvidenceTheSubquestsNameExists()
        {
            foreach (var c in _all)
            {
                if (!SubquestRules.Handles(c.caseId)) continue;
                foreach (var o in c.objectives)
                    if (o.type == ObjectiveType.AcquireEvidence)
                        Assert.IsTrue(_evidence.Contains(o.targetId), c.caseId + " objective " + o.targetId);
                foreach (var d in c.decisions)
                {
                    foreach (var e in d.requiredEvidenceIds)
                        Assert.IsTrue(_evidence.Contains(e), c.caseId + "/" + d.decisionId + " needs " + e);
                    foreach (var q in d.consequences)
                        if (q.type == ConsequenceType.GrantEvidence)
                            Assert.IsTrue(_evidence.Contains(q.targetId), c.caseId + " grants " + q.targetId);
                }
            }
        }

        [Test]
        public void EverySubquestCanBeClosedWhateverHappenedOnTheNight()
        {
            // A report with every option gated off is a shift that cannot end. At least one
            // decision per quest asks for nothing but having looked.
            foreach (var c in _all)
            {
                if (!SubquestRules.Handles(c.caseId)) continue;
                bool open = false;
                string handedOver = c.failSafe != null ? c.failSafe.alternateEvidenceId : null;
                foreach (var d in c.decisions)
                {
                    if (d.availability != null && d.availability.Length > 0) continue;
                    bool needsOnlyWhatTheDeadlineGives = true;
                    foreach (var e in d.requiredEvidenceIds)
                        if (e != handedOver) needsOnlyWhatTheDeadlineGives = false;
                    if (needsOnlyWhatTheDeadlineGives) open = true;
                }
                // Complementary flag pairs: exactly one of these rows is always open.
                if (c.caseId == "N1-R01" || c.caseId == "N3-R14") open = true;
                Assert.IsTrue(open, c.caseId + " has no ungated way to file");
            }
        }

        [Test]
        public void TheRealPoolDrawsMainStoryAndThreeOrFourRandoms()
        {
            foreach (int night in AuthoredNights)
            {
                for (int seed = 1; seed <= 40; seed++)
                {
                    var service = new NightPoolService();
                    service.BeginCampaign(seed);
                    var drawn = new List<string>(service.SelectForNight(night, Night(night),
                        new NightPoolService.Conditions { Hp = 100, San = 100 }));

                    Assert.Contains("N" + night + "-M01", drawn);
                    Assert.Contains(StoryOf[night], drawn);
                    int randoms = drawn.Count - 2;
                    Assert.That(randoms, Is.InRange(NightPoolService.RandomFloor, NightPoolService.RandomCeiling),
                                "night " + night + " seed " + seed);
                }
            }
        }

        [Test]
        public void TheBuildingIsB1ToSixFAndTheLiftNumbersMatch()
        {
            Assert.AreEqual(-1, ElevatorController.NumberOf(FloorPlan.B1));
            Assert.AreEqual(1, ElevatorController.NumberOf(FloorPlan.F1));
            Assert.AreEqual(6, ElevatorController.NumberOf(FloorPlan.F6));
            Assert.AreEqual(7, ElevatorController.Panel.Length, "one button per floor, B1 to 6F");
            Assert.IsNull(ZoneGroups.GroupOf(ZoneIds.Rooftop), "the roof is not built (v5.1 3.1)");
            Assert.IsNull(ZoneGroups.GroupOf(ZoneIds.PhantomFloor13), "no thirteenth floor is built");
            Assert.AreEqual(ZoneGroups.B1, ZoneGroups.GroupOf(ZoneIds.Archive));
        }
    }
}
