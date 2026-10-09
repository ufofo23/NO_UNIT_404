using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// N2-M01, the two Seo Jun-hos (GDD v5.1 11).
    ///
    /// The campaign walk closes this quest by submitting a decision straight into
    /// CaseService, which proves the night can end and says nothing about what the caretaker
    /// is shown on the way. This file is for the quest as something a person reads and plays.
    /// </summary>
    public sealed class NightTwoMainTests
    {
        const string CaseId = "N2-M01";

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        /// <summary>
        /// Every line the quest puts on screen is in the string table, in both languages.
        ///
        /// The main quests went in with their keys written and their strings not: the task
        /// card, all four report options and every reason on the night summary came up as
        /// the key itself. Nothing failed, because a missing string is a warning in the log
        /// and a build that runs.
        /// </summary>
        [Test]
        public void EverythingTheQuestShowsIsInTheStringTableInBothLanguages()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            Assert.IsNotNull(definition, CaseId + " is not in the content database");

            var keys = new List<string> { definition.titleKey, definition.summaryKey };

            foreach (var objective in definition.objectives) keys.Add(objective.titleKey);

            foreach (var decision in definition.decisions)
            {
                keys.Add(decision.labelKey);
                keys.Add(decision.resultKey);
                AddReasons(decision.consequences, keys);
            }

            AddReasons(definition.consequences, keys);
            if (definition.failSafe != null && definition.failSafe.enabled)
                keys.Add(definition.failSafe.notifyKey);

            foreach (var evidenceId in definition.evidenceIds)
            {
                var evidence = ServiceHub.Content.FindEvidence(evidenceId);
                Assert.IsNotNull(evidence, CaseId + " lists evidence that does not exist: " + evidenceId);
                keys.Add(evidence.displayNameKey);
                keys.Add(evidence.descriptionKey);
            }

            var missing = new List<string>();
            foreach (var language in new[] { "en", "ko" })
            {
                var loc = new LocalizationService();
                loc.Initialize(language);

                foreach (var key in keys)
                {
                    if (string.IsNullOrEmpty(key)) { missing.Add("(empty key)"); continue; }
                    if (!loc.HasKey(key) || string.IsNullOrWhiteSpace(loc.Get(key)))
                        missing.Add(language + ":" + key);
                }
            }

            Assert.IsEmpty(missing, "missing strings: " + string.Join(", ", missing.ToArray()));
        }

        // ---- evidence channels (GDD v5.1 19.2) ---------------------------------

        static List<string> InvariantsOf(CaseDefinition definition)
        {
            var invariants = new List<string>();
            foreach (var evidenceId in definition.evidenceIds)
            {
                var evidence = ServiceHub.Content.FindEvidence(evidenceId);
                if (evidence == null || evidence.tags == null) continue;
                if (System.Array.IndexOf(evidence.tags, EvidenceTags.Invariant) >= 0) invariants.Add(evidenceId);
            }
            return invariants;
        }

        /// <summary>The night-2 camera anomalies that hand over a piece of evidence.</summary>
        static HashSet<string> GrantedByTheCameraWall()
        {
            var granted = new HashSet<string>();
            foreach (var anomaly in ServiceHub.Content.Anomalies)
                if (anomaly.nightIndex == 2 && !string.IsNullOrEmpty(anomaly.evidenceId))
                    granted.Add(anomaly.evidenceId);
            return granted;
        }

        /// <summary>What talking to this caller can put in the tray.</summary>
        static HashSet<string> GrantedByTalkingTo(string conversationId)
        {
            var granted = new HashSet<string>();
            var conversation = ServiceHub.Content.FindDialogue(conversationId);
            Assert.IsNotNull(conversation, conversationId + " is not in the content database");

            foreach (var node in conversation.nodes)
                foreach (var consequence in node.onEnter)
                    if (consequence != null && consequence.type == ConsequenceType.GrantEvidence)
                        granted.Add(consequence.targetId);

            return granted;
        }

        /// <summary>
        /// The scenario names four invariants and the rule wants two of them, so there have
        /// to be four to choose from: a caretaker who only ever finds two has no slack, and
        /// the "at least two" rule quietly becomes "exactly these two".
        /// </summary>
        [Test]
        public void TheQuestOffersFourInvariantsAndTheSightingIsNotOneOfThem()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            var invariants = InvariantsOf(definition);

            CollectionAssert.AreEquivalent(
                new[] { "E06_CCTV_WEATHER_MISMATCH", "EV_WAYBILL_ORDER", "EV_LOBBY_CLOCK", "EV_LOBBY_FOOTPRINTS" },
                invariants);

            // Seeing two of him is the question, not an answer to it.
            CollectionAssert.DoesNotContain(invariants, "EV_CAM02_DOUBLE");
            CollectionAssert.Contains(definition.evidenceIds, "EV_CAM02_DOUBLE");
        }

        /// <summary>
        /// v5.1 19.2: a main cannot be settled from one screen. The camera wall hands over
        /// the sighting and one invariant; the rest have to come from the door and the room.
        /// </summary>
        [Test]
        public void TheCameraWallAloneCannotReachTwoInvariants()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            var onCamera = GrantedByTheCameraWall();

            CollectionAssert.Contains(onCamera, "EV_CAM02_DOUBLE", "nothing on the wall records the sighting");
            CollectionAssert.Contains(onCamera, "E06_CCTV_WEATHER_MISMATCH");

            int fromCameraAlone = 0;
            foreach (var invariant in InvariantsOf(definition))
                if (onCamera.Contains(invariant)) fromCameraAlone++;

            Assert.AreEqual(1, fromCameraAlone,
                "two invariants off the camera wall would let the quest be judged without leaving the desk");
        }

        /// <summary>
        /// Either Jun-ho can be asked for the waybills. Which of them the caretaker happens
        /// to question must not decide whether that invariant exists this run.
        /// </summary>
        [Test]
        public void TheWaybillsCanBeReadOffEitherCaller()
        {
            CollectionAssert.Contains(GrantedByTalkingTo("D_N2_JUNHO_ENTRY"), "EV_WAYBILL_ORDER");
            CollectionAssert.Contains(GrantedByTalkingTo("D_N2_JUNHO_SECOND"), "EV_WAYBILL_ORDER");
        }

        static void AddReasons(ConsequenceDefinition[] consequences, List<string> keys)
        {
            if (consequences == null) return;

            foreach (var consequence in consequences)
                if (consequence != null && !string.IsNullOrEmpty(consequence.reasonKey))
                    keys.Add(consequence.reasonKey);
        }
    }
}
