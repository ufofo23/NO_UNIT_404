using NUnit.Framework;
using NO404.Core;
using NO404.Endings;

namespace NO404.Tests
{
    /// <summary>
    /// v5.0 30.3 — the acceptance tests for the gate and the grade.
    ///
    /// These are the last thing the campaign does and the only place its six nights are
    /// finally worth something, so the thresholds are asserted directly rather than inferred
    /// from a playthrough. The interesting cases are all on the boundaries and in the two
    /// restrictions v5.0 8.2 adds under its own table, which is exactly the sort of rule that
    /// looks decorative and turns out to be the whole design.
    /// </summary>
    public sealed class EndingGradeTests
    {
        [Test]
        public void FullConditionIsAnA()
        {
            Assert.AreEqual(EndingIds.A_RecordedPeople, EndingService.GradeFor(100, 100));
        }

        [Test]
        public void TheGradeBoundariesAreWhereTheDocumentPutsThem()
        {
            // A needs the score and both floors (v5.0 8.2).
            Assert.AreEqual(EndingIds.A_RecordedPeople, EndingService.GradeFor(90, 80));
            Assert.AreNotEqual(EndingIds.A_RecordedPeople, EndingService.GradeFor(74, 100),
                               "HP 74 is below A's own floor of 75");

            Assert.AreEqual(EndingIds.B_SafeSilence, EndingService.GradeFor(75, 65));
            Assert.AreEqual(EndingIds.C_Erased404, EndingService.GradeFor(60, 50));
            Assert.AreEqual(EndingIds.D_CommunityCollapse, EndingService.GradeFor(45, 40));
        }

        /// <summary>
        /// The restriction that does the real work.
        ///
        /// Somebody physically untouched who can no longer trust what they saw scores 74.8 and
        /// would read as a B on the score alone. v5.0 8.2 forbids B or better below forty of
        /// either, which is what stops the campaign from letting a player trade their mind for
        /// a better letter.
        /// </summary>
        [Test]
        public void NerveCannotBeTradedForABetterLetter()
        {
            Assert.AreEqual(EndingIds.D_CommunityCollapse, EndingService.GradeFor(100, 39),
                            "SAN 39 forbids B or better however untouched the body is");

            Assert.AreEqual(EndingIds.D_CommunityCollapse, EndingService.GradeFor(39, 100),
                            "and the same in the other direction");

            Assert.AreNotEqual(EndingIds.A_RecordedPeople, EndingService.GradeFor(100, 49),
                               "SAN 49 forbids A");
            Assert.AreNotEqual(EndingIds.A_RecordedPeople, EndingService.GradeFor(54, 100),
                               "HP 54 forbids A");
        }

        [Test]
        public void TheGateIsCheckedBeforeTheGrade()
        {
            var vitals = new VitalService();

            vitals.Damage(VitalService.Max - 29, "test");     // HP 29
            Assert.AreEqual(EndingGate.PhysicalCollapse, vitals.Gate);

            var fresh = new VitalService();
            fresh.Strain(VitalService.Max - 29, "test");       // SAN 29
            Assert.AreEqual(EndingGate.MentalBreakdown, fresh.Gate);

            var ok = new VitalService();
            ok.Damage(VitalService.Max - 30, "test");          // exactly 30 is still allowed
            Assert.AreEqual(EndingGate.Eligible, ok.Gate);
        }

        /// <summary>
        /// The gate is a failure result, not a fifth grade (v5.0 8.1).
        ///
        /// Somebody at 29 HP and 100 SAN would grade D on the arithmetic. They must not get D:
        /// D is an ending somebody survived, and this is not one.
        /// </summary>
        [Test]
        public void CollapsingIsNotTheSameAsGradingD()
        {
            var vitals = new VitalService();
            vitals.Damage(VitalService.Max - 29, "test");

            Assert.AreNotEqual(EndingGate.Eligible, vitals.Gate);
            Assert.AreEqual(EndingIds.D_CommunityCollapse, EndingService.GradeFor(29, 100),
                            "the arithmetic alone would call this D, which is why the gate runs first");
        }

        [Test]
        public void EveryGradeIsSomethingTheScoreCanActuallyReach()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            for (int hp = 0; hp <= 100; hp += 1)
                for (int san = 0; san <= 100; san += 1)
                    if (hp >= VitalService.EndingGateMinimum && san >= VitalService.EndingGateMinimum)
                        seen.Add(EndingService.GradeFor(hp, san));

            foreach (var id in EndingIds.All)
                CollectionAssert.Contains(seen, id, id + " is unreachable for every survivable state");
        }

        [Test]
        public void TheFailureResultIsNotInTheGallery()
        {
            CollectionAssert.DoesNotContain(EndingIds.All, EndingIds.NoEnding);
        }
    }
}
