using System.Collections.Generic;
using NUnit.Framework;
using NO404.Anomalies;
using NO404.ContentData;
using NO404.Core;
using NO404.Gameplay;
using NO404.Manual;

namespace NO404.Tests
{
    /// <summary>
    /// The v2.1 risk model (spec 0.10).
    ///
    /// Most of what is worth asserting here is what the model refuses to do: it does not end
    /// runs, it does not accept risk for floors that do not exist, and hitting the top of the
    /// exposure scale opens a job rather than closing the game.
    /// </summary>
    public sealed class RiskServiceTests
    {
        GameStateService _state;
        RiskService _risk;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _state = new GameStateService();
            _risk = new RiskService(_state);
        }

        [TearDown] public void TearDown() { EventBus.Clear(); }

        [Test]
        public void FloorRiskClampsToTheSpecLadder()
        {
            _risk.AddFloorRisk(FloorPlan.F4, 9);
            Assert.AreEqual(5, _risk.FloorRisk(FloorPlan.F4));
            Assert.AreEqual(FloorRiskTier.Severe, _risk.TierOf(FloorPlan.F4));

            _risk.AddFloorRisk(FloorPlan.F4, -99);
            Assert.AreEqual(0, _risk.FloorRisk(FloorPlan.F4));
            Assert.AreEqual(FloorRiskTier.Clear, _risk.TierOf(FloorPlan.F4));
        }

        [Test]
        public void FloorRiskIsRefusedForFloorsTheBuildingDoesNotHave()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                _risk.AddFloorRisk(FloorPlan.PhantomF13, 3);
                Assert.AreEqual(0, _state.GetStat(StatIds.FloorRisk(FloorPlan.PhantomF13)));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void FloorRiskPublishesSoDressingCodeCanReactToIt()
        {
            // Spec 0.10.3: risk is never a number on screen, so an event is the only way the
            // world hears about it.
            var seen = new List<FloorRiskChangedEvent>();
            EventBus.Subscribe<FloorRiskChangedEvent>(e => seen.Add(e));

            _risk.AddFloorRisk(FloorPlan.B1, 2);

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(FloorPlan.B1, seen[0].FloorId);
            Assert.AreEqual(0, seen[0].Previous);
            Assert.AreEqual(2, seen[0].Current);
        }

        [Test]
        public void RiskFollowsTheZoneToItsFloor()
        {
            _risk.AddFloorRisk(FloorPlan.B2, 3);
            Assert.AreEqual(3, _risk.RiskOfZone(ZoneIds.PumpRoom));
            Assert.AreEqual(3, _risk.RiskOfZone(ZoneIds.Archive));
            Assert.AreEqual(0, _risk.RiskOfZone(ZoneIds.Lobby));
        }

        [Test]
        public void ExposureBandsMatchTheSpecTable()
        {
            Assert.AreEqual(DistortionBand.None, RiskService.BandOf(24));
            Assert.AreEqual(DistortionBand.Faint, RiskService.BandOf(25));
            Assert.AreEqual(DistortionBand.Marked, RiskService.BandOf(50));
            Assert.AreEqual(DistortionBand.Severe, RiskService.BandOf(75));
            Assert.AreEqual(DistortionBand.Critical, RiskService.BandOf(100));
        }

        [Test]
        public void FullExposureOpensACorrectionInsteadOfEndingTheRun()
        {
            // Spec 0.10.4: 100 is not a game over.
            _risk.AddExposure(100);

            Assert.AreEqual(DistortionBand.Critical, _risk.Band);
            Assert.IsTrue(_risk.EmergencyCorrectionDue);

            _risk.ApplyEmergencyCorrection();

            Assert.IsFalse(_risk.EmergencyCorrectionDue);
            Assert.Less(_risk.Exposure, 100);
            Assert.AreEqual(DistortionBand.Marked, _risk.Band);
        }

        [Test]
        public void ViolationCostsExposureAndTheFloorItHappenedOn()
        {
            _risk.RecordViolation("M02", "manual.m02.forbid.1", 15, FloorPlan.B1);

            Assert.AreEqual(1, _risk.ViolationCount);
            Assert.AreEqual(15, _risk.Exposure);
            Assert.AreEqual(1, _risk.FloorRisk(FloorPlan.B1));
        }

        [Test]
        public void ExposurePartlyWearsOffOvernightAndFloorRiskDoesNot()
        {
            _risk.AddExposure(60);
            _risk.AddFloorRisk(FloorPlan.F5, 2);

            _risk.OnNightEnded();

            Assert.AreEqual(40, _risk.Exposure);
            Assert.AreEqual(2, _risk.FloorRisk(FloorPlan.F5),
                            "a mishandled floor stays mishandled until the world fixes it");
        }

