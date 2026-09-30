using System.Collections.Generic;
using NO404.Core;

namespace NO404.Pressure
{
    /// <summary>
    /// How close the night has got to the player (GDD 15.4). Five stages, one direction of
    /// travel per action: doing the job pushes it back down, ignoring the job lets it walk in.
    /// </summary>
    public enum PressureStage
    {
        Calm = 0,
        Near = 1,
        AtTheDoor = 2,
        Inside = 3,
        Confrontation = 4
    }

    /// <summary>
    /// The night's counter-pressure (GDD 15.4).
    ///
    /// Before this existed every system in the game waited for the player: cases sat still,
    /// anomalies were filed at leisure and nothing in the building ever moved first. The shift
    /// was an escape room with a clock that only measured how long the player felt like taking.
    ///
    /// This is the one service that acts without being asked. It collects everything the
    /// caretaker did not deal with - motion nobody reviewed, sightings nobody filed, the wrong
    /// person waved through the front door, a deadline that came and went - turns it into a
    /// single value, and spends that value walking something down the corridor towards the
    /// office.
    ///
    /// Two rules bound it, both inherited:
    /// - GDD 14.4 / CLAUDE.md: it never removes archive-critical evidence, so no amount of
    ///   pressure can close off the truth ending.
    /// - GDD 15.2: it never kills. The top of the ladder costs evidence, performance and ten
    ///   game minutes, then drops back to <see cref="ConfrontationResetsTo"/> and starts again.
    ///
    /// Deliberately free of scene objects so the whole ladder is testable in EditMode.
    /// </summary>
    public sealed class NightPressureService
    {
        public const int Max = 100;

        public const int NearAt = 25;
        public const int AtTheDoorAt = 50;
        public const int InsideAt = 75;
        public const int ConfrontationAt = 100;

        // ---- what raises it ------------------------------------------------

        /// <summary>An authored anomaly (GDD 12.3) whose window closed with nothing filed.</summary>
        public const int UnreportedSighting = 6;
        /// <summary>
        /// A contradictory resident crossing (GDD 12.6) nobody filed.
        ///
        /// Far smaller than an authored anomaly, and capped per night, because there are
        /// dozens of these a night and only a handful of those. Charging both the same is how
        /// a system meant to make the night lean in turns into a system that ends it.
        /// </summary>
        public const int TrafficMissed = 1;
        /// <summary>Ceiling on what missed crossings may add across one whole shift.</summary>
        public const int TrafficMissedCapPerNight = 8;
        /// <summary>Filed under the wrong category, or filed against an empty channel.</summary>
        public const int MisreportedSighting = 4;
        /// <summary>Someone was let in who should not have been. Also spawns an intruder.</summary>
        public const int WrongAdmit = 10;
        /// <summary>A genuine caller was turned away. The building notices that too.</summary>
        public const int RefusedGenuine = 5;
        /// <summary>A case ran past its deadline and the fail-safe had to close it.</summary>
        public const int MissedDeadline = 10;
        /// <summary>Per game minute spent under the low-reserve line (NightPowerService).</summary>
        public const int LowPowerPerMinute = 1;
        /// <summary>
        /// One crossing by a wrongly admitted visitor that nobody filed.
        ///
        /// This replaced a per-minute drip. The drip was invisible, compounded, and could not
        /// be argued with: three intruders on a long night out-weighed every judgement the
        /// player had made all shift put together. A cost per crossing is the same pressure
        /// attached to the thing the player can actually see and name.
        /// </summary>
        public const int IntruderMissed = 4;

        // ---- what lowers it ------------------------------------------------

