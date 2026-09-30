using System.Collections.Generic;
using NO404.Core;

namespace NO404.Facility
{
    /// <summary>
    /// The night reserve (GDD 15.5).
    ///
    /// The building runs off a night circuit with a finite reserve, and every way of seeing
    /// costs some of it. Watching one channel full screen - which is the only place a report
    /// can be filed - is the most expensive thing the caretaker can do, so the surveillance
    /// half of the game finally has a price attached to it instead of being a free screensaver
    /// the player can leave running for eight hours.
    ///
    /// This is the everyday layer. It sits underneath the scripted night-5 outage in
    /// FacilityMeterService, which keeps owning the five-circuit choice from GDD 9.6.
    ///
    /// Stored in tenths of a percent so the drain stays integer arithmetic; CLAUDE.md forbids
    /// accumulating float time and the same reasoning applies to anything the clock drives.
    /// </summary>
    public sealed class NightPowerService
    {
        public const int Full = 1000;           // tenths of a percent

        /// <summary>Below this the corridors start to fail and the pressure ladder feeds.</summary>
        public const int LowReserve = 250;
        /// <summary>Below this the CCTV wall starts dropping channels.</summary>
        public const int CriticalReserve = 120;
        /// <summary>Reserve lost per dead channel below <see cref="CriticalReserve"/>.</summary>
        public const int ReservePerLostChannel = 25;

        // ---- draw, in tenths of a percent per game HOUR ----------------------
        //
        // Per hour rather than per minute because a shift is 480 game minutes: at per-minute
        // rates the reserve empties before 22:30 no matter what the player does, which is not
        // a budget, just a fuse.

        public const int BaseDraw = 45;
        /// <summary>The 6-up wall. Cheap, and correspondingly hard to catch anything on.</summary>
        public const int CctvGridDraw = 25;
        /// <summary>
        /// One channel, full screen. The only view a report can be filed from (GDD 12.5), and
        /// so the most expensive thing in the building: watching this way for a whole shift
        /// only just survives on two breaker trips, and each of those empties the office.
        /// </summary>
        public const int CctvSingleDraw = 120;
        public const int FlashlightDraw = 40;
        public const int CorridorLightsDraw = 30;
        /// <summary>Flat cost per elevator ride, charged on arrival.</summary>
        public const int ElevatorRideCost = 80;

        /// <summary>
        /// Night 5 runs the building on an overloaded feed, but that scarcity is already
        /// spent: GDD 9.6 gives the player three of five circuits and asks them to choose.
        /// Charging the night reserve extra on top of that is billing the same shortage twice,
        /// and the simulator showed it - night 5 ended at 10% without the player watching
        /// anything at all.
        /// </summary>
        public const int BlackoutNight = 5;

        /// <summary>
        /// The reserve a shift opens on, in tenths of a percent.
        ///
        /// Every night used to start full, and the budget table in GDD 15.5 says what that
        /// meant: a caretaker who simply does the job ends the shift at 40% and never once
        /// meets the low-reserve warning, the dying channels or the breaker. The whole of
        /// 15.5 - the FNAF half of this game's design - was therefore invisible for the first
        /// two hours of play, which is the window Steam refunds inside.
        ///
        /// Night 1 opens at 24% - below <see cref="LowReserve"/>, so the warning is not
        /// something the shift works its way towards over five hours but the first thing it
        /// says. It is the game's opening night now that the prologue is gone (GDD 9.1), and
        /// the cold open needs the reserve and the cut feeds to point at the same door: the
        /// basement board is the only thing in the building that answers either.
        ///
        /// 62% was the old value, chosen by the simulator for a night that opened calm and
        /// gave the player until 02:56 to notice the meter. That night no longer exists. What
        /// the simulator was actually protecting against - a teaching night that is the
        /// hardest night in the game - is handled instead by the trip being compulsory and
        /// early: one board throw inside the first twenty minutes is worth +30%, and the
        /// hour's cooldown then leaves a second throw available for the back half. The shift
        /// therefore has 24 + 30 + 30 = 84% to spend against roughly 60-80% of draw, which is
        /// tight everywhere and fatal nowhere.
        ///
        /// Every other night is unchanged, so GDD 25.4 still holds where it was measured.
        /// </summary>
        static readonly int[] StartingReserveByNight = { 240, 240, Full, Full, Full, Full, Full };

