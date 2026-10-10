using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Cases;
using NO404.Core;
using NO404.Interaction;
using NO404.UI;

namespace NO404.Tests
{
    /// <summary>
    /// N4-M01 played from the sync to clocking off, through the screens and the room a
    /// player uses (GDD v5.1 13, 30.5).
    ///
    /// The unit tests hold each rule still. This is the one that shows they meet in the
    /// built night: the row appears, the database screen has the buttons on it, the ledger
    /// is a thing in the basement that can be walked up to, pressing a button puts exactly
    /// one line on the report form, and filing it plays the sequence and leaves a shift that
    /// can end.
    /// </summary>
    public sealed class NightFourMainPlaythroughTests
    {
        const string CaseId = "N4-M01";
        const string Row = "res_404";
        const int PatienceFrames = 600;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.ResetPlaythrough();
        }

        [TearDown]
        public void TearDown()
        {
            MainOnlyMode.Set(false);
            ServiceHub.Save.WritesSuspended = false;

            var loop = GameLoop.Instance;
            if (loop != null)
            {
                loop.OpenPc(false);
                loop.DebugReturnToMenu();
            }
        }

        static IEnumerator OpenNightFour(GameLoop loop)
        {
            Assert.IsTrue(loop.DebugNewGameAt(new DevStartOptions
            {
                Night = 4,
                MainOnly = true,
                PriorNights = DevPriorNights.HandledWell
            }));

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   !(loop.Mode == GameMode.Playing && ServiceHub.State.NightIndex == 4))
                yield return null;

            Assert.AreEqual(4, ServiceHub.State.NightIndex, "night 4 never started");

