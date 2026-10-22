using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;
using NO404.Endings;

namespace NO404.Tests
{
    /// <summary>
    /// v5.1 8 and 30.5 - the single ending.
    ///
    /// There is one ending, "404호는 있었습니다", and it is reached by finishing the last
    /// night and getting the evidence out alive. HP and SAN grade nothing. What six nights of
    /// choices are worth is a set of shots inside that one ending, each with two forms, and
    /// neither form is the bad one.
    ///
    /// These replace the v5.0 grade tests, which asserted the A-D thresholds v5.1 removed.
    /// </summary>
    public sealed class SingleEndingTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        /// <summary>Night 6 with its main open, nothing done yet.</summary>
        static void OpenTheLastNight()
        {
            ServiceHub.ResetPlaythrough();
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(1);

            ServiceHub.State.BeginNight(6);
            ServiceHub.Cases.BeginNight(6);

            var runtime = ServiceHub.Cases.Find(EndingService.FinalMainId);
            Assert.IsNotNull(runtime, "N6-M01 was not dealt on night 6");
            if (runtime.State == CaseState.Dormant) Assert.IsTrue(ServiceHub.Cases.TryStartCase(runtime.CaseId));
        }

        static void SendTheEvidence(string decisionId = "dec_send_police")
        {
            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            Assert.IsTrue(ServiceHub.Cases.SubmitDecision(EndingService.FinalMainId, decisionId, attached).Accepted);
        }

        // ---- reaching it ---------------------------------------------------------

        [Test]
        public void ThereIsOneEnding()
        {
            CollectionAssert.AreEqual(new[] { EndingIds.SingleEnding404Existed }, EndingIds.All);
        }

