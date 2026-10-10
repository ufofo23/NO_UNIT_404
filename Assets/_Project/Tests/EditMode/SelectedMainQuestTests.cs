using NUnit.Framework;
using NO404.Cases;
using NO404.Core;
using NO404.Evidence;

namespace NO404.Tests
{
    public sealed class SelectedMainQuestTests
    {
        [SetUp]
        public void Reset()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        void Start(int night)
        {
            ServiceHub.State.BeginNight(night);
            ServiceHub.Cases.BeginNight(night);
            Assert.IsTrue(ServiceHub.Cases.TryStartCase("N" + night + "-M01"));
        }

        void Complete(string caseId)
        {
            foreach (var objective in ServiceHub.Cases.Find(caseId).Definition.objectives)
                ServiceHub.Cases.NotifyObjective(objective.type, objective.targetId);
        }

        [TestCase(1, "dec_preserve_bill")]
        [TestCase(3, "dec_withdrew")]
        [TestCase(5, "dec_residents_first")]
        public void InvestigationCannotBeSkipped(int night, string decision)
        {
            Start(night);
            Assert.IsFalse(ServiceHub.Cases.SubmitDecision("N" + night + "-M01", decision, null).Accepted);
        }

        [Test]
        public void BillReadingCostsSanOnceAndDiscardRemovesIt()
        {
            Start(1);
            int san = ServiceHub.Vitals.San;
            ServiceHub.Evidence.Acquire("EV_404_BILL", EvidenceSource.WorldPickup);
            ServiceHub.Evidence.Acquire("EV_404_BILL", EvidenceSource.WorldPickup);
            Assert.AreEqual(san - 3, ServiceHub.Vitals.San);
            Complete("N1-M01");
            Assert.IsTrue(ServiceHub.Cases.SubmitDecision("N1-M01", "dec_discard_bill", null).Accepted);
            Assert.IsFalse(ServiceHub.Evidence.Has("EV_404_BILL"));
            Assert.AreEqual(1, ServiceHub.State.GetDebt(DebtIds.Record));
        }

        [Test]
        public void HeightComparisonNeedsPersonalRecordAndStaysUncertain()
        {
            Start(3);
            ServiceHub.Evidence.Acquire("EV_HEIGHT_MARKS", EvidenceSource.WorldPickup);
            Assert.IsFalse(SelectedMainQuestRules.CanAct("compare_height"));
            SelectedMainQuestRules.Act("health_record");
            SelectedMainQuestRules.Act("compare_height");
            Assert.IsTrue(ServiceHub.State.GetFlag("N3_HEIGHT_MATCH_NOTED"));
            Assert.IsFalse(SelectedMainQuestRules.CanAct("compare_height"));
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.Knows404));
            StringAssert.DoesNotContain("윤하린", Loc.T("evidence.ev_height_marks.desc"));
        }

        [Test]
        public void SecondWrongDoorLosesThePlayerAndMarkerAllowsSoloRecovery()
        {
            Start(3);
            SelectedMainQuestRules.Act("wrong_door");
            Assert.IsFalse(ServiceHub.State.GetFlag("N3_PLAYER_LOST"));
            SelectedMainQuestRules.Act("wrong_door");
            Assert.IsTrue(ServiceHub.State.GetFlag("N3_PLAYER_LOST"));
            Assert.AreEqual(1, ServiceHub.State.GetStat("PLAYER_LOST_COUNT"));
            SelectedMainQuestRules.Act("return_marker");
            Assert.IsFalse(ServiceHub.State.GetFlag("N3_PLAYER_LOST"));
        }

        [Test]
        public void StaffIdAloneCannotConfirmRescueOrRemainsLocation()
        {
            Start(5);
            ServiceHub.Evidence.Acquire("EV_DONGSIK_ID", EvidenceSource.WorldPickup);
            Assert.IsFalse(ServiceHub.State.GetFlag("DONGSIK_LAST_BROADCAST_FOUND"));
            ServiceHub.Evidence.Acquire("EV_FIRE_TAPE_2009", EvidenceSource.WorldPickup);
            ServiceHub.Evidence.Acquire("EV_N5_WORK_LOG", EvidenceSource.WorldPickup);
            Assert.IsTrue(ServiceHub.State.GetFlag("DONGSIK_LAST_BROADCAST_FOUND"));
            Assert.IsFalse(ServiceHub.State.GetFlag("DONGSIK_REMAINS_LOCATION_HINT"));
            ServiceHub.Evidence.Acquire("EV_N5_LAST_POSITION", EvidenceSource.WorldPickup);
            Assert.IsTrue(ServiceHub.State.GetFlag("DONGSIK_REMAINS_LOCATION_HINT"));
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.DongsikRescued));
        }

        [Test]
        public void ChoiResponsibilityRequiresBothSignedApprovalAndDeletionHistory()
        {
            Start(5);
            ServiceHub.Evidence.Acquire("EV_CHOI_APPROVAL", EvidenceSource.WorldPickup);
            Assert.IsFalse(ServiceHub.State.GetFlag("CHOI_2009_RESPONSIBILITY_CONFIRMED"));
            ServiceHub.Evidence.Acquire("EV_404_DELETION", EvidenceSource.WorldPickup);
            Assert.IsTrue(ServiceHub.State.GetFlag("CHOI_2009_RESPONSIBILITY_CONFIRMED"));
            StringAssert.Contains("02:08", ServiceHub.Cctv.TimestampFor(ServiceHub.Clock.GameSecond));
        }

        [Test]
        public void FourthFloorButtonOpensTheServiceSubzoneDuringNightThree()
        {
            Start(3);
            var go = new UnityEngine.GameObject("LiftButtonTest");
            try
            {
                var button = go.AddComponent<NO404.Gameplay.ElevatorButton>();
                button.Setup(null, NO404.Gameplay.FloorPlan.F4, ZoneIds.Floor04, "ui.elevator.f4", AccessLevel.Staff1);
                Assert.AreEqual(ZoneIds.ServicePassage, button.DestinationZone);
                ServiceHub.ResetPlaythrough();
                Assert.AreEqual(ZoneIds.Floor04, button.DestinationZone);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void MainContentHasLocalisedObjectivesDecisionsAndEvidence()
        {
            foreach (string id in new[] { "N1-M01", "N3-M01", "N5-M01" })
            {
                var quest = ServiceHub.Content.FindCase(id);
                Assert.IsTrue(ServiceHub.Localization.HasKey(quest.titleKey), quest.titleKey);
                Assert.IsTrue(ServiceHub.Localization.HasKey(quest.summaryKey), quest.summaryKey);
                foreach (var objective in quest.objectives)
                    Assert.IsTrue(ServiceHub.Localization.HasKey(objective.titleKey), objective.titleKey);
                foreach (var decision in quest.decisions)
                {
                    Assert.IsTrue(ServiceHub.Localization.HasKey(decision.labelKey), decision.labelKey);
                    Assert.IsTrue(ServiceHub.Localization.HasKey(decision.resultKey), decision.resultKey);
                }
                foreach (var evidence in quest.evidenceIds)
                {
                    var definition = ServiceHub.Content.FindEvidence(evidence);
                    Assert.IsNotNull(definition, evidence);
                    Assert.IsTrue(ServiceHub.Localization.HasKey(definition.descriptionKey), definition.descriptionKey);
                }
            }
        }
    }
}
