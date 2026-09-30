using NUnit.Framework;
using NO404.Core;
using NO404.Visitors;

namespace NO404.Tests
{
    /// <summary>
    /// The caller's script leaves with the caller.
    ///
    /// This is the report that produced it. A caretaker finished night 1 - six of six jobs
    /// filed, every call answered, nobody at the door - and the clock-off button still refused,
    /// saying they were mid-conversation. The console agreed: "conversation open True" with
    /// everything else clear.
    ///
    /// Nothing was open on screen. Every doorstep script ends on an "await" node, which is the
    /// caller standing at the door waiting to be let in or turned away, and a terminal node is
    /// closed only by the view's Continue button. Nobody presses Continue on a caller they are
    /// about to judge - they press admit or refuse - so the dialogue service was left holding a
    /// conversation with somebody who had already walked away, for the rest of the shift.
    ///
    /// It cost more than the button. TryStartVisitorTalk will not open a script while the
    /// service is busy, so every caller after the first arrived at a door panel with no
    /// questions on it, and the two independent facts GDD 13.2 wants before a judgement can be
    /// graded correct were unreachable through the door itself.
    /// </summary>
    public sealed class DoorstepTalkTests
    {
        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();

            ServiceHub.State.BeginNight(1);
            ServiceHub.Interphone.Reset();
            ServiceHub.Dialogue.End();
        }

        /// <summary>What the interphone app does the frame a caller reaches the door.</summary>
        static void OpenTheDoorPanel()
        {
            var visitor = ServiceHub.Interphone.Active;
            if (visitor == null || ServiceHub.Dialogue.IsActive) return;
            ServiceHub.Dialogue.Start(visitor.conversationId);
        }

        [Test]
        public void JudgingACallerClosesTheirScript()
        {
            ServiceHub.Interphone.Enqueue("vis_miran_n1");
            OpenTheDoorPanel();
            Assert.IsTrue(ServiceHub.Dialogue.IsActive, "the door panel should have opened a script");

            ServiceHub.Interphone.Grant(VisitorAccessLevel.FloorPass);

            Assert.IsFalse(ServiceHub.Dialogue.IsActive,
                           "the caller has left the door; their conversation must go with them");
        }

        [Test]
        public void AHeldCallerKeepsTheirScript()
        {
            ServiceHub.Interphone.Enqueue("vis_miran_n1");
            OpenTheDoorPanel();

            // Hold is not a judgement: GDD 13.3 keeps them standing there.
            ServiceHub.Interphone.Grant(VisitorAccessLevel.Hold);

            Assert.IsTrue(ServiceHub.Dialogue.IsActive,
                          "a held caller is still at the door and still talking");
        }

        [Test]
        public void TheNextCallerGetsTheirOwnScript()
        {
            // Oh Mi-ran twice, on the two nights she comes - the same shared script both
            // times, which is what makes this the case that used to break. The stale
            // conversation satisfied the view's "is this caller's script open" check, so the
            // second caller was handed the first one's finished script with no questions left
            // on it.
            ServiceHub.Interphone.Enqueue("vis_miran_n1");
            ServiceHub.Interphone.Enqueue("vis_miran_n3");

            OpenTheDoorPanel();

            // Walk the first caller's script the way a caretaker does: ask something, hear the
            // answer, and end up on "await" - the node with no questions left on it, where the
            // caller is waiting to be judged. Leaving the script on its opening node instead
            // hides the bug, because an opening node still has three questions to show and the
            // second caller inherits them.
            ServiceHub.Dialogue.Choose("ask_tonight");
            ServiceHub.Dialogue.Advance();
            Assert.AreEqual(0, ServiceHub.Dialogue.CurrentLine.Choices.Count,
                            "the first caller should be out of questions and waiting");

            ServiceHub.Interphone.Grant(VisitorAccessLevel.Reject);

            Assert.AreEqual("vis_miran_n3", ServiceHub.Interphone.Active.visitorId,
                            "the queue should have moved on");

            OpenTheDoorPanel();
            Assert.IsTrue(ServiceHub.Dialogue.IsActive, "the second caller needs a script of their own");
            Assert.Greater(ServiceHub.Dialogue.CurrentLine.Choices.Count, 0,
                           "a caller with no questions on the panel cannot be checked against anything");
            Assert.AreEqual("start", ServiceHub.Dialogue.CurrentLine.NodeId,
                            "the second caller should be at the top of their own script, " +
                            "not inheriting where the first one left off");
        }

        [Test]
        public void AJudgementDoesNotCutOffSomethingElseOnTheLine()
        {
            // The radio, the handset and the door all share one dialogue service. Ending
            // whatever happens to be open would hang up on a call the caretaker is in.
            ServiceHub.Dialogue.Start("D_PROLOGUE_RADIO");
            ServiceHub.Interphone.Enqueue("vis_miran_n1");

            ServiceHub.Interphone.Grant(VisitorAccessLevel.FloorPass);

            Assert.IsTrue(ServiceHub.Dialogue.IsActive, "the radio was not the caller's script");
            Assert.AreEqual("D_PROLOGUE_RADIO", ServiceHub.Dialogue.Current.conversationId);
        }
    }
}