        /// <summary>The reserve <see cref="BeginNight"/> will open the given night on.</summary>
        public static int StartingReserveFor(int nightIndex)
        {
            if (nightIndex < 0) return Full;
            if (nightIndex >= StartingReserveByNight.Length) return Full;
            return StartingReserveByNight[nightIndex];
        }

        // ---- the recovery verb ----------------------------------------------

        /// <summary>Objective target for "the board has been thrown" (ObjectiveType.PerformAction).</summary>
        public const string BreakerActionId = "breaker_reset";

        /// <summary>What one trip to the basement breaker panel puts back.</summary>
        public const int BreakerResetAmount = 300;
        /// <summary>Game seconds before the panel can be reset again.</summary>
        public const int BreakerCooldownSeconds = 3600;
        public const int BreakerResetsPerNight = 2;

        readonly List<string> _darkChannels = new List<string>();

        int _reserve = Full;
        int _drawCarry;         // game-seconds x tenths-per-hour, integer only (CLAUDE.md)
        int _lastGameSecond;
        int _nightIndex;
        int _breakerResetsUsed;
        int _breakerReadyAtGameSecond;
        bool _wasLow;

        /// <summary>Corridor lighting. Cheap to leave on, and the dark is not free either.</summary>
        public bool CorridorLightsOn { get; private set; } = true;

        public int Reserve { get { return _reserve; } }
        public int ReservePercent { get { return _reserve / 10; } }
        public float Reserve01 { get { return _reserve / (float)Full; } }

        public bool IsLow { get { return _reserve <= LowReserve; } }
        public bool IsCritical { get { return _reserve <= CriticalReserve; } }
        public bool IsDead { get { return _reserve <= 0; } }

        public int BreakerResetsRemaining { get { return BreakerResetsPerNight - _breakerResetsUsed; } }
        public IReadOnlyList<string> DarkChannels { get { return _darkChannels; } }

        public bool BreakerReady
        {
            get
            {
                if (BreakerResetsRemaining <= 0) return false;
                var clock = ServiceHub.Clock;
                return clock == null || clock.GameSecond >= _breakerReadyAtGameSecond;
            }
        }

        public void BeginNight(int nightIndex)
        {
            _nightIndex = nightIndex;
            _reserve = StartingReserveFor(nightIndex);
            _drawCarry = 0;
            _breakerResetsUsed = 0;
            _breakerReadyAtGameSecond = 0;
            _wasLow = false;
            CorridorLightsOn = true;
            RestoreDarkChannels();
            _lastGameSecond = ServiceHub.Clock != null ? ServiceHub.Clock.GameSecond : GameClock.ShiftStartSecond;

            // A shift may now open below the warning line (GDD 15.5), and the warning is only
            // ever raised by ApplyThresholds on a change. Without this the player would be
            // told about a shortage they had already been living with for a game hour, by
            // which time the meter has stopped being information and started being an ambush.
            _wasLow = IsLow;
            EventBus.Publish(new PowerReserveChangedEvent(ReservePercent, _wasLow));

            if (_wasLow)
            {
                EventBus.Publish(new NotificationEvent("ui.notify.power_low", NotificationSeverity.Warning));
                if (ServiceHub.Audio != null) ServiceHub.Audio.PlayCue(AudioCue.PowerWarning);
            }

            RefreshDarkChannels();
        }