        /// <summary>v5.1 8.1: FinalMainQuestCompleted AND ExternalEvidenceSent, alive.</summary>
        [Test]
        public void TheEndingNeedsTheLastMainFinishedAndTheEvidenceOut()
        {
            OpenTheLastNight();
            Assert.AreEqual(EndingIds.NoEnding, ServiceHub.Endings.Evaluate().endingId,
                            "the last night is still open and the ending was reached");

            SendTheEvidence();

            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.ExternalEvidenceSent));
            Assert.AreEqual(EndingIds.SingleEnding404Existed, ServiceHub.Endings.Evaluate().endingId);
        }

        /// <summary>
        /// v5.1 8.1: no grade. Any SAN reaches the same ending - being worn down makes the
        /// night harder, not the ending worse.
        /// </summary>
        [TestCase(100)]
        [TestCase(40)]
        [TestCase(1)]
        public void SanDoesNotGradeTheEnding(int san)
        {
            OpenTheLastNight();
            SendTheEvidence();

            ServiceHub.Vitals.Strain(ServiceHub.Vitals.San - san, "test");
            Assert.AreEqual(san, ServiceHub.Vitals.San);
            Assert.AreEqual(EndingIds.SingleEnding404Existed, ServiceHub.Endings.Evaluate().endingId);
        }

        [Test]
        public void ThereIsNoEndingForSomebodyWhoDidNotWalkOut()
        {
            OpenTheLastNight();
            SendTheEvidence();

            ServiceHub.Vitals.Damage(ServiceHub.Vitals.Hp, "test");
            Assert.AreEqual(0, ServiceHub.Vitals.Hp);
            Assert.AreEqual(EndingIds.NoEnding, ServiceHub.Endings.Evaluate().endingId);
        }

        // ---- what is in it ---------------------------------------------------------

        /// <summary>v5.1 8.2: the frame of the ending is the same in every campaign.</summary>
        [Test]
        public void TheFrameIsTheSameWhateverWasDecided()
        {
            var bare = ServiceHub.Endings.EndingShots();

            foreach (var flag in new[]
                     {
                         FlagIds.EndSunjaTrusted, FlagIds.EndSunjaTestimonyFull, FlagIds.End2009CallPreserved,
                         FlagIds.EndChildItemsPreserved, FlagIds.EndDongsikRemainsRecovered,
                         FlagIds.EndDongsikBroadcastVerified, FlagIds.EndHarinDoorOpened,
                         FlagIds.EndLedgerSecured, FlagIds.EndHarinRecordFirstRegistered
                     })
                ServiceHub.State.SetFlag(flag, true);

            var full = ServiceHub.Endings.EndingShots();

            foreach (var common in new[]
                     {
                         "ending.shot.exit_404", "ending.shot.days_later", "ending.shot.dawn", "ending.shot.404_restored"
                     })
            {
                CollectionAssert.Contains(bare, common);
                CollectionAssert.Contains(full, common);
            }

            Assert.AreEqual("ending.shot.exit_404", bare[0], "the ending does not open on leaving 404");
            Assert.AreEqual("ending.shot.404_restored", bare[bare.Count - 1], "the ending does not close on the plan");
            Assert.AreEqual("ending.shot.404_restored", full[full.Count - 1]);
        }

        /// <summary>
        /// v5.1 8.3 / 30.5: each fixed-story choice changes at least one shot inside the same
        /// ending - and the version for the other choice is a shot too, not a hole.
        /// </summary>
        [TestCase("END_SUNJA_TRUSTED", "ending.shot.sunja.trusted", "ending.shot.sunja.empty_chair")]
        [TestCase("END_CHILD_ITEMS_PRESERVED", "ending.shot.child_items.kept", "ending.shot.child_items.photo")]
        [TestCase("END_DONGSIK_REMAINS_RECOVERED", "ending.shot.dongsik.recovered", "ending.shot.dongsik.located")]
        [TestCase("END_DONGSIK_LAST_BROADCAST_VERIFIED", "ending.shot.broadcast.original", "ending.shot.broadcast.other_record")]
        [TestCase("END_HARIN_DOOR_OPENED", "ending.shot.harin_door.memory", "ending.shot.harin_door.slipper")]
        [TestCase("END_LEDGER_SECURED", "ending.shot.ledger.opened", "ending.shot.ledger.digital")]
        [TestCase("END_HARIN_RECORD_FIRST_REGISTERED", "ending.shot.harin_record.printed", "ending.shot.harin_record.handwritten")]
        public void EachChoiceIsAShotWithTwoForms(string flag, string withIt, string withoutIt)
        {
            var without = ServiceHub.Endings.EndingShots();
            CollectionAssert.Contains(without, withoutIt);
            CollectionAssert.DoesNotContain(without, withIt);

            ServiceHub.State.SetFlag(flag, true);

            var with = ServiceHub.Endings.EndingShots();
            CollectionAssert.Contains(with, withIt);
            CollectionAssert.DoesNotContain(with, withoutIt);
        }

        /// <summary>v5.1 8.3: the 2009 call is the one shot that is simply left out without it.</summary>
        [Test]
        public void TheCallFrom2009IsOnlyThereIfItWasKept()
        {
            CollectionAssert.DoesNotContain(ServiceHub.Endings.EndingShots(), "ending.shot.call_2009");
            ServiceHub.State.SetFlag(FlagIds.End2009CallPreserved, true);
            CollectionAssert.Contains(ServiceHub.Endings.EndingShots(), "ending.shot.call_2009");
        }

        [TestCase("dec_send_police", "ending.shot.sent.police")]
        [TestCase("dec_send_media", "ending.shot.sent.media")]
        [TestCase("dec_send_residents", "ending.shot.sent.residents")]
        public void WhereTheEvidenceWentIsAShotInTheSameEnding(string decision, string shot)
        {
            OpenTheLastNight();
            SendTheEvidence(decision);

            Assert.AreEqual(EndingIds.SingleEnding404Existed, ServiceHub.Endings.Evaluate().endingId);
            CollectionAssert.Contains(ServiceHub.Endings.EndingShots(), shot);
        }

        /// <summary>v5.1 0.13 / 30.4: no shot in the ending has Park Dong-sik alive.</summary>
        [Test]
        public void NothingInTheEndingHasHimAlive()
        {
            ServiceHub.State.SetFlag(FlagIds.DongsikRescued, true);

            var loc = new LocalizationService();
            loc.Initialize("ko");

            foreach (var key in ServiceHub.Endings.EndingShots())
            {
                StringAssert.DoesNotContain("survived", key);
                StringAssert.DoesNotContain("살아서", loc.Get(key), key + " has him alive");
            }
        }

        [Test]
        public void EveryShotIsInTheStringTableInBothLanguages()
        {
            var keys = new HashSet<string>();
            foreach (var flag in new[] { false, true })
            {
                foreach (var id in new[]
                         {
                             FlagIds.EndSunjaTrusted, FlagIds.End2009CallPreserved, FlagIds.EndChildItemsPreserved,
                             FlagIds.EndDongsikRemainsRecovered, FlagIds.EndDongsikBroadcastVerified,
                             FlagIds.EndHarinDoorOpened, FlagIds.EndLedgerSecured, FlagIds.EndHarinRecordFirstRegistered
                         })
                    ServiceHub.State.SetFlag(id, flag);

                foreach (var key in ServiceHub.Endings.EndingShots()) keys.Add(key);
            }

            ServiceHub.State.SetFlag(FlagIds.EndSunjaTestimonyFull, true);
            foreach (var key in ServiceHub.Endings.EndingShots()) keys.Add(key);
            ServiceHub.State.SetFlag(FlagIds.SubmittedToMedia, true);
            foreach (var key in ServiceHub.Endings.EndingShots()) keys.Add(key);
            ServiceHub.State.SetFlag(FlagIds.SubmittedToMedia, false);
            ServiceHub.State.SetFlag(FlagIds.SubmittedToResidents, true);
            foreach (var key in ServiceHub.Endings.EndingShots()) keys.Add(key);

            foreach (var ending in ServiceHub.Endings.AllOrdered)
            {
                keys.Add(ending.titleKey);
                keys.Add(ending.summaryKey);
                keys.Add(ending.bodyKey);
            }

            var missing = new List<string>();
            foreach (var language in new[] { "en", "ko" })
            {
                var loc = new LocalizationService();
                loc.Initialize(language);
                foreach (var key in keys)
                    if (!loc.HasKey(key) || string.IsNullOrWhiteSpace(loc.Get(key))) missing.Add(language + ":" + key);
            }

            Assert.IsEmpty(missing, "missing strings: " + string.Join(", ", missing.ToArray()));
        }

        /// <summary>
        /// v5.1 0.16: her full name appears twice in the whole game, the night-4 database and
        /// the list in the ending. The ending is one of the two.
        /// </summary>
        [Test]
        public void HerNameIsOnTheListWhicheverWayItGotThere()
        {
            var loc = new LocalizationService();
            loc.Initialize("ko");

            StringAssert.Contains("윤하린", loc.Get("ending.shot.harin_record.printed"));
            StringAssert.Contains("윤하린", loc.Get("ending.shot.harin_record.handwritten"));
        }
    }
}
