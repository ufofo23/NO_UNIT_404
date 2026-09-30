using System.Collections.Generic;
using NUnit.Framework;
using NO404.Core;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// The door as the middle of an event rather than the end of one (v3.0 B-02, 38).
    ///
    /// The rule these exist to keep is the most emphatic sentence in the v3.0 document:
    /// `방문자 승인 / 거절만으로 사건 종료 금지`. Pressing a button used to make a person
    /// disappear. Now it makes a person appear - upstairs, on a route, on cameras that may or
    /// may not be watching - and the caretaker owns whatever they do next.
    /// </summary>
    public sealed class VisitorAccessTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
            ServiceHub.State.BeginNight(3);
            ServiceHub.Player.EnterZone(ZoneIds.Office);
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond);
        }

        // -----------------------------------------------------------------
        // the ladder itself
        // -----------------------------------------------------------------

        [Test]
        public void TurningSomebodyAwayIsAlwaysOnThePanel()
        {
            for (int night = 0; night <= 6; night++)
            {
                var taught = VisitorAccess.TaughtBy(night);
                CollectionAssert.Contains(taught, VisitorAccessLevel.Reject,
                                          "night " + night + " has to let the caretaker say no");
            }
        }

        [Test]
        public void TheRungsThatHandOverFloorSpaceAreTaughtLater()
        {
            var prologue = new List<VisitorAccessLevel>(VisitorAccess.TaughtBy(0));

            Assert.IsFalse(prologue.Contains(VisitorAccessLevel.FloorPass),
                           "handing over a floor on the first shift is a decision nobody has " +
                           "been given a reason to understand yet");
            Assert.IsFalse(prologue.Contains(VisitorAccessLevel.FullTemporary));

            CollectionAssert.Contains(VisitorAccess.TaughtBy(1), VisitorAccessLevel.FloorPass);
        }

        [Test]
        public void BeingWalkedInIsSaferThanBeingHandedTheFloor()
        {
            // The enum order and the risk order are deliberately different, and this is the
            // pair that makes the distinction earn its keep: Escorted reaches further than
            // LobbyOnly and is less dangerous than FloorPass, because somebody is standing
            // next to them the whole time.
            Assert.Less(VisitorAccess.RiskRank(VisitorAccessLevel.LobbyOnly),
                        VisitorAccess.RiskRank(VisitorAccessLevel.Escorted));
            Assert.Less(VisitorAccess.RiskRank(VisitorAccessLevel.Escorted),
                        VisitorAccess.RiskRank(VisitorAccessLevel.FloorPass));
        }

        [Test]
        public void RefusingAndHoldingPutNobodyInTheBuilding()
        {
            Assert.IsFalse(VisitorAccess.LetsThemIn(VisitorAccessLevel.Reject));
            Assert.IsFalse(VisitorAccess.LetsThemIn(VisitorAccessLevel.Hold));
            Assert.IsTrue(VisitorAccess.LetsThemIn(VisitorAccessLevel.Vestibule));
        }

        [Test]
        public void HandingOverTheWholeBuildingIsNeverTheAuthoredAnswer()
        {
            // v3.0 G-01. The rung exists so the player can make that mistake, not so the
            // content can ask for it.
            foreach (var visitor in ServiceHub.Content.Visitors)
                if (visitor != null)
                    Assert.AreNotEqual(VisitorAccessLevel.FullTemporary, visitor.correctAccess,
                                       visitor.visitorId + " should never need the run of the building");
        }

        [Test]
        public void EverybodyWhoCanBeGivenAFloorHasARouteToLeave()
        {
            foreach (var visitor in ServiceHub.Content.Visitors)
            {
                if (visitor == null || !VisitorAccess.Unaccompanied(visitor.correctAccess)) continue;

                Assert.IsNotNull(visitor.expectedRoute, visitor.visitorId + " has no route");
                Assert.Greater(visitor.expectedRoute.Length, 1,
                               visitor.visitorId + " can be sent upstairs but walks nowhere, so " +
                               "nothing they do can ever read as off-route (v3.0 38.5)");
            }
        }

        // -----------------------------------------------------------------
        // grading a grant
        // -----------------------------------------------------------------

        [Test]
        public void GivingSomebodyMoreThanTheyNeededIsADifferentMistakeFromGivingThemLess()
        {
            var carer = ServiceHub.Content.FindVisitor("vis_miran_n3");
            Assert.AreEqual(VisitorAccessLevel.FloorPass, carer.correctAccess);

            Assert.AreEqual(InterphoneService.GrantJudgement.OverCautious,
                            InterphoneService.Judge(carer, VisitorAccessLevel.LobbyOnly),
                            "keeping the carer in the lobby is over-caution, not a safety failure");

            Assert.AreEqual(InterphoneService.GrantJudgement.OverPermissive,
                            InterphoneService.Judge(carer, VisitorAccessLevel.FullTemporary),
                            "and the run of the building is the other kind of wrong");
        }

        [Test]
        public void TheRightRungReachedByGuessingIsStillNotGraded()
        {
            // GDD 13.2 survives the rework: two facts, or the answer does not count even when
            // the button pressed happened to be the right one.
            var carer = ServiceHub.Content.FindVisitor("vis_miran_n3");
            ServiceHub.Interphone.Enqueue("vis_miran_n3");

            Assert.AreEqual(InterphoneService.GrantJudgement.Uninformed,
                            InterphoneService.Judge(carer, VisitorAccessLevel.FloorPass));

            ServiceHub.Interphone.MarkChecked("residents.303");
            ServiceHub.Interphone.MarkChecked("access.compare");

            Assert.AreEqual(InterphoneService.GrantJudgement.Correct,
                            InterphoneService.Judge(carer, VisitorAccessLevel.FloorPass));
        }

        // -----------------------------------------------------------------
        // and then they are inside
        // -----------------------------------------------------------------

        [Test]
        public void LettingSomebodyInPutsThemInTheBuilding()
        {
            Grant("vis_miran_n3", VisitorAccessLevel.FloorPass);

            Assert.AreEqual(1, ServiceHub.ActiveVisitors.Count,
                            "the door is the middle of the event, not the end of it (v3.0 B-02)");

            var tracked = ServiceHub.ActiveVisitors.Find("vis_miran_n3");
            Assert.AreEqual(ZoneIds.Lobby, tracked.LastKnownZone, "they come in through the lobby");
            Assert.IsFalse(tracked.Token.revoked);
        }

        [Test]
        public void TurningSomebodyAwayPutsNobodyAnywhere()
        {
            Grant("vis_minho_n3", VisitorAccessLevel.Reject);
            Assert.AreEqual(0, ServiceHub.ActiveVisitors.Count);
        }

        [Test]
        public void APassReachesTheFloorItWasGivenForAndNoFurther()
        {
            var token = Grant("vis_miran_n3", VisitorAccessLevel.FloorPass);

            Assert.IsTrue(token.Allows(ZoneIds.Lobby));
            Assert.IsTrue(token.Allows(ZoneIds.Floor03), "303 is on the third floor");
            Assert.IsFalse(token.Allows(ZoneIds.Floor04),
                           "a pass for one floor is not a pass for the one above it");
            Assert.IsFalse(token.Allows(ZoneIds.Archive));
        }

        [Test]
        public void PullingAPassStopsTheNextDoorRatherThanTeleportingAnybody()
        {
            var token = Grant("vis_miran_n3", VisitorAccessLevel.FloorPass);
            var tracked = ServiceHub.ActiveVisitors.Find("vis_miran_n3");
            var wasIn = tracked.CurrentZone;

            Assert.IsTrue(ServiceHub.ActiveVisitors.Revoke("vis_miran_n3"));

            Assert.IsTrue(token.revoked);
            Assert.IsFalse(token.Allows(ZoneIds.Lobby), "the reader stops honouring it everywhere");
            Assert.AreEqual(wasIn, tracked.CurrentZone,
                            "nobody is removed from a room they are already standing in (v3.0 45.3)");
            Assert.AreEqual(1, ServiceHub.ActiveVisitors.Count,
                            "and they are still in the building, which is the caretaker's problem now");
        }

        // -----------------------------------------------------------------
        // the gap between where they were and where they are
        // -----------------------------------------------------------------

        /// <summary>
        /// Walks a visitor to the end of their route, one leg at a time.
        ///
        /// Leg by leg rather than one big jump on purpose: a single Advance past several legs
        /// would re-observe them on the way through and hide exactly the gap being tested.
        /// </summary>
        static void WalkToDestination(string visitorId)
        {
            var tracked = ServiceHub.ActiveVisitors.Find(visitorId);
            for (int i = 0; i < 6 && tracked.State != VisitorState.AtDestination; i++)
            {
                ServiceHub.Clock.AdvanceSeconds(ActiveVisitorService.SecondsPerLeg + 1);
                ServiceHub.ActiveVisitors.Tick();
            }
            Assert.AreEqual(VisitorState.AtDestination, tracked.State);
        }

        [Test]
        public void ACardThatNothingHasUpdatedStopsClaimingToKnowWhereTheyAre()
        {
            // Min-seo is going to the second floor, which has no camera on it. She is not
            // doing anything wrong; she is simply somewhere the building cannot see, and after
            // three minutes the card has to admit that rather than go on showing a stale room
            // as though it were live. That admission is the whole difference between this and
            // a minimap (v3.0 38.4).
            Grant("vis_minseo", VisitorAccessLevel.FloorPass);
            var tracked = ServiceHub.ActiveVisitors.Find("vis_minseo");
            Assert.AreEqual(ZoneIds.Floor02, tracked.Definition.destinationZone);

            WalkToDestination("vis_minseo");
            Assert.AreEqual(0, (int)(tracked.Flags & VisitorFlags.Untracked));

            ServiceHub.Clock.AdvanceSeconds(ActiveVisitorService.StaleAfterSeconds + 5);
            ServiceHub.ActiveVisitors.Tick();

            Assert.AreNotEqual(0, (int)(tracked.Flags & VisitorFlags.Untracked),
                               "three minutes with nothing seeing them is not a known position");
            Assert.AreEqual("ui.access.state.unknown", ActiveVisitorService.StateKey(tracked));
        }

        [Test]
        public void StandingInTheSameRoomIsHowACaretakerUpdatesTheCard()
        {
            Grant("vis_minseo", VisitorAccessLevel.FloorPass);
            var tracked = ServiceHub.ActiveVisitors.Find("vis_minseo");

            WalkToDestination("vis_minseo");
            ServiceHub.Clock.AdvanceSeconds(ActiveVisitorService.StaleAfterSeconds + 5);
            ServiceHub.ActiveVisitors.Tick();
            Assert.AreNotEqual(0, (int)(tracked.Flags & VisitorFlags.Untracked));

            // Go up there and look. A caretaker standing in the room is an observation like
            // any other, and it is the only one available on a floor with no camera.
            ServiceHub.Player.EnterZone(tracked.CurrentZone);
            ServiceHub.ActiveVisitors.Tick();

            Assert.AreEqual(0, (int)(tracked.Flags & VisitorFlags.Untracked),
                            "the walk upstairs is what buys the information back");
        }

        [Test]
        public void AnEscortBreaksWhenTheCaretakerWandersOff()
        {
            Grant("vis_engineer_n3", VisitorAccessLevel.Escorted);
            var tracked = ServiceHub.ActiveVisitors.Find("vis_engineer_n3");

            ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            ServiceHub.ActiveVisitors.Tick();
            Assert.AreNotEqual(0, (int)(tracked.Flags & VisitorFlags.Escorted));

            ServiceHub.Player.EnterZone(ZoneIds.Office);
            ServiceHub.ActiveVisitors.Tick();                      // starts the grace period
            ServiceHub.Clock.AdvanceSeconds(ActiveVisitorService.EscortGraceSeconds + 5);
            ServiceHub.ActiveVisitors.Tick();

            Assert.AreNotEqual(0, (int)(tracked.Flags & VisitorFlags.EscortBroken),
                               "walking away from somebody you agreed to walk in is the whole cost " +
                               "of the escort rung (v3.0 38.9)");
        }

        [Test]
        public void SomebodyOffTheirRouteCostsTheFloorRatherThanPoppingAMessageBox()
        {
            var courier = ServiceHub.Content.FindVisitor("vis_courier_late");
            Assert.IsTrue(courier.canDeviate, "the cold open's courier is the first one who strays");

            string offRouteZone = null;
            EventBus.Subscribe<VisitorOffRouteEvent>(e => offRouteZone = e.ZoneId);

            ServiceHub.Interphone.Enqueue("vis_courier_late");
            ServiceHub.Interphone.Grant(VisitorAccessLevel.FloorPass);

            // Walk the whole route out. He follows it honestly first and then does not.
            for (int i = 0; i < 6; i++)
            {
                ServiceHub.Clock.AdvanceSeconds(ActiveVisitorService.SecondsPerLeg + 1);
                ServiceHub.ActiveVisitors.Tick();
            }

            Assert.IsNotNull(offRouteZone,
                             "a caller who leaves their route has to leave a trace, or the pass " +
                             "was a decision with no consequence");
            Assert.AreEqual(ZoneIds.Floor04, offRouteZone);
        }

        static AccessToken Grant(string visitorId, VisitorAccessLevel level)
        {
            ServiceHub.Interphone.Enqueue(visitorId);
            Assert.AreEqual(visitorId, ServiceHub.Interphone.Active.visitorId);
            ServiceHub.Interphone.Grant(level);

            var tracked = ServiceHub.ActiveVisitors.Find(visitorId);
            return tracked != null ? tracked.Token : null;
        }
    }
}
