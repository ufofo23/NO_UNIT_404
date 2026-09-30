using NUnit.Framework;
using NO404.Anomalies;
using NO404.Core;
using NO404.Cases;
using NO404.Gameplay;
using NO404.Save;

namespace NO404.Tests
{
    /// <summary>
    /// The M01..M18 machinery actually running (v2.1 spec 30.2).
    ///
    /// Everything about these eighteen events was checked as data and nothing was checked as
    /// behaviour: the content tests prove that every event has a page, a procedure and a
    /// recovery route, and none of them proves that working the procedure resolves it, that a
    /// wrong answer can be tried again, or that a chain hands over. Spec 30.2 lists ten paths
    /// each event must survive; the six below are the ones that are the service rather than
    /// the room, and they are the six that can be held down here rather than in a playtest.
    ///
    /// M06 is the worked example throughout - it is night 1's head, so it is Active the moment
    /// a shift starts, and its procedure is four plain steps with one prohibition.
    /// </summary>
    public sealed class ManualEventRuntimeTests
    {
        const string M06 = ManualEventIds.M06_LostParcel;
        const string M14 = ManualEventIds.M14_Treadmills;

        ManualEventService Events { get { return ServiceHub.ManualEvents; } }

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        [TearDown] public void TearDown() { EventBus.Clear(); }

        // ---- helpers ---------------------------------------------------------

        /// <summary>Open night 1, whose head is M06.</summary>
        void BeginNightOne()
        {
            ServiceHub.State.BeginNight(1);
            Events.BeginNight(1);
        }

        /// <summary>Work M06 the way its page says, minus the last step.</summary>
        void WorkTheParcel()
        {
            Events.SetCounter(M06, "recordChecks", 2);
            Events.CompleteObjective(M06, "obj_verify");
            Events.CompleteObjective(M06, "obj_stamp");
            Events.CompleteObjective(M06, "obj_shelve");
        }

        static int Violations
        {
            get { return ServiceHub.State.GetStat(StatIds.ManualViolationCount); }
        }

        // ---- the shift opens -------------------------------------------------

        [Test]
        public void TheHeadOfTheNightIsActiveAsSoonAsTheShiftStarts()
        {
            BeginNightOne();
            Assert.AreEqual(ManualEventState.Active, Events.Find(M06).State,
                            "night 1 should open with the parcel already in hand");
        }

        [Test]
        public void MoreThanOneEventCanBeInFlightAtOnce()
        {
            // Spec 30.2 path 7: an M event has to survive a routine job landing on top of it.
            // The postcards are night 1's second head, so the first shift already proves the
            // service can carry two.
            BeginNightOne();

            int active = 0;
            foreach (var runtime in Events.AllEvents)
                if (runtime.State == ManualEventState.Active) active++;

            Assert.GreaterOrEqual(active, 2, "night 1 should have the parcel and the postcard");
        }

        [Test]
        public void AnAnomalyAndARoutineJobCanBeOnTheGoTogether()
        {
            // Spec 30.2 path 7 proper. The earlier test proves the service can carry two M
            // events; this one proves the two systems do not stand on each other - a routine
            // task starting must not close an anomaly, and an anomaly resolving must not take
            // the task with it. They are scheduled by different services and share a player.
            BeginNightOne();
            ServiceHub.Cases.BeginNight(1);

            Assert.IsTrue(ServiceHub.Cases.TryStartCase("N1-M01"), "night 1's main should be startable");
            Assert.AreEqual(ManualEventState.Active, Events.Find(M06).State,
                            "a routine job arriving must not close the anomaly");

            var t01 = ServiceHub.Cases.Find("N1-M01");
            Assert.IsTrue(t01.State.IsActive());

            // Finish the anomaly with the task still open.
            WorkTheParcel();
            Events.CompleteObjective(M06, "obj_return_to_office");

            Assert.AreEqual(ManualEventState.ResolvedCorrect, Events.Find(M06).State);
            Assert.IsTrue(ServiceHub.Cases.Find("N1-M01").State.IsActive(),
                          "and the parcel being dealt with must not close the parking job");
        }

        // ---- path 1 and 2: with the page, and without it ---------------------

        [Test]
        public void WorkingTheProcedureResolvesItCorrectly()
        {
            BeginNightOne();
            WorkTheParcel();

            Assert.AreEqual(ManualEventState.Active, Events.Find(M06).State,
                            "three of four steps is not a finished procedure");

            Events.CompleteObjective(M06, "obj_return_to_office");

            // There is no submit button. The last step of the page is what closes the event.
            Assert.AreEqual(ManualEventState.ResolvedCorrect, Events.Find(M06).State);
        }

