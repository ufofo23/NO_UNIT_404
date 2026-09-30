using System.Collections.Generic;
using NUnit.Framework;
using NO404.Core;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// The seams a second caretaker is pushed through (v3.0 34, 37.3, 46).
    ///
    /// No networking runs here. What is tested is the two things that have to be true before
    /// networking can help: that the building's rules ask about the *shift* rather than about
    /// whoever is holding this copy of the game, and that a client's services can be told the
    /// host's answer without the UI above them knowing the difference.
    ///
    /// Both of these were silent failures waiting to happen. A rule written against
    /// `ServiceHub.Player.CurrentZone` keeps compiling and keeps returning something plausible
    /// in a two-handed shift; it is just wrong, and wrong in a way that only shows up as "my
    /// friend says he can see it and I can't".
    /// </summary>
    public sealed class CoopSeamTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
            ServiceHub.State.BeginNight(3);
            ServiceHub.Player.EnterZone(ZoneIds.Office);
        }

        // -----------------------------------------------------------------
        // presence
        // -----------------------------------------------------------------

        [Test]
        public void ASoloShiftIsAOneCaretakerRoster()
        {
            Assert.AreEqual(1, ServiceHub.Presence.Count);
            Assert.IsTrue(ServiceHub.Presence.AnyoneIn(ZoneIds.Office));
            Assert.IsFalse(ServiceHub.Presence.AnyoneIn(ZoneIds.Lobby));
        }

        [Test]
        public void TheLocalPlayerReportsThemselvesWhenTheyMove()
        {
            ServiceHub.Player.EnterZone(ZoneIds.Lobby);

            Assert.IsTrue(ServiceHub.Presence.AnyoneIn(ZoneIds.Lobby),
                          "walking somewhere has to update the roster the rules read");
            Assert.IsFalse(ServiceHub.Presence.AnyoneIn(ZoneIds.Office));
        }

        [Test]
        public void AColleagueAtTheGlassIsSeenByTheCaretakerAtTheDesk()
        {
            // The whole point of a two-handed shift (v3.0 37.3). One of them is sitting at the
            // desk and one is standing in the lobby, and the observation belongs to the shift.
            ServiceHub.Player.EnterZone(ZoneIds.Office);
            Assert.IsFalse(DoorReadService.AtTheGlass);

            ServiceHub.Presence.Report("P2", ZoneIds.Lobby);

            Assert.IsTrue(DoorReadService.AtTheGlass,
                          "somebody is at the glass; it does not matter whose keyboard it is");
        }

        [Test]
        public void AColleagueUpstairsSatisfiesAnEscort()
        {
            ServiceHub.Interphone.Enqueue("vis_engineer_n3");
            ServiceHub.Interphone.Grant(VisitorAccessLevel.Escorted);

            var tracked = ServiceHub.ActiveVisitors.Find("vis_engineer_n3");
            Assert.IsNotNull(tracked);

            // The caretaker who granted the escort is at the desk; the other one is walking it.
            ServiceHub.Player.EnterZone(ZoneIds.Office);
            ServiceHub.Presence.Report("P2", tracked.CurrentZone);
            ServiceHub.ActiveVisitors.Tick();

            Assert.AreNotEqual(0, (int)(tracked.Flags & VisitorFlags.Escorted),
                               "an escort is a person being with them, not a person being you");
        }

        [Test]
        public void SomebodyWhoLeavesStopsCountingAsPresent()
        {
            ServiceHub.Presence.Report("P2", ZoneIds.Lobby);
            Assert.IsTrue(DoorReadService.AtTheGlass);

            ServiceHub.Presence.Remove("P2");

            Assert.IsFalse(DoorReadService.AtTheGlass,
                           "a caretaker who has disconnected is not still standing in the lobby");
        }

        // -----------------------------------------------------------------
        // the mirror
        // -----------------------------------------------------------------

        [Test]
        public void AClientCanBeToldWhoIsAtTheDoorAndWhatHasBeenLookedUp()
        {
            // What arrives over the wire is an id and a bitmask; both machines already have
            // the caller themselves, so nothing about the person travels.
            int mask = (1 << System.Array.IndexOf(AppIds.Order, AppIds.Residents)) |
                       (1 << System.Array.IndexOf(AppIds.Order, AppIds.Access));

            ServiceHub.Interphone.ApplyMirror("vis_minho_n3", mask);

            Assert.AreEqual("vis_minho_n3", ServiceHub.Interphone.Active.visitorId);
            Assert.IsTrue(ServiceHub.Interphone.HasConsulted(AppIds.Residents));
            Assert.IsTrue(ServiceHub.Interphone.HasConsulted(AppIds.Access));
            Assert.IsFalse(ServiceHub.Interphone.HasConsulted(AppIds.Cctv));
            Assert.IsTrue(ServiceHub.Interphone.HasEnoughInformation);
        }

        [Test]
        public void AClientCanBeToldHowTheCallerIsHoldingUp()
        {
            ServiceHub.Interphone.ApplyMirror("vis_minho_n3", 0);
            var visitor = ServiceHub.Interphone.Active;
            var read = ServiceHub.Interphone.Read;

            // Tell masks index into the visitor's own array, which both machines have.
            int noted = 1 << 0;
            int revealed = (1 << 0) | (1 << 3);
            int used = 1 << System.Array.IndexOf(DoorRead.All, PressureTactic.AskAgain);

            read.ApplyMirror(62, false, false, "door.default.silence", revealed, noted, used);

            Assert.AreEqual(62, read.Agitation);
            Assert.AreEqual("ui.door.mood.rattled", read.AgitationKey);
            Assert.AreEqual("door.default.silence", read.LastReplyKey);
            Assert.IsTrue(read.HasNoted(visitor.tells[0].tellId));
            Assert.IsTrue(read.IsRevealed(visitor.tells[3].tellId));
            Assert.IsTrue(read.HasUsed(PressureTactic.AskAgain));
            Assert.IsFalse(read.HasUsed(PressureTactic.ShowMe));
        }

        [Test]
        public void WhatIsVisibleIsStillWorkedOutLocally()
        {
            // The host says what has been *revealed*; each machine decides what is *visible*,
            // because that depends on where its own caretakers are standing. Sending
            // visibility instead would mean the person at the desk sees what the person at the
            // glass sees, which deletes the asymmetry the whole mode exists for.
            ServiceHub.Interphone.ApplyMirror("vis_minho_n3", 0);
            var read = ServiceHub.Interphone.Read;
            read.ApplyMirror(0, false, false, null, 0, 0, 0);

            ServiceHub.Player.EnterZone(ZoneIds.Office);
            int fromTheDesk = read.VisibleTells().Count;

            ServiceHub.Presence.Report("P2", ZoneIds.Lobby);
            int withSomebodyAtTheGlass = read.VisibleTells().Count;

            Assert.Greater(withSomebodyAtTheGlass, fromTheDesk,
                           "the same snapshot has to mean different things on the two screens");
        }

        [Test]
        public void AClientCanBeToldWhoIsInTheBuilding()
        {
            var rows = new List<ActiveVisitorService.MirrorRow>
            {
                new ActiveVisitorService.MirrorRow
                {
                    VisitorId = "vis_miran_n3",
                    LastKnownZone = ZoneIds.Floor03,
                    LastKnownSecond = ServiceHub.Clock.GameSecond - 240,
                    EnteredSecond = ServiceHub.Clock.GameSecond - 900,
                    Flags = VisitorFlags.Untracked,
                    Level = VisitorAccessLevel.FloorPass,
                    State = VisitorState.AtDestination,
                    Revoked = false
                }
            };

            ServiceHub.ActiveVisitors.ApplyMirror(rows);

            Assert.AreEqual(1, ServiceHub.ActiveVisitors.Count);

            var tracked = ServiceHub.ActiveVisitors.Find("vis_miran_n3");
            Assert.AreEqual(ZoneIds.Floor03, tracked.LastKnownZone);
            Assert.AreEqual(VisitorAccessLevel.FloorPass, tracked.Token.level);
            Assert.AreEqual("ui.access.state.unknown", ActiveVisitorService.StateKey(tracked),
                            "a stale row reads as stale on the client too");
        }

        [Test]
        public void AMirroredBoardReplacesRatherThanAccumulates()
        {
            var one = new List<ActiveVisitorService.MirrorRow>
            {
                Row("vis_miran_n3"), Row("vis_jaeseok_n3")
            };
            ServiceHub.ActiveVisitors.ApplyMirror(one);
            Assert.AreEqual(2, ServiceHub.ActiveVisitors.Count);

            // One of them left. A merge that kept the stale row would have a caretaker
            // walking upstairs after somebody who is already outside.
            ServiceHub.ActiveVisitors.ApplyMirror(new List<ActiveVisitorService.MirrorRow> { Row("vis_miran_n3") });

            Assert.AreEqual(1, ServiceHub.ActiveVisitors.Count);
            Assert.IsNull(ServiceHub.ActiveVisitors.Find("vis_jaeseok_n3"));
        }

        static ActiveVisitorService.MirrorRow Row(string visitorId)
        {
            return new ActiveVisitorService.MirrorRow
            {
                VisitorId = visitorId,
                LastKnownZone = ZoneIds.Lobby,
                LastKnownSecond = ServiceHub.Clock.GameSecond,
                EnteredSecond = ServiceHub.Clock.GameSecond,
                Level = VisitorAccessLevel.LobbyOnly,
                State = VisitorState.Inside
            };
        }
    }
}
