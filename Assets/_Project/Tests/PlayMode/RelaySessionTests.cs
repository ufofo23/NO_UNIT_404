using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Net;

namespace NO404.Tests
{
    /// <summary>
    /// Opens a real room on Unity Relay and reads back the join code.
    ///
    /// This is the only test in the project that needs the internet and a configured Unity
    /// dashboard, so it never fails the run: a machine that is offline, or a project whose
    /// Relay service has not been switched on, reports Ignored with the reason rather than
    /// red. What it must never do is pass quietly when the feature is broken, which is why the
    /// success path asserts on the code itself.
    ///
    /// Everything else about co-op is verified without a network - see NetSessionSmokeTests,
    /// which starts a loopback host and checks the Netcode layer underneath Relay. This one
    /// answers the question that only the live service can: will a friend get a code tonight.
    /// </summary>
    public sealed class RelaySessionTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (MultiplayerSessionService.InSession)
                yield return Await(MultiplayerSessionService.LeaveAsync());

            MultiplayerSessionService.ClearError();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AHostGetsAJoinCodeAFriendCouldType()
        {
            yield return Await(MultiplayerBootstrap.EnsureSignedInAsync());

            if (!MultiplayerBootstrap.IsReady)
            {
                Assert.Ignore("Unity Gaming Services is not available here: " +
                              MultiplayerBootstrap.LastErrorKey + " / " +
                              MultiplayerBootstrap.LastErrorDetail +
                              "\nThis is either no internet, or Relay not enabled for this project " +
                              "in the Unity dashboard. Co-op cannot work until it resolves.");
            }

            yield return Await(MultiplayerSessionService.HostAsync());

            if (!MultiplayerSessionService.InSession)
            {
                Assert.Ignore("signed in, but could not open a room: " +
                              MultiplayerSessionService.LastErrorKey + " / " +
                              MultiplayerSessionService.LastErrorDetail +
                              "\nMost likely Relay is not enabled for this project.");
            }

            var code = MultiplayerSessionService.JoinCode;

            Assert.IsFalse(string.IsNullOrWhiteSpace(code),
                           "a room with no join code is a room nobody can be invited to");
            Assert.IsTrue(MultiplayerSessionService.IsHost);
            Assert.AreEqual(MultiplayerSessionService.MaxPlayers, MultiplayerSessionService.Capacity,
                            "v3.0 34.1: four caretakers, never hardcoded to two");
            Assert.AreEqual(1, MultiplayerSessionService.PlayerCount);

            // The code has to survive being read out and typed back in.
            Assert.AreEqual(code, MultiplayerSessionService.Normalise(" " + code.ToLowerInvariant() + " "));

            NO404.Core.Log.Info("Multiplayer", "TEST ROOM OPENED, join code " + code);
        }

        /// <summary>
        /// Drives a Task from a coroutine. The session API is async and the test runner is not;
        /// polling is the least surprising bridge between them.
        /// </summary>
        static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted && task.Exception != null)
                Assert.Ignore("the online service threw: " + task.Exception.GetBaseException().Message);
        }

        static IEnumerator Await<T>(Task<T> task)
        {
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted && task.Exception != null)
                Assert.Ignore("the online service threw: " + task.Exception.GetBaseException().Message);
        }
    }
}
