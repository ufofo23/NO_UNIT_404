using NUnit.Framework;
using NO404.Core;
using NO404.Facility;
using NO404.Pressure;
using NO404.Save;

namespace NO404.Tests
{
    /// <summary>
    /// GDD 15.4. The ladder is the one system in the game that acts without being asked, so
    /// it is also the one that must never be able to act past the two limits it inherits:
    /// it cannot kill, and it cannot close off the truth ending.
    /// </summary>
    public sealed class NightPressureTests
    {
        [SetUp] public void SetUp() { EventBus.Clear(); }
        [TearDown] public void TearDown() { EventBus.Clear(); }

        static NightPressureService OnNight(int nightIndex)
        {
            var service = new NightPressureService();
            service.BeginNight(nightIndex);
            return service;
        }

        [Test]
        public void StagesMatchTheirThresholds()
        {
            Assert.AreEqual(PressureStage.Calm, NightPressureService.StageFor(0));
            Assert.AreEqual(PressureStage.Calm, NightPressureService.StageFor(NightPressureService.NearAt - 1));
            Assert.AreEqual(PressureStage.Near, NightPressureService.StageFor(NightPressureService.NearAt));
            Assert.AreEqual(PressureStage.AtTheDoor, NightPressureService.StageFor(NightPressureService.AtTheDoorAt));
            Assert.AreEqual(PressureStage.Inside, NightPressureService.StageFor(NightPressureService.InsideAt));
            Assert.AreEqual(PressureStage.Confrontation, NightPressureService.StageFor(NightPressureService.Max));
        }

        /// <summary>
        /// GDD 9.1 / 15.4. There is no free shift any more. The prologue used to switch the
        /// ladder off at the source, so the game's first night was one on which nothing the
        /// player did could reach them - which is exactly what the cold open replaced.
        /// </summary>
        [Test]
        public void TheFirstNightOfTheGameIsNotFree()
        {
            var pressure = OnNight(1);

            Assert.IsFalse(pressure.Suppressed, "no shift suppresses the ladder any more");

            pressure.TickSeconds(3600);
            Assert.AreEqual(6, pressure.Value, "the building drifts from the first game hour");

            pressure.NoteWrongAdmit("vis_courier_late");
            Assert.AreEqual(6 + NightPressureService.WrongAdmit, pressure.Value,
                            "the cold open's wrong caller costs what a wrong caller costs");
        }

        [Test]
        public void DriftAloneClimbsFasterOnLaterNights()
        {
            var early = OnNight(1);
            var late = OnNight(6);

            early.TickSeconds(3600);
            late.TickSeconds(3600);

            Assert.AreEqual(6, early.Value);
            Assert.AreEqual(16, late.Value);
        }

        /// <summary>
        /// GDD 15.4's drift table, in isolation - no traffic, no callers, no cases, which is
        /// not a night anyone plays. It pins the one thing drift alone is meant to guarantee:
        /// the last night gets there on its own, and only once. The real mixed-source curve
        /// lives in GDD 25.4 and is measured by Tools > NO404 > Data > Simulate Nights.
        /// </summary>
        [Test]
        public void DriftAloneConfrontsOnceOnTheLastNight()
        {
            var pressure = OnNight(6);

            // A full shift is 22:00 -> 06:00, eight game hours.
            pressure.TickSeconds(8 * 3600);

            Assert.AreEqual(1, pressure.ConfrontationCount,
                "night six spent doing nothing has to arrive somewhere - and only once");
        }

