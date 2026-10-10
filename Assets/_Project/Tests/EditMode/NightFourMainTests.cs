using System.Collections.Generic;
using NUnit.Framework;
using NO404.Cases;
using NO404.Core;
using NO404.ContentData;
using NO404.Evidence;
using NO404.Save;
using NO404.UI;

namespace NO404.Tests
{
    /// <summary>
    /// N4-M01, the household that is not on any list (GDD v5.1 13).
    ///
    /// The sync puts a row for unit 404 in the resident database - three names and a move-in
    /// date - and the chairman rings to say it is an error and to delete it. What is done
    /// about the row is done on the row: print, export, delete, or nothing. The report filed
    /// afterwards says which.
    ///
    /// It is the night the caretaker reads her own name, and the tests below hold down where
    /// that lands, what each of the four answers leaves behind, and that the one which
    /// cannot be taken back still leaves a way to the truth.
    /// </summary>
    public sealed class NightFourMainTests
    {
        const string CaseId = "N4-M01";
        const string Row = "res_404";

        readonly List<string> _notices = new List<string>();
        readonly List<string> _cinematics = new List<string>();

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

        // ---- helpers ---------------------------------------------------------------

        /// <summary>Night 4 with its main handed out and nothing looked at yet.</summary>
        void OpenTheQuest()
        {
            ServiceHub.ResetPlaythrough();
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(1);

            ServiceHub.State.BeginNight(4);
            ServiceHub.Cases.BeginNight(4);

            // Earlier nights wear the caretaker down before this one starts. At full nerve a
            // SAN gain has nowhere to go and would read as a rule that does not fire.
            ServiceHub.Vitals.Strain(15, "reason.strain");

            _notices.Clear();
            _cinematics.Clear();
            EventBus.Subscribe<NotificationEvent>(evt => _notices.Add(evt.BodyKey));
            EventBus.Subscribe<CinematicRequestedEvent>(evt => _cinematics.Add(evt.CinematicId));

            var runtime = ServiceHub.Cases.Find(CaseId);
            Assert.IsNotNull(runtime, CaseId + " was not dealt on night 4");
            if (runtime.State == CaseState.Dormant)
                Assert.IsTrue(ServiceHub.Cases.TryStartCase(CaseId), CaseId + " would not start");
        }

        /// <summary>Opens the 404 row, the way the database screen reports it.</summary>
        static void ReadTheRow()
        {
            ServiceHub.Cases.NotifyObjective(ObjectiveType.ViewRecord, Row);
        }

        static void ReadTheLedger()
        {
            ServiceHub.Evidence.Acquire("EV_PAPER_LEDGER", EvidenceSource.WorldPickup);
        }

        static bool Do(string actionId)
        {
            return ServiceHub.Residents.Perform(Row, actionId);
        }

        static List<string> ButtonsOnTheRow()
        {
            var ids = new List<string>();
            foreach (var action in ServiceHub.Residents.AvailableActions(ServiceHub.Content.FindResident(Row)))
                ids.Add(action.actionId);
            return ids;
        }

        static List<string> OnTheReportForm()
        {
            var offered = new List<string>();
            foreach (var decision in ServiceHub.Content.FindCase(CaseId).decisions)
            {
                string unmet;
                if (ConditionEvaluator.EvaluateAll(decision.availability, out unmet))
                    offered.Add(decision.decisionId);
            }
            return offered;
        }

        static DecisionResult File(string decisionId)
        {
            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            return ServiceHub.Cases.SubmitDecision(CaseId, decisionId, attached);
        }

        // ---- what is on screen -----------------------------------------------------