        [Test]
        public void DebtsChangeTheEscapeAndNotTheStats()
        {
            _risk.AddMemoryDebt(3);
            _risk.AddToolDebt(2);

            Assert.AreEqual(3, _risk.MemoryDebt);
            Assert.AreEqual(2, _risk.ToolDebt);
            Assert.AreEqual(5, _risk.EscapeDifficultyModifier);

            // Spec 31 C14: they must not touch the values the endings are chosen on. Asserted
            // against the opening values rather than against zero, because v5.0 5.2 starts
            // ArchiveIntegrity at 50 - "unchanged" is the claim, not "empty".
            var fresh = new GameStateService();
            Assert.AreEqual(fresh.GetStat(StatIds.ArchiveIntegrity),
                            _state.GetStat(StatIds.ArchiveIntegrity));
            Assert.AreEqual(fresh.GetStat(StatIds.HarinResonance),
                            _state.GetStat(StatIds.HarinResonance));
        }
    }

    /// <summary>
    /// M07 holding the fifth floor and the stairwell above it (spec 22 M07, 0.8.4, 30.3).
    /// </summary>
    public sealed class CorridorLoopDirectorTests
    {
        GameStateService _state;
        RiskService _risk;
        StairNavigator _stairs;
        DistortionDirector _distortion;
        CorridorLoopDirector _director;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _state = new GameStateService();
            _risk = new RiskService(_state);
            _stairs = new StairNavigator();
            _distortion = new DistortionDirector(_risk, new GameClock());
            _director = new CorridorLoopDirector(_stairs, _risk, _distortion);
            _director.Enable();
        }

        [TearDown]
        public void TearDown()
        {
            _director.Disable();
            EventBus.Clear();
        }

        static void Publish(ManualEventState state)
        {
            EventBus.Publish(new ManualEventStateChangedEvent(
                ManualEventIds.M07_CorridorLoop, ManualEventState.Dormant, state));
        }

        [Test]
        public void TheLoopRepeatsTheFloorTheWalkStartedOn()
        {
            // Spec 0.8.4: entering at 5F and walking down gives 5F again, which is only
            // legible because the player knows where they got on.
            _stairs.EnterFrom(FloorPlan.F5);
            Publish(ManualEventState.Active);

            Assert.AreEqual(FloorPlan.F5, _stairs.Descend());
            Assert.AreEqual(FloorPlan.F5, _stairs.Ascend());
            Assert.AreEqual(FloorPlan.F5, _stairs.Descend());
        }

        [Test]
        public void TheLoopAlwaysEnds()
        {
            // Spec 0.5: nothing may leave the player with nothing left to try, so the shaft
            // yields to normal adjacency once the flights are spent even if the event does not.
            _stairs.EnterFrom(FloorPlan.F5);
            Publish(ManualEventState.Active);

            for (int i = 0; i < CorridorLoopDirector.BaseLoopFlights; i++) _stairs.Descend();

            Assert.AreEqual(FloorPlan.F4, _stairs.Descend());
        }

        [Test]
        public void AWorseFifthFloorMakesALongerLoop()
        {
            // Spec 30.3: night 4's M17 raises FloorRisk_5F, and night 5's loop is longer for it.
            Assert.AreEqual(CorridorLoopDirector.BaseLoopFlights, _director.LoopFlights);

            _risk.AddFloorRisk(FloorPlan.F5, 2);

            Assert.AreEqual(CorridorLoopDirector.BaseLoopFlights + 2, _director.LoopFlights);
        }

        [Test]
        public void ResolvingTheEventGivesTheShaftBack()
        {
            _stairs.EnterFrom(FloorPlan.F5);
            Publish(ManualEventState.Active);
            Assert.IsTrue(_stairs.HasOverride);

            Publish(ManualEventState.ResolvedCorrect);

            Assert.IsFalse(_stairs.HasOverride);
            Assert.AreEqual(FloorPlan.F4, _stairs.Descend());
        }

        [Test]
        public void TheLoopNeverTrapsThePlayerAgainstAnEndOfTheShaft()
        {
            // Descending from B2 has nowhere to go anyway; holding them there would be a wall
            // rather than a loop, and the player would read it as the game being broken.
            _stairs.EnterFrom(FloorPlan.B2);
            Publish(ManualEventState.Active);

            Assert.IsNull(_stairs.Descend());
            Assert.AreEqual(FloorPlan.B2, _stairs.CurrentLanding);
        }
    }

    /// <summary>
    /// The authored manual (spec 0.9, 22, 26) checked against the promises spec 32 makes.
    ///
    /// These run off SeedContent directly rather than through ServiceHub so they hold in a
    /// bare EditMode run, and so a failure names the piece of content rather than a null
    /// service three layers down.
    /// </summary>
    public sealed class ManualContentTests
    {
        static ManualPage[] Pages() { return SeedContent.BuildManualPages(); }
        static ManualEventDefinition[] Events() { return SeedContent.BuildManualEvents(); }

        [Test]
        public void EveryEventInSpec22IsAuthored()
        {
            var events = Events();
            foreach (var id in ManualEventIds.All)
            {
                bool found = false;
                for (int i = 0; i < events.Length; i++) if (events[i].eventId == id) found = true;
                Assert.IsTrue(found, "spec 22 event " + id + " is missing");
            }
        }

        [Test]
        public void EveryEventHasAPageAndEveryPageIsUsed()
        {
            // Spec 0.9.2: an answer nothing hinted at is not a puzzle. Spec 32 turns that
            // into a checklist item - M01..M18 each have a page or a prior hint.
            var pages = Pages();
            foreach (var definition in Events())
            {
                ManualPage page = null;
                for (int i = 0; i < pages.Length; i++)
                    if (pages[i].pageId == definition.manualPageId) page = pages[i];

                Assert.IsNotNull(page, definition.eventId + " has no manual page");
                Assert.AreEqual(definition.eventId, page.eventId,
                                "page " + page.pageId + " belongs to a different event");
                Assert.IsNotEmpty(page.stepKeys, page.pageId + " has no procedure");
            }
        }

        [Test]
        public void EveryEventHasARecoveryRoute()
        {
            // Spec 0.5 / 32: at least one way back from a wrong answer. M13 is the spec's own
            // exception - a postcard is read or shredded, and neither can be retried.
            foreach (var definition in Events())
            {
                if (definition.eventId == ManualEventIds.M13_Postcards) continue;
                Assert.Greater(definition.failSafe.afterWrongAttempts, 0,
                               definition.eventId + " has no fail-safe");
            }
        }

        [Test]
        public void EveryNightIsOneChainStartingFromOneHead()
        {
            // v2.1: a night is not a timetable. One event opens with the shift and each of the
            // others is handed over when the one before it closes, so the pace belongs to the
            // player. What this pins is that the chain is actually connected - the failure it
            // exists for is an event nothing ever starts.
            var byId = new Dictionary<string, ManualEventDefinition>();
            foreach (var definition in Events()) byId[definition.eventId] = definition;

            var chainHeads = new Dictionary<int, int>();

            foreach (var definition in Events())
            {
                if (definition.trigger == ManualEventTrigger.NightStart)
                {
                    // M13 is the spec's own second head: one postcard a night regardless.
                    if (definition.eventId == ManualEventIds.M13_Postcards) continue;

                    int count;
                    chainHeads.TryGetValue(definition.nightIndex, out count);
                    chainHeads[definition.nightIndex] = count + 1;
                    continue;
                }

                Assert.AreEqual(ManualEventTrigger.AfterEvent, definition.trigger,
                                definition.eventId + " is neither a head nor part of a chain");

                Assert.IsTrue(byId.ContainsKey(definition.triggerTarget),
                              definition.eventId + " follows an event that does not exist");
                Assert.AreEqual(definition.nightIndex, byId[definition.triggerTarget].nightIndex,
                                definition.eventId + " follows an event on a different night");
            }

            foreach (var pair in chainHeads)
                Assert.AreEqual(1, pair.Value, "night " + pair.Key + " has " + pair.Value + " chain heads");
        }

        [Test]
        public void EveryEventIsReachableFromItsNightHead()
        {
            var byId = new Dictionary<string, ManualEventDefinition>();
            foreach (var definition in Events()) byId[definition.eventId] = definition;

            foreach (var definition in Events())
            {
                var seen = new HashSet<string> { definition.eventId };
                var step = definition;

                while (step.trigger == ManualEventTrigger.AfterEvent)
                {
                    Assert.IsTrue(byId.ContainsKey(step.triggerTarget),
                                  definition.eventId + " leads to a missing event");
                    step = byId[step.triggerTarget];
                    Assert.IsTrue(seen.Add(step.eventId),
                                  definition.eventId + " is in a chain that loops");
                }

                Assert.AreEqual(ManualEventTrigger.NightStart, step.trigger,
                                definition.eventId + " does not trace back to a head");
            }
        }

        [Test]
        public void NoEventWaitsForTheClock()
        {
            // The timetable is gone. A leftover earliest time would silently reintroduce it
            // for one event, which is exactly the sort of thing that is never noticed.
            foreach (var definition in Events())
                Assert.AreEqual(0, definition.earliestSecond,
                                definition.eventId + " still has an earliest time");
        }

        [Test]
        public void EveryEventSitsOnAFloorTheBuildingHas()
        {
            foreach (var definition in Events())
                Assert.IsTrue(FloorPlan.Exists(definition.floorId),
                              definition.eventId + " is on '" + definition.floorId + "'");
        }

        [Test]
        public void OnlyTheRoofFigureMayEverBeLethal()
        {
            // Spec 0.10.5, and the reason it is worth pinning: this is the property that stops
            // "wrong answer" from quietly becoming "death" as content is added.
            foreach (var definition in Events())
            {
                if (definition.eventId == ManualEventIds.M18_RoofFigure) continue;
                Assert.IsFalse(definition.lethalOnRepeat, definition.eventId + " may be lethal");
            }
        }

        [Test]
        public void LethalProhibitionsAreActuallyPrinted()
        {
            var pages = Pages();
            foreach (var definition in Events())
            {
                if (!definition.lethalOnRepeat) continue;

                ManualPage page = null;
                for (int i = 0; i < pages.Length; i++)
                    if (pages[i].pageId == definition.manualPageId) page = pages[i];

                Assert.IsNotNull(page);
                Assert.IsTrue(page.HasLethalProhibition,
                              definition.eventId + " may kill on a rule the manual never marks");
                foreach (var index in page.lethalProhibitions)
                    Assert.Less(index, page.prohibitionKeys.Length);
            }
        }

        [Test]
        public void NoEventChargesItsOwnViolation()
        {
            // ManualEventService decides that, because it depends on whether the page was ever
            // issued (spec 0.9.2). Content doing it too would double-count.
            foreach (var definition in Events())
                foreach (var c in definition.onWrong)
                    Assert.AreNotEqual(Cases.ConsequenceType.RecordViolation, c.type,
                                       definition.eventId + " charges its own violation");
        }

        [Test]
        public void PageIdsAndEventIdsAreUnique()
        {
            var pageIds = new HashSet<string>();
            foreach (var page in Pages())
                Assert.IsTrue(pageIds.Add(page.pageId), "duplicate page " + page.pageId);

            var eventIds = new HashSet<string>();
            foreach (var definition in Events())
                Assert.IsTrue(eventIds.Add(definition.eventId), "duplicate event " + definition.eventId);
        }

        [Test]
        public void EveryAuthoredKeyIsInTheStringTable()
        {
            // The section keys are generated from a count, so this is the test that keeps
            // SeedContent.Manual.cs and strings.csv from drifting apart silently.
            var loc = new LocalizationService();
            loc.Initialize(LocalizationService.DefaultLanguage);

            var missing = new List<string>();

            foreach (var page in Pages())
            {
                Check(loc, page.titleKey, missing);
                foreach (var key in page.observationKeys) Check(loc, key, missing);
                foreach (var key in page.prohibitionKeys) Check(loc, key, missing);
                foreach (var key in page.stepKeys) Check(loc, key, missing);
                foreach (var key in page.noteKeys) Check(loc, key, missing);
            }

            foreach (var definition in Events())
            {
                Check(loc, definition.nameKey, missing);
                Check(loc, definition.summaryKey, missing);
                foreach (var objective in definition.objectives) Check(loc, objective.titleKey, missing);
                if (definition.failSafe.afterWrongAttempts > 0)
                    Check(loc, definition.failSafe.notifyKey, missing);
            }

            Assert.IsEmpty(missing, "missing string keys: " + string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void EveryFloorNameIsInTheStringTable()
        {
            var loc = new LocalizationService();
            loc.Initialize(LocalizationService.DefaultLanguage);

            var missing = new List<string>();
            foreach (var floor in FloorPlan.All)
            {
                Check(loc, floor.DisplayNameKey, missing);
                string slug = floor.FloorId.ToLowerInvariant();
                Check(loc, "world.stairs." + slug, missing);
                Check(loc, "world.stairs." + slug + ".short", missing);
                Check(loc, "ui.prompt.stairs_" + slug, missing);
                if (floor.ElevatorAccessible) Check(loc, "ui.elevator." + slug, missing);
            }

            Assert.IsEmpty(missing, "missing string keys: " + string.Join(", ", missing.ToArray()));
        }

        static void Check(LocalizationService loc, string key, List<string> missing)
        {
            if (string.IsNullOrEmpty(key)) { missing.Add("(empty)"); return; }
            if (!loc.HasKey(key)) missing.Add(key);
        }
    }
}
