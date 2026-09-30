using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NO404.Core;

namespace NO404.Tests
{
    public sealed class GameClockTests
    {
        [SetUp] public void SetUp() { EventBus.Clear(); }
        [TearDown] public void TearDown() { EventBus.Clear(); }

        [Test]
        public void StartsAtShiftStart()
        {
            var clock = new GameClock();
            Assert.AreEqual(GameClock.ShiftStartSecond, clock.GameSecond);
            Assert.AreEqual(22, clock.Hour);
            Assert.AreEqual(0, clock.Minute);
            Assert.AreEqual("22:00", clock.ToClockString());
        }

        [Test]
        public void OneGameMinutePassesAtTheAuthoredShiftRate()
        {
            // Written against the constant rather than against the number it happens to hold.
            // The rate is a design dial (GDD 6.2) and it has already moved once - from four
            // real seconds to the game minute to ten - and a test that has to be edited every
            // time the dial turns is a test that stops saying anything.
            var clock = new GameClock();

            int realSeconds = Mathf.RoundToInt(60f / GameClock.GameSecondsPerRealSecond);
            for (int i = 0; i < realSeconds; i++) clock.Tick(1f);

            Assert.AreEqual(GameClock.ShiftStartSecond + 60, clock.GameSecond);
        }

        [Test]
        public void SmallDeltasDoNotDriftOverAWholeShift()
        {
            var clock = new GameClock();

            // A whole shift, fed a frame at a time at sixty frames a second.
            int shiftSeconds = GameClock.ShiftEndSecond - GameClock.ShiftStartSecond;
            int realSeconds = Mathf.RoundToInt(shiftSeconds / GameClock.GameSecondsPerRealSecond);

            const float step = 1f / 60f;
            for (int i = 0; i < 60 * realSeconds; i++) clock.Tick(step);

            // Integer seconds mean the only permitted error is the sub-second carry.
            Assert.AreEqual(GameClock.ShiftEndSecond, clock.GameSecond, 1);
            Assert.IsTrue(clock.ShiftOver);
        }

        [Test]
        public void TimeScaleZeroFreezesTheClock()
        {
            var clock = new GameClock { TimeScale = 0f };
            clock.Tick(10f);
            Assert.AreEqual(GameClock.ShiftStartSecond, clock.GameSecond);
        }

        [Test]
        public void PausedClockDoesNotAdvance()
        {
            var clock = new GameClock { Paused = true };
            clock.Tick(10f);
            Assert.AreEqual(GameClock.ShiftStartSecond, clock.GameSecond);
        }

        [Test]
        public void MinuteEventFiresOncePerGameMinute()
        {
            var clock = new GameClock();
            int fired = 0;
            clock.OnMinuteChanged += _ => fired++;

            // Three game minutes, however long that is in real seconds at the current rate.
            const int minutes = 3;
            float realSeconds = minutes * 60f / GameClock.GameSecondsPerRealSecond;

            int frames = Mathf.RoundToInt(realSeconds / 0.2f);
            for (int i = 0; i < frames; i++) clock.Tick(0.2f);

            Assert.AreEqual(minutes, fired);
        }

        [Test]
        public void EarlyMorningHoursAreStoredAsTheSecondHalfOfTheNight()
        {
            var clock = new GameClock();
            clock.SetTime(2, 14);

            Assert.AreEqual(2, clock.Hour);
            Assert.IsTrue(clock.GameSecond > GameClock.ShiftStartSecond,
                          "02:14 must sort after 22:00 within the same shift");
        }

        [Test]
        public void InWindowHandlesOpenEndedWindows()
        {
            Assert.IsTrue(GameClock.InWindow(100, 50, 150));
            Assert.IsFalse(GameClock.InWindow(200, 50, 150));
            Assert.IsTrue(GameClock.InWindow(200, 50, 0), "endSecond 0 means 'until end of shift'");
        }

