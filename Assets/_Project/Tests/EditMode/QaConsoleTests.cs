using NUnit.Framework;
using UnityEngine;
using NO404.Anomalies;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// The developer console commands spec 30.2 needs (v2.1 spec 30.2 paths 1, 2, 8, 9, 10).
    ///
    /// Four of the ten paths every anomaly has to survive are states rather than actions - the
    /// rule read beforehand or not read at all, exposure past 75, a floor already at 4, the
    /// accessibility hints on. A tester cannot reach those by playing well or badly; they have
    /// to be put there.
    ///
    /// The reason these commands exist at all rather than the tester using stat.set is the
    /// first test below, and it is worth reading before touching any of this.
    /// </summary>
    public sealed class QaConsoleTests
    {
        int _savedHintMode;

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
            _savedHintMode = ServiceHub.Settings.Current.hintMode;
        }

        [TearDown]
        public void TearDown()
        {
            ServiceHub.Settings.Current.hintMode = _savedHintMode;
            EventBus.Clear();
        }

        // ---- why these commands exist --------------------------------------

        [Test]
        public void SettingTheCounterDirectlyChangesTheNumberAndNothingElse()
        {
            // stat.set writes straight into GameStateService, so no band changes hands and no
            // floor is told it got worse. A tester who used it would set the risk to 4, walk
            // the corridor, find it spotless and report the dressing as broken - and the
            // dressing would be working perfectly.
            //
            // This is pinned rather than fixed on purpose: stat.set is the raw poke and should
            // stay raw. What it must not be is the documented way to reach these states.
            int risks = 0;
            EventBus.Subscribe<FloorRiskChangedEvent>(evt => risks++);

            DevConsole.Execute("stat.set " + StatIds.FloorRisk(FloorPlan.F5) + " 4");

            Assert.AreEqual(4, ServiceHub.State.GetStat(StatIds.FloorRisk(FloorPlan.F5)),
                            "the number does move");
            Assert.AreEqual(0, risks, "and nothing in the building hears about it");
        }

        [Test]
        public void TheRiskCommandGoesThroughTheServiceSoTheBuildingHearsAboutIt()
        {
            int risks = 0;
            EventBus.Subscribe<FloorRiskChangedEvent>(evt => risks++);

            var reply = DevConsole.Execute("risk.floor F5 4");

            Assert.AreEqual(4, ServiceHub.Risk.FloorRisk(FloorPlan.F5));
            Assert.AreEqual(FloorRiskTier.Hostile, ServiceHub.Risk.TierOf(FloorPlan.F5));
            Assert.Greater(risks, 0, "the floor has to be told");
            StringAssert.Contains("Hostile", reply);
        }

        [Test]
        public void AZoneBuiltOntoAnAlreadyRuinedFloorComesUpDressed()
        {
            // The case the streaming makes and the event never covers: the risk was raised
            // three rooms ago, and this corridor is only being built now. There is no
            // FloorRiskChangedEvent left to catch, so the dressing has to read the floor for
            // itself when it is told which floor it is on.
            DevConsole.Execute("risk.floor F5 4");

            var zone = new GameObject("ZoneRoot");
            try
            {
                var dressing = zone.AddComponent<FloorDressing>().Setup(ZoneIds.Floor05, FloorPlan.F5);
                Assert.AreEqual(FloorRiskTier.Hostile, dressing.AppliedTier,
                                "a corridor built onto a hostile floor is already a hostile corridor");

                int props = 0;
                foreach (Transform child in zone.transform)
                    if (child.name.StartsWith("Risk") && child.gameObject.activeSelf) props++;

                Assert.Greater(props, 0, "and it should look like one");
            }
            finally
            {
                Object.DestroyImmediate(zone);
            }
        }

        [Test]
        public void TheExposureCommandMovesTheBandRatherThanJustTheNumber()
        {
            int bands = 0;
            EventBus.Subscribe<DistortionBandChangedEvent>(evt => bands++);

            var reply = DevConsole.Execute("risk.exposure 80");

            Assert.AreEqual(80, ServiceHub.Risk.Exposure);
            Assert.AreEqual(DistortionBand.Severe, ServiceHub.Risk.Band);
            Assert.Greater(bands, 0);
            StringAssert.Contains("Severe", reply);

            // And spec 30.2 path 8 is now genuinely set up: the effects are live.
            Assert.IsTrue(ServiceHub.Distortion.CluesAreHardToTellApart);
            Assert.IsTrue(ServiceHub.Distortion.MayMisreadFloor);
        }

        [Test]
        public void ExposureCanBeBroughtBackDownAgain()
        {
            // A tester walking the bands needs to go both ways without restarting the shift.
            DevConsole.Execute("risk.exposure 80");
            DevConsole.Execute("risk.exposure 0");

            Assert.AreEqual(0, ServiceHub.Risk.Exposure);
            Assert.AreEqual(DistortionBand.None, ServiceHub.Risk.Band);
        }

        [Test]
        public void AFloorTheBuildingDoesNotHaveIsRefused()
        {
            // Spec 0.7.1. The console is the easiest place in the game to invent a thirteenth
            // floor by accident, so it is the place that has to say no.
            var reply = DevConsole.Execute("risk.floor F13 3");
            StringAssert.Contains("no floor", reply);
            Assert.AreEqual(0, ServiceHub.State.GetStat(StatIds.FloorRisk("F13")));
        }

        // ---- paths 1 and 2: with the rule, and without it --------------------

        [Test]
        public void TheBinderCanBeFilledAndEmptied()
        {
            DevConsole.Execute("manual.page all");
            for (int i = 0; i < ManualEventIds.All.Length; i++)
                Assert.IsTrue(ServiceHub.Manual.HasPageFor(ManualEventIds.All[i]),
                              "path 1: " + ManualEventIds.All[i] + " should have been read first");

            DevConsole.Execute("manual.forget");

            // Path 2 does not mean an empty drawer. Spec 0.9.1 puts the general rules there
            // before the first shift, and a state the game never has is not worth testing in.
            // What it means is that no anomaly's own page has been issued.
            for (int i = 0; i < ManualEventIds.All.Length; i++)
                Assert.IsFalse(ServiceHub.Manual.HasPageFor(ManualEventIds.All[i]),
                               "path 2: " + ManualEventIds.All[i] + " should be unread");
        }

        [Test]
        public void ASingleRuleCanBeIssuedOnItsOwn()
        {
            DevConsole.Execute("manual.forget");
            int baseline = ServiceHub.Manual.UnlockedCount;

            DevConsole.Execute("manual.page " + ManualEventIds.M07_CorridorLoop);

            Assert.IsTrue(ServiceHub.Manual.HasPageFor(ManualEventIds.M07_CorridorLoop));
            Assert.AreEqual(baseline + 1, ServiceHub.Manual.UnlockedCount,
                            "one rule, not the binder");
        }

        // ---- driving an anomaly from a cold start ----------------------------

        [Test]
        public void AnAnomalyCanBeStartedAndWorkedFromTheConsole()
        {
            ServiceHub.State.BeginNight(1);
            ServiceHub.ManualEvents.BeginNight(1);

            const string m14 = ManualEventIds.M14_Treadmills;
            Assert.AreEqual(ManualEventState.Dormant, ServiceHub.ManualEvents.Find(m14).State);

            DevConsole.Execute("manual.start " + m14);
            Assert.AreEqual(ManualEventState.Active, ServiceHub.ManualEvents.Find(m14).State);

            DevConsole.Execute("manual.counter " + m14 + " machinesStopped 3");
            Assert.AreEqual(3, ServiceHub.ManualEvents.Counter(m14, "machinesStopped"));

            DevConsole.Execute("manual.step " + m14 + " obj_announce");
            Assert.IsTrue(ServiceHub.ManualEvents.Find(m14).IsComplete("obj_announce"));
        }

        [Test]
        public void TheListShowsWhatIsRunningAndWhetherItsRuleWasIssued()
        {
            ServiceHub.State.BeginNight(1);
            ServiceHub.ManualEvents.BeginNight(1);
            DevConsole.Execute("manual.forget");

            var reply = DevConsole.Execute("manual.list");
            StringAssert.Contains(ManualEventIds.M06_LostParcel, reply);
            StringAssert.Contains("no page", reply);

            DevConsole.Execute("manual.page all");
            StringAssert.Contains("page issued", DevConsole.Execute("manual.list"));
        }

        // ---- the machines ----------------------------------------------------

        [Test]
        public void EveryMachineCanBeWokenWithoutPlayingToItsNight()
        {
            DevConsole.Execute("tool.wake all");

            for (int i = 0; i < ManualEventIds.Tools.Length; i++)
                Assert.IsTrue(ServiceHub.AnomalyTools.IsAwake(ManualEventIds.Tools[i]),
                              ManualEventIds.Tools[i] + " should be awake");
        }

        // ---- path 10: the accessibility hints --------------------------------

        [Test]
        public void TheHintModeCanBeTurnedAllTheWayUp()
        {
            DevConsole.Execute("hints.set always");
            Assert.AreEqual(HintMode.Always, DifficultyProfile.Hints);

            DevConsole.Execute("hints.set off");
            Assert.AreEqual(HintMode.Off, DifficultyProfile.Hints);
        }

        [Test]
        public void AnUnknownHintModeSaysSoRatherThanPickingOne()
        {
            DevConsole.Execute("hints.set delayed");
            var reply = DevConsole.Execute("hints.set sometimes");

            StringAssert.Contains("off|delayed|always", reply);
            Assert.AreEqual(HintMode.Delayed, DifficultyProfile.Hints, "and leaves it alone");
        }

        // ---- the readout -----------------------------------------------------

        [Test]
        public void TheRiskReadoutNamesEveryFloorThatIsNotClear()
        {
            DevConsole.Execute("risk.exposure 60");
            DevConsole.Execute("risk.floor F4 3");
            DevConsole.Execute("risk.floor B2 1");

            var reply = DevConsole.Execute("risk.show");

            StringAssert.Contains("Marked", reply);
            StringAssert.Contains(FloorPlan.F4, reply);
            StringAssert.Contains(FloorPlan.B2, reply);
            Assert.IsFalse(reply.Contains(FloorPlan.F2), "a clear floor is not worth a line");
        }

        [Test]
        public void EveryQaCommandIsInTheHelpText()
        {
            // A command nobody can find is a command that does not exist. The four paths this
            // whole file is for are the ones a tester is least likely to guess at.
            var help = DevConsole.Execute("help");

            foreach (var command in new[]
            {
                "risk.exposure", "risk.floor", "risk.show",
                "manual.list", "manual.start", "manual.page", "manual.forget",
                "manual.counter", "manual.step", "tool.wake", "hints.set"
            })
                StringAssert.Contains(command, help);
        }
    }
}