        public const int CorrectReportRelief = -5;
        /// <summary>Naming one contradictory crossing. Small, because there are dozens.</summary>
        public const int TrafficReportedRelief = -1;
        /// <summary>
        /// Judging a caller correctly (GDD 15.4).
        ///
        /// This was -6 when a night ran four callers. The door roster now runs eight or nine
        /// (GDD 13.4), and at -6 the extra relief alone was worth about twenty points a night:
        /// night 1 on the normal profile stopped reaching 50, so the office-door knock that
        /// GDD 15.6 exists to deliver never fired on the night v1.5 added it for.
        ///
        /// This is the same rule 15.4 already applies to contradictory crossings - a night
        /// makes dozens of those, so each is worth 1 - and for the same reason: when the value
        /// is not scaled to how often the thing happens, the ladder measures how many callers
        /// the night scheduled instead of how well the player judged them.
        /// </summary>
        /// <summary>
        /// Relief per correctly judged caller.
        ///
        /// Was -2, against a roster of eight or nine callers a night. The roster is five now
        /// (GDD 13.4), and a night's work at the door has to be worth the same whether it is
        /// spread over nine strangers or five people - otherwise cutting the volume quietly
        /// made every night harder. Five at -4 is twenty, which is where the old eight-to-ten
        /// at -2 landed and where GDD 25.4 measured the profiles.
        /// </summary>
        public const int CorrectVisitorRelief = -4;
        public const int CaseClosedRelief = -8;
        /// <summary>
        /// Per game HOUR the office door is bolted, not per minute.
        ///
        /// Per minute this was worth roughly minus four hundred and eighty points across a
        /// shift, which meant bolting the door at 22:00 and never opening it again won the
        /// pressure game outright - a hiding place that good is not a trade, it is an exploit.
        /// Twenty an hour is set just above the worst night's drift (16/h): bolting holds the
        /// line and recovers slowly, and never erases a night of neglect.
        /// </summary>
        public const int LockedDoorReliefPerGameHour = -20;
        /// <summary>Looking through the peephole and seeing what is actually there.</summary>
        public const int PeepholeRelief = -10;

        // ---- what the top of the ladder costs -------------------------------

        public const int ConfrontationTimeLossSeconds = 600;   // ten game minutes
        public const int ConfrontationPerformancePenalty = 6;
        public const int ConfrontationResetsTo = 40;

        /// <summary>
        /// Baseline drift, in pressure points per game hour, by night. By night 6 a shift
        /// spent doing nothing at all reaches the top of the ladder once - which is the point.
        ///
        /// Index 0 used to be the teaching night and used to be zero. There is no teaching
        /// night any more (GDD 9.1): the game opens on night 1, in the middle of the blackout,
        /// and a first shift that cannot be got wrong is the exact thing the cold open was
        /// written to delete. The slot is kept only so a dev-console jump to night 0 behaves
        /// like night 1 rather than like a shift where nothing can happen.
        ///
        /// Drift alone on night 1 is 48 across a full shift, deliberately just under
        /// <see cref="AtTheDoorAt"/>: a shift spent doing nothing still does not summon the
        /// knock by the clock alone, but a single missed sighting on top of it does. The cold
        /// open no longer waits for that - it knocks on script, twenty minutes in (GDD 15.6).
        /// </summary>
        static readonly int[] DriftPerGameHourByNight = { 6, 6, 7, 8, 10, 12, 16 };

        readonly List<string> _intruders = new List<string>();

        int _value;
        int _driftCarry;        // game-seconds x points-per-hour, integer only (CLAUDE.md)
        int _boltCarry;         // same, for the bolted-door relief
        int _minuteCarry;       // game seconds towards the next whole minute
        int _lastGameSecond;
        int _nightIndex;
        int _trafficMissedSoFar;

        public int Value { get { return _value; } }
        public PressureStage Stage { get; private set; }
        public int ConfrontationCount { get; private set; }

        /// <summary>0..1 for the HUD. The player never sees the number itself (GDD 16.5).</summary>
        public float Value01 { get { return _value / (float)Max; } }

        /// <summary>Set by OfficeDoorController. Bolted means slow relief and a closed door.</summary>
        public bool OfficeDoorLocked { get; set; }

        /// <summary>People admitted who should not have been and have not been found again.</summary>
        public int IntruderCount { get { return _intruders.Count; } }
        public IReadOnlyList<string> Intruders { get { return _intruders; } }