        /// <summary>
        /// GDD 15.4: the clock alone must not summon the knock on night one.
        ///
        /// The margin used to be wide - 32 against a door at 50 - and is now deliberately thin.
        /// Night 1's drift was raised so that a normal first playthrough meets GDD 15.6 on the
        /// night v1.5 wanted it on, and 48 is the most that can be spent on the clock while
        /// still leaving the knock as something the player's own neglect has to pay for. What
        /// is being pinned here is the boundary, not the number: doing nothing all shift ends
        /// one stage below the door, and one missed sighting on top of it does not.
        /// </summary>
        [Test]
        public void DriftAloneLeavesTheFirstNightBelowTheDoor()
        {
            var pressure = OnNight(1);
            pressure.TickSeconds(8 * 3600);

            Assert.AreEqual(48, pressure.Value);
            Assert.Less(pressure.Value, NightPressureService.AtTheDoorAt,
                        "the clock alone must never knock on night one");
            Assert.AreEqual(PressureStage.Near, pressure.Stage);
            Assert.AreEqual(0, pressure.ConfrontationCount);
        }

        /// <summary>
        /// The other half of the same boundary: night one is meant to reach the door once the
        /// player has actually left something undone. A single unfiled sighting is the
        /// smallest amount of neglect there is, and it has to be enough.
        /// </summary>
        [Test]
        public void OneMissedSightingKnocksOnTheFirstNight()
        {
            var pressure = OnNight(1);
            pressure.TickSeconds(8 * 3600);
            pressure.NoteSightingExpired();

            Assert.GreaterOrEqual(pressure.Value, NightPressureService.AtTheDoorAt);
            Assert.AreEqual(PressureStage.AtTheDoor, pressure.Stage,
                            "night one has to be able to reach the knock (GDD 15.6)");
        }

        [Test]
        public void DoingTheJobPushesTheNightBack()
        {
            var pressure = OnNight(3);

            pressure.NoteDeadlineMissed();
            pressure.NoteDeadlineMissed();
            int raised = pressure.Value;
            Assert.Greater(raised, 0);

            pressure.NoteCaseClosedWell();
            Assert.Less(pressure.Value, raised);
        }

        [Test]
        public void ReliefNeverGoesBelowZero()
        {
            var pressure = OnNight(4);

            for (int i = 0; i < 20; i++) pressure.NoteVisitorCorrect();

            Assert.AreEqual(0, pressure.Value);
            Assert.AreEqual(PressureStage.Calm, pressure.Stage);
        }

        /// <summary>
        /// GDD 15.4. An intruder used to cost per game minute, which the night simulator showed
        /// was the single dominant term in the whole ladder: three of them out-weighed every
        /// judgement the player made all shift. The cost is now attached to the crossings they
        /// actually make, which are the thing the player can see and name.
        /// </summary>
        /// <summary>
        /// GDD 9.1 / 15.4. The cold open's third caller, nine minutes into the game, is the
        /// first thing that can be got wrong - and admitting them runs the whole intruder
        /// loop: a body in the building that walks the cameras and can be named. It used to
        /// run that loop for free on night 0. It does not run for free any more.
        /// </summary>
        [Test]
        public void TheColdOpensWrongAdmitPutsSomeoneInsideAndCharges()
        {
            var pressure = OnNight(1);

            pressure.NoteWrongAdmit("vis_courier_late");

            Assert.AreEqual(1, pressure.IntruderCount, "the wrong answer becomes a body");
            Assert.AreEqual(NightPressureService.WrongAdmit, pressure.Value,
                            "and the first night of the game bills it like any other");

            int admitted = pressure.Value;
            pressure.NoteIntruderMissed();
            Assert.AreEqual(admitted + NightPressureService.IntruderMissed, pressure.Value,
                            "every crossing nobody files costs again");

            Assert.IsTrue(pressure.ClearIntruder("vis_courier_late"), "and can still be found");
            Assert.AreEqual(0, pressure.IntruderCount);
        }

        [Test]
        public void AnIntruderCostsPerCrossingNotPerMinute()
        {
            var pressure = OnNight(2);

            pressure.NoteWrongAdmit("vis_courier_late");
            Assert.AreEqual(1, pressure.IntruderCount);

            int admitted = pressure.Value;
            pressure.TickSeconds(600);   // ten game minutes of them walking around
            int drifted = pressure.Value;

            pressure.NoteIntruderMissed();

            Assert.AreEqual(NightPressureService.IntruderMissed, pressure.Value - drifted,
                            "a missed crossing is what costs");
            Assert.Less(drifted - admitted, NightPressureService.IntruderMissed,
                        "and time alone must not out-cost the crossing");
        }

