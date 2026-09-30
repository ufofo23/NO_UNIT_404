using System.Collections.Generic;
using NUnit.Framework;
using NO404.Anomalies;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// The five anomalous tools as authored (v2.1 spec 23), read straight out of SeedContent.
    ///
    /// These are content assertions rather than behaviour ones, and they are the ones worth
    /// having: every failure mode a tool has is a failure of the data. A machine that never
    /// wakes, a menu line that opens a screen nobody wrote, a drawer that hands over an object
    /// for nothing - all of them run perfectly and all of them are wrong.
    /// </summary>
    public sealed class AnomalyToolContentTests
    {
        static AnomalyToolDefinition[] Tools() { return SeedContent.BuildAnomalyTools(); }

        static AnomalyToolDefinition Find(string toolId)
        {
            var tools = Tools();
            for (int i = 0; i < tools.Length; i++) if (tools[i].toolId == toolId) return tools[i];
            return null;
        }

        [Test]
        public void EveryToolInSpec23IsAuthored()
        {
            foreach (var id in ManualEventIds.Tools)
                Assert.IsNotNull(Find(id), "spec 23 tool " + id + " is missing");
        }

        [Test]
        public void EveryToolStandsSomewhereRealAndWakesOnItsNight()
        {
            foreach (var tool in Tools())
            {
                Assert.IsTrue(FloorPlan.Exists(tool.floorId),
                              tool.toolId + " is on " + tool.floorId + ", which is not a floor");
                Assert.Greater(System.Array.IndexOf(ZoneIds.All, tool.zoneId), -1,
                               tool.toolId + " is in unknown zone " + tool.zoneId);

                // Spec 21 gives each machine a night. Zero would mean it never comes on, and
                // nothing else in the data would say so.
                Assert.Greater(tool.unlockNight, 0, tool.toolId + " never becomes available");
                Assert.IsNotNull(tool.RootMenu, tool.toolId + " has no root menu");
            }
        }

        [Test]
        public void TheUnlockNightsAreTheOnesSpec21Gives()
        {
            Assert.AreEqual(4, Find(ManualEventIds.A01_ReceiptPrinter).unlockNight);
            Assert.AreEqual(2, Find(ManualEventIds.A02_WishParcelLocker).unlockNight);
            Assert.AreEqual(3, Find(ManualEventIds.A03_LoungeVcr).unlockNight);
            Assert.AreEqual(5, Find(ManualEventIds.A04_EndlessToolbox).unlockNight);
            Assert.AreEqual(6, Find(ManualEventIds.A05_LostAndFoundMachine).unlockNight);
        }

        [Test]
        public void EveryMenuLinkPointsAtAMenuThatExists()
        {
            foreach (var tool in Tools())
                foreach (var menu in tool.menus)
                    foreach (var option in menu.options)
                    {
                        if (string.IsNullOrEmpty(option.nextMenuId)) continue;
                        Assert.IsNotNull(tool.FindMenu(option.nextMenuId),
                                         tool.toolId + "/" + option.optionId + " opens " +
                                         option.nextMenuId + ", which does not exist");
                    }
        }

        [Test]
        public void EveryMenuCanBeFinishedAtRatherThanOnlyNavigatedThrough()
        {
            foreach (var tool in Tools())
                foreach (var menu in tool.menus)
                {
                    bool hasExit = false;
                    foreach (var option in menu.options)
                        if (string.IsNullOrEmpty(option.nextMenuId)) hasExit = true;

                    Assert.IsTrue(hasExit, tool.toolId + " menu " + menu.menuId +
                                           " has no line that ends the transaction");
                }
        }

        [Test]
        public void NothingIsGivenAwayForFree()
        {
            // Spec 23 opens by saying these are not cheats. Every object that comes out of one
            // of these machines is paid for by a debt, a token, or a hold the caretaker is
            // now inside - and this is the check that keeps that true as content grows.
            foreach (var tool in Tools())
                foreach (var menu in tool.menus)
                    foreach (var option in menu.options)
                    {
                        if (string.IsNullOrEmpty(option.grantsFlagId)) continue;

                        bool paid = option.opensHold
                                 || !string.IsNullOrEmpty(option.consumesFlagId)
                                 || (option.onChosen != null && option.onChosen.Length > 0);

                        Assert.IsTrue(paid, tool.toolId + "/" + option.optionId +
                                            " hands something over for nothing");
                    }
        }

        [Test]
        public void EveryItemDispensedIsAKnownItem()
        {
            foreach (var tool in Tools())
                foreach (var menu in tool.menus)
                    foreach (var option in menu.options)
                    {
                        if (!string.IsNullOrEmpty(option.grantsFlagId))
                            Assert.Greater(System.Array.IndexOf(ItemIds.All, option.grantsFlagId), -1,
                                           tool.toolId + " grants unknown item " + option.grantsFlagId);

                        if (!string.IsNullOrEmpty(option.consumesFlagId))
                            Assert.Greater(System.Array.IndexOf(ItemIds.All, option.consumesFlagId), -1,
                                           tool.toolId + " spends unknown item " + option.consumesFlagId);
                    }
        }

        [Test]
        public void NoToolCanEverDecideAnEnding()
        {
            // Spec 32: leaning on these machines must make the last night harder and must not
            // close an ending off. The concrete form of that promise is that nothing a tool
            // does touches a stat the ending algorithm reads - so the only stats they are
            // allowed to move are the two debts and the exposure counter.
            foreach (var tool in Tools())
                foreach (var menu in tool.menus)
                    foreach (var option in menu.options)
                    {
                        var all = new List<ConsequenceDefinition>();
                        if (option.onChosen != null) all.AddRange(option.onChosen);
                        if (tool.onHoldExpired != null) all.AddRange(tool.onHoldExpired);
                        if (tool.onLateRelease != null) all.AddRange(tool.onLateRelease);

                        foreach (var c in all)
                        {
                            if (c == null || c.type != ConsequenceType.StatDelta) continue;
                            Assert.IsTrue(c.targetId == StatIds.MemoryDebt || c.targetId == StatIds.ToolDebt,
                                          tool.toolId + "/" + option.optionId + " moves " +
                                          c.targetId + ", which the ending reads");
                        }
                    }
        }

        [Test]
        public void TheTwoToolsWithHoldsAreTheOnesSpec23Names()
        {
            var locker = Find(ManualEventIds.A02_WishParcelLocker);
            Assert.AreEqual(AnomalyToolHold.CloseDoor, locker.hold);
            // Five real seconds, whatever the shift rate currently is. The escalation lands
            // after that window, not inside it.
            Assert.AreEqual(GameClock.RealSeconds(5), locker.holdGameSeconds);
            Assert.IsTrue(locker.holdSurvivesExpiry,
                          "spec 23 A02: the door is still open when the window runs out");
            Assert.AreEqual(3, locker.holdWarnKeys.Length);
            Assert.AreEqual(locker.holdWarnKeys.Length, locker.holdWarnAtGameSeconds.Length);
            Assert.Greater(locker.onLateRelease.Length, 0, "getting out late has to cost something");

            var toolbox = Find(ManualEventIds.A04_EndlessToolbox);
            Assert.AreEqual(AnomalyToolHold.ReturnTool, toolbox.hold);
            // Eight real minutes, which spec 23 A04 substitutes for the in-fiction half hour.
            Assert.AreEqual(GameClock.RealMinutes(8), toolbox.holdGameSeconds);
            Assert.IsFalse(toolbox.holdSurvivesExpiry, "the tool is gone when the hour is up");
            Assert.Greater(toolbox.onHoldExpired.Length, 0, "an unreturned tool has to cost something");

            foreach (var tool in Tools())
            {
                if (tool.hold != AnomalyToolHold.None) continue;
                Assert.AreEqual(0, tool.holdGameSeconds, tool.toolId + " has a hold time but no hold");
            }
        }

        [Test]
        public void TheOnlyRouteThatPaysMemoryBackIsGatedOnOwingSome()
        {
            // Spec 23 A03 and A05 each give a memory back. Neither may be usable before there
            // is a debt for it to pay, or the caretaker could bank memory in advance and take
            // the deep questions for free.
            int refunds = 0;

            foreach (var tool in Tools())
                foreach (var menu in tool.menus)
                    foreach (var option in menu.options)
                    {
                        if (option.onChosen == null) continue;
                        foreach (var c in option.onChosen)
                        {
                            if (c == null || c.type != ConsequenceType.AddMemoryDebt || c.amount >= 0)
                                continue;

                            refunds++;
                            bool gated = !string.IsNullOrEmpty(option.consumesFlagId);
                            if (option.conditions != null)
                                foreach (var condition in option.conditions)
                                    if (condition != null &&
                                        condition.type == ConditionType.StatGreaterOrEqual &&
                                        condition.keyA == StatIds.MemoryDebt) gated = true;

                            Assert.IsTrue(gated, tool.toolId + "/" + option.optionId +
                                                 " pays memory back with nothing owed");
                        }
                    }

            Assert.AreEqual(2, refunds, "spec 23 gives exactly two ways to get a memory back");
        }

        [Test]
        public void TheTapeThatStartsTheVcrIsActuallyHandedOver()
        {
            // A03 is gated on the tape M18 leaves behind. If nothing granted it the machine
            // would be permanently dead and nothing else in the content would notice.
            var vcr = Find(ManualEventIds.A03_LoungeVcr);
            bool gatedOnTape = false;
            foreach (var condition in vcr.unlockConditions)
                if (condition.type == ConditionType.FlagEquals && condition.keyA == ItemIds.BlackTape)
                    gatedOnTape = true;
            Assert.IsTrue(gatedOnTape, "A03 should want the tape");

            bool granted = false;
            foreach (var definition in SeedContent.BuildManualEvents())
            {
                if (definition.eventId != ManualEventIds.M18_RoofFigure) continue;

                // On every outcome: a wrong answer on night 3 must not delete a machine from
                // the rest of the game.
                Assert.IsTrue(Grants(definition.onCorrect, ItemIds.BlackTape),
                              "M18 correct does not leave the tape");
                Assert.IsTrue(Grants(definition.onWrong, ItemIds.BlackTape),
                              "M18 wrong does not leave the tape");
                granted = true;
            }
            Assert.IsTrue(granted, "M18 is missing");
        }

        static bool Grants(ConsequenceDefinition[] consequences, string flagId)
        {
            if (consequences == null) return false;
            foreach (var c in consequences)
                if (c != null && c.type == ConsequenceType.SetFlag && c.targetId == flagId && c.boolValue)
                    return true;
            return false;
        }
    }

    /// <summary>
    /// M13 as spec 22 prices it.
    ///
    /// The postcards are the one manual event whose right answer is to walk away, and the
    /// three courses have to cost three different things or the temptation is not one.
    /// </summary>
    public sealed class PostcardEconomyTests
    {
        static ManualEventDefinition Postcards()
        {
            foreach (var definition in SeedContent.BuildManualEvents())
                if (definition.eventId == ManualEventIds.M13_Postcards) return definition;
            return null;
        }

        static int DistortionOf(ConsequenceDefinition[] consequences)
        {
            int total = 0;
            if (consequences == null) return 0;
            foreach (var c in consequences)
                if (c != null && c.type == ConsequenceType.AddDistortion) total += c.amount;
            return total;
        }

        [Test]
        public void ShreddingItUnreadIsTheOnlyThingThatLowersExposure()
        {
            var definition = Postcards();
            Assert.AreEqual(-2, DistortionOf(definition.onCorrect),
                            "spec 22 M13: shredding it unread is worth two back");
            Assert.AreEqual(2, DistortionOf(definition.onPartial),
                            "spec 22 M13: five to read it, three back for the memo");
            Assert.AreEqual(5, DistortionOf(definition.onWrong),
                            "spec 22 M13: reading it and recovering nothing costs five");
        }

        [Test]
        public void OnlyWritingAGuessIntoTheFactColumnBreaksARule()
        {
            // Spec 22 M13 forbids exactly one thing. Reading the postcard is a choice the
            // manual offers, so no clause about it may carry a rule key - otherwise a
            // caretaker who took the second course would be charged with a violation.
            var definition = Postcards();
            int ruleClauses = 0;

            foreach (var check in definition.correctConditions)
            {
                if (check == null || string.IsNullOrEmpty(check.ruleKey)) continue;
                ruleClauses++;
                Assert.AreEqual("guessedAsFact", check.counterId,
                                "the only forbidden act is writing a guess as fact");
            }

            Assert.AreEqual(1, ruleClauses, "M13 has exactly one prohibition");

            // And it has to be tested first, or a course the manual allows would be reported
            // as the broken rule instead.
            Assert.AreEqual("guessedAsFact", definition.correctConditions[0].counterId);
        }

        [Test]
        public void ReadingItAndLoggingItProperlyIsAWayThroughRatherThanAFailure()
        {
            var definition = Postcards();
            Assert.Greater(definition.partialConditions.Length, 0,
                           "spec 22 M13 has a second course and it is not a wrong answer");

            bool wantsTheLog = false;
            foreach (var check in definition.partialConditions)
                if (check != null && check.counterId == "logged") wantsTheLog = true;

            Assert.IsTrue(wantsTheLog, "the second course is the separated log");
        }
    }
}