        /// <summary>Every line the quest, the row and the sequence put on screen exists in both languages.</summary>
        [Test]
        public void EverythingTheQuestShowsIsInTheStringTableInBothLanguages()
        {
            var definition = ServiceHub.Content.FindCase(CaseId);
            Assert.IsNotNull(definition, CaseId + " is not in the content database");

            var keys = new List<string> { definition.titleKey, definition.summaryKey };

            AddKeys(definition.onStart, keys);
            foreach (var objective in definition.objectives)
            {
                keys.Add(objective.titleKey);
                AddKeys(objective.onComplete, keys);
            }

            foreach (var decision in definition.decisions)
            {
                keys.Add(decision.labelKey);
                keys.Add(decision.resultKey);
                AddKeys(decision.consequences, keys);
            }

            if (definition.failSafe != null && definition.failSafe.enabled)
                keys.Add(definition.failSafe.notifyKey);

            foreach (var evidenceId in definition.evidenceIds)
            {
                var evidence = ServiceHub.Content.FindEvidence(evidenceId);
                Assert.IsNotNull(evidence, CaseId + " lists evidence that does not exist: " + evidenceId);
                keys.Add(evidence.displayNameKey);
                keys.Add(evidence.descriptionKey);
            }

            var row = ServiceHub.Content.FindResident(Row);
            keys.Add(row.nameKey);
            keys.Add(row.glimpseNameKey);
            foreach (var note in row.notes) keys.Add(note.noteKey);
            foreach (var action in row.actions)
            {
                keys.Add(action.labelKey);
                AddKeys(action.consequences, keys);
            }

            keys.Add("ui.prompt.read_ledger");
            keys.AddRange(StorySequenceView.KeysOf(SeedContent.CinematicN4));

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

        /// <summary>Reason keys, and the text of anything a consequence announces.</summary>
        static void AddKeys(ConsequenceDefinition[] consequences, List<string> keys)
        {
            if (consequences == null) return;

            foreach (var consequence in consequences)
            {
                if (consequence == null) continue;
                if (!string.IsNullOrEmpty(consequence.reasonKey)) keys.Add(consequence.reasonKey);
                if (consequence.type == ConsequenceType.Notify) keys.Add(consequence.targetId);
            }
        }

        [Test]
        public void TheSequenceTheQuestAsksForIsOneTheGameCanPlay()
        {
            Assert.IsTrue(StorySequenceView.Knows(SeedContent.CinematicN4));
            Assert.Greater(StorySequenceView.KeysOf(SeedContent.CinematicN4).Count, 2);
        }

        // ---- the row ---------------------------------------------------------------

        /// <summary>
        /// v5.1 13: the row appears after the sync, which on a night paced by the caretaker
        /// is when the case is handed out - not when the shift starts.
        /// </summary>
        [Test]
        public void TheRowIsNotThereUntilTheCaseIsHandedOut()
        {
            ServiceHub.ResetPlaythrough();
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(1);
            ServiceHub.State.BeginNight(4);

            Assert.IsNull(ServiceHub.Residents.FindById(Row), "404 is on the list before anything has happened");

            OpenTheQuest();

            Assert.IsNotNull(ServiceHub.Residents.FindById(Row), "the case is open and the row is not there");
            CollectionAssert.Contains(_notices, "ui.notify.db_sync_new_record");
        }

        /// <summary>
        /// v5.1 13: three names and a move-in date. And v5.1 0.16: this row is where the
        /// younger daughter's full name is written out, so until the row has been revealed
        /// the list shows the masked surname and nothing more.
        /// </summary>
        [Test]
        public void TheRowCarriesThreeNamesAndTheListMasksThemUntilItIsRevealed()
        {
            var row = ServiceHub.Content.FindResident(Row);

            var loc = new LocalizationService();
            loc.Initialize("ko");

            string shown = string.Empty;
            foreach (var note in row.notes) shown += loc.Get(note.noteKey) + "\n";

            StringAssert.Contains("윤미정", shown);
            StringAssert.Contains("윤서우", shown);
            StringAssert.Contains("윤하린", shown);
            StringAssert.Contains("2008-12-19", shown);
            StringAssert.Contains("2009", shown, "nothing on the row says its fields are older than it is");

            Assert.AreEqual(row.glimpseNameKey, ServiceHub.Residents.DisplayNameKey(row),
                            "the unrevealed row shows a name on the list");
            StringAssert.DoesNotContain("하린", loc.Get(row.glimpseNameKey));
            StringAssert.DoesNotContain("미정", loc.Get(row.glimpseNameKey));

            ServiceHub.State.SetFlag(FlagIds.Knows404, true);
            Assert.AreEqual(row.nameKey, ServiceHub.Residents.DisplayNameKey(row));
        }

        // ---- reading it ------------------------------------------------------------

        /// <summary>v5.1 13: SAN -8 on seeing her own name. On reading it, once, whatever is done next.</summary>
        [Test]
        public void ReadingTheRowIsWhereItLands()
        {
            OpenTheQuest();
            int before = ServiceHub.Vitals.San;

            ReadTheRow();
            Assert.AreEqual(-8, ServiceHub.Vitals.San - before);
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_DB404_ROW"), "the row was read and nothing was kept of it");

            ReadTheRow();
            Assert.AreEqual(-8, ServiceHub.Vitals.San - before, "reading it again was charged again");
        }

        /// <summary>v5.1 13: SAN +3 for settling it on paper - once there is something to settle.</summary>
        [Test]
        public void ThePaperLedgerSteadiesHerOnlyAfterTheRowHasBeenRead()
        {
            OpenTheQuest();
            ReadTheRow();

            int before = ServiceHub.Vitals.San;
            ReadTheLedger();
            Assert.AreEqual(3, ServiceHub.Vitals.San - before);

            OpenTheQuest();
            before = ServiceHub.Vitals.San;
            ReadTheLedger();
            Assert.AreEqual(0, ServiceHub.Vitals.San - before,
                            "a ledger read before the row has been seen confirmed something");
        }

        /// <summary>v5.1 13: what was kept from nights 1 and 3 lines up with the row.</summary>
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void WhatWasKeptFromEarlierNightsLinesUpWithTheRow(bool keptTheBill, bool foundTheHeights)
        {
            OpenTheQuest();
            ServiceHub.State.SetFlag(FlagIds.BillPreserved404, keptTheBill);
            if (foundTheHeights) ServiceHub.Evidence.Acquire("EV_HEIGHT_MARKS", EvidenceSource.WorldPickup);
            _notices.Clear();

            ReadTheRow();

            Assert.AreEqual(keptTheBill, _notices.Contains("ui.memo.n4_bill_match"));
            Assert.AreEqual(foundTheHeights, _notices.Contains("ui.memo.n4_height_match"));
        }

        // ---- what is done about it -------------------------------------------------

        [Test]
        public void TheRowOffersItsThreeButtonsOnlyWhileTheCaseIsOpenAndUndecided()
        {
            ServiceHub.ResetPlaythrough();
            ServiceHub.State.SetFlag(FlagIds.Knows404, true);
            CollectionAssert.IsEmpty(ButtonsOnTheRow(), "the row can be acted on with no case open");

            OpenTheQuest();
            CollectionAssert.AreEquivalent(new[] { "print", "export", "delete" }, ButtonsOnTheRow());

            Assert.IsTrue(Do("print"));
            CollectionAssert.IsEmpty(ButtonsOnTheRow(), "something was done to the row and more is still offered");
            Assert.IsFalse(Do("delete"), "the row was printed and then deleted as well");
        }

        /// <summary>
        /// The report is the log entry for what was done to the row. Whatever that was, the
        /// form offers the one line that says so - and doing nothing is the fourth answer.
        /// </summary>
        [TestCase("print", "dec_print_and_keep")]
        [TestCase("export", "dec_export")]
        [TestCase("delete", "dec_delete")]
        [TestCase(null, "dec_keep_quiet")]
        public void TheFormOffersExactlyTheReportThatMatchesWhatWasDone(string action, string expected)
        {
            OpenTheQuest();
            ReadTheRow();
            if (action != null) Assert.IsTrue(Do(action));

            CollectionAssert.AreEqual(new[] { expected }, OnTheReportForm());
        }

        /// <summary>v5.1 13's table: what each of the four answers is worth.</summary>
        [TestCase("print", "dec_print_and_keep", "PRINT", true, 15, 15, 0)]
        [TestCase("export", "dec_export", "EXPORT", true, 12, 25, 0)]
        [TestCase(null, "dec_keep_quiet", "KEEP", true, 0, 5, 0)]
        [TestCase("delete", "dec_delete", "DELETE", false, -15, 5, 2)]
        public void EachAnswerLeavesWhatTheTableSaysItLeaves(string action, string decision, string recorded,
                                                           bool nameKept, int archive, int chairman, int recordDebt)
        {
            OpenTheQuest();
            ReadTheRow();
            if (action != null) Assert.IsTrue(Do(action));

            var state = ServiceHub.State;
            int archiveBefore = state.GetStat(StatIds.ArchiveIntegrity);
            int chairmanBefore = state.GetStat(StatIds.ChairmanAlert);
            int resonanceBefore = state.GetStat(StatIds.HarinResonance);

            Assert.IsTrue(File(decision).Accepted);

            Assert.AreEqual(recorded, state.GetChoice(ChoiceIds.Db404Action));
            Assert.AreEqual(nameKept, state.GetFlag(FlagIds.HarinRecordPreserved));
            Assert.AreEqual(archive, state.GetStat(StatIds.ArchiveIntegrity) - archiveBefore);
            Assert.AreEqual(chairman, state.GetStat(StatIds.ChairmanAlert) - chairmanBefore);
            Assert.AreEqual(10, state.GetStat(StatIds.HarinResonance) - resonanceBefore);
            Assert.AreEqual(recordDebt, state.GetStat(DebtIds.Record));
        }

        [Test]
        public void PrintingLeavesASheetOfPaperAndExportingLeavesAMark()
        {
            OpenTheQuest();
            Assert.IsTrue(Do("print"));
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_DB404_PRINT"));

            OpenTheQuest();
            Assert.IsTrue(Do("export"));
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.Db404Exported));
        }