        [Test]
        public void ARuleTheCaretakerWasNeverGivenIsAMistakeRatherThanAViolation()
        {
            // Spec 0.9.2, and the single most unfair thing this system could do. The binder is
            // emptied first so the page genuinely was never issued.
            BeginNightOne();
            ServiceHub.Manual.Reset();
            Assert.IsFalse(ServiceHub.Manual.IsUnlocked(ManualEventIds.PageOf(M06)));

            int before = Violations;

            Events.SetCounter(M06, "recordChecks", 2);
            Events.SetCounter(M06, "opened", 1);          // the prohibition
            Events.CompleteObjective(M06, "obj_verify");
            Events.CompleteObjective(M06, "obj_stamp");
            Events.CompleteObjective(M06, "obj_shelve");
            Events.CompleteObjective(M06, "obj_return_to_office");

            Assert.AreEqual(ManualEventState.ResolvedWrong, Events.Find(M06).State,
                            "it is still the wrong answer");
            Assert.AreEqual(before, Violations,
                            "but not a violation of a rule nobody had been given");
        }

        [Test]
        public void BreakingARuleTheCaretakerWasGivenIsAViolation()
        {
            BeginNightOne();
            ServiceHub.Manual.Unlock(ManualEventIds.PageOf(M06), "test");

            int before = Violations;

            Events.SetCounter(M06, "recordChecks", 2);
            Events.SetCounter(M06, "opened", 1);
            Events.CompleteObjective(M06, "obj_verify");
            Events.CompleteObjective(M06, "obj_stamp");
            Events.CompleteObjective(M06, "obj_shelve");
            Events.CompleteObjective(M06, "obj_return_to_office");

            Assert.AreEqual(ManualEventState.ResolvedWrong, Events.Find(M06).State);
            Assert.AreEqual(before + 1, Violations);
        }

        // ---- path 3 and 4: getting it wrong, and getting it wrong twice ------

        [Test]
        public void AWrongAnswerCanAlwaysBeTriedAgainFromTheStart()
        {
            // Spec 30.2 path 3 and spec 0.5. Recovery means starting the count over rather
            // than resuming halfway through a sequence that was already got wrong.
            BeginNightOne();
            Events.SetCounter(M06, "opened", 1);
            Events.SetCounter(M06, "recordChecks", 2);
            foreach (var step in new[] { "obj_verify", "obj_stamp", "obj_shelve", "obj_return_to_office" })
                Events.CompleteObjective(M06, step);

            Assert.AreEqual(ManualEventState.ResolvedWrong, Events.Find(M06).State);

            Assert.IsTrue(Events.Retry(M06));
            var runtime = Events.Find(M06);

            Assert.AreEqual(ManualEventState.Active, runtime.State);
            Assert.AreEqual(0, runtime.Counter("opened"), "the counters start clean");
            Assert.AreEqual(0, runtime.Counter("recordChecks"));
            Assert.IsFalse(runtime.IsComplete("obj_verify"), "and so do the steps");

            // The record of how it went does not reset. The failsafe reads it.
            Assert.AreEqual(1, runtime.WrongAttempts);
        }

        [Test]
        public void ACorrectlyResolvedEventIsNotRetried()
        {
            BeginNightOne();
            WorkTheParcel();
            Events.CompleteObjective(M06, "obj_return_to_office");

            Assert.IsFalse(Events.Retry(M06), "there is nothing to try again");
        }

        [Test]
        public void TheFailSafeOpensOnceTheAttemptsAreSpent()
        {
            // Spec 30.2 path 4 and spec 0.5: two wrong answers must open a way through.
            BeginNightOne();
            var runtime = Events.Find(M06);
            int needed = runtime.Definition.failSafe.afterWrongAttempts;
            Assert.Greater(needed, 0, "M06 should have a recovery route at all");

            for (int attempt = 0; attempt < needed; attempt++)
            {
                Events.SetCounter(M06, "opened", 1);
                foreach (var step in new[] { "obj_verify", "obj_stamp", "obj_shelve", "obj_return_to_office" })
                    Events.CompleteObjective(M06, step);

                Assert.IsFalse(runtime.FailSafeFired, "the route should not open early");
                Events.Retry(M06);
                Events.Tick();
            }

            Assert.IsTrue(runtime.FailSafeFired, "after " + needed + " wrong answers there has to be a way on");
        }

        // ---- the chain -------------------------------------------------------

        [Test]
        public void ClosingAnEventOpensTheNextOneEvenWhenItWasGotWrong()
        {
            // Spec 0.3 and 0.5. A chain that stalled on a wrong answer would take the rest of
            // the night with it, which is the worst version of both rules.
            BeginNightOne();
            Assert.AreEqual(ManualEventState.Dormant, Events.Find(M14).State);

            Events.SetCounter(M06, "opened", 1);
            foreach (var step in new[] { "obj_verify", "obj_stamp", "obj_shelve", "obj_return_to_office" })
                Events.CompleteObjective(M06, step);

            Assert.AreEqual(ManualEventState.ResolvedWrong, Events.Find(M06).State);
            Assert.AreEqual(ManualEventState.Active, Events.Find(M14).State,
                            "the gym should still be waiting for them");
        }