        [Test]
        public void FindingTheIntruderEndsThem()
        {
            var pressure = OnNight(2);
            pressure.NoteWrongAdmit("vis_courier_late");

            Assert.IsTrue(pressure.ClearOldestIntruder());
            Assert.AreEqual(0, pressure.IntruderCount);
            Assert.IsFalse(pressure.ClearOldestIntruder());
        }

        [Test]
        public void TheSameIntruderIsOnlyCountedOnce()
        {
            var pressure = OnNight(2);

            pressure.NoteWrongAdmit("vis_junho_second");
            pressure.NoteWrongAdmit("vis_junho_second");

            Assert.AreEqual(1, pressure.IntruderCount);
        }

        [Test]
        public void AConfrontationCostsAndThenLetsGo()
        {
            var pressure = OnNight(5);

            pressure.Add(NightPressureService.Max, "test");

            Assert.AreEqual(1, pressure.ConfrontationCount);
            Assert.AreEqual(NightPressureService.ConfrontationResetsTo, pressure.Value,
                            "GDD 15.2: the top of the ladder is a cost, not an end state");
            Assert.AreEqual(0, pressure.IntruderCount);
            Assert.AreNotEqual(PressureStage.Confrontation, pressure.Stage);
        }

        [Test]
        public void ANewNightStartsFromNothing()
        {
            var pressure = OnNight(4);
            pressure.NoteWrongAdmit("vis_test");
            pressure.NoteDeadlineMissed();

            pressure.BeginNight(5);

            Assert.AreEqual(0, pressure.Value);
            Assert.AreEqual(0, pressure.IntruderCount);
            Assert.IsFalse(pressure.OfficeDoorLocked);
        }

        [Test]
        public void LoadingRestoresTheLadderWhereItWas()
        {
            var pressure = new NightPressureService();
            pressure.LoadFrom(62, 4, new[] { "vis_a", "vis_b" });

            Assert.AreEqual(62, pressure.Value);
            Assert.AreEqual(PressureStage.AtTheDoor, pressure.Stage);
            Assert.AreEqual(2, pressure.IntruderCount);
        }
    }

    /// <summary>
    /// GDD 15.4 / 15.6. The bolt has to hold the line without being the whole game: at minus
    /// one per game minute it was worth 480 points across a shift and bolting at 22:00 simply
    /// won. It is now priced per hour, just above the worst night's drift.
    /// </summary>
    public sealed class OfficeBoltTests
    {
        [SetUp] public void SetUp() { EventBus.Clear(); }
        [TearDown] public void TearDown() { EventBus.Clear(); }

        [Test]
        public void BoltingOutpacesTheWorstNightsDriftButOnlyJust()
        {
            Assert.Greater(-NightPressureService.LockedDoorReliefPerGameHour, 16,
                           "bolting has to be able to hold the line on night six");
            Assert.Less(-NightPressureService.LockedDoorReliefPerGameHour, 60,
                        "and must never erase a night of neglect on its own");
        }

        [Test]
        public void AnUnboltedDoorRelievesNothing()
        {
            var pressure = new NightPressureService();
            pressure.BeginNight(6);
            pressure.Add(50, "test");

            int before = pressure.Value;
            pressure.TickSeconds(3600);

            Assert.Greater(pressure.Value, before, "an open door lets the night keep coming");
        }

        [Test]
        public void ABoltedDoorTurnsTheHourAround()
        {
            var pressure = new NightPressureService();
            pressure.BeginNight(6);
            pressure.Add(50, "test");

            int before = pressure.Value;
            pressure.OfficeDoorLocked = true;
            pressure.TickSeconds(3600);

            Assert.Less(pressure.Value, before, "bolted, an hour has to be a net retreat");
        }
    }