        [Test]
        public void ShiftProgressIsClamped()
        {
            var clock = new GameClock();
            Assert.AreEqual(0f, clock.ShiftProgress, 0.001f);

            clock.SetGameSecond(GameClock.ShiftEndSecond + 5000);
            Assert.AreEqual(1f, clock.ShiftProgress, 0.001f);
        }
    }

    public sealed class EventBusTests
    {
        readonly struct Ping { public readonly int Value; public Ping(int value) { Value = value; } }
        readonly struct Pong { public readonly int Value; public Pong(int value) { Value = value; } }

        [SetUp] public void SetUp() { EventBus.Clear(); }
        [TearDown] public void TearDown() { EventBus.Clear(); }

        [Test]
        public void PublishReachesSubscriber()
        {
            int received = 0;
            EventBus.Subscribe<Ping>(e => received = e.Value);

            EventBus.Publish(new Ping(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void UnsubscribeStopsDelivery()
        {
            int count = 0;
            System.Action<Ping> handler = _ => count++;

            EventBus.Subscribe(handler);
            EventBus.Publish(new Ping(1));
            EventBus.Unsubscribe(handler);
            EventBus.Publish(new Ping(1));

            Assert.AreEqual(1, count);
        }

        [Test]
        public void DoubleSubscribeDoesNotDoubleDeliver()
        {
            int count = 0;
            System.Action<Ping> handler = _ => count++;

            EventBus.Subscribe(handler);
            EventBus.Subscribe(handler);
            EventBus.Publish(new Ping(1));

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, EventBus.SubscriberCount<Ping>());
        }

        [Test]
        public void EventsAreRoutedByType()
        {
            int pings = 0, pongs = 0;
            EventBus.Subscribe<Ping>(_ => pings++);
            EventBus.Subscribe<Pong>(_ => pongs++);

            EventBus.Publish(new Ping(1));

            Assert.AreEqual(1, pings);
            Assert.AreEqual(0, pongs);
        }

        [Test]
        public void AThrowingHandlerDoesNotStopTheOthers()
        {
            int reached = 0;
            EventBus.Subscribe<Ping>(_ => { throw new System.InvalidOperationException("boom"); });
            EventBus.Subscribe<Ping>(_ => reached++);

            // The bus logs the exception as an error; the test only asserts isolation.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            EventBus.Publish(new Ping(1));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(1, reached);
        }

        [Test]
        public void SubscribingDuringDispatchIsSafe()
        {
            int outer = 0, inner = 0;
            EventBus.Subscribe<Ping>(_ =>
            {
                outer++;
                if (outer == 1) EventBus.Subscribe<Ping>(__ => inner++);
            });

            EventBus.Publish(new Ping(1));
            Assert.AreEqual(0, inner, "a handler added mid-dispatch must not run for the current event");

            EventBus.Publish(new Ping(2));
            Assert.AreEqual(1, inner);
        }
    }

    public sealed class SettingsSerializationTests
    {
        [Test]
        public void RoundTripPreservesValues()
        {
            var settings = new GameSettings
            {
                language = "en",
                fieldOfView = 82f,
                mouseSensitivity = 2.5f,
                headBobAmount = 0.4f,
                subtitles = false,
                hudOpacity = 0.8f
            };

            var restored = SettingsService.Deserialize(SettingsService.Serialize(settings));

            Assert.IsNotNull(restored);
            Assert.AreEqual("en", restored.language);
            Assert.AreEqual(82f, restored.fieldOfView, 0.001f);
            Assert.AreEqual(2.5f, restored.mouseSensitivity, 0.001f);
            Assert.AreEqual(0.4f, restored.headBobAmount, 0.001f);
            Assert.IsFalse(restored.subtitles);
            Assert.AreEqual(0.8f, restored.hudOpacity, 0.001f);
        }

        [Test]
        public void SanitizeClampsToTheGddRanges()
        {
            var settings = new GameSettings
            {
                fieldOfView = 200f,
                mouseSensitivity = 99f,
                headBobAmount = 5f,
                masterVolume = -3f,
                language = null
            };

            settings.Sanitize();

            Assert.AreEqual(90f, settings.fieldOfView, 0.001f);       // GDD 8.2: 60-90
            Assert.AreEqual(5f, settings.mouseSensitivity, 0.001f);   // GDD 8.2: 0.1-5.0
            Assert.AreEqual(1f, settings.headBobAmount, 0.001f);
            Assert.AreEqual(0f, settings.masterVolume, 0.001f);
            Assert.AreEqual("ko", settings.language);
        }

        [Test]
        public void DeserializingGarbageReturnsNullInsteadOfThrowing()
        {
            Assert.IsNull(SettingsService.Deserialize(null));
            Assert.IsNull(SettingsService.Deserialize(string.Empty));
        }
    }

    public sealed class GameStateTests
    {
        [SetUp] public void SetUp() { EventBus.Clear(); }
        [TearDown] public void TearDown() { EventBus.Clear(); }

        [Test]
        public void InitialValuesMatchTheBalanceTable()
        {
            var state = new GameStateService();

            // v5.0 5.2. BuildingSafety opens lower and ArchiveIntegrity opens in the middle
            // rather than at nothing, because v5.0 spends both of them in either direction -
            // an archive that can only ever go up is not a thing the player can lose.
            Assert.AreEqual(60, state.GetStat(StatIds.Performance));
            Assert.AreEqual(50, state.GetStat(StatIds.CommunityTrust));
            Assert.AreEqual(70, state.GetStat(StatIds.BuildingSafety));
            Assert.AreEqual(50, state.GetStat(StatIds.ArchiveIntegrity));
            Assert.AreEqual(0, state.GetStat(StatIds.ChairmanAlert));
            Assert.AreEqual(0, state.GetStat(StatIds.HarinResonance));
        }

        [Test]
        public void StatsAreClampedToTheirOwnMaximum()
        {
            var state = new GameStateService();

            state.AddStat(StatIds.CommunityTrust, 500);
            Assert.AreEqual(100, state.GetStat(StatIds.CommunityTrust));

            // v5.0 5.2 put the three hidden stats on the same 0..100 scale as the visible
            // ones. They are hidden because the player is never shown the number, not because
            // the number is small.
            state.AddStat(StatIds.HarinResonance, 500);
            Assert.AreEqual(100, state.GetStat(StatIds.HarinResonance), "hidden stats run 0..100 under v5.0");

            state.AddStat(StatIds.ArchiveIntegrity, 500);
            Assert.AreEqual(100, state.GetStat(StatIds.ArchiveIntegrity));

            // v5.0 5.3. A debt is a count out of five, and five is as bad as it gets.
            state.AddDebt(DebtIds.Record, 500, "test");
            Assert.AreEqual(DebtIds.Max, state.GetDebt(DebtIds.Record), "a debt caps at five");

            state.AddStat(StatIds.BuildingSafety, -500);
            Assert.AreEqual(0, state.GetStat(StatIds.BuildingSafety));
        }

        [Test]
        public void StatChangePublishesExactlyOnce()
        {
            var state = new GameStateService();
            int events = 0;
            EventBus.Subscribe<StatChangedEvent>(_ => events++);

            state.AddStat(StatIds.Performance, 5);
            state.AddStat(StatIds.Performance, 0);          // no change
            state.SetStat(StatIds.Performance, 65);         // same value

            Assert.AreEqual(1, events);
        }

        [Test]
        public void Staff1AccessIsGrantedFromTheStart()
        {
            var state = new GameStateService();

            Assert.IsTrue(state.HasAccess(AccessLevel.Staff1));
            Assert.IsFalse(state.HasAccess(AccessLevel.Key404));

            state.GrantAccess(AccessLevel.Key404);
            Assert.IsTrue(state.HasAccess(AccessLevel.Key404));
        }

        [Test]
        public void LoadFromRestoresStatsFlagsAndAccess()
        {
            var state = new GameStateService();

            var stats = new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>(StatIds.Performance, 12)
            };
            var flags = new List<KeyValuePair<string, bool>>
            {
                new KeyValuePair<string, bool>(FlagIds.Knows404, true)
            };

            state.LoadFrom(4, stats, flags, new[] { (int)AccessLevel.Archive });

            Assert.AreEqual(4, state.NightIndex);
            Assert.AreEqual(12, state.GetStat(StatIds.Performance));
            Assert.IsTrue(state.GetFlag(FlagIds.Knows404));
            Assert.IsTrue(state.HasAccess(AccessLevel.Archive));
        }
    }

