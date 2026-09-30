using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Anomalies;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Tests
{
    /// <summary>
    /// Every M event can actually be played (v2.1 spec 22).
    ///
    /// The failure this exists to catch is the one the data cannot show: an event whose
    /// judgement reads a counter that nothing in the building raises, or whose procedure has a
    /// step that nothing can tick off. The first makes the event unwinnable, the second means
    /// it never resolves at all - and both look completely reasonable in SeedContent, because
    /// the half that is missing lives in the world rather than in the definition.
    ///
    /// A PlayMode test because the answer only exists in a built scene: the props are created
    /// by WorldBuilder, and which floor they land on is the thing being checked.
    /// </summary>
    public sealed class ManualStageCoverageTests
    {
        GameObject _root;
        readonly List<ManualProp> _props = new List<ManualProp>();
        readonly Dictionary<string, HashSet<string>> _counters = new Dictionary<string, HashSet<string>>();
        readonly Dictionary<string, HashSet<string>> _objectives = new Dictionary<string, HashSet<string>>();

        /// <summary>Zones this test builds for itself: everything the core scene does not own.</summary>
        static string[] StreamedZones()
        {
            var zones = new List<string>();
            for (int i = 0; i < ZoneGroups.Streamed.Length; i++)
                zones.AddRange(ZoneGroups.ZonesIn(ZoneGroups.Streamed[i]));
            return zones.ToArray();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Bootstrap installs itself before the first scene loads, so the services exist.
            while (!ServiceHub.Ready) yield return null;

            ServiceHub.Zones.UnloadAllStreamed();
            int frames = 0;
            while (!ServiceHub.Zones.IsSettled && ++frames < 600) yield return null;

            // The office and the lobby come from the real core scene, which stays loaded for
            // the whole session. Building those here instead would overwrite the zone registry
            // and the office statics that the streaming and terminal tests read, and destroying
            // them afterwards would leave both null - a mess this test has no business making.
            ServiceHub.Zones.LoadCoreImmediate();
            yield return null;

            // The floors are a different matter: nothing else has them loaded right now, and a
            // coverage sweep needs all of them at once, which no streamed scene ever does.
            _root = new GameObject("CoverageFloors");
            WorldBuilder.Create(_root.transform).Build(StreamedZones());
            yield return null;

            // The inertness test asserts what props do before their event starts, so the
            // precondition is established rather than assumed - a session that had already
            // reached an anomaly would otherwise make it pass or fail for the wrong reason.
            ServiceHub.ManualEvents.Reset();

            _props.Clear();
            _counters.Clear();
            _objectives.Clear();

            // Scene-wide, so props in the core scene are counted alongside the floors above.
            _props.AddRange(Object.FindObjectsByType<ManualProp>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));

            foreach (var prop in _props)
            {
                var writtenCounters = new List<string>();
                var writtenObjectives = new List<string>();
                prop.CollectWrites(writtenCounters, writtenObjectives);

                Bucket(_counters, prop.EventId).UnionWith(writtenCounters);
                Bucket(_objectives, prop.EventId).UnionWith(writtenObjectives);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Only the floors this test built: the core scene is left exactly as it was found.
            var built = StreamedZones();
            for (int i = 0; i < built.Length; i++) ZoneRegistry.Unregister(built[i]);

            if (_root != null) Object.DestroyImmediate(_root);
            _props.Clear();
        }

        static HashSet<string> Bucket(Dictionary<string, HashSet<string>> map, string key)
        {
            if (string.IsNullOrEmpty(key)) key = "(none)";
            HashSet<string> set;
            if (!map.TryGetValue(key, out set)) map[key] = set = new HashSet<string>();
            return set;
        }

        static IEnumerable<ManualEventDefinition> Events()
        {
            return ServiceHub.Content.ManualEvents;
        }

        [Test]
        public void EveryEventHasPropsSomewhereInTheBuilding()
        {
            foreach (var definition in Events())
                Assert.IsTrue(_counters.ContainsKey(definition.eventId),
                              definition.eventId + " has no props anywhere in the building");
        }

        [Test]
        public void EveryJudgedCounterIsRaisedBySomething()
        {
            var missing = new List<string>();

            foreach (var definition in Events())
            {
                var written = Bucket(_counters, definition.eventId);
                CheckCounters(definition, definition.correctConditions, written, missing);
                CheckCounters(definition, definition.partialConditions, written, missing);
            }

            Assert.IsEmpty(missing,
                           "counters read by a judgement that nothing in the world writes: " +
                           string.Join(", ", missing.ToArray()));
        }

        static void CheckCounters(ManualEventDefinition definition, ManualCheckDefinition[] checks,
                                  HashSet<string> written, List<string> missing)
        {
            if (checks == null) return;
            for (int i = 0; i < checks.Length; i++)
            {
                var check = checks[i];
                if (check == null) continue;

                if (!written.Contains(check.counterId))
                    missing.Add(definition.eventId + "/" + check.counterId);

                if (!string.IsNullOrEmpty(check.otherCounterId) && !written.Contains(check.otherCounterId))
                    missing.Add(definition.eventId + "/" + check.otherCounterId);
            }
        }

        [Test]
        public void EveryRequiredStepCanBeCompleted()
        {
            var missing = new List<string>();

            foreach (var definition in Events())
            {
                var written = Bucket(_objectives, definition.eventId);
                for (int i = 0; i < definition.objectives.Length; i++)
                {
                    var objective = definition.objectives[i];
                    if (objective == null || objective.optional) continue;
                    if (!written.Contains(objective.objectiveId))
                        missing.Add(definition.eventId + "/" + objective.objectiveId);
                }
            }

            // A required step nothing completes is worse than a wrong answer: the event stays
            // Active for the rest of the night with no way to close it, and under the chain
            // model that takes every event after it down too (spec 0.5).
            Assert.IsEmpty(missing,
                           "required steps that nothing in the world completes: " +
                           string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void EveryPrintedProhibitionCanActuallyBeBroken()
        {
            // Spec 0.4 forbids the UI from marking the answer, which only means anything if
            // the wrong answer is reachable. A judgement clause naming a manual rule is a
            // promise that the rule can be broken - if nothing in the world can raise that
            // counter, the prohibition is decoration and the page is lying about the risk.
            //
            // Events with no rule-keyed clause are not covered here on purpose: M03 is failed
            // by not doing the work rather than by doing something forbidden, and spec 22 says
            // so outright.
            var unbreakable = new List<string>();

            foreach (var definition in Events())
            {
                var written = Bucket(_counters, definition.eventId);
                for (int i = 0; i < definition.correctConditions.Length; i++)
                {
                    var check = definition.correctConditions[i];
                    if (check == null || string.IsNullOrEmpty(check.ruleKey)) continue;
                    if (!written.Contains(check.counterId))
                        unbreakable.Add(definition.eventId + "/" + check.ruleKey);
                }
            }

            Assert.IsEmpty(unbreakable,
                           "prohibitions nothing in the world can break: " +
                           string.Join(", ", unbreakable.ToArray()));
        }

        [Test]
        public void PropsAreInertUntilTheirEventStarts()
        {
            // Spec 0.7.2: the same floor on every night. Nothing belonging to an anomaly may
            // answer the player before the anomaly is running.
            var context = new PlayerContext { ZoneId = ZoneIds.Lobby };

            foreach (var prop in _props)
            {
                var interactable = prop as IInteractable;
                if (interactable == null) continue;

                string reasonKey;
                Assert.IsFalse(interactable.CanInteract(context, out reasonKey),
                               prop.name + " can be used before its event has started");
            }
        }
    }
}