    /// <summary>GDD 15.5. Watching has to cost something, and running out has to be survivable.</summary>
    public sealed class NightPowerTests
    {
        [SetUp] public void SetUp() { EventBus.Clear(); }
        [TearDown] public void TearDown() { EventBus.Clear(); }

        static NightPowerService OnNight(int nightIndex)
        {
            var service = new NightPowerService();
            service.BeginNight(nightIndex);
            return service;
        }

        [Test]
        public void ANightStartsWithAFullReserve()
        {
            var power = OnNight(2);
            Assert.AreEqual(NightPowerService.Full, power.Reserve);
            Assert.AreEqual(100, power.ReservePercent);
            Assert.IsFalse(power.IsLow);
        }

        /// <summary>
        /// GDD 9.1 / 15.5. Night 1 is the first night of the game now that the prologue is
        /// gone, and it opens under the warning line rather than approaching it: the shortage
        /// is the opening, not a slope the shift works its way down over five hours.
        ///
        /// This used to open at 62% and assert the opposite - deliberately above the line so
        /// that 22:00 was not an alarm. That was the right call for a shift that opened calm.
        /// This one opens on twelve dead cameras, and the reserve has to point at the same
        /// door they do, which is the basement board.
        /// </summary>
        [Test]
        public void NightOneOpensAlreadyUnderTheWarningLine()
        {
            var power = OnNight(1);

            Assert.LessOrEqual(power.Reserve, NightPowerService.LowReserve);
            Assert.IsTrue(power.IsLow, "the cold open's shortage is not a slope, it is the opening");
            Assert.Greater(power.Reserve, NightPowerService.CriticalReserve,
                           "the reserve must not also be killing channels - the blackout is scripted");
        }

        /// <summary>
        /// The shift still has to be finishable. Both board throws are what pays for it now
        /// (GDD 15.5 recovery: 2 x +30%), where 62% needed only one - which is the trade the
        /// cold open makes: the first trip is compulsory and early instead of optional and late.
        /// </summary>
        [Test]
        public void NightOneSurvivesOnItsTwoBreakerTrips()
        {
            var power = OnNight(1);

            int drawn = power.CurrentDrawPerHour() * 8;
            int budget = power.Reserve + NightPowerService.BreakerResetAmount * NightPowerService.BreakerResetsPerNight;

            Assert.Greater(budget, drawn, "night 1 cannot be survived even on both breaker trips");
        }

        /// <summary>Every other night is untouched, so GDD 25.4's measurements still hold.</summary>
        [Test]
        public void OnlyNightOneOpensPartCharged()
        {
            // Index 0 tracks night 1 rather than Full: night 0 is no longer a shift the game
            // can reach (GDD 9.1), and a dev-console jump to it should behave like night 1
            // rather than like a shift that was never designed.
            Assert.AreEqual(NightPowerService.StartingReserveFor(1), NightPowerService.StartingReserveFor(0));

            for (int night = 2; night <= 6; night++)
                Assert.AreEqual(NightPowerService.Full, NightPowerService.StartingReserveFor(night),
                                "night " + night + " no longer opens full");
        }

        [Test]
        public void WatchingOneChannelCostsMoreThanTheWall()
        {
            var power = OnNight(1);

            // The service reads the player tracker, which EditMode has no instance of, so the
            // constants themselves are what this asserts: the expensive view is the one a
            // report can be filed from.
            Assert.Greater(NightPowerService.CctvSingleDraw, NightPowerService.CctvGridDraw);
            Assert.AreEqual(NightPowerService.BaseDraw + NightPowerService.CorridorLightsDraw,
                            power.CurrentDrawPerHour());
        }

        [Test]
        public void TurningTheCorridorLightsOffSavesDraw()
        {
            var power = OnNight(1);
            int lit = power.CurrentDrawPerHour();

            power.SetCorridorLights(false);

            Assert.Less(power.CurrentDrawPerHour(), lit);
        }

