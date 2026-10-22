using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Interaction;
using NO404.UI;

namespace NO404.Tests
{
    /// <summary>
    /// N6-M01 played from the sealed door to the ending, in the built 404 (GDD v5.1 15, 30.4,
    /// 30.5).
    ///
    /// The unit tests hold the rules still. This is the one that walks into the room: the
    /// fixtures are there, the one that was Park Dong-sik is remains and not a man to talk
    /// to, the rescue plays, the minutes run out on the second thing, the send closes the
    /// night and the game shows the one ending.
    /// </summary>
    public sealed class NightSixMainPlaythroughTests
    {
        const string CaseId = "N6-M01";
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
            if (loop != null) loop.DebugReturnToMenu();
        }

        static IEnumerator OpenNightSix(GameLoop loop)
        {
            // "Handled badly" leaves the fire door open on night 5 - and so no balanced route.
            Assert.IsTrue(loop.DebugNewGameAt(new DevStartOptions
            {
                Night = 6,
                MainOnly = true,
                PriorNights = DevPriorNights.Untouched
            }));

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   !(loop.Mode == GameMode.Playing && ServiceHub.State.NightIndex == 6))
                yield return null;
            Assert.AreEqual(6, ServiceHub.State.NightIndex, "night 6 never started");

            int frames = 0;
            while (!ServiceHub.Cases.Find(CaseId).State.IsActive() && ++frames < PatienceFrames)
                yield return null;
            Assert.IsTrue(ServiceHub.Cases.Find(CaseId).State.IsActive(), "the last night has no main quest");
        }

        static IEnumerator FixtureFor(string evidenceId, System.Action<EvidencePickup> found)
        {
            EvidencePickup fixture = null;
            int frames = 0;
            while (fixture == null && ++frames < PatienceFrames)
            {
                foreach (var pickup in Object.FindObjectsByType<EvidencePickup>(FindObjectsSortMode.None))
                    if (pickup.EvidenceId == evidenceId) fixture = pickup;
                if (fixture == null) yield return null;
            }

            Assert.IsNotNull(fixture, "nothing in 404 gives " + evidenceId);
            found(fixture);
        }

        [UnityTest]
        public IEnumerator FromTheSealedDoorToTheOneEnding()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            yield return OpenNightSix(loop);
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.N6BalancedRoute),
                           "an untouched building was given time for both");

            // ---- into 404 ----------------------------------------------------------------
            ServiceHub.Zones.RequestZone(ZoneIds.Unit404);
            ServiceHub.Player.EnterZone(ZoneIds.Unit404);
            Net.NetShift.Request(Net.NetShift.ShiftAct.EnterZone, ZoneIds.Unit404);

            EvidencePickup echo = null, remains = null, ledger = null;
            yield return FixtureFor("EV_N6_LAST_RESCUE", f => echo = f);
            yield return FixtureFor("EV_DONGSIK_REMAINS", f => remains = f);
            yield return FixtureFor("E19_ORIGINAL_LEDGER", f => ledger = f);

            // v5.1 30.4: there is no Park Dong-sik in 404 to talk to or to rescue.
            foreach (var talk in Object.FindObjectsByType<DialogueInteractable>(FindObjectsSortMode.None))
                Assert.AreNotEqual("Dongsik", talk.name, "a living Park Dong-sik is still in 404");

            string reason;
            Assert.IsFalse(remains.CanInteract(new PlayerContext(), out reason),
                           "his remains can be taken before the rescue has been watched to its end");

            // ---- the rescue, to where it ends -------------------------------------------
            echo.Interact(new PlayerContext());
            yield return null;

            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.DongsikRemainsFound));

            StorySequenceView story = null;
            foreach (var candidate in Object.FindObjectsByType<StorySequenceView>(FindObjectsInactive.Include,
                                                                                FindObjectsSortMode.None))
                story = candidate;
            Assert.IsNotNull(story);
            Assert.IsTrue(story.IsRunning, "the rescue was watched and nothing played");
            Assert.AreEqual(SeedContent.CinematicN6Echo, story.PlayingId);
            story.Stop();

            // ---- the minutes: the ledger, and then there is no going back for him ---------
            Assert.IsTrue(remains.CanInteract(new PlayerContext(), out reason),
                          "the rescue ended and there is nothing where it ended (" + reason + ")");
            Assert.IsTrue(ledger.CanInteract(new PlayerContext(), out reason));

            ledger.Interact(new PlayerContext());
            yield return null;

            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.EndLedgerSecured));
            Assert.IsFalse(remains.CanInteract(new PlayerContext(), out reason),
                           "the ledger was taken first and there was still time to go back for him");

            // ---- the send, and the ending ---------------------------------------------------
            int frames = 0;
            while (ServiceHub.Cases.Find(CaseId).State != CaseState.DecisionReady && ++frames < PatienceFrames)
                yield return null;
            Assert.AreEqual(CaseState.DecisionReady, ServiceHub.Cases.Find(CaseId).State,
                            "everything required was done and the report never became ready");

            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);
            Net.NetShift.RequestDecision(CaseId, "dec_send_residents", attached);

            frames = 0;
            while (loop.Mode != GameMode.Ending && ++frames < PatienceFrames) yield return null;

            Assert.AreEqual(GameMode.Ending, loop.Mode, "the evidence went out and the game did not end");
            Assert.IsNotNull(ServiceHub.Endings.Reached);
            Assert.AreEqual(EndingIds.SingleEnding404Existed, ServiceHub.Endings.Reached.endingId);
            CollectionAssert.Contains(ServiceHub.Endings.EndingShots(), "ending.shot.ledger.opened");
            CollectionAssert.Contains(ServiceHub.Endings.EndingShots(), "ending.shot.dongsik.located");
            CollectionAssert.Contains(ServiceHub.Endings.EndingShots(), "ending.shot.sent.residents");

            // ---- what the screen shows ------------------------------------------------------
            yield return null;
            EndingView view = null;
            foreach (var candidate in Object.FindObjectsByType<EndingView>(FindObjectsSortMode.None)) view = candidate;
            Assert.IsNotNull(view, "the game ended and no ending screen is open");

            // The longest the ending gets: the one shot that is only there with the call kept,
            // and the longest form of each of the others.
            ServiceHub.State.SetFlag(FlagIds.End2009CallPreserved, true);
            ServiceHub.State.SetFlag(FlagIds.EndSunjaTestimonyFull, true);
            ServiceHub.State.SetFlag(FlagIds.EndDongsikBroadcastVerified, true);
            ServiceHub.State.SetFlag(FlagIds.SubmittedToPolice, true);

            string language = LocalizationService.Instance.Language;
            try
            {
                foreach (var lang in new[] { "ko", "en" })
                {
                    LocalizationService.Instance.SetLanguage(lang);
                    view.Show(ServiceHub.Endings.Reached);
                    Canvas.ForceUpdateCanvases();

                    Text title = null, body = null;
                    foreach (var text in view.GetComponentsInChildren<Text>())
                    {
                        if (text.name == "Title") title = text;
                        if (text.name == "Body") body = text;
                    }

                    Assert.AreEqual(Loc.T(ServiceHub.Endings.Reached.titleKey), title.text, lang);
                    StringAssert.StartsWith(Loc.T("ending.shot.exit_404"), body.text, lang);
                    StringAssert.EndsWith(Loc.T("ending.shot.404_restored"), body.text, lang);
                    Assert.LessOrEqual(body.preferredHeight, body.rectTransform.rect.height,
                                       lang + ": the ending runs out of its box and over the button");
                }
            }
            finally
            {
                LocalizationService.Instance.SetLanguage(language);
            }
        }
    }
}