    /// <summary>
    /// GDD 15.3. The access schedule, checked against the rooms each night actually sends the
    /// player to.
    ///
    /// This exists because of a specific failure. The cold open (GDD 9.1) made throwing the
    /// basement breaker night 1's first objective, and the basement is behind Staff-2, and
    /// Staff-2 was handed out on night 2 - so the night's compulsory errand sat behind a
    /// locked door and the camera wall could never come back. The full-playthrough smoke test
    /// passed anyway: it pushes the clock to 06:00, the P0 fail-safe closes the case, and a
    /// shift nobody could play still reaches an ending.
    ///
    /// A test that walks the world would have caught it, and so does this - more cheaply. The
    /// rule it guards is one line: a night may not require a room it has not been given the
    /// key to.
    /// </summary>
    public sealed class AccessScheduleTests
    {
        [Test]
        public void TheBasementIsOpenOnTheNightTheColdOpenSendsThePlayerThere()
        {
            Assert.IsTrue(GameLoop.GrantsAccessOnNight(AccessLevel.Staff2, 1, false),
                "night 1 requires the basement breaker board (GDD 9.1), so it requires Staff-2");
        }

        [Test]
        public void EveryNightCanOpenTheOfficeAndTheCorridors()
        {
            for (int night = 1; night <= GameLoop.FinalNight; night++)
                Assert.IsTrue(GameLoop.GrantsAccessOnNight(AccessLevel.Staff1, night, false),
                              "night " + night + " cannot leave the office");
        }