        /// <summary>
        /// Whether the ladder is switched off for this shift.
        ///
        /// It never is. This existed for night 0, the prologue, which charged nothing for
        /// anything - and that free shift is precisely what the cold open replaced (GDD 9.1).
        /// The property is kept because it is the honest place to say so: the game no longer
        /// has a night on which the building does not answer.
        /// </summary>
        public bool Suppressed { get { return false; } }

        public void BeginNight(int nightIndex)
        {
            _nightIndex = nightIndex;
            _value = 0;
            _driftCarry = 0;
            _boltCarry = 0;
            _minuteCarry = 0;
            _entryCarry = 0;
            _intruders.Clear();
            OfficeDoorLocked = false;
            ConfrontationCount = 0;
            _trafficMissedSoFar = 0;
            _lastGameSecond = ServiceHub.Clock != null ? ServiceHub.Clock.GameSecond : GameClock.ShiftStartSecond;

            var previous = Stage;
            Stage = PressureStage.Calm;
            if (previous != Stage) EventBus.Publish(new PressureStageChangedEvent(previous, Stage));
            EventBus.Publish(new PressureChangedEvent(0, 0, "reason.pressure_night_start"));
        }

        public int DriftPerGameHour
        {
            get
            {
                int night = _nightIndex < 0 ? 0 : _nightIndex;
                if (night >= DriftPerGameHourByNight.Length) night = DriftPerGameHourByNight.Length - 1;
                return DriftPerGameHourByNight[night];
            }
        }

        /// <summary>Called once per frame by GameLoop. Reads the clock; never accumulates float.</summary>
        public void Tick()
        {
            var clock = ServiceHub.Clock;
            if (clock == null) return;

            int now = clock.GameSecond;
            int elapsed = now - _lastGameSecond;
            _lastGameSecond = now;

            // A load or a rewind can move the clock backwards; that is not elapsed time.
            if (elapsed <= 0) return;
            TickSeconds(elapsed);
        }

        /// <summary>The clock-free half of <see cref="Tick"/>, so EditMode can drive it.</summary>
        public void TickSeconds(int elapsedGameSeconds)
        {
            if (elapsedGameSeconds <= 0 || Suppressed) return;

            _driftCarry += elapsedGameSeconds * DriftPerGameHour;
            while (_driftCarry >= 3600)
            {
                _driftCarry -= 3600;
                Add(1, "reason.pressure_drift");
            }

            // Bolting the door buys quiet at the price of every caller left standing outside
            // and every errand that is on the wrong side of it (GDD 15.6).
            if (OfficeDoorLocked)
            {
                _boltCarry += elapsedGameSeconds * -LockedDoorReliefPerGameHour;
                while (_boltCarry >= 3600)
                {
                    _boltCarry -= 3600;
                    Add(-1, "reason.pressure_door_locked");
                }
            }
            else _boltCarry = 0;

            _minuteCarry += elapsedGameSeconds;
            while (_minuteCarry >= 60)
            {
                _minuteCarry -= 60;
                TickMinute();
            }
        }

        void TickMinute()
        {
            var power = ServiceHub.Power;
            if (power != null && power.IsLow) Add(LowPowerPerMinute, "reason.pressure_low_power");

            // Spec 16 T05: an entrance left on AUTO does not fail loudly. It just keeps
            // letting people in, and the night keeps getting heavier for a reason the
            // caretaker decided two nights ago and has stopped thinking about.
            var state = ServiceHub.State;
            if (state == null) return;

            int leak = state.GetStat(StatIds.UnauthorizedEntryRisk);
            if (leak <= 0) return;

            _entryCarry += leak;
            while (_entryCarry >= MinutesPerUnauthorizedEntry)
            {
                _entryCarry -= MinutesPerUnauthorizedEntry;
                Add(1, "reason.pressure_open_entrance");
            }
        }

        /// <summary>
        /// Minutes of an open entrance that add up to one point of pressure, at risk 1.
        ///
        /// Twenty is deliberately slow. The door being open is a background fact rather than
        /// an event, and it should cost about four points across a whole shift - noticeable
        /// on a night that was already going badly, and never the thing that decided it.
        /// </summary>
        const int MinutesPerUnauthorizedEntry = 20;
        int _entryCarry;