        // ---- path 5: a save taken mid-procedure ------------------------------

        [Test]
        public void AnEventInFlightComesBackFromASaveWithItsWorkIntact()
        {
            BeginNightOne();
            WorkTheParcel();

            var data = ServiceHub.Save.Capture(0, SaveReason.Manual);

            // Everything is thrown away, exactly as a reload does.
            Events.Reset();
            Assert.AreEqual(ManualEventState.Dormant, Events.Find(M06).State);

            Events.LoadFrom(data.manualEvents);
            var runtime = Events.Find(M06);

            Assert.AreEqual(ManualEventState.Active, runtime.State);
            Assert.AreEqual(2, runtime.Counter("recordChecks"), "the checks it had already made");
            Assert.IsTrue(runtime.IsComplete("obj_shelve"), "and the steps it had already taken");
            Assert.IsFalse(runtime.IsComplete("obj_return_to_office"));

            // And it can be finished from there rather than started over.
            Events.CompleteObjective(M06, "obj_return_to_office");
            Assert.AreEqual(ManualEventState.ResolvedCorrect, Events.Find(M06).State);
        }

        [Test]
        public void ARestoredEventIsBackOnTheActiveListSoItsFailSafeStillTicks()
        {
            // The subtle half of a reload: a runtime can be Active and invisible to Tick if
            // nothing put it back on the list, and the only symptom is a recovery route that
            // silently never opens.
            BeginNightOne();
            WorkTheParcel();

            var data = ServiceHub.Save.Capture(0, SaveReason.Manual);
            Events.Reset();
            Events.LoadFrom(data.manualEvents);

            bool listed = false;
            foreach (var runtime in Events.ActiveEvents)
                if (runtime.Definition.eventId == M06) listed = true;

            Assert.IsTrue(listed, "a restored event has to be one the service is still watching");
        }

        // ---- spec 0.10.5: the one event allowed to end a shift ---------------

        [Test]
        public void NothingIsLethalOnAFirstMistake()
        {
            BeginNightOne();
            Assert.IsFalse(Events.MayBeLethal(M06), "a parcel is not lethal at all");

            // M18 is the only event that opts in, and even it needs a repeat.
            Assert.IsFalse(Events.MayBeLethal(ManualEventIds.M18_RoofFigure),
                           "not before the caretaker has already broken the rule once");
        }

        [Test]
        public void TheOnlyLethalEventNeedsOptInAPrintedWarningAndARepeat()
        {
            ServiceHub.State.BeginNight(3);
            Events.BeginNight(3);

            const string m18 = ManualEventIds.M18_RoofFigure;
            var runtime = Events.Find(m18);
            Assert.IsTrue(runtime.Definition.lethalOnRepeat, "M18 should be the one that opts in");

            var page = ServiceHub.Manual.Find(runtime.Definition.manualPageId);
            Assert.IsNotNull(page);
            Assert.IsTrue(page.HasLethalProhibition, "and its page has to print the warning");

            Assert.IsFalse(Events.MayBeLethal(m18), "one breach is never enough");

            Events.Begin(m18);
            Events.SetCounter(m18, "approachedWithinFiveMetres", 1);
            Events.CompleteObjective(m18, "obj_broadcast");
            Events.CompleteObjective(m18, "obj_wait");

            Assert.AreEqual(ManualEventState.ResolvedWrong, runtime.State);
            Assert.IsTrue(Events.MayBeLethal(m18), "the second time, it can cost the shift");
        }

        // ---- the one event that comes back every night -----------------------

        [Test]
        public void TheNightlyEventComesBackCleanRatherThanResolved()
        {
            BeginNightOne();
            const string m13 = ManualEventIds.M13_Postcards;

            Events.SetCounter(m13, "shredded", 1);
            Events.CompleteObjective(m13, "obj_decide");
            Assert.IsTrue(Events.Find(m13).State.IsResolved());

            ServiceHub.State.BeginNight(2);
            Events.BeginNight(2);

            var runtime = Events.Find(m13);
            Assert.AreEqual(ManualEventState.Active, runtime.State, "there is a postcard every night");
            Assert.AreEqual(0, runtime.Counter("shredded"), "and it is not last night's postcard");
        }

        [Test]
        public void AOneOffEventDoesNotComeBackOnALaterNight()
        {
            BeginNightOne();
            WorkTheParcel();
            Events.CompleteObjective(M06, "obj_return_to_office");
            Assert.AreEqual(ManualEventState.ResolvedCorrect, Events.Find(M06).State);

            ServiceHub.State.BeginNight(2);
            Events.BeginNight(2);

            Assert.AreEqual(ManualEventState.ResolvedCorrect, Events.Find(M06).State,
                            "a parcel dealt with stays dealt with");
        }
    }
}
