using System.Collections.Generic;
using NUnit.Framework;
using NO404.Core;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// The door as a thing a person does, rather than a comparison a database does
    /// (GDD 13.4 - 13.7).
    ///
    /// The report behind all of this: "그냥 관리 PC에서만 계속 뭘 보고 문 열어주고" - the whole
    /// job was sitting at one screen reading rows and pressing a button, forty-nine times a
    /// week, against callers who did not have names. These pin the two answers to that. The
    /// roster is five a night and everybody on it is somebody; and the person at the door can
    /// be read as well as looked up, which means the caretaker has to leave the chair, and
    /// means being wrong about a frightened neighbour costs something.
    /// </summary>
    public sealed class DoorReadTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
            ServiceHub.State.BeginNight(1);
            ServiceHub.Player.EnterZone(ZoneIds.Office);
        }

        static List<VisitorDefinition> RosterFor(int night)
        {
            var roster = new List<VisitorDefinition>();
            foreach (var visitor in ServiceHub.Content.Visitors)
                if (visitor != null && visitor.nightIndex == night) roster.Add(visitor);
            return roster;
        }

        // -----------------------------------------------------------------
        // the roster
        // -----------------------------------------------------------------

        [Test]
        public void NoNightPutsMoreThanFivePeopleAtTheDoor()
        {
            for (int night = 1; night <= 6; night++)
                Assert.LessOrEqual(RosterFor(night).Count, 5,
                                   "night " + night + " goes over the GDD 13.4 ceiling; past five " +
                                   "the player stops meeting people and starts clearing a queue");
        }

        [Test]
        public void EveryNightStillHasADoorToAnswer()
        {
            for (int night = 1; night <= 6; night++)
                Assert.GreaterOrEqual(RosterFor(night).Count, 4,
                                      "night " + night + " has too few callers to be a shift");
        }

        [Test]
        public void TheCastRepeatsAcrossTheWeek()
        {
            // The point of cutting the roster was to make room for people who come back. If
            // every caller in the game is a one-off, five a night is just a thinner queue.
            var byName = new Dictionary<string, int>();
            for (int night = 1; night <= 6; night++)
                foreach (var visitor in RosterFor(night))
                {
                    int seen;
                    byName.TryGetValue(visitor.nameKey, out seen);
                    byName[visitor.nameKey] = seen + 1;
                }

            int recurring = 0;
            foreach (var pair in byName) if (pair.Value >= 2) recurring++;

            Assert.GreaterOrEqual(recurring, 4,
                                  "at least four callers have to come back on a later night, or " +
                                  "there is nobody in this building the player gets to know");
        }

        [Test]
        public void EveryCallerCanBeRead()
        {
            foreach (var visitor in ServiceHub.Content.Visitors)
            {
                if (visitor == null) continue;

                Assert.GreaterOrEqual(visitor.tells.Length, 3,
                                      visitor.visitorId + " has nothing to notice about them");
                Assert.GreaterOrEqual(visitor.RealTellCount, 1,
                                      visitor.visitorId + " is all noise; the read layer can never " +
                                      "establish anything about them");
                Assert.IsNotNull(visitor.ResponseFor(PressureTactic.Reassure),
                                 visitor.visitorId + " cannot be talked back down once pushed");
            }
        }

        [Test]
        public void SomebodyWhoReadsLikeALiarAndIsNotOneIsAlwaysAdmitted()
        {
            // GDD 13.6. A Shaken caller gives every deception signal a liar gives, and the
            // correct call is still to open the door. If one of them were refusable the rule
            // would collapse into "agitation means guilty", which is the thing this exists to
            // stop being true.
            int shaken = 0;
            foreach (var visitor in ServiceHub.Content.Visitors)
            {
                if (visitor == null || visitor.truth != VisitorTruth.Shaken) continue;
                shaken++;

                // Let in somewhere - how far in is a separate question, and for two of them
                // the honest answer is the lobby and a chair.
                Assert.IsFalse(visitor.ShouldBeRefused,
                               visitor.visitorId + " is frightened, not dishonest, and must be let in");
                Assert.IsNotEmpty(visitor.hiddenReasonKey ?? string.Empty,
                                  visitor.visitorId + " cracks without ever saying what was wrong");
            }

            Assert.GreaterOrEqual(shaken, 4,
                                  "without enough of these the player can safely treat every " +
                                  "nervous caller as a liar, and the door goes back to arithmetic");
        }

        // -----------------------------------------------------------------
        // what a note is worth
        // -----------------------------------------------------------------

        [Test]
        public void NoticingNoiseEstablishesNothing()
        {
            var read = Begin("vis_miran_n1");

            foreach (var tell in read.VisibleTells())
                if (tell.weight == TellWeight.Noise) read.Note(tell.tellId);

            Assert.Greater(read.NoiseNoteCount, 0, "the caller should have noise to notice");
            Assert.AreEqual(0, read.EstablishedFactCount,
                            "a shift note full of things that are true of everybody is not evidence");
        }

        [Test]
        public void TwoRealObservationsAreEnoughOnTheirOwn()
        {
            // The lobby route. A caretaker standing at their own front door with no computer
            // at all can still make an informed call, which is what stops the game being a
            // spreadsheet with a doorbell attached (GDD 13.2 / 13.7).
            ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            var read = Begin("vis_minho_n3");

            int noted = 0;
            foreach (var tell in read.VisibleTells())
            {
                if (tell.weight == TellWeight.Noise || noted >= 2) continue;
                read.Note(tell.tellId);
                noted++;
            }

            Assert.AreEqual(2, noted, "the fake contractor should be readable in the lobby");
            Assert.IsTrue(ServiceHub.Interphone.HasEnoughInformation,
                          "two things actually seen are two independent facts");
        }

        [Test]
        public void TheGlassShowsWhatTheCameraCannot()
        {
            var read = Begin("vis_minho_n3");

            int fromTheDesk = read.VisibleTells().Count;
            ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            int fromTheGlass = read.VisibleTells().Count;

            Assert.Greater(fromTheGlass, fromTheDesk,
                           "walking down to the lobby has to buy something, or nobody will");

            ServiceHub.Player.EnterZone(ZoneIds.Office);
            Assert.AreEqual(fromTheDesk, read.VisibleTells().Count,
                            "and it has to stop being visible when the caretaker walks back up");
        }

        [Test]
        public void SomethingSeenThroughTheGlassCannotBeNotedFromTheDesk()
        {
            var read = Begin("vis_minho_n3");

            string glassTell = null;
            for (int i = 0; i < read.Visitor.tells.Length; i++)
                if (read.Visitor.tells[i].channel == ReadChannel.Glass) glassTell = read.Visitor.tells[i].tellId;

            Assert.IsNotNull(glassTell);
            read.Note(glassTell);
            Assert.IsFalse(read.HasNoted(glassTell),
                           "the caretaker cannot write down something they are not in the room to see");
        }

        // -----------------------------------------------------------------
        // pressure
        // -----------------------------------------------------------------

        [Test]
        public void PushingCostsTheNightRealMinutes()
        {
            var read = Begin("vis_miran_n1");
            int before = ServiceHub.Clock.GameSecond;

            read.Use(PressureTactic.AskAgain);

            Assert.AreEqual(before + DoorReadService.AskAgainSeconds, ServiceHub.Clock.GameSecond,
                            "a question is a minute, and the shift is made of minutes");
        }

        [Test]
        public void TheSameQuestionCannotBeAskedTwice()
        {
            var read = Begin("vis_miran_n1");
            read.Use(PressureTactic.AskAgain);

            Assert.IsTrue(read.HasUsed(PressureTactic.AskAgain));
            Assert.AreEqual("ui.door.blocked.already_tried", read.BlockedReasonKey(PressureTactic.AskAgain));
        }

        [Test]
        public void YouCannotCiteARecordYouHaveNotOpened()
        {
            var read = Begin("vis_minho_n3");

            Assert.AreEqual("ui.door.blocked.no_record", read.BlockedReasonKey(PressureTactic.Confront),
                            "confronting somebody with a document nobody has read is a bluff");

            int before = ServiceHub.Clock.GameSecond;
            read.Use(PressureTactic.Confront);
            Assert.AreEqual(before, ServiceHub.Clock.GameSecond, "and a refused tactic costs nothing");

            ServiceHub.Interphone.MarkChecked("residents.304");
            Assert.IsNull(read.BlockedReasonKey(PressureTactic.Confront),
                          "having looked it up is what earns the accusation");
        }

        [Test]
        public void ALiarComesApartUnderEnoughOfIt()
        {
            var read = Begin("vis_police_n5");
            ServiceHub.Interphone.MarkChecked("access.compare");

            // He is the most composed liar in the game: nothing short of most of what the
            // caretaker has moves him, and the badge he keeps not producing is the thing that
            // finally does.
            read.Use(PressureTactic.ShowMe);
            read.Use(PressureTactic.Confront);
            Assert.IsFalse(read.Cracked, "two pushes should not be enough on this one");

            read.Use(PressureTactic.AskAgain);
            Assert.IsTrue(read.Cracked, "and a third should be");
        }

        [Test]
        public void BreakingSomebodyWhoHadNothingToHideCostsTheBuilding()
        {
            // The other half of GDD 13.6, and the reason the tactics are not free. A caretaker
            // who treats every caller as a suspect gets answers and loses the neighbours.
            var read = Begin("vis_dohyeon_n3");
            int trustBefore = ServiceHub.State.GetStat(StatIds.CommunityTrust);

            // Everything the caretaker has, on a man whose only crime is visiting his father
            // after a night shift.
            read.Use(PressureTactic.AskAgain);
            read.Use(PressureTactic.Silence);
            read.Use(PressureTactic.ShowMe);
            read.Use(PressureTactic.Confront);

            Assert.IsTrue(read.Cracked,
                          "using every tactic in the game has to land on anybody, or the panel " +
                          "is charging the shift for buttons that do nothing");
            Assert.Less(ServiceHub.State.GetStat(StatIds.CommunityTrust), trustBefore,
                        "the son of a resident who was interrogated on his father's doorstep " +
                        "will say so, and the building will hear it");
        }

        [Test]
        public void EasingOffIsTheOnlyWayToLowerTheTemperature()
        {
            var read = Begin("vis_sua_n4");
            read.Use(PressureTactic.AskAgain);

            int hot = read.Agitation;
            Assert.Greater(hot, 0);

            read.Use(PressureTactic.Reassure);
            Assert.Less(read.Agitation, hot, "backing off has to actually back off");
        }

        [Test]
        public void AFrightenedCallerSaysWhatIsActuallyWrongRatherThanConfessingToACrime()
        {
            var read = Begin("vis_sua_n4");
            var visitor = read.Visitor;

            // Two questions. She is at her limit before the caretaker has spent three minutes
            // on her, and stopping the moment she gives way is the point of the test - one more
            // push and the last thing she said would be her walking away from the panel.
            read.Use(PressureTactic.AskAgain);
            Assert.IsFalse(read.Cracked);
            read.Use(PressureTactic.Silence);

            Assert.IsTrue(read.Cracked, "she is barely holding on; she should not be hard to break");
            Assert.AreEqual(visitor.hiddenReasonKey, read.LastReplyKey,
                            "what comes out of her is the truth about Ji-woo, not a confession");
            Assert.IsFalse(visitor.ShouldBeRefused,
                           "and after all of that, letting her in is still the right call");
        }

        [Test]
        public void TheCallerAndTheirReadingLeaveTogether()
        {
            var read = Begin("vis_miran_n1");
            read.Use(PressureTactic.AskAgain);
            Assert.IsNotNull(read.Visitor);

            ServiceHub.Interphone.Grant(VisitorAccessLevel.FloorPass);

            Assert.IsNull(ServiceHub.Interphone.Read.Visitor,
                          "the next person at the door starts from nothing known about them");
            Assert.AreEqual(0, ServiceHub.Interphone.Read.Agitation);
        }

        [Test]
        public void ACallerYouTurnedAwayComesBackHarder()
        {
            // What the roster cut was for. Five callers a night made room for the same people
            // to come back, and this is the thing that makes coming back mean something: the
            // way a caretaker treated somebody on Monday is waiting for them on Wednesday.
            ServiceHub.Interphone.Enqueue("vis_miran_n1");
            ServiceHub.Interphone.Grant(VisitorAccessLevel.Reject);

            var read = Begin("vis_miran_n3");

            Assert.AreEqual(VisitorAccessLevel.Reject, read.LastDecision,
                            "she should remember being refused");
            Assert.AreEqual(DoorReadService.RefusedBeforeAgitation, read.Agitation,
                            "and arrive part of the way to her limit because of it");
        }

        [Test]
        public void ACallerYouTreatedWellArrivesWhereAnybodyElseWould()
        {
            ServiceHub.Interphone.Enqueue("vis_miran_n1");
            ServiceHub.Interphone.Grant(VisitorAccessLevel.FloorPass);

            var read = Begin("vis_miran_n3");

            Assert.AreEqual(VisitorAccessLevel.FloorPass, read.LastDecision);
            Assert.AreEqual(0, read.Agitation,
                            "having been let in is not something to hold against the caretaker");
        }

        static DoorReadService Begin(string visitorId)
        {
            ServiceHub.Interphone.Enqueue(visitorId);
            Assert.AreEqual(visitorId, ServiceHub.Interphone.Active.visitorId,
                            "the test caller should be the one at the door");
            return ServiceHub.Interphone.Read;
        }
    }
}