        /// <summary>Called once per frame by GameLoop.</summary>
        public void Tick()
        {
            var clock = ServiceHub.Clock;
            if (clock == null) return;

            int now = clock.GameSecond;
            int elapsed = now - _lastGameSecond;
            _lastGameSecond = now;
            if (elapsed <= 0) return;

            _drawCarry += elapsed * CurrentDrawPerHour();
            while (_drawCarry >= 3600)
            {
                _drawCarry -= 3600;
                Drain(1, "reason.power_draw");
            }
        }

        /// <summary>What the building is pulling right now, in tenths of a percent per game hour.</summary>
        public int CurrentDrawPerHour()
        {
            int draw = BaseDraw;

            if (CorridorLightsOn) draw += CorridorLightsDraw;

            // Per caretaker, not per game (v3.0 34). The corridor lights are the building's;
            // a torch and a screen are somebody's, and on a three-handed shift there are three
            // of each to pay for. In single player the roster holds exactly one entry and this
            // charges exactly what it always did.
            var presence = ServiceHub.Presence;
            if (presence == null) return draw + (FlashlightOn ? FlashlightDraw : 0);

            draw += FlashlightDraw * presence.TorchCount;

            int grid, fullscreen;
            presence.CountScreens(out grid, out fullscreen);
            draw += CctvGridDraw * grid + CctvSingleDraw * fullscreen;

            return draw;
        }

        /// <summary>
        /// The local caretaker's torch.
        ///
        /// Kept because the pressure ladder and a handful of rules ask "is a torch on in this
        /// room", which is a question about this copy of the game. What the reserve is charged
        /// comes from the presence roster instead.
        /// </summary>
        public bool FlashlightOn { get; set; }

        public void SetCorridorLights(bool on)
        {
            if (CorridorLightsOn == on) return;
            CorridorLightsOn = on;
            EventBus.Publish(new PowerReserveChangedEvent(ReservePercent, IsLow));
            Log.Info("Power", "corridor lights " + (on ? "on" : "off"));
        }

        /// <summary>Charged by ElevatorController when the car actually moves.</summary>
        public void ChargeElevatorRide() { Drain(ElevatorRideCost, "reason.power_elevator"); }

        public void Drain(int tenths, string reasonKey)
        {
            if (tenths <= 0) return;

            int previous = _reserve;
            _reserve = UnityEngine.Mathf.Max(0, _reserve - tenths);
            if (_reserve == previous) return;

            ApplyThresholds(reasonKey);
        }

        /// <summary>
        /// The basement breaker panel. This is why the caretaker leaves the office: the reserve
        /// can only be topped up from a room with no interphone in it, so buying power always
        /// costs whatever happens at the front door while nobody is watching it.
        /// </summary>
        public bool TryResetBreaker()
        {
            if (!BreakerReady) return false;

            _breakerResetsUsed++;
            int previous = _reserve;
            _reserve = UnityEngine.Mathf.Min(Full, _reserve + BreakerResetAmount);

            var clock = ServiceHub.Clock;
            _breakerReadyAtGameSecond = (clock != null ? clock.GameSecond : 0) + BreakerCooldownSeconds;

            RestoreDarkChannels();
            ApplyThresholds("reason.power_breaker_reset");

            // The cold open's blackout is not a reserve state, but this board is what ends it
            // (GDD 9.2). Ordering matters: the dark-channel bookkeeping above has to be done
            // before the wall comes back, or the reserve's own casualties come back with it.
            if (ServiceHub.Cctv != null) ServiceHub.Cctv.RestoreCutFeeds();

            if (ServiceHub.Cases != null)
                ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.PerformAction, BreakerActionId);

            if (ServiceHub.Audio != null) ServiceHub.Audio.PlayCue(AudioCue.BreakerReset);
            EventBus.Publish(new NotificationEvent("ui.notify.breaker_reset", NotificationSeverity.Info));
            Log.Info("Power", "breaker reset " + previous + " -> " + _reserve);
            return true;
        }