        [Test]
        public void TheRestOfTheScheduleStillRamps()
        {
            Assert.IsFalse(GameLoop.GrantsAccessOnNight(AccessLevel.Maintenance, 2, false));
            Assert.IsTrue(GameLoop.GrantsAccessOnNight(AccessLevel.Maintenance, 3, false),
                          "the service passage opens on night 3 (GDD 9.4)");

            Assert.IsFalse(GameLoop.GrantsAccessOnNight(AccessLevel.Archive, 3, true));
            Assert.IsTrue(GameLoop.GrantsAccessOnNight(AccessLevel.Archive, 4, true),
                          "Taeho's cooperation opens the records room a night early");
            Assert.IsFalse(GameLoop.GrantsAccessOnNight(AccessLevel.Archive, 4, false));

            // C11 lives in the records room, so night 5 opens it no matter what happened.
            Assert.IsTrue(GameLoop.GrantsAccessOnNight(AccessLevel.Archive, 5, false),
                          "C11 must never be locked out of its own location (GDD 11.2)");
        }

        /// <summary>The 404 key is picked up, never scheduled - it is evidence, not a grade.</summary>
        [Test]
        public void TheSteelDoorKeyIsNeverGrantedByTheSchedule()
        {
            for (int night = 0; night <= GameLoop.FinalNight; night++)
                Assert.IsFalse(GameLoop.GrantsAccessOnNight(AccessLevel.Key404, night, true),
                               "the 404 key comes from the mailbox, not from the calendar");
        }
    }
}
