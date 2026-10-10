using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Tests
{
    public sealed class SelectedMainWorldTests
    {
        GameObject _root;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.Zones.UnloadAllStreamed();
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;
            ServiceHub.ResetPlaythrough();
            ServiceHub.Dialogue.End();
            _root = new GameObject("SelectedMainWorld");
            // Streamed zones only. The elevator lives in the core scene; building a second
            // copy here and destroying it unregistered the real one for every later test.
            WorldBuilder.Create(_root.transform).Build(new[] { ZoneIds.Parking, ZoneIds.Floor03, ZoneIds.Floor04, ZoneIds.ServicePassage });
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveFireRecordsAreReachableOnB1AndOnlyWorkDuringTheirQuest()
        {
            foreach (string name in new[] { "FireCallTape2009", "DutyAccessLog2009", "ChoiSignedApproval", "Unit404DeletionHistory" })
            {
                MainQuestInteractable prop = null;
                foreach (var candidate in _root.GetComponentsInChildren<MainQuestInteractable>())
                    if (candidate.name == name) prop = candidate;
                Assert.IsNotNull(prop, name);
                Assert.AreEqual(FloorPlan.B1, FloorPlan.FloorOfZone(ZoneIds.Parking));
                var context = new PlayerContext();
                string reason;
                Assert.IsFalse(prop.CanInteract(context, out reason), "Future records must be gated");
                ServiceHub.State.BeginNight(5);
                ServiceHub.Cases.BeginNight(5);
                ServiceHub.Cases.TryStartCase("N5-M01");
                Assert.IsTrue(prop.CanInteract(context, out reason));
                Physics.SyncTransforms();
                var center = prop.GetComponent<Collider>().bounds.center;
                RaycastHit hit;
                Assert.IsTrue(Physics.Raycast(center - Vector3.forward, Vector3.forward, out hit, 1.2f), name);
                Assert.AreEqual(prop.gameObject, hit.collider.gameObject, name + " is blocked by another prop");
                prop.Interact(context);
                Assert.IsFalse(prop.CanInteract(context, out reason), "Collected records must survive scene rebuilds");
                ServiceHub.ResetPlaythrough();
            }
            yield return null;
        }
    }
}