            int frames = 0;
            while (!ServiceHub.Cases.Find(CaseId).State.IsActive() && ++frames < PatienceFrames)
                yield return null;

            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsActive(),
                          "the night started and never handed out its main quest");
        }

        static PcShellView OpenTheDatabase(GameLoop loop)
        {
            loop.OpenPc(true);

            PcShellView shell = null;
            foreach (var candidate in Object.FindObjectsByType<PcShellView>(FindObjectsSortMode.None))
                shell = candidate;
            Assert.IsNotNull(shell, "the facility PC is open and has no screen");

            shell.Open(AppIds.Residents);
            return shell;
        }

        static UnityEngine.UI.Button ButtonNamed(PcShellView shell, string name)
        {
            foreach (var button in shell.GetComponentsInChildren<UnityEngine.UI.Button>(false))
                if (button.name == name) return button;
            return null;
        }

        static UnityEngine.UI.Button ButtonUnder(PcShellView shell, string parentName)
        {
            foreach (var button in shell.GetComponentsInChildren<UnityEngine.UI.Button>(false))
                if (button.transform.parent != null && button.transform.parent.name == parentName) return button;
            return null;
        }

        /// <summary>
        /// v5.1 13: the chairman calls it a system error and says to delete it now. The call
        /// is part of the night - a shift cannot be clocked off with the handset ringing -
        /// so the walk answers it, hears the instruction and refuses.
        /// </summary>
        static IEnumerator TakeTheChairmansCall()
        {
            int frames = 0;
            while (!ServiceHub.Phone.IsRinging && ++frames < PatienceFrames)
            {
                // The call is on the night's timetable; the walk does not sit out the wait.
                if (frames == 30) ServiceHub.Clock.SetTime(22, 25);
                yield return null;
            }

            Assert.IsTrue(ServiceHub.Phone.IsRinging, "the row appeared and nobody rang about it");

            // Whoever is on the line is answered, until the line is clear. Night 4 has more
            // than one caller; the one this quest is about has to be among them.
            bool chairman = false;
            for (int call = 0; call < 4 && ServiceHub.Phone.IsRinging; call++)
            {
                if (ServiceHub.Phone.Active.callId == "CALL_N4_CHAIRMAN") chairman = true;

                ServiceHub.Phone.Answer();
                yield return null;

                for (int guard = 0; guard < 16 && ServiceHub.Dialogue.IsActive; guard++)
                {
                    var line = ServiceHub.Dialogue.CurrentLine;
                    if (!line.HasChoices) ServiceHub.Dialogue.Advance();
                    else
                    {
                        string pick = line.Choices[0].choiceId;
                        foreach (var choice in line.Choices) if (choice.choiceId == "refuse") pick = "refuse";
                        ServiceHub.Dialogue.Choose(pick);
                    }
                    yield return null;
                }

                Assert.IsFalse(ServiceHub.Dialogue.IsActive, "a call never ended");
                for (int i = 0; i < 5; i++) yield return null;
            }

            Assert.IsTrue(chairman, "the chairman never rang to say the row was an error");
            Assert.IsFalse(ServiceHub.Phone.IsRinging || ServiceHub.Phone.InCall, "the line is still open");
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

        [UnityTest]
        public IEnumerator ReadPrintedCheckedAgainstPaperAndReported()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            yield return OpenNightFour(loop);
            int sanAtStart = ServiceHub.Vitals.San;

            // ---- the database screen: the row is on the list, and opens ---------------
            var shell = OpenTheDatabase(loop);
            for (int i = 0; i < 5; i++) yield return null;

            var rowButton = ButtonUnder(shell, "Row_" + Row);
            Assert.IsNotNull(rowButton, "the sync ran and unit 404 is not on the list");

            rowButton.onClick.Invoke();
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsTrue(ServiceHub.Evidence.Has("EV_DB404_ROW"), "the row was opened and nothing was kept of it");
            Assert.AreEqual(-8, ServiceHub.Vitals.San - sanAtStart, "reading her own name should cost eight");

            // ---- the three buttons are on the record, and one of them is pressed ------
            Assert.IsNotNull(ButtonNamed(shell, "Action_export"), "the record cannot be exported from its own screen");
            Assert.IsNotNull(ButtonNamed(shell, "Action_delete"), "the record cannot be deleted from its own screen");

            var print = ButtonNamed(shell, "Action_print");
            Assert.IsNotNull(print, "the record cannot be printed from its own screen");
            print.onClick.Invoke();
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual("PRINT", ServiceHub.State.GetChoice(ChoiceIds.Db404Action));
            Assert.IsTrue(ServiceHub.Evidence.Has("EV_DB404_PRINT"));
            Assert.IsNull(ButtonNamed(shell, "Action_delete"), "the record was printed and can still be deleted");

            loop.OpenPc(false);

            // ---- the chairman rings to say it is an error, and is told no -------------
            yield return TakeTheChairmansCall();

            // ---- the basement: the paper ledger is a thing that can be walked up to -----
            ServiceHub.Zones.RequestZone(ZoneIds.Archive);
            int frames = 0;
            EvidencePickup ledger = null;
            while (ledger == null && ++frames < PatienceFrames)
            {
                foreach (var pickup in Object.FindObjectsByType<EvidencePickup>(FindObjectsSortMode.None))
                    if (pickup.EvidenceId == "EV_PAPER_LEDGER") ledger = pickup;
                if (ledger == null) yield return null;
            }
            Assert.IsNotNull(ledger, "there is no paper ledger anywhere in the records room");

            string reason;
            Assert.IsTrue(ledger.CanInteract(new PlayerContext(), out reason),
                          "the ledger cannot be read on night 4 (" + reason + ")");
            ledger.Interact(new PlayerContext());
            yield return null;

            Assert.IsTrue(ServiceHub.Evidence.Has("EV_PAPER_LEDGER"));
            Assert.AreEqual(-8 + 3, ServiceHub.Vitals.San - sanAtStart, "the paper should give three back");

            var renderer = ledger.GetComponent<Renderer>();
            Assert.IsTrue(renderer != null && renderer.enabled, "the building's ledger left with the caretaker");

            // ---- the report ----------------------------------------------------------
            frames = 0;
            while (ServiceHub.Cases.Find(CaseId).State != CaseState.DecisionReady && ++frames < PatienceFrames)
                yield return null;

            Assert.AreEqual(CaseState.DecisionReady, ServiceHub.Cases.Find(CaseId).State,
                            "everything was done and the report never became ready");
            CollectionAssert.AreEqual(new[] { "dec_print_and_keep" }, OnTheReportForm());

            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            Net.NetShift.RequestDecision(CaseId, "dec_print_and_keep", attached);
            yield return null;

            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsResolved(), "the report was not accepted");
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.HarinRecordPreserved));
            Assert.AreEqual(-8 + 3 - 4, ServiceHub.Vitals.San - sanAtStart);

            // ---- CIN-N4 is on screen, and ends on its own ------------------------------
            StorySequenceView story = null;
            foreach (var candidate in Object.FindObjectsByType<StorySequenceView>(FindObjectsInactive.Include,
                                                                                FindObjectsSortMode.None))
                story = candidate;
            Assert.IsNotNull(story, "the game has nothing to play a story sequence with");
            Assert.IsTrue(story.IsRunning, "the report was filed and the sequence did not start");
            Assert.AreEqual("CIN-N4", story.PlayingId);

            float deadline = Time.realtimeSinceStartup + 40f;
            while (story.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(story.IsRunning, "the sequence never ended on its own");

            // ---- and the shift can end -------------------------------------------------
            frames = 0;
            while (loop.CurrentShiftBlocker != GameLoop.ShiftBlocker.None && ++frames < PatienceFrames)
                yield return null;

            Assert.AreEqual(GameLoop.ShiftBlocker.None, loop.CurrentShiftBlocker,
                            "the main is closed and the shift still cannot be clocked off");
        }

        /// <summary>
        /// Deleting from the screen: the row leaves the list at once, the detail pane does
        /// not go on showing a record that is gone, and the form has the one line for it.
        /// </summary>
        [UnityTest]
        public IEnumerator DeletingFromTheScreenTakesTheRowOffTheScreen()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop);

            yield return OpenNightFour(loop);

            var shell = OpenTheDatabase(loop);
            for (int i = 0; i < 5; i++) yield return null;

            var rowButton = ButtonUnder(shell, "Row_" + Row);
            Assert.IsNotNull(rowButton);
            rowButton.onClick.Invoke();
            for (int i = 0; i < 5; i++) yield return null;

            var delete = ButtonNamed(shell, "Action_delete");
            Assert.IsNotNull(delete);
            delete.onClick.Invoke();
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsNull(ButtonUnder(shell, "Row_" + Row), "the deleted row is still on the list");
            Assert.IsNull(ButtonNamed(shell, "Action_print"), "a deleted record is still offering to be printed");
            Assert.IsNull(ServiceHub.Residents.FindById(Row));

            CollectionAssert.AreEqual(new[] { "dec_delete" }, OnTheReportForm());
        }
    }
}
