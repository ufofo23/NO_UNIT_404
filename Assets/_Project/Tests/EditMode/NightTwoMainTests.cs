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

        static void AddReasons(ConsequenceDefinition[] consequences, List<string> keys)
        {
            if (consequences == null) return;

            foreach (var consequence in consequences)
                if (consequence != null && !string.IsNullOrEmpty(consequence.reasonKey))
                    keys.Add(consequence.reasonKey);
        }
    }
}