        void ApplyThresholds(string reasonKey)
        {
            bool low = IsLow;
            EventBus.Publish(new PowerReserveChangedEvent(ReservePercent, low));

            if (low && !_wasLow)
            {
                EventBus.Publish(new NotificationEvent("ui.notify.power_low", NotificationSeverity.Warning));
                if (ServiceHub.Audio != null) ServiceHub.Audio.PlayCue(AudioCue.PowerWarning);
            }
            _wasLow = low;

            RefreshDarkChannels();

            // The office door is on the same circuit as everything else. At zero the bolt
            // releases, which is the one state where locking the door is not an option.
            if (IsDead && ServiceHub.Pressure != null)
                ServiceHub.Pressure.Add(NO404.Pressure.NightPressureService.MissedDeadline,
                                        "reason.pressure_blackout");
        }

        /// <summary>
        /// Below the critical line the wall loses one channel per 2.5% of reserve. Which
        /// channel is decided by the night seed, so a night is always the same night - GDD
        /// 12.6 makes the same promise about resident traffic.
        /// </summary>
        void RefreshDarkChannels()
        {
            var cctv = ServiceHub.Cctv;
            if (cctv == null) return;

            int shouldBeDark = _reserve >= CriticalReserve
                ? 0
                : UnityEngine.Mathf.Min(cctv.Channels.Count,
                                        1 + (CriticalReserve - _reserve) / ReservePerLostChannel);

            while (_darkChannels.Count > shouldBeDark)
            {
                string id = _darkChannels[_darkChannels.Count - 1];
                _darkChannels.RemoveAt(_darkChannels.Count - 1);
                cctv.SetFeedState(id, CCTV.FeedState.Live);
            }

            if (_darkChannels.Count >= shouldBeDark) return;

            var random = new System.Random(_nightIndex * 977 + 41);
            var order = new List<string>();
            for (int i = 0; i < cctv.Channels.Count; i++) order.Add(cctv.Channels[i].CameraId);

            // Fisher-Yates against the night seed, so the same night always loses the same
            // cameras in the same order.
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var swap = order[i]; order[i] = order[j]; order[j] = swap;
            }

            for (int i = 0; i < order.Count && _darkChannels.Count < shouldBeDark; i++)
            {
                if (_darkChannels.Contains(order[i])) continue;
                _darkChannels.Add(order[i]);
                cctv.SetFeedState(order[i], CCTV.FeedState.SignalLost);
                Log.Warn("Power", "lost channel " + order[i] + " at " + ReservePercent + "%");
            }
        }

        void RestoreDarkChannels()
        {
            var cctv = ServiceHub.Cctv;
            for (int i = 0; i < _darkChannels.Count; i++)
                if (cctv != null) cctv.SetFeedState(_darkChannels[i], CCTV.FeedState.Live);
            _darkChannels.Clear();
        }

        public void Reset()
        {
            RestoreDarkChannels();
            _reserve = Full;
            _drawCarry = 0;
            _nightIndex = 0;
            _breakerResetsUsed = 0;
            _breakerReadyAtGameSecond = 0;
            _wasLow = false;
            FlashlightOn = false;
            CorridorLightsOn = true;
            _lastGameSecond = GameClock.ShiftStartSecond;
        }

        public void LoadFrom(int reserve, bool corridorLightsOn, int breakerResetsUsed)
        {
            Reset();
            _reserve = UnityEngine.Mathf.Clamp(reserve, 0, Full);
            CorridorLightsOn = corridorLightsOn;
            _breakerResetsUsed = UnityEngine.Mathf.Clamp(breakerResetsUsed, 0, BreakerResetsPerNight);
            _lastGameSecond = ServiceHub.Clock != null ? ServiceHub.Clock.GameSecond : GameClock.ShiftStartSecond;
            _wasLow = IsLow;
            RefreshDarkChannels();
        }

        public int BreakerResetsUsed { get { return _breakerResetsUsed; } }
    }
}