        /// <summary>
        /// The single entry point. Positive raises, negative relieves. Gains are scaled by
        /// difficulty; relief never is, so an easier setting is never also a slower recovery.
        /// </summary>
        public void Add(int delta, string reasonKey)
        {
            if (delta == 0) return;
            if (Suppressed && delta > 0) return;

            if (delta > 0)
            {
                float scaled = delta * DifficultyProfile.PressureGainFactor;
                delta = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(scaled));
            }

            int previous = _value;
            _value = UnityEngine.Mathf.Clamp(_value + delta, 0, Max);
            if (_value == previous) return;

            EventBus.Publish(new PressureChangedEvent(previous, _value, reasonKey));
            RefreshStage();
        }

        void RefreshStage()
        {
            var next = StageFor(_value);
            if (next == Stage) return;

            var previous = Stage;
            Stage = next;
            EventBus.Publish(new PressureStageChangedEvent(previous, next));
            Log.Info("Pressure", previous + " -> " + next + " at " + _value);

            if (next == PressureStage.Confrontation) Confront();
        }

        public static PressureStage StageFor(int value)
        {
            if (value >= ConfrontationAt) return PressureStage.Confrontation;
            if (value >= InsideAt) return PressureStage.Inside;
            if (value >= AtTheDoorAt) return PressureStage.AtTheDoor;
            if (value >= NearAt) return PressureStage.Near;
            return PressureStage.Calm;
        }

        // ---- hooks, called by the systems that already know ------------------

        public void NoteSightingExpired() { Add(UnreportedSighting, "reason.pressure_unreported"); }

        /// <summary>
        /// One contradictory crossing nobody filed (GDD 12.6). Capped across the shift: a late
        /// night schedules nearly fifty of these, and an uncapped drip would drown out every
        /// judgement the player actually made.
        /// </summary>
        public void NoteTrafficMissed()
        {
            if (_trafficMissedSoFar >= TrafficMissedCapPerNight) return;
            _trafficMissedSoFar += TrafficMissed;
            Add(TrafficMissed, "reason.pressure_traffic_missed");
        }

        /// <summary>One contradictory crossing named on camera.</summary>
        public void NoteTrafficReported() { Add(TrafficReportedRelief, "reason.pressure_traffic_filed"); }

        /// <summary>An intruder walked past a camera and nobody filed it. Not capped.</summary>
        public void NoteIntruderMissed() { Add(IntruderMissed, "reason.pressure_intruder"); }

        /// <summary>Pressure charged by missed crossings so far tonight. Ceiling is the cap.</summary>
        public int TrafficMissedSoFar { get { return _trafficMissedSoFar; } }
        public void NoteReportMiscategorised() { Add(MisreportedSighting, "reason.pressure_misreported"); }
        public void NoteReportCorrect() { Add(CorrectReportRelief, "reason.pressure_report_filed"); }
        public void NoteVisitorCorrect() { Add(CorrectVisitorRelief, "reason.pressure_door_held"); }
        public void NoteGenuineRefused() { Add(RefusedGenuine, "reason.pressure_refused_genuine"); }
        public void NoteCaseClosedWell() { Add(CaseClosedRelief, "reason.pressure_case_closed"); }
        public void NoteDeadlineMissed() { Add(MissedDeadline, "reason.pressure_deadline_missed"); }
        public void NotePeephole() { Add(PeepholeRelief, "reason.pressure_peephole"); }

        /// <summary>
        /// A visitor who should have been turned away is now inside the building. This is the
        /// thing the judgement screen never used to do: the wrong answer stops being a score
        /// and becomes a body that keeps raising the pressure until it is found again.
        /// </summary>
        public void NoteWrongAdmit(string visitorId)
        {
            if (!string.IsNullOrEmpty(visitorId) && !_intruders.Contains(visitorId))
                _intruders.Add(visitorId);

            Add(WrongAdmit, "reason.pressure_wrong_admit");
            EventBus.Publish(new NotificationEvent("ui.notify.someone_inside", NotificationSeverity.Warning));
        }

