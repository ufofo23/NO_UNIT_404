using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Tests
{
    /// <summary>
    /// Everything the caretaker can interact with is resting on something.
    ///
    /// A prop is placed by a number in code, and a number can put a paper a hand's width above
    /// a counter that is not there, or a panel twenty centimetres off a wall. Nothing else in
    /// the suite notices: the ray still reaches it and the quest still closes. This looks at
    /// each one with every quest open and asks whether it touches the building - a wall, a
    /// floor, a ceiling, a piece of furniture - anything that is not itself an interactable.
    /// </summary>
    public sealed class PropPlacementTests
    {
        GameObject _root;

        /// <summary>How far a prop may stand off its support and still count as on it.</summary>
        const float Contact = 0.04f;

        static readonly string[] Streamed =
        {
            ZoneIds.Parking, ZoneIds.RecyclingYard, ZoneIds.Machinery, ZoneIds.PumpRoom, ZoneIds.Archive,
            ZoneIds.Floor03, ZoneIds.Floor04, ZoneIds.ServicePassage, ZoneIds.Unit404, ZoneIds.Floor05, ZoneIds.Floor06
        };

        [UnitySetUp]
        public IEnumerator Setup()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.Zones.UnloadAllStreamed();
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;
            ServiceHub.Zones.LoadCoreImmediate();
            yield return null;
            ServiceHub.ResetPlaythrough();
            _root = new GameObject("PlacementWorld");
            WorldBuilder.Create(_root.transform).Build(Streamed);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(_root);
            ServiceHub.ResetPlaythrough();
            yield return null;
        }

        /// <summary>Props cannot hold each other up; a door can hold its own peephole.</summary>
        static bool IsProp(Collider c)
        {
            return c.GetComponentInParent<SubquestInteractable>() != null ||
                   c.GetComponentInParent<MainQuestInteractable>() != null ||
                   c.GetComponentInParent<EvidencePickup>() != null;
        }

        [UnityTest]
        public IEnumerator NothingTheCaretakerCanTouchIsFloating()
        {
            // A prop's own box is read from its collider, so it need not be switched on; what
            // it rests on - walls, floors, furniture, doors - always is.
            yield return null;
            Physics.SyncTransforms();

            var floating = new List<string>();
            var seen = new HashSet<Object>();
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(behaviour is IInteractable) || !seen.Add(behaviour.gameObject)) continue;
                if (behaviour is ZoneTransition) continue;   // doors are the wall
                // The v2.1 night events no longer run on any night (every night has its v5.1
                // pool), so their props never appear; the 2009 rescue is an apparition.
                if (behaviour is NO404.Anomalies.ManualProp || behaviour.name == "LastRescueEcho") continue;

                var box = behaviour.GetComponent<BoxCollider>();
                if (box == null) continue;

                var t = behaviour.transform;
                Vector3 centre = t.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f + Vector3.one * Contact;

                bool supported = false;
                foreach (var other in Physics.OverlapBox(centre, half, t.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(t) || IsProp(other)) continue;
                    supported = true;
                    break;
                }

                if (!supported)
                {
                    var zone = t.parent;
                    floating.Add((zone != null ? zone.name + "/" : "") + behaviour.name +
                                 " (" + behaviour.GetType().Name + ") at " + t.localPosition.ToString("F2"));
                }
            }

            floating.Sort();
            CollectionAssert.IsEmpty(floating, floating.Count + " floating: " + string.Join("; ", floating.ToArray()));
        }
    }
}
