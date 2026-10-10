using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NO404.Anomalies;
using NO404.Cases;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// dev.mainonly: the shift with everything except its spine taken off it.
    ///
    /// A debug switch is worth testing for one reason - the moment it stops being trusted it
    /// stops being used, and a playtester who cannot tell whether the quiet night in front of
    /// them is the switch working or the content being broken has lost the only thing the
    /// switch was for. So what it takes away, what it keeps, and that it puts everything back
    /// are all held down here.
    ///
    /// The last of those is the one that would rot silently. A switch that spent a draw, or
    /// left an event half-open, would make the *next* normal shift wrong rather than this one.
    /// </summary>
    public sealed class MainOnlyModeTests
    {
        readonly List<CaseDefinition> _made = new List<CaseDefinition>();

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
            ServiceHub.ResetPlaythrough();
            ServiceHub.ManualEvents.YieldToAuthoredPools = false;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            // The switch is a static, so a test that left it on would take the next one with
            // it - and that one would fail somewhere else entirely.
            MainOnlyMode.Set(false);
            EventBus.Clear();

            for (int i = 0; i < _made.Count; i++)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);

            _made.Clear();
        }

        // ---- a night for the pool to draw ------------------------------------

        CaseDefinition Quest(string id, int night, QuestType type, bool main = false)
        {
            var def = ScriptableObject.CreateInstance<CaseDefinition>();
            def.caseId = id;
            def.nightIndex = night;
            def.questType = type;
            def.isFixedMain = main;
            def.baseWeight = 100;
            def.estimatedMinutes = 6f;
            _made.Add(def);
            return def;
        }

        List<CaseDefinition> BuildPool(int night)
        {
            var pool = new List<CaseDefinition>
            {
                Quest("N" + night + "-M01", night, QuestType.Main, main: true)
            };

            for (int i = 0; i < 5; i++) pool.Add(Quest("N" + night + "-NRM" + i, night, QuestType.Normal));
            for (int i = 0; i < 4; i++) pool.Add(Quest("N" + night + "-MIX" + i, night, QuestType.Mixed));
            for (int i = 0; i < 3; i++) pool.Add(Quest("N" + night + "-ANM" + i, night, QuestType.Anomaly));
            pool.Add(Quest("N" + night + "-CON0", night, QuestType.Consequence));
            pool.Add(Quest("N" + night + "-FIN0", night, QuestType.FinalPressure));

            return pool;
        }

        static NightPoolService.Conditions Healthy
        {
            get { return new NightPoolService.Conditions { Hp = 100, San = 100 }; }
        }

        List<string> DrawNight(int seed, int night)
        {
            var service = new NightPoolService();
            service.BeginCampaign(seed);
            return new List<string>(service.SelectForNight(night, BuildPool(night), Healthy));
        }

        [Test]
        public void TheDrawKeepsTheSpineAndNothingElse()
        {
            MainOnlyMode.Set(true);

            var drawn = DrawNight(4242, 3);

            Assert.AreEqual(1, drawn.Count, "main-only draws exactly the night's spine");
            Assert.AreEqual("N3-M01", drawn[0]);
        }

        [Test]
        public void ANormalNightStillDrawsAroundItsSpine()
        {
            var drawn = DrawNight(4242, 3);

            Assert.Contains("N3-M01", drawn);
            Assert.GreaterOrEqual(drawn.Count - 1, NightPoolService.RandomFloor,
                                  "the switch is off, so the night is a whole night");
        }

        [Test]
        public void SwitchingItOffDealsTheNightItWouldHaveDealt()
        {
            // The seed is what makes a campaign reproducible (v5.0 4.4 step 10), so the switch
            // is only safe if it spends none of it. Draw the night, strip it, draw it again:
            // the third result has to be the first, or a playtester who flipped the switch for
            // one shift has quietly moved every night after it.
            var before = DrawNight(9001, 2);

            MainOnlyMode.Set(true);
            var stripped = DrawNight(9001, 2);
            MainOnlyMode.Set(false);

            var after = DrawNight(9001, 2);

            Assert.AreEqual(1, stripped.Count);
            CollectionAssert.AreEqual(before, after, "the seed is unspent");
        }

        // ---- what the switch keeps -------------------------------------------

        [Test]
        public void WhatTheMainOwnsRidesInWithIt()
        {
            MainOnlyMode.Set(true);
            ServiceHub.State.BeginNight(1);
            ServiceHub.Cases.BeginNight(1);

            Assert.IsFalse(MainOnlyMode.Suppresses("N1-M01"),
                           "a call or an anomaly named against tonight's main IS the main");
            Assert.IsTrue(MainOnlyMode.Suppresses("N3-M01"),
                          "another night's spine is not tonight's work");
            Assert.IsTrue(MainOnlyMode.Suppresses(null),
                          "naming no case at all means belonging to the night at large");
        }

        [Test]
        public void NothingIsSuppressedWhileTheSwitchIsOff()
        {
            ServiceHub.State.BeginNight(1);
            ServiceHub.Cases.BeginNight(1);

            Assert.IsFalse(MainOnlyMode.Suppresses(null));
            Assert.IsFalse(MainOnlyMode.Suppresses("N3-M01"));
        }

        // ---- what the switch takes away --------------------------------------

        [Test]
        public void TheManualEventChainNeverOpensItself()
        {
            MainOnlyMode.Set(true);
            ServiceHub.State.BeginNight(1);
            ServiceHub.ManualEvents.BeginNight(1);

            var head = ServiceHub.ManualEvents.Find(ManualEventIds.M06_LostParcel);
            Assert.IsNotNull(head);
            Assert.AreNotEqual(ManualEventState.Active, head.State,
                               "night 1's head is the manual, and the manual is not a main quest");
        }

        [Test]
        public void TheManualEventChainComesBackWhenItIsSwitchedOff()
        {
            MainOnlyMode.Set(true);
            ServiceHub.State.BeginNight(1);
            ServiceHub.ManualEvents.BeginNight(1);

            MainOnlyMode.Set(false);
            ServiceHub.ManualEvents.BeginNight(1);

            Assert.AreEqual(ManualEventState.Active,
                            ServiceHub.ManualEvents.Find(ManualEventIds.M06_LostParcel).State,
                            "nothing was consumed while the switch was on");
        }

        [Test]
        public void TheStalkerStaysAway()
        {
            MainOnlyMode.Set(true);
            ServiceHub.State.BeginNight(5);
            ServiceHub.Threat.BeginNight(5);

            Assert.IsFalse(ServiceHub.Threat.IsActive, "night 5 patrols; main-only does not");

            MainOnlyMode.Set(false);
            ServiceHub.Threat.BeginNight(5);

            Assert.IsTrue(ServiceHub.Threat.IsActive, "and the window is still there afterwards");
        }
    }
}
