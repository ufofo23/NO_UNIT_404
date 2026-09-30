using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Phone;

namespace NO404.Tests
{
    /// <summary>
    /// The night's events arrive with the case they belong to (GDD 15.7, 13.4).
    ///
    /// Every one of these links was already in the content and read by nothing. A call carried
    /// the id of the case it was about; a story caller was written to arrive during a specific
    /// scene. Both then went out on a clock, and the case they belonged to went out on the
    /// caretaker's pace, so the two halves of a beat drifted apart: a neighbour ringing to
    /// complain about the noise from 304 either well before the 304 job existed or long after
    /// it was filed.
    ///
    /// What these hold is that the link is now load-bearing, and that nothing is lost when a
    /// case never opens.
    /// </summary>
    public sealed class CaseBeatTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        static PhoneCallDefinition Call(string callId)
        {
            foreach (var definition in ServiceHub.Content.PhoneCalls)
                if (definition.callId == callId) return definition;
            return null;
        }

        [Test]
        public void TheNoiseComplaintIsAboutTheCaseItArrivesWith()
        {
            var call = Call("CALL_N1_305_NOISE");
            Assert.IsNotNull(call, "night 1's complaint call is missing");
            Assert.AreEqual("N1-M01", call.caseId,
                "the neighbour is ringing about the vacant unit, so it belongs to that case");
        }

        [Test]
        public void EveryStoryCallNamesTheCaseItBelongsTo()
        {
            // The tie is the schedule now, so a call with no case is a call back on a clock.
            // That is allowed - but it should be a decision rather than an oversight, and
            // right now every authored call has one.
            foreach (var definition in ServiceHub.Content.PhoneCalls)
                Assert.IsFalse(string.IsNullOrEmpty(definition.caseId),
                               definition.callId + " has no case, so it can only ring on the hour");
        }

        [Test]
        public void ACallDoesNotRingBeforeItsCaseOpens()
        {
            ServiceHub.State.BeginNight(1);
            ServiceHub.Cases.BeginNight(1);
            ServiceHub.Phone.ScheduleNight(1);

            // Well past the authored 22:07, and C01 has not been handed out.
            ServiceHub.Clock.SetTime(22, 30);
            ServiceHub.Phone.Tick();

            Assert.IsFalse(ServiceHub.Phone.IsRinging,
                "the complaint arrived before the caretaker had the job it is about");
        }

        [Test]
        public void ACallRingsOnceItsCaseOpens()
        {
            ServiceHub.State.BeginNight(1);
            ServiceHub.Cases.BeginNight(1);
            ServiceHub.Phone.ScheduleNight(1);
            ServiceHub.Clock.SetTime(22, 30);

            ServiceHub.Phone.NotifyCaseStarted("N1-M01");
            ServiceHub.Phone.Tick();

            // Immediately, and with no clock advanced. A delay would be a time, and a time is
            // exactly what put the complaint about 304 an hour away from the 304 job.
            Assert.IsTrue(ServiceHub.Phone.IsRinging,
                "the handset goes when the caretaker is handed the job it is about");
        }

        [Test]
        public void ACaseThatNeverOpensStillGivesUpItsCall()
        {
            // There is no hour at which a held call gives up and rings anyway - that would be
            // a clock, and the clock is what was removed. What there is instead is a release
            // for when the night has no work left to start, because that is the only moment a
            // beat can be said to have failed to arrive. A dropped call would be a beat the
            // player was owed and never told about.
            ServiceHub.State.BeginNight(1);
            ServiceHub.Cases.BeginNight(1);
            ServiceHub.Phone.ScheduleNight(1);

            ServiceHub.Clock.SetTime(3, 0);
            ServiceHub.Phone.Tick();
            Assert.IsFalse(ServiceHub.Phone.IsRinging,
                "no amount of clock brings a call whose case has not opened");

            ServiceHub.Phone.ReleaseCallsWaitingOnCases();
            ServiceHub.Phone.Tick();

            Assert.IsTrue(ServiceHub.Phone.IsRinging,
                "but nothing the player was owed may be silently dropped");
        }

    }
}
