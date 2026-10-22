using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Cases;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Tests
{
    /// <summary>
    /// Nights 2/4/6 (v5.1 11, 13, 15) in the built world.
    ///
    /// SubquestWorldTests already looks at every prop from the side it faces. What it cannot
    /// see is two things standing in the same place with their faces apart - which is what a
    /// new night's props do when they are dropped onto walls the earlier nights and the mains
    /// already furnished. The rule is that props never share a volume; this holds the even
    /// nights to it against every interactable in the building, and plays the two props that
    /// do more than hand over a card.
    /// </summary>
    public sealed class EvenNightSubquestWorldTests
    {
        GameObject _root;

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
            ServiceHub.Dialogue.End();
            _root = new GameObject("EvenSubquestWorld");
            WorldBuilder.Create(_root.transform).Build(Streamed);
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            ServiceHub.Dialogue.End();
            Object.Destroy(_root);
            ServiceHub.ResetPlaythrough();
            yield return null;
        }

        static void Open(int night, string caseId)
        {
            ServiceHub.State.BeginNight(night);
            ServiceHub.Cases.BeginNight(night);
            ServiceHub.Cases.RestoreDraw(new[] { caseId });
            Assert.IsTrue(ServiceHub.Cases.TryStartCase(caseId), caseId + " did not start");
        }

        static SubquestInteractable Prop(string name)
        {
            foreach (var prop in Object.FindObjectsByType<SubquestInteractable>(FindObjectsSortMode.None))
                if (prop.name == name) return prop;
            Assert.Fail("no prop called " + name);
            return null;
        }

        static bool IsEven(string caseId)
        {
            return caseId != null && caseId.Length > 1 && (caseId[1] == '2' || caseId[1] == '4' || caseId[1] == '6');
        }

        [UnityTest]
        public IEnumerator NoEvenNightPropSharesAVolumeWithAnythingInteractable()
        {
            var problems = new List<string>();
            int checkedProps = 0;

            foreach (var prop in Object.FindObjectsByType<SubquestInteractable>(FindObjectsSortMode.None))
            {
                if (!IsEven(prop.CaseId)) continue;
                checkedProps++;

                var box = prop.GetComponent<BoxCollider>();
                Assert.IsNotNull(box, prop.name);
                Vector3 centre = prop.transform.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size, prop.transform.lossyScale) * 0.49f;

                foreach (var other in Physics.OverlapBox(centre, half, prop.transform.rotation))
                {
                    if (other.gameObject == prop.gameObject) continue;
                    if (other.GetComponentInParent<IInteractable>() == null) continue;
                    problems.Add(prop.name + " (" + prop.CaseId + ") overlaps " + other.name);
                }
            }

            Assert.AreEqual(73, checkedProps, "the even nights' props are not all in the world");
            CollectionAssert.IsEmpty(problems, string.Join("; ", problems.ToArray()));
            yield return null;
        }

        /// <summary>
        /// The risky branch of a quest whose careful branch is the one being filed. Not the 404
        /// mailbox slot: taking what is in it is that quest's Correct row (v5.1 N4-R12).
        /// </summary>
        static readonly HashSet<string> RiskyActions = new HashSet<string>
        {
            "n4r11_scrape", "n4r13_watch", "n6r05_open", "n6r06_clear", "n6r08_vent", "n6r09_force"
        };

        /// <summary>Whatever the conversation offers first - the careful answer in all three.</summary>
        static void AnswerFirst()
        {
            for (int guard = 0; guard < 24 && ServiceHub.Dialogue.IsActive; guard++)
            {
                var line = ServiceHub.Dialogue.CurrentLine;
                if (line != null && line.HasChoices) ServiceHub.Dialogue.Choose(line.Choices[0].choiceId);
                else ServiceHub.Dialogue.Advance();
            }
        }

        /// <summary>
        /// Each of the 42 even-night quests, opened on its own and played only with what the
        /// building gives it: its props, its camera, its call or its door. Every one has to end
        /// in an accepted report, and the careful way through has to reach a Correct row - the
        /// only exception being a quest whose variant tonight has no Correct row on offer.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryEvenQuestCanBeFiledFromWhatTheBuildingGivesIt()
        {
            var problems = new List<string>();
            var props = Object.FindObjectsByType<SubquestInteractable>(FindObjectsSortMode.None);
            var quests = new List<string>();
            foreach (var definition in ServiceHub.Content.Cases)
                if (definition != null && IsEven(definition.caseId) && SubquestRules.Handles(definition.caseId))
                    quests.Add(definition.caseId);
            Assert.AreEqual(42, quests.Count);

            foreach (var id in quests)
            {
                ServiceHub.ResetPlaythrough();
                ServiceHub.Dialogue.End();
                int night = id[1] - '0';
                Open(night, id);
                var definition = ServiceHub.Content.FindCase(id);

                // Its call, if it has one, answered the careful way.
                ServiceHub.Phone.ScheduleNight(night);
                ServiceHub.Phone.NotifyCaseStarted(id);
                ServiceHub.Phone.Tick();
                if (ServiceHub.Phone.IsRinging && ServiceHub.Phone.Active.caseId == id)
                {
                    ServiceHub.Phone.Answer();
                    AnswerFirst();
                }

                // Its cameras, the way the CCTV app reports a channel being watched.
                foreach (var objective in definition.objectives)
                    if (objective.type == ObjectiveType.ViewCctvChannel)
                        ServiceHub.Cases.NotifyObjective(ObjectiveType.ViewCctvChannel, objective.targetId);

                // Its props, twice over so an action that needs another first (a breaker
                // before a panel) gets its turn.
                for (int pass = 0; pass < 2; pass++)
                    foreach (var prop in props)
                    {
                        if (prop.CaseId != id || RiskyActions.Contains(prop.Action ?? "")) continue;
                        if (!string.IsNullOrEmpty(prop.RequiredFlag) && !ServiceHub.State.GetFlag(prop.RequiredFlag)) continue;
                        string reason;
                        if (!prop.CanInteract(new PlayerContext(), out reason)) continue;
                        prop.Interact(new PlayerContext());
                        AnswerFirst();
                    }

                var runtime = ServiceHub.Cases.Find(id);
                if (!runtime.AllRequiredObjectivesComplete())
                {
                    problems.Add(id + ": the building does not give everything its report needs");
                    continue;
                }

                var attached = new List<string>();
                foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);

                bool anyCorrectOnOffer = false;
                foreach (var d in definition.decisions)
                {
                    string unmet;
                    if (d.quality == DecisionQuality.Correct && ConditionEvaluator.EvaluateAll(d.availability, out unmet))
                        anyCorrectOnOffer = true;
                }

                DecisionResult filed = DecisionResult.Rejected("none");
                foreach (var d in definition.decisions)
                    if (d.quality == DecisionQuality.Correct && !filed.Accepted)
                        filed = ServiceHub.Cases.SubmitDecision(id, d.decisionId, attached);

                if (!filed.Accepted && anyCorrectOnOffer)
                    problems.Add(id + ": the careful way through cannot file its Correct row (" + filed.RejectReason + ")");

                foreach (var d in definition.decisions)
                    if (!filed.Accepted) filed = ServiceHub.Cases.SubmitDecision(id, d.decisionId, attached);

                if (!filed.Accepted) problems.Add(id + ": no row can be filed (" + filed.RejectReason + ")");
                yield return null;
            }

            CollectionAssert.IsEmpty(problems, string.Join("; ", problems.ToArray()));
        }

        /// <summary>v5.1 N4-R10: knocking on 303 is where the testimony is given.</summary>
        [UnityTest]
        public IEnumerator Knocking303StartsSunjasTestimony()
        {
            Open(4, "N4-R10");
            var door = Prop("Door303Sunja");

            string reason;
            Assert.IsTrue(door.CanInteract(new PlayerContext(), out reason), reason);
            door.Interact(new PlayerContext());
            yield return null;

            Assert.IsTrue(ServiceHub.Dialogue.IsActive);
            Assert.AreEqual("D_N4R10_SUNJA", ServiceHub.Dialogue.Current.conversationId);
            Assert.IsFalse(door.CanInteract(new PlayerContext(), out reason), "the door knocks again mid-conversation");
        }

        /// <summary>v5.1 N6-R05: the door inside 404, opened from where it stands.</summary>
        [UnityTest]
        public IEnumerator TheUnplannedDoorOpensOnceFromInside404()
        {
            Open(6, "N6-R05");
            var plate = Prop("UnplannedDoorPlate");
            var handle = Prop("UnplannedDoorHandle");

            string reason;
            plate.Interact(new PlayerContext());
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_N6R05_DOOR"));

            int san = ServiceHub.Vitals.San;
            Assert.IsTrue(handle.CanInteract(new PlayerContext(), out reason), reason);
            handle.Interact(new PlayerContext());
            Assert.AreEqual(san - 10, ServiceHub.Vitals.San);
            Assert.IsFalse(handle.CanInteract(new PlayerContext(), out reason), "the door opens twice");
            yield return null;
        }
    }
}
