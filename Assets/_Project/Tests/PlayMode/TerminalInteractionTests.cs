using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Tests
{
    /// <summary>
    /// Regression coverage for the office terminal -> facility PC path. Two separate bugs have
    /// hit this exact interaction (OnActivated never wired, and a lingering dialogue leaving
    /// the PC canvas covered), so it is worth a real PlayMode assertion rather than trusting
    /// manual playtesting alone.
    /// </summary>
    public sealed class TerminalInteractionTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
        }

        [UnityTest]
        public IEnumerator InteractingWithTheOfficeTerminalOpensThePc()
        {
            GameLoop.Instance.NewGame();
            yield return null;
            yield return null;

            Assert.IsNotNull(WorldBuilder.OfficeTerminal, "the terminal should exist in the core scene after NewGame");
            Assert.IsNotNull(WorldBuilder.OfficeTerminal.OnActivated,
                             "GameLoop.EnsureWorld must wire OnActivated or pressing E does nothing");

            var context = new PlayerContext { ZoneId = ZoneIds.Office, DistanceToTarget = 0.5f };
            string reason;
            Assert.IsTrue(WorldBuilder.OfficeTerminal.CanInteract(context, out reason),
                          "the terminal should be interactable on a fresh game: " + reason);

            WorldBuilder.OfficeTerminal.Interact(context);
            yield return null;

            Assert.IsTrue(ServiceHub.Player.InPcMode, "the facility PC should be open right after the interaction");
        }
    
        /// <summary>
        /// GDD 13. Judging the caller at the door is half of what this game is, and a playtest
        /// found all four of its controls dead: 문 열기 / 대기 요청 / 거절 / 경비 호출 pressed,
        /// highlighted, and did nothing at all.
        ///
        /// So the whole path gets a real assertion rather than manual checking - a caller is
        /// put at the door and judged, and the service has to actually resolve them.
        /// </summary>
        [UnityTest]
        public IEnumerator JudgingTheCallerAtTheDoorResolvesThem()
        {
            GameLoop.Instance.NewGame();
            yield return null;
            yield return null;

            var interphone = ServiceHub.Interphone;
            interphone.Reset();

            interphone.Enqueue("vis_n0_courier");
            Assert.IsNotNull(interphone.Active, "the caller never reached the door");
            Assert.AreEqual("vis_n0_courier", interphone.Active.visitorId);

            interphone.Grant(Visitors.VisitorAccessLevel.FloorPass);

            Assert.AreEqual(Visitors.VisitorAccessLevel.FloorPass,
                            interphone.DecisionFor("vis_n0_courier"),
                            "the decision was not recorded - the door buttons do nothing");
            Assert.IsNull(interphone.Active, "the caller is still at the door after being judged");
        }

        /// <summary>
        /// Every one of the four decisions has to move the queue. Hold is the exception by
        /// design - it keeps them there and costs time (GDD 13.3) - so it is asserted as such
        /// rather than skipped.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryDoorDecisionDoesSomething()
        {
            GameLoop.Instance.NewGame();
            yield return null;
            yield return null;

            var interphone = ServiceHub.Interphone;

            Visitors.VisitorAccessLevel[] resolving =
            {
                Visitors.VisitorAccessLevel.FloorPass,
                Visitors.VisitorAccessLevel.Reject,
                Visitors.VisitorAccessLevel.Reject
            };

            for (int i = 0; i < resolving.Length; i++)
            {
                interphone.Reset();
                interphone.Enqueue("vis_n0_courier");
                Assert.IsNotNull(interphone.Active);

                interphone.Grant(resolving[i]);

                Assert.IsNull(interphone.Active, resolving[i] + " left the caller at the door");
                Assert.AreEqual(resolving[i], interphone.DecisionFor("vis_n0_courier"),
                                resolving[i] + " was not recorded");
            }

            // Hold keeps them waiting on purpose, and costs game time for it.
            interphone.Reset();
            interphone.Enqueue("vis_n0_courier");
            int before = ServiceHub.Clock.GameSecond;

            interphone.Grant(Visitors.VisitorAccessLevel.Hold);

            Assert.IsNotNull(interphone.Active, "hold is supposed to keep them at the door");
            Assert.Greater(ServiceHub.Clock.GameSecond, before, "hold has to cost time (GDD 13.3)");
        }

        /// <summary>
        /// A bolted office door stops the queue (GDD 15.6), and that refusal has to be visible.
        /// It used to be a silent early return, which is indistinguishable from a broken button.
        /// </summary>
        [UnityTest]
        public IEnumerator ARefusedDecisionSaysWhy()
        {
            GameLoop.Instance.NewGame();
            yield return null;
            yield return null;

            var interphone = ServiceHub.Interphone;
            interphone.Reset();
            interphone.Enqueue("vis_n0_courier");

            string notified = null;
            System.Action<NotificationEvent> handler = evt => notified = evt.BodyKey;
            EventBus.Subscribe(handler);

            interphone.SetDoorBlocked(true);
            interphone.Grant(Visitors.VisitorAccessLevel.FloorPass);

            EventBus.Unsubscribe(handler);
            interphone.SetDoorBlocked(false);

            Assert.IsNotNull(interphone.Active, "a blocked door must not resolve the caller");
            Assert.AreEqual("ui.notify.front_door_blocked", notified,
                            "the player has to be told why the door buttons did nothing");
        }
}
}