        /// <summary>The player found one on camera and filed it. Removes its per-minute drip.</summary>
        public bool ClearIntruder(string visitorId)
        {
            if (string.IsNullOrEmpty(visitorId) || !_intruders.Remove(visitorId)) return false;
            Add(CorrectReportRelief, "reason.pressure_intruder_found");
            return true;
        }

        /// <summary>Clears the oldest intruder. Used when a report lands on the right channel.</summary>
        public bool ClearOldestIntruder()
        {
            if (_intruders.Count == 0) return false;
            return ClearIntruder(_intruders[0]);
        }

        // ---- the top of the ladder ------------------------------------------

        /// <summary>
        /// GDD 15.2: this costs, it does not kill. One piece of non-critical evidence, six
        /// performance and ten game minutes, and then the corridor is empty again.
        /// </summary>
        void Confront()
        {
            ConfrontationCount++;

            string taken = TakeOneNonCriticalEvidence();

            if (ServiceHub.State != null)
                ServiceHub.State.AddStat(StatIds.Performance, -ConfrontationPerformancePenalty,
                                         "reason.pressure_confronted");

            if (ServiceHub.Clock != null)
                ServiceHub.Clock.AdvanceSeconds(TimeLossSeconds);

            _intruders.Clear();
            _value = ConfrontationResetsTo;
            _driftCarry = 0;

            var previous = Stage;
            Stage = StageFor(_value);
            EventBus.Publish(new PressureChangedEvent(Max, _value, "reason.pressure_confronted"));
            if (previous != Stage) EventBus.Publish(new PressureStageChangedEvent(previous, Stage));

            EventBus.Publish(new NotificationEvent("ui.notify.pressure_confronted", NotificationSeverity.Urgent));

            if (ServiceHub.Audio != null) ServiceHub.Audio.PlayCue(AudioCue.OfficeDoorForced);
            if (ServiceHub.Analytics != null)
                ServiceHub.Analytics.Track("pressure_confrontation", taken == null ? string.Empty : taken, _nightIndex);

            Log.Warn("Pressure", "confrontation on night " + _nightIndex + ", took " + (taken == null ? "nothing" : taken));
        }

        /// <summary>GDD 24.2: the accessibility setting halves what a confrontation costs.</summary>
        public static int TimeLossSeconds
        {
            get
            {
                return DifficultyProfile.SoftenedConsequences
                    ? ConfrontationTimeLossSeconds / 2
                    : ConfrontationTimeLossSeconds;
            }
        }

        static string TakeOneNonCriticalEvidence()
        {
            if (ServiceHub.Evidence == null) return null;

            foreach (var pair in ServiceHub.Evidence.Owned)
            {
                var definition = pair.Value != null ? pair.Value.Definition : null;
                if (definition == null || definition.archiveCritical) continue;

                string id = pair.Key;
                ServiceHub.Evidence.Remove(id);
                return id;
            }

            return null;
        }

        public void Reset()
        {
            _nightIndex = 0;
            _value = 0;
            _driftCarry = 0;
            _boltCarry = 0;
            _minuteCarry = 0;
            _lastGameSecond = GameClock.ShiftStartSecond;
            _intruders.Clear();
            OfficeDoorLocked = false;
            ConfrontationCount = 0;
            _trafficMissedSoFar = 0;
            Stage = PressureStage.Calm;
        }

        public void LoadFrom(int value, int nightIndex, IEnumerable<string> intruders)
        {
            Reset();
            _nightIndex = nightIndex;
            _value = UnityEngine.Mathf.Clamp(value, 0, Max);
            Stage = StageFor(_value);
            _lastGameSecond = ServiceHub.Clock != null ? ServiceHub.Clock.GameSecond : GameClock.ShiftStartSecond;

            if (intruders == null) return;
            foreach (var id in intruders)
                if (!string.IsNullOrEmpty(id) && !_intruders.Contains(id)) _intruders.Add(id);
        }
    }
}
