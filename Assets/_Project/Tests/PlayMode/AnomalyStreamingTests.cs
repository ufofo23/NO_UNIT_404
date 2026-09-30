using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Anomalies;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// An anomaly in flight while its floor is unloaded and rebuilt
    /// (v2.1 spec 30.2 path 6, spec 0.7.2).
    ///
    /// This is the path a player takes constantly and a test almost never does: the procedure
    /// is half done on the fifth floor, they go down to the office to check a record, and the
    /// fifth floor is destroyed behind them. When they come back it is built again from
    /// scratch - new GameObjects, new components, no memory of anything.
    ///
    /// What has to survive is not the props, which cannot. It is that the rebuilt props read
    /// the running event and put themselves back in the state it implies. Spec 0.7.2 is the
    /// reason the design works that way at all, and nothing was checking it.
    ///
    /// Real streaming rather than a hand-built world, because the thing being tested is the
    /// scene actually going away.
    /// </summary>
    public sealed class AnomalyStreamingTests
    {
        // M17 lives on the fifth floor, which is a streamed group with nothing else in it.
        const string Event = ManualEventIds.M17_Unit504Noise;
        const string Zone = ZoneIds.Floor05;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;

            ServiceHub.Zones.UnloadAllStreamed();
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;

            ServiceHub.Zones.LoadCoreImmediate();
            yield return null;

            ServiceHub.ManualEvents.Reset();
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            ServiceHub.ManualEvents.Reset();
            ServiceHub.Zones.UnloadAllStreamed();

            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;
        }

        static IEnumerator WaitForZone(string zoneId)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!ZoneRegistry.IsLoaded(zoneId) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ZoneRegistry.IsLoaded(zoneId), "timed out waiting for " + zoneId);
        }

        static List<ManualProp> PropsFor(string eventId)
        {
            var found = new List<ManualProp>();
            var all = Object.FindObjectsByType<ManualProp>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].EventId == eventId) found.Add(all[i]);
            return found;
        }

        /// <summary>A prop is awake when the collider it interacts through is on.</summary>
        static int AwakeProps(List<ManualProp> props)
        {
            int count = 0;
            for (int i = 0; i < props.Count; i++)
            {
                var collider = props[i].GetComponent<Collider>();
                if (collider != null && collider.enabled) count++;
            }
            return count;
        }

        [UnityTest]
        public IEnumerator APropIsInertUntilItsEventIsRunning()
        {
            ServiceHub.Zones.RequestZone(Zone);
            yield return WaitForZone(Zone);
            yield return null;

            var props = PropsFor(Event);
            Assert.Greater(props.Count, 0, "the fifth floor should carry M17");

            // Spec 0.7.2: the floor has the same shape on every night. What changes is whether
            // the anomaly is happening, not whether the room contains it.
            Assert.AreEqual(ManualEventState.Dormant, ServiceHub.ManualEvents.Find(Event).State);
        }

        [UnityTest]
        public IEnumerator TheWorkSurvivesTheFloorBeingDestroyedAndRebuilt()
        {
            ServiceHub.Zones.RequestZone(Zone);
            yield return WaitForZone(Zone);
            yield return null;

            ServiceHub.State.BeginNight(4);
            Assert.IsTrue(ServiceHub.ManualEvents.Begin(Event), "M17 should be startable on night 4");
            yield return null;

            int awakeBefore = AwakeProps(PropsFor(Event));
            Assert.Greater(awakeBefore, 0, "an active event should wake its props");

            // Half the procedure, then away.
            ServiceHub.ManualEvents.SetCounter(Event, "causeChoice", 2);
            ServiceHub.ManualEvents.CompleteObjective(Event, "obj_measure");

            ServiceHub.Zones.UnloadAllStreamed();
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;

            Assert.IsFalse(ZoneRegistry.IsLoaded(Zone), "the floor should really be gone");
            Assert.AreEqual(0, PropsFor(Event).Count, "and its props with it");

            // The event does not live in the scene, so it is still running.
            var runtime = ServiceHub.ManualEvents.Find(Event);
            Assert.AreEqual(ManualEventState.Active, runtime.State);
            Assert.AreEqual(2, runtime.Counter("causeChoice"));

            // Back up the stairs.
            ServiceHub.Zones.RequestZone(Zone);
            yield return WaitForZone(Zone);
            yield return null;

            var rebuilt = PropsFor(Event);
            Assert.Greater(rebuilt.Count, 0, "the floor should have been rebuilt");
            Assert.AreEqual(awakeBefore, AwakeProps(rebuilt),
                            "the rebuilt props should wake to the event that is still running");

            // And the procedure carries on from where it was rather than starting over.
            Assert.AreEqual(2, runtime.Counter("causeChoice"), "the reading it already took");
            Assert.IsTrue(runtime.IsComplete("obj_measure"), "and the step it already finished");
        }

        [UnityTest]
        public IEnumerator ARebuiltFloorAlsoComesBackWithItsRiskDressing()
        {
            // The same rule for the dressing: the corridor is destroyed and rebuilt, and the
            // floor it belongs to is still the floor it was.
            ServiceHub.Risk.AddFloorRisk(FloorPlan.F5, 4, "test");

            try
            {
                ServiceHub.Zones.RequestZone(Zone);
                yield return WaitForZone(Zone);
                yield return null;

                var dressing = ZoneRegistry.Find(Zone).GetComponent<FloorDressing>();
                Assert.IsNotNull(dressing, "a rebuilt corridor still belongs to a floor");
                Assert.AreEqual(FloorRiskTier.Hostile, dressing.AppliedTier,
                                "and that floor is still hostile");
            }
            finally
            {
                ServiceHub.Risk.AddFloorRisk(FloorPlan.F5, -ServiceHub.Risk.FloorRisk(FloorPlan.F5), "test");
            }
        }
    }
}