        /// <summary>
        /// GDD 15.5 / 9.6. Night five is scarce by script - three circuits out of five - and
        /// the simulator showed that charging the reserve extra on top of that ended the night
        /// at 10% with the player watching nothing at all. One shortage, billed once.
        /// </summary>
        [Test]
        public void TheOverloadedNightIsNotBilledTwice()
        {
            var ordinary = OnNight(1);
            var blackout = OnNight(NightPowerService.BlackoutNight);

            Assert.AreEqual(ordinary.CurrentDrawPerHour(), blackout.CurrentDrawPerHour());
        }

        [Test]
        public void DrainingPastTheLineFlagsLowAndThenCritical()
        {
            var power = OnNight(2);

            power.Drain(NightPowerService.Full - NightPowerService.LowReserve, "test");
            Assert.IsTrue(power.IsLow);
            Assert.IsFalse(power.IsCritical);

            power.Drain(NightPowerService.LowReserve - NightPowerService.CriticalReserve, "test");
            Assert.IsTrue(power.IsCritical);
        }

        [Test]
        public void TheReserveNeverGoesNegative()
        {
            var power = OnNight(2);
            power.Drain(NightPowerService.Full * 4, "test");

            Assert.AreEqual(0, power.Reserve);
            Assert.IsTrue(power.IsDead);
        }

        [Test]
        public void TheBreakerPutsPowerBackTwicePerNight()
        {
            var power = OnNight(3);
            power.Drain(NightPowerService.Full - 100, "test");

            Assert.IsTrue(power.TryResetBreaker());
            Assert.AreEqual(100 + NightPowerService.BreakerResetAmount, power.Reserve);

            // The cooldown is measured against the clock, which EditMode does not run, so the
            // second reset is only blocked once the budget itself is spent.
            Assert.AreEqual(NightPowerService.BreakerResetsPerNight - 1, power.BreakerResetsRemaining);
        }

        [Test]
        public void TheBreakerCannotOverfillTheReserve()
        {
            var power = OnNight(3);
            power.Drain(50, "test");
            power.TryResetBreaker();

            Assert.AreEqual(NightPowerService.Full, power.Reserve);
        }

        [Test]
        public void ANewNightRefillsAndReleasesTheBudget()
        {
            var power = OnNight(3);
            power.Drain(NightPowerService.Full / 2, "test");
            power.TryResetBreaker();
            power.SetCorridorLights(false);

            power.BeginNight(4);

            Assert.AreEqual(NightPowerService.Full, power.Reserve);
            Assert.AreEqual(NightPowerService.BreakerResetsPerNight, power.BreakerResetsRemaining);
            Assert.IsTrue(power.CorridorLightsOn);
        }

        [Test]
        public void LoadingRestoresTheReserveAndTheSpentBudget()
        {
            var power = new NightPowerService();
            power.LoadFrom(180, false, 1);

            Assert.AreEqual(180, power.Reserve);
            Assert.IsFalse(power.CorridorLightsOn);
            Assert.AreEqual(1, power.BreakerResetsRemaining);
            Assert.IsTrue(power.IsLow);
        }
    }

    public sealed class PressureSaveMigrationTests
    {
        [Test]
        public void AScheduleThreeSaveGainsACalmCorridorAndAFullReserve()
        {
            var data = new SaveData { schemaVersion = 3, nightIndex = 4 };
            data.accessLevels.Add((int)AccessLevel.Staff1);

            Assert.IsTrue(SaveMigrations.TryMigrate(data));

            Assert.AreEqual(SaveData.CurrentSchemaVersion, data.schemaVersion);
            Assert.AreEqual(0, data.pressure);
            Assert.AreEqual(4, data.pressureNightIndex);
            Assert.IsNotNull(data.pressureIntruders);
            Assert.AreEqual(NightPowerService.Full, data.powerReserve);
            Assert.IsTrue(data.corridorLightsOn);
        }
    }
}
