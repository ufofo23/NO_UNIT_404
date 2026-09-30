using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Net;

namespace NO404.Tests
{
    /// <summary>
    /// Does the networking layer actually come up?
    ///
    /// Everything else about co-op is tested against its seams, which is worth something and
    /// is not worth much on its own: the failures that matter here only happen with a real
    /// NetworkManager, a real transport and a real spawn. Two have already been caught this
    /// way and neither was visible to a compiler or to any unit test - a NetworkManager
    /// nested under another object silently refuses to start, and a NetworkObject built at
    /// runtime has an id hash of zero and is silently unspawnable.
    ///
    /// A loopback host is used rather than a Relay session. Relay needs Unity Gaming Services
    /// credentials that a headless test run does not have, and the thing worth pinning here is
    /// the Netcode layer underneath it: prefabs registered, objects spawned, ownership right,
    /// publishing safe. Relay itself is verified by two machines and a join code.
    /// </summary>
    public sealed class NetSessionSmokeTests
    {
        NetSession _session;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // PlayMode runs the real Bootstrap, so the services install themselves - and so
            // does the NetSession this suite is about.
            while (!ServiceHub.Ready) yield return null;

            // Deliberately no EventBus.Clear(). In EditMode that is harmless housekeeping; in
            // PlayMode there is a live GameLoop subscribed to the bus, and tearing its
            // subscriptions out leaves every test that runs after this one in a game that has
            // stopped listening to itself.
            ServiceHub.ResetPlaythrough();
            _session = NetSession.Instance;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_session != null && _session.Running) _session.ShutdownTransport();
            yield return null;
            yield return null;
        }

        // -----------------------------------------------------------------
        // the generated prefabs
        // -----------------------------------------------------------------

        [Test]
        public void BothGeneratedPrefabsExistWhereTheSessionLooksForThem()
        {
            AssertPrefab(NetSession.ShiftPrefabResource);
            AssertPrefab(NetSession.PlayerPrefabResource);
        }

        static GameObject AssertPrefab(string resource)
        {
            var prefab = Resources.Load<GameObject>(resource);

            Assert.IsNotNull(prefab, "missing " + resource +
                                     " - run Tools > NO404 > Net > Rebuild Net Prefabs");

            var netObject = prefab.GetComponent<NetworkObject>();
            Assert.IsNotNull(netObject, resource + " needs a NetworkObject to be spawnable");

            // The whole reason these are assets rather than built in code. A zero hash is the
            // silent failure that cost a working build.
            Assert.AreNotEqual(0u, netObject.PrefabIdHash,
                               resource + " has no id hash, so Netcode cannot spawn it - this is " +
                               "what happens when a NetworkObject is created at runtime");

            return prefab;
        }

        [Test]
        public void ThePlayerPrefabCarriesItsOwnMovement()
        {
            var prefab = Resources.Load<GameObject>(NetSession.PlayerPrefabResource);

            Assert.IsNotNull(prefab.GetComponent<NetworkTransform>(),
                             "without this a colleague never appears to move");
            Assert.IsNotNull(prefab.GetComponent<NetPlayer>(),
                             "the component that decides owner from remote is the point of the prefab");
        }

        [Test]
        public void TheSessionRegistersThePrefabsAtBoot()
        {
            Assert.IsNotNull(_session, "Bootstrap should have created a session object");
            Assert.IsTrue(_session.PrefabsReady, "both prefabs must load before anyone can host");

            var manager = _session.Manager;
            Assert.IsNotNull(manager, "the session owns the NetworkManager");
            Assert.IsNotNull(manager.NetworkConfig.PlayerPrefab,
                             "no player prefab means nobody gets a body");
            Assert.IsFalse(manager.NetworkConfig.EnableSceneManagement,
                           "both machines build the same world locally; there is no scene to hand over");
        }

        [Test]
        public void TheNetworkManagerIsNotNestedUnderAnything()
        {
            // Netcode refuses to run a nested NetworkManager. This is the bug that made co-op
            // completely dead while every other check in the project passed.
            Assert.IsNull(_session.transform.parent,
                          "a nested NetworkManager silently refuses to start");
        }

        [Test]
        public void ASoloShiftIsAuthoritativeWithoutASession()
        {
            Assert.IsTrue(NetSession.Authoritative,
                          "single player must never wait on networking to simulate its own night");
        }

        // -----------------------------------------------------------------
        // join codes
        // -----------------------------------------------------------------

        [Test]
        public void JoinCodesSurviveBeingReadOutLoudAndTypedBackIn()
        {
            Assert.AreEqual("AB12CD", MultiplayerSessionService.Normalise("  ab12cd \n"));
            Assert.AreEqual("AB12CD", MultiplayerSessionService.Normalise("AB 12 CD"));
            Assert.IsNull(MultiplayerSessionService.Normalise("   "));
            Assert.IsNull(MultiplayerSessionService.Normalise(null));
        }

        // -----------------------------------------------------------------
        // a real host, over loopback
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator AHostSpawnsTheSharedShiftAndItsOwnPlayer()
        {
            Assert.IsTrue(_session.Manager.StartHost(), "the loopback host failed to start");

            for (int i = 0; i < 60 && NetShift.Instance == null; i++) yield return null;

            Assert.IsTrue(_session.Running);
            Assert.IsTrue(_session.IsHost);
            Assert.IsTrue(NetSession.Authoritative, "a host simulates the night");

            Assert.IsNotNull(NetShift.Instance,
                             "the shared shift object never spawned, so no client could ever be told " +
                             "what the night is doing");
            Assert.IsTrue(NetShift.Instance.IsSpawned);

            // v3.0 item 6: one player object per connection, owned by that connection.
            var mine = _session.Manager.LocalClient.PlayerObject;
            Assert.IsNotNull(mine, "the host should get a player object of its own");
            Assert.IsTrue(mine.IsOwner, "and should own it");
            Assert.AreEqual("P1", mine.GetComponent<NetPlayer>().PlayerId);
        }

        [UnityTest]
        public IEnumerator TheHostCanPublishALiveShiftWithoutThrowing()
        {
            Assert.IsTrue(_session.Manager.StartHost());
            for (int i = 0; i < 60 && NetShift.Instance == null; i++) yield return null;
            Assert.IsNotNull(NetShift.Instance);

            ServiceHub.State.BeginNight(3);
            ServiceHub.Interphone.Enqueue("vis_miran_n3");
            ServiceHub.Interphone.Grant(Visitors.VisitorAccessLevel.FloorPass);
            Assert.AreEqual(1, ServiceHub.ActiveVisitors.Count);

            // Somebody at the door, somebody upstairs, and a board with a row on it exercises
            // every field the snapshot carries.
            for (int i = 0; i < 5; i++)
            {
                NetShift.Instance.Publish();
                yield return null;
            }

            Assert.Pass("published a live shift for five frames without throwing");
        }

        [UnityTest]
        public IEnumerator TheHostIsOnTheRosterAndCanBeSeenStanding()
        {
            Assert.IsTrue(_session.Manager.StartHost());
            for (int i = 0; i < 60 && NetShift.Instance == null; i++) yield return null;

            Assert.AreEqual("P1", ServiceHub.Presence.LocalPlayerId,
                            "the host is the first caretaker on the shift");

            ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            Assert.IsTrue(ServiceHub.Presence.AnyoneIn(ZoneIds.Lobby));
            Assert.IsTrue(Visitors.DoorReadService.AtTheGlass,
                          "somebody at the glass is somebody at the glass, session or not");
        }
    }
}
