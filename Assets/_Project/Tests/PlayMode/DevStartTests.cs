using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// The developer start (GDD 20.21): a fresh game opened on a chosen night.
    ///
    /// It is a debug door, and the reason to test a debug door is the same as for
    /// dev.mainonly: the night somebody is looking at through it has to be the night the
    /// campaign would actually have dealt, or every conclusion drawn from it is about a
    /// shift that does not exist. So what it opens, what it carries in from the nights it
    /// skipped, and that it leaves nothing switched off behind it are held down here.
    /// </summary>
    public sealed class DevStartTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            ServiceHub.ResetPlaythrough();

            Assert.IsTrue(DevConsole.Enabled, "the developer start needs the debug hooks");
        }

        [TearDown]
        public void TearDown()
        {
            // Both are switches on the sitting, and the next test is the same sitting.
            MainOnlyMode.Set(false);
            ServiceHub.Save.WritesSuspended = false;

            // And the shift it opened is still running. The next suite starts from the menu.
            var loop = GameLoop.Instance;
            if (loop != null) loop.DebugReturnToMenu();
        }

        static IEnumerator StartAndWait(GameLoop loop, DevStartOptions options)
        {
            Assert.IsTrue(loop.DebugNewGameAt(options), "the developer start was refused");

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   !(loop.Mode == GameMode.Playing && ServiceHub.State.NightIndex == options.Night))
                yield return null;

            Assert.AreEqual(GameMode.Playing, loop.Mode, "the shift never started");
            Assert.AreEqual(options.Night, ServiceHub.State.NightIndex);
        }

        [UnityTest]
        public IEnumerator OpensTheChosenNightWithItsMainAndWithoutTouchingTheSaves()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop, "no GameLoop - the test scene did not boot");

            yield return StartAndWait(loop, new DevStartOptions
            {
                Night = 2,
                MainOnly = true,
                PriorNights = DevPriorNights.HandledWell
            });

            CollectionAssert.Contains(new List<string>(ServiceHub.Cases.DrawnTonight), "N2-M01",
                                      "night 2 was opened without its main quest");

            Assert.IsTrue(MainOnlyMode.Active, "main-only was asked for and is not on");
            Assert.IsTrue(ServiceHub.Save.WritesSuspended,
                          "saving is off unless asked for, and this sitting can still write");

            // Night 1's main, closed well, is what keeps the 404 bill - and night 2 reads it.
            Assert.IsTrue(ServiceHub.State.GetFlag(FlagIds.BillPreserved404),
                          "night 2 opened as though night 1 had never been played");
        }

        [UnityTest]
        public IEnumerator EarlierNightsHandledBadlyLeaveTheirDebtsBehind()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop);

            yield return StartAndWait(loop, new DevStartOptions
            {
                Night = 3,
                AllowSaves = true,
                PriorNights = DevPriorNights.HandledBadly
            });

            Assert.IsFalse(ServiceHub.Save.WritesSuspended, "saving was asked for and is still off");
            Assert.IsFalse(MainOnlyMode.Active);

            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.BillPreserved404));
            Assert.GreaterOrEqual(ServiceHub.State.GetStat(DebtIds.Record), 1,
                                  "discarding the bill on night 1 left no record debt");
            Assert.GreaterOrEqual(ServiceHub.State.GetStat(DebtIds.Access), 1,
                                  "the unverified pass on night 2 left no access debt");
        }

        /// <summary>
        /// The pause menu's route: the developer start asked for while a shift is running.
        /// </summary>
        [UnityTest]
        public IEnumerator ChangingNightMidShiftReplacesTheShiftRatherThanStackingOnIt()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop);

            yield return StartAndWait(loop, new DevStartOptions
            {
                Night = 2,
                MainOnly = true,
                PriorNights = DevPriorNights.HandledWell
            });

            yield return StartAndWait(loop, new DevStartOptions { Night = 5, MainOnly = true });

            var drawn = new List<string>(ServiceHub.Cases.DrawnTonight);
            CollectionAssert.Contains(drawn, "N5-M01", "night 5 was opened without its main quest");
            CollectionAssert.DoesNotContain(drawn, "N2-M01", "the shift that was replaced is still dealt");

            // The second start asked for untouched earlier nights, so the first one's must go.
            Assert.IsFalse(ServiceHub.State.GetFlag(FlagIds.BillPreserved404),
                           "the replaced shift's history leaked into the new one");
        }

        [UnityTest]
        public IEnumerator ARealNewGameAfterwardsSavesAndRunsAWholeNightAgain()
        {
            var loop = GameLoop.Instance;
            Assert.IsNotNull(loop);

            yield return StartAndWait(loop, new DevStartOptions { Night = 4, MainOnly = true });

            loop.NewGame(false);

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && ServiceHub.State.NightIndex != 1)
                yield return null;

            Assert.AreEqual(1, ServiceHub.State.NightIndex, "the new game never started");
            Assert.IsFalse(ServiceHub.Save.WritesSuspended,
                           "a campaign started after a developer session would never save");
            Assert.IsFalse(MainOnlyMode.Active,
                           "a campaign started after a developer session would have only its mains");
        }
    }
}
