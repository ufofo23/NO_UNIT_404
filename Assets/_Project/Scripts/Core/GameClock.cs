using System;

namespace NO404.Core
{
    public interface IGameClock
    {
        int GameSecond { get; }
        int Hour { get; }
        int Minute { get; }
        float TimeScale { get; set; }
        bool Paused { get; set; }
        event Action<int> OnMinuteChanged;
    }

    /// <summary>
    /// Integer-second game clock (GDD 20.16). Float accumulation is confined to a single
    /// carry variable so long shifts cannot drift.
    ///
    /// A shift runs 22:00 -> 06:00 and the default rate is 1 game minute per 10 real seconds
    /// (GDD 6.2), i.e. 6 game seconds per real second.
    ///
    /// It used to be four real seconds to the game minute, which put a whole shift in about
    /// half an hour. That is a lot of building to cross in the time it takes the clock to move
    /// a quarter of an hour: a caretaker who walked to the fifth floor and back had spent most
    /// of an in-game hour on the stairs, so the night read as a countdown rather than as a job.
    /// At ten seconds the walk costs what a walk should and the shift is a shift.
    ///
    /// Nothing else in the game hard-codes the rate. Anything measured in real seconds -
    /// the locker door, the borrowed tool, an anomaly holding the lens - multiplies by the
    /// constant below, so those keep the durations they were designed with.
    /// </summary>
    public sealed class GameClock : IGameClock
    {
        public const int ShiftStartSecond = 22 * 3600;      // 22:00
        public const int ShiftEndSecond = 30 * 3600;        // 06:00 next day
        public const float GameSecondsPerRealSecond = 6f;   // 1 game minute = 10 real seconds

        /// <summary>Game seconds in a given number of real seconds, at the default rate.</summary>
        public static int RealSeconds(float seconds)
        {
            return (int)(seconds * GameSecondsPerRealSecond);
        }

        /// <summary>Game seconds in a given number of real minutes, at the default rate.</summary>
        public static int RealMinutes(float minutes)
        {
            return RealSeconds(minutes * 60f);
        }

        float _carry;
        int _gameSecond;
        int _lastMinuteStamp = -1;

        public event Action<int> OnMinuteChanged;

        public GameClock(int startSecond = ShiftStartSecond)
        {
            _gameSecond = startSecond;
            _lastMinuteStamp = startSecond / 60;
        }

        public int GameSecond { get { return _gameSecond; } }
        public int Hour { get { return (_gameSecond / 3600) % 24; } }
        public int Minute { get { return (_gameSecond / 60) % 60; } }
        public int Second { get { return _gameSecond % 60; } }
        public float TimeScale { get; set; } = 1f;
        public bool Paused { get; set; }

        /// <summary>True once the shift has reached 06:00.</summary>
        public bool ShiftOver { get { return _gameSecond >= ShiftEndSecond; } }

        /// <summary>0..1 progress through the current shift, useful for the HUD bar.</summary>
        public float ShiftProgress
        {
            get
            {
                float span = ShiftEndSecond - ShiftStartSecond;
                float done = _gameSecond - ShiftStartSecond;
                return done <= 0f ? 0f : (done >= span ? 1f : done / span);
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (Paused || TimeScale <= 0f) return;

            _carry += unscaledDeltaTime * GameSecondsPerRealSecond * TimeScale;
            if (_carry < 1f) return;

            int whole = (int)_carry;
            _carry -= whole;
            AdvanceSeconds(whole);
        }

        public void AdvanceSeconds(int seconds)
        {
            if (seconds <= 0) return;
            _gameSecond += seconds;
            RaiseMinuteIfNeeded();
        }

        public void SetTime(int hour, int minute, int second = 0)
        {
            int normalizedHour = hour;
            // 00:00-05:59 belongs to the second half of the night, so it is stored as 24-29h.
            if (normalizedHour < 12) normalizedHour += 24;
            _gameSecond = normalizedHour * 3600 + minute * 60 + second;
            _carry = 0f;
            RaiseMinuteIfNeeded(force: true);
        }

        public void SetGameSecond(int gameSecond)
        {
            _gameSecond = gameSecond;
            _carry = 0f;
            RaiseMinuteIfNeeded(force: true);
        }

        /// <summary>
        /// Take the host's clock (v3.0 46.1).
        ///
        /// Same as <see cref="SetGameSecond"/> except that it does not force the minute event.
        /// A client is handed a new second thirty times a second, and forcing would fire the
        /// minute tick on every one of them - which today is harmless only because nothing
        /// subscribes yet, and would become a very confusing bug the day something does.
        /// Jumping the clock is still a minute change when the minute actually changes.
        /// </summary>
        public void MirrorTo(int gameSecond)
        {
            if (_gameSecond == gameSecond) return;

            _gameSecond = gameSecond;
            _carry = 0f;
            RaiseMinuteIfNeeded();
        }

        void RaiseMinuteIfNeeded(bool force = false)
        {
            int stamp = _gameSecond / 60;
            if (!force && stamp == _lastMinuteStamp) return;
            _lastMinuteStamp = stamp;

            var cb = OnMinuteChanged;
            if (cb != null) cb(_gameSecond);
            EventBus.Publish(new GameMinuteChangedEvent(_gameSecond, Hour, Minute));
        }

        public string ToClockString()
        {
            return Hour.ToString("00") + ":" + Minute.ToString("00");
        }

        public static string FormatSecond(int gameSecond)
        {
            int h = (gameSecond / 3600) % 24;
            int m = (gameSecond / 60) % 60;
            int s = gameSecond % 60;
            return h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
        }

        /// <summary>True when <paramref name="gameSecond"/> falls inside an inclusive window.</summary>
        public static bool InWindow(int gameSecond, int startSecond, int endSecond)
        {
            if (endSecond <= startSecond) return gameSecond >= startSecond;
            return gameSecond >= startSecond && gameSecond <= endSecond;
        }
    }
}
