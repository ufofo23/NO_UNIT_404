using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Interaction;

namespace NO404.Tests
{
    /// <summary>
    /// The two invariants of N2-M01 that are only in the room (GDD v5.1 11): the lobby wall
    /// clock and the dry floor inside the entrance.
    ///
    /// Content tests can say the evidence is defined. Only the built lobby can say there is
    /// something in it to walk up to, and a quest whose answer needs two invariants is not
    /// finishable by leaving the desk if the room it sends the caretaker to is empty.
    /// </summary>
    public sealed class NightTwoLobbyTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.ResetPlaythrough();
        }

        [TearDown]
        public void TearDown()
        {
            MainOnlyMode.Set(false);
            ServiceHub.Save.WritesSuspended = false;

            var loop = GameLoop.Instance;
            if (loop != null) loop.DebugReturnToMenu();
        }

        static IEnumerator OpenNight(GameLoop loop, int night)
        {
            Assert.IsTrue(loop.DebugNewGameAt(new DevStartOptions { Night = night, MainOnly = true }));

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   !(loop.Mode == GameMode.Playing && ServiceHub.State.NightIndex == night))
                yield return null;

            Assert.AreEqual(night, ServiceHub.State.NightIndex, "the shift never started");
        }

        static EvidencePickup FixtureFor(string evidenceId)
        {
            foreach (var pickup in Object.FindObjectsByType<EvidencePickup>(FindObjectsSortMode.None))
                if (pickup.EvidenceId == evidenceId) return pickup;

            return null;
        }

        [UnityTest]
        public IEnumerator BothRoomInvariantsAreInTheLobbyAndStayThereOnceRead()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            yield return OpenNight(loop, 2);

            foreach (var evidenceId in new[] { "EV_LOBBY_CLOCK", "EV_LOBBY_FOOTPRINTS" })
            {
                var fixture = FixtureFor(evidenceId);
                Assert.IsNotNull(fixture, "nothing in the building gives " + evidenceId);

                string reason;
                var context = new PlayerContext();
                Assert.IsTrue(fixture.CanInteract(context, out reason),
                              evidenceId + " cannot be read on night 2 (" + reason + ")");

                fixture.Interact(context);
                yield return null;

                Assert.IsTrue(ServiceHub.Evidence.Has(evidenceId), evidenceId + " was read and not recorded");

                // A clock that vanished from the wall when it was looked at would be a
                // stranger thing than anything this night is trying to show.
                var renderer = fixture.GetComponent<Renderer>();
                Assert.IsTrue(renderer != null && renderer.enabled, evidenceId + " disappeared when it was read");
            }
        }

        /// <summary>
        /// On any other night they are furniture. The floor being dry is only a fact about
        /// the rain on night 2; the same mat on night 5 must not hand over that sentence.
        /// </summary>
        [UnityTest]
        public IEnumerator OnAnotherNightTheSameFixturesSayNothing()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop);

            yield return OpenNight(loop, 3);

            foreach (var evidenceId in new[] { "EV_LOBBY_CLOCK", "EV_LOBBY_FOOTPRINTS" })
            {
                var fixture = FixtureFor(evidenceId);
                Assert.IsNotNull(fixture, evidenceId + " has no fixture");

                string reason;
                Assert.IsFalse(fixture.CanInteract(new PlayerContext(), out reason),
                               evidenceId + " can still be read on night 3");
            }
        }
    }
}
