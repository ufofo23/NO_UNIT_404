using NUnit.Framework;
using UnityEngine;
using NO404.Anomalies;
using NO404.ContentData;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Tests
{
    /// <summary>
    /// What the risk model actually does to the world (v2.1 spec 0.10.3, 0.10.4).
    ///
    /// Both counters were fully modelled and read by nothing: FloorRiskChangedEvent had no
    /// subscribers and DistortionBandChangedEvent had none either, so a caretaker at exposure
    /// 74 with a floor at risk 4 played the same game as one who had done everything right.
    /// These pin the two things that must stay true of the dressing - that it appears, and
    /// that it never changes an answer.
    /// </summary>
    public sealed class DistortionDressingTests
    {
        GameStateService _state;
        RiskService _risk;
        DistortionDirector _distortion;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _state = new GameStateService();
            _risk = new RiskService(_state);
            _distortion = new DistortionDirector(_risk, new GameClock());
        }

        [TearDown] public void TearDown() { EventBus.Clear(); }

        void SetExposure(int value)
        {
            _state.AddStat(StatIds.DistortionExposure, value - _risk.Exposure, "test");
        }

        [Test]
        public void ACleanRunLooksClean()
        {
            SetExposure(0);
            Assert.AreEqual(DistortionBand.None, _distortion.Band);
            Assert.AreEqual(0f, _distortion.GlyphCorruptionChance);
            Assert.IsFalse(_distortion.MayMisreadFloor);
            Assert.IsFalse(_distortion.MayShowFalseTaskCards);
            Assert.IsFalse(_distortion.CluesAreHardToTellApart);
            Assert.AreEqual(0, _distortion.ExtraLoopFlights);
        }

        [Test]
        public void EachBandTurnsOnWhatSpec0104SaysItDoes()
        {
            SetExposure(30);   // 25-49
            Assert.AreEqual(DistortionBand.Faint, _distortion.Band);
            Assert.Greater(_distortion.GlyphCorruptionChance, 0f);
            Assert.IsFalse(_distortion.MayMisreadFloor, "wrong floor readouts start at 50");

            SetExposure(60);   // 50-74
            Assert.AreEqual(DistortionBand.Marked, _distortion.Band);
            Assert.IsTrue(_distortion.MayMisreadFloor);
            Assert.IsTrue(_distortion.MayShowFalseTaskCards);
            Assert.Greater(_distortion.ExtraLoopFlights, 0, "stairs should repeat more often");
            Assert.IsFalse(_distortion.CluesAreHardToTellApart, "that band starts at 75");

            SetExposure(80);   // 75-99
            Assert.AreEqual(DistortionBand.Severe, _distortion.Band);
            Assert.IsTrue(_distortion.CluesAreHardToTellApart);
        }

        [Test]
        public void ItGetsWorseRatherThanDifferent()
        {
            // Every effect is cumulative, so nothing the caretaker had learned to live with
            // quietly switches off as the exposure climbs.
            SetExposure(30);
            float faint = _distortion.GlyphCorruptionChance;

            SetExposure(60);
            Assert.Greater(_distortion.GlyphCorruptionChance, faint);

            SetExposure(80);
            Assert.Greater(_distortion.GlyphCorruptionChance, faint);
            Assert.IsTrue(_distortion.MayMisreadFloor, "a Severe run still misreads floors");
        }

        [Test]
        public void ACorruptedGlyphIsOneCharacterAndNeverAWrongNumberToActOn()
        {
            SetExposure(99);
            const string reading = "04:12";

            // Spec 0.10.4 / 32: presentation may be doubted, answers may not be changed. The
            // shape of that promise here is that the text keeps its length and its lines, so
            // the surface redraws correct next frame and nothing downstream ever parses this.
            for (int i = 0; i < 200; i++)
            {
                var corrupted = _distortion.Corrupt(reading);
                Assert.AreEqual(reading.Length, corrupted.Length);
            }
        }

        [Test]
        public void NothingIsCorruptedBelowTheFirstBand()
        {
            SetExposure(10);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual("22:41", _distortion.Corrupt("22:41"));
        }

        [Test]
        public void TheFalseTaskCardIsTheSameOneAllNightAndLooksLikeARealOne()
        {
            SetExposure(0);
            Assert.IsFalse(_distortion.FalseTaskCardFor(3).Exists, "a clean run gets no false cards");

            SetExposure(60);
            var first = _distortion.FalseTaskCardFor(3);
            Assert.IsTrue(first.Exists);

            // Stable within the night. A card that changed on every tablet open would read as
            // a UI fault and could never be checked against anything.
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(first.Id, _distortion.FalseTaskCardFor(3).Id);

            // And it must not collide with a routine task, or a real card could be mistaken
            // for the ghost - which is the one direction spec 32 does not allow.
            foreach (var definition in SeedContent.BuildCases())
                Assert.AreNotEqual(definition.caseId, first.Id);
            foreach (var definition in SeedContent.BuildLateCases())
                Assert.AreNotEqual(definition.caseId, first.Id);
        }

        [Test]
        public void ExposureLengthensTheCorridorLoopOnTopOfTheFloorsOwnRisk()
        {
            var stairs = new StairNavigator();
            var director = new CorridorLoopDirector(stairs, _risk, _distortion);

            SetExposure(0);
            Assert.AreEqual(CorridorLoopDirector.BaseLoopFlights, director.LoopFlights);

            // Spec 30.3 chains the floor's own risk from M17; spec 0.10.4 adds what the
            // caretaker is carrying. They are different things and they stack.
            _risk.AddFloorRisk(FloorPlan.F5, 2);
            int withRisk = director.LoopFlights;
            Assert.AreEqual(CorridorLoopDirector.BaseLoopFlights + 2, withRisk);

            SetExposure(60);
            Assert.Greater(director.LoopFlights, withRisk);
        }
    }

    /// <summary>
    /// The signs themselves (spec 0.10.4).
    ///
    /// The director deciding that a landing may misread is only half of it - the half that
    /// was already tested and that changed nothing on screen. What matters is that the label
    /// distorts, that it comes back, and that it can never name a floor the building does not
    /// have.
    /// </summary>
    public sealed class DistortedLabelTests
    {
        [Test]
        public void AMisreadFloorIsAlwaysAFloorTheBuildingActuallyHas()
        {
            // Spec 0.7.1: there is no thirteenth storey, so no sign may ever be able to claim
            // one. Reading the neighbour off FloorPlan is what guarantees that, and this is
            // the test that keeps it read off FloorPlan.
            foreach (var floorId in FloorPlan.Order)
            {
                for (int i = 0; i < 40; i++)
                {
                    var neighbour = DistortedLabel.NeighbourOf(floorId);
                    Assert.IsNotNull(neighbour, floorId + " has no neighbour to be mistaken for");
                    Assert.IsTrue(FloorPlan.Exists(neighbour), neighbour + " is not a floor");
                    Assert.AreNotEqual(floorId, neighbour, "a sign misreading as itself is not a misreading");
                }
            }
        }

        [Test]
        public void TheMisreadingIsNeverMoreThanOneFloorOut()
        {
            // Far enough to cost a flight before it is noticed, close enough that it is not
            // obviously a bug. Both halves matter.
            var order = FloorPlan.Order;
            foreach (var floorId in order)
            {
                int index = System.Array.IndexOf(order, floorId);
                for (int i = 0; i < 40; i++)
                {
                    int neighbour = System.Array.IndexOf(order, DistortedLabel.NeighbourOf(floorId));
                    Assert.AreEqual(1, Mathf.Abs(neighbour - index));
                }
            }
        }

        [Test]
        public void TheKeyItWouldShowIsOneTheStringTableHas()
        {
            // The sign swaps in another landing's own short key rather than composing text,
            // so a floor added to FloorPlan without a string would show up here rather than
            // as a hash-marked placeholder on a wall during a playtest.
            var loc = new LocalizationService();
            loc.Initialize("ko");

            foreach (var floorId in FloorPlan.Order)
                Assert.IsTrue(loc.HasKey(DistortedLabel.LandingKeyOf(floorId)),
                              "no landing key for " + floorId);
        }
    }

    /// <summary>
    /// The floor dressing itself (spec 0.10.3), driven straight through Apply so the tier
    /// ladder can be walked without a streamed scene.
    /// </summary>
    public sealed class FloorDressingTests
    {
        GameObject _zone;
        FloorDressing _dressing;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _zone = new GameObject("ZoneRoot");
            _dressing = _zone.AddComponent<FloorDressing>().Setup(ZoneIds.Floor05, FloorPlan.F5);
        }

        [TearDown]
        public void TearDown()
        {
            if (_zone != null) Object.DestroyImmediate(_zone);
            EventBus.Clear();
        }

        static int PropsNamed(GameObject root, string name)
        {
            int count = 0;
            foreach (Transform child in root.transform)
                if (child.name == name && child.gameObject.activeSelf) count++;
            return count;
        }

        [Test]
        public void AFloorAtZeroHasNothingOnIt()
        {
            _dressing.Apply(FloorRiskTier.Clear);

            Assert.AreEqual(0, PropsNamed(_zone, "RiskAfterimage"));
            Assert.AreEqual(0, PropsNamed(_zone, "RiskFalseDoor"));
            Assert.AreEqual(0, PropsNamed(_zone, "RiskBlockedRoute"));
        }

        [Test]
        public void EachTierAddsWhatSpec0103SaysAndKeepsWhatCameBefore()
        {
            _dressing.Apply(FloorRiskTier.Unsettled);
            Assert.AreEqual(0, PropsNamed(_zone, "RiskAfterimage"), "tier 1 is sound and light only");

            _dressing.Apply(FloorRiskTier.Deceptive);
            Assert.AreEqual(1, PropsNamed(_zone, "RiskAfterimage"));
            Assert.AreEqual(0, PropsNamed(_zone, "RiskFalseDoor"));

            _dressing.Apply(FloorRiskTier.Obstructive);
            Assert.AreEqual(1, PropsNamed(_zone, "RiskAfterimage"), "tier 3 keeps the afterimage");
            Assert.AreEqual(1, PropsNamed(_zone, "RiskFalseDoor"));
            Assert.AreEqual(0, PropsNamed(_zone, "RiskBlockedRoute"));

            _dressing.Apply(FloorRiskTier.Hostile);
            Assert.AreEqual(1, PropsNamed(_zone, "RiskBlockedRoute"));
        }

        [Test]
        public void PuttingAFloorRightTakesTheDressingBackOff()
        {
            // Spec 0.10.3 is a ladder in both directions. A floor that was repaired has to
            // stop looking wrong, or nothing the caretaker does to fix one ever reads.
            _dressing.Apply(FloorRiskTier.Hostile);
            Assert.AreEqual(1, PropsNamed(_zone, "RiskFalseDoor"));

            _dressing.Apply(FloorRiskTier.Clear);
            Assert.AreEqual(0, PropsNamed(_zone, "RiskAfterimage"));
            Assert.AreEqual(0, PropsNamed(_zone, "RiskFalseDoor"));
            Assert.AreEqual(0, PropsNamed(_zone, "RiskBlockedRoute"));
        }

        [Test]
        public void TheDoorThatIsNotADoorCanStillBeTried()
        {
            // Spec 0.4: the UI never marks the wrong answer. A false door the player cannot
            // even reach for would be exactly that marking, done with a collider.
            _dressing.Apply(FloorRiskTier.Obstructive);

            FalseDoor door = null;
            foreach (Transform child in _zone.transform)
            {
                var candidate = child.GetComponent<FalseDoor>();
                if (candidate != null) door = candidate;
            }

            Assert.IsNotNull(door, "tier 3 should put a door there");

            var context = default(PlayerContext);
            string reason;
            Assert.IsTrue(door.CanInteract(context, out reason));
        }
    }
}