        // ---- the one that cannot be taken back -------------------------------------

        /// <summary>
        /// v5.1 13: deleting does not close the truth route. The row is gone the moment the
        /// button is pressed - not when it is reported - and the paper in the basement is
        /// still the paper in the basement.
        /// </summary>
        [Test]
        public void DeletingTakesTheRowAtOnceAndLeavesThePaper()
        {
            OpenTheQuest();
            ReadTheRow();
            Assert.IsTrue(Do("delete"));

            Assert.IsNull(ServiceHub.Residents.FindById(Row), "the row was deleted and is still on the list");
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.Db404Deleted));
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.HarinRecordPreserved));
            CollectionAssert.IsEmpty(ButtonsOnTheRow());

            // What she read is still what she read.
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_DB404_ROW"));

            ReadTheLedger();
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_PAPER_LEDGER"));
            Assert.IsTrue(File("dec_delete").Accepted, "a deleted row cannot be reported");

            bool queued = false;
            foreach (var deferred in ServiceHub.Cases.NextNightQueue)
                if (deferred.type == ConsequenceType.GrantEvidence && deferred.targetId == "EV_PAPER_LEDGER")
                    queued = true;

            Assert.IsTrue(queued, "nothing carries the paper record into night 5 for somebody who never went down");
        }

        // ---- CIN-N4 ----------------------------------------------------------------

        /// <summary>
        /// v5.1 8.5 / 13: after the report, the name on the row and the name on her staff
        /// card are the same name. SAN -4, once, whichever answer was given.
        /// </summary>
        [TestCase("print", "dec_print_and_keep")]
        [TestCase("delete", "dec_delete")]
        [TestCase(null, "dec_keep_quiet")]
        public void TheMemoryComesBackAfterTheReportWhateverWasDecided(string action, string decision)
        {
            OpenTheQuest();
            ReadTheRow();
            if (action != null) Assert.IsTrue(Do(action));

            CollectionAssert.IsEmpty(_cinematics, "the memory came back before anything was decided");

            int before = ServiceHub.Vitals.San;
            Assert.IsTrue(File(decision).Accepted);

            CollectionAssert.AreEqual(new[] { SeedContent.CinematicN4 }, _cinematics);
            Assert.AreEqual(-4, ServiceHub.Vitals.San - before);
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.MemoryOfTheFireRestored));
        }

        /// <summary>
        /// v5.1 13: a caretaker who noticed on night 3 that 123 was her own height gets there
        /// a step sooner - at the screen. The report then has nothing left to bring back.
        /// </summary>
        [Test]
        public void SomebodyWhoMadeTheConnectionOnNightThreeRemembersAtTheScreen()
        {
            OpenTheQuest();
            ServiceHub.State.SetFlag("N3_HEIGHT_MATCH_NOTED", true);

            int before = ServiceHub.Vitals.San;
            ReadTheRow();

            CollectionAssert.AreEqual(new[] { SeedContent.CinematicN4 }, _cinematics);
            Assert.AreEqual(-8 - 4, ServiceHub.Vitals.San - before);

            before = ServiceHub.Vitals.San;
            Assert.IsTrue(File("dec_keep_quiet").Accepted);

            Assert.AreEqual(1, _cinematics.Count, "the same memory came back twice");
            Assert.AreEqual(0, ServiceHub.Vitals.San - before, "and was charged for twice");
        }

        // ---- save and load ---------------------------------------------------------

        /// <summary>
        /// A shift saved after the row was deleted comes back with the row deleted, and the
        /// form still offers the report that says so. A reload that put the row back would
        /// be a way to take back the one thing that cannot be.
        /// </summary>
        [Test]
        public void ADeletedRowStaysDeletedThroughASave()
        {
            OpenTheQuest();
            ReadTheRow();
            Assert.IsTrue(Do("delete"));

            var saved = ServiceHub.Save.Capture(0, SaveReason.Manual);
            var reloaded = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(saved));
            ServiceHub.Save.Restore(reloaded);

            Assert.IsNull(ServiceHub.Residents.FindById(Row), "the reload put the deleted row back");
            Assert.AreEqual("DELETE", ServiceHub.State.GetChoice(ChoiceIds.Db404Action));
            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsActive());
            CollectionAssert.AreEqual(new[] { "dec_delete" }, OnTheReportForm());
            CollectionAssert.IsEmpty(ButtonsOnTheRow());
        }
    }
}
