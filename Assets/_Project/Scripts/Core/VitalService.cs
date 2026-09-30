using System;

namespace NO404.Core
{
    /// <summary>Where SAN has got to, and what the building is allowed to do about it (v5.0 7.2).</summary>
    public enum SanBand
    {
        /// <summary>75-100. Nothing but the occasional trick of the light.</summary>
        Stable = 0,
        /// <summary>50-74. A knock that was not there, movement at the edge of vision.</summary>
        Uneasy = 1,
        /// <summary>30-49. Radio and telephone leave a tail; the UI slips for a frame.</summary>
        Shaken = 2,
        /// <summary>1-29. Being alone costs more, and getting lost becomes likely.</summary>
        Critical = 3,
        /// <summary>0. The night is over and no ending is reachable.</summary>
        Breakdown = 4
    }

    /// <summary>Why the campaign stopped short of an ending (v5.0 8.1).</summary>
    public enum EndingGate
    {
        Eligible = 0,
        PhysicalCollapse = 1,
        MentalBreakdown = 2
    }

    /// <summary>
    /// The caretaker's body and nerve (v5.0 6, 7, 8.1).
    ///
    /// HP and SAN are not two more numbers on the dashboard. They are the campaign's only
    /// spendable resources and they decide the ending outright: below thirty of either at the
    /// final reveal there is no A/B/C/D at all, only a failure result. Everything about the
    /// way they move follows from that.
    ///
    /// So neither of them is allowed to fall for a reason the player could not have seen
    /// coming. v5.0 6.1 is explicit that a jump scare costs nothing and that a single ordinary
    /// mistake may not take thirty points, and 7.1 says the same about merely having witnessed
    /// something: what costs SAN is ignoring a rule twice, walking a corridor that has already
    /// been shown to be wrong, or leaving a memory intrusion alone. <see cref="Damage"/> and
    /// <see cref="Strain"/> therefore both take a reason key, and the caller is expected to
    /// have earned it.
    ///
    /// Recovery is deliberately scarce and deliberately guaranteed. Four first-aid kits for a
    /// whole campaign, two grounding minutes a night, and a fixed amount back at every shift's
    /// end - so a bad night is survivable and a run of bad nights is not.
    /// </summary>
    public sealed class VitalService
    {
        public const int Max = 100;

        // ---- the gate (v5.0 8.1) -------------------------------------------
        public const int EndingGateMinimum = 30;

        // ---- first aid (v5.0 6.3) ------------------------------------------
        public const int FirstAidKitsPerCampaign = 4;
        public const int FirstAidHeal = 18;

        // ---- grounding (v5.0 7.3) ------------------------------------------
        public const int GroundingPerNight = 2;
        public const int GroundingCalm = 8;
        /// <summary>Real seconds the director must keep quiet for grounding to pay out.</summary>
        public const float GroundingSeconds = 35f;

        /// <summary>Reading an anomaly correctly off invariant evidence (v5.0 7.3).</summary>
        public const int InvariantReadCalm = 3;
        /// <summary>Two ordinary jobs finished cleanly in a row, capped per night.</summary>
        public const int RoutineCalm = 2;
        public const int RoutineCalmPerNight = 4;

        // ---- end of shift (v5.0 6.3 / 7.3) ---------------------------------
        public const int NightEndHeal = 5;
        public const int NightEndHealWhenHurt = 8;
        public const int HurtBelow = 40;
        public const int NightEndCalm = 7;
        public const int NightEndCalmWhenShaken = 10;
        public const int ShakenBelow = 35;

        public int Hp { get; private set; }
        public int San { get; private set; }

        public int FirstAidRemaining { get; private set; }
        public int GroundingRemaining { get; private set; }

        int _routineCalmThisNight;
        int _cleanRoutineStreak;

        /// <summary>Raised whenever either value moves, with the reason key that moved it.</summary>
        public event Action<int, int, string> OnHpChanged;
        public event Action<int, int, string> OnSanChanged;

        /// <summary>Raised once when HP or SAN reaches zero and the night is lost.</summary>
        public event Action<bool> OnCollapse;

        public VitalService() { ResetToNewGame(); }

        public void ResetToNewGame()
        {
            Hp = Max;
            San = Max;
            FirstAidRemaining = FirstAidKitsPerCampaign;
            GroundingRemaining = GroundingPerNight;
            _routineCalmThisNight = 0;
            _cleanRoutineStreak = 0;
        }

        public void BeginNight(int nightIndex)
        {
            GroundingRemaining = GroundingPerNight;
            _routineCalmThisNight = 0;
            _cleanRoutineStreak = 0;
        }

        // -----------------------------------------------------------------
        // losing it
        // -----------------------------------------------------------------

        /// <summary>
        /// Physical harm. The reason key is not decoration - it is the evidence that this was
        /// a risk the caretaker took rather than a number the game decided to subtract.
        /// </summary>
        public void Damage(int amount, string reasonKey)
        {
            if (amount <= 0) return;
            SetHp(Hp - amount, reasonKey);
        }

        /// <summary>What being in this building does to somebody who keeps ignoring it.</summary>
        public void Strain(int amount, string reasonKey)
        {
            if (amount <= 0) return;
            SetSan(San - amount, reasonKey);
        }

        // -----------------------------------------------------------------
        // getting it back
        // -----------------------------------------------------------------

        /// <summary>
        /// The office first-aid kit. Four for the whole campaign and none of them usable in
        /// the middle of something (v5.0 6.3), which is what stops it being a button that
        /// makes the health bar irrelevant.
        /// </summary>
        public bool UseFirstAid(bool duringCriticalEvent)
        {
            if (duringCriticalEvent || FirstAidRemaining <= 0 || Hp >= Max) return false;

            FirstAidRemaining--;
            SetHp(Hp + FirstAidHeal, "reason.first_aid");
            return true;
        }

        /// <summary>
        /// Thirty-five seconds in the one room that is still an office (v5.0 7.3).
        ///
        /// The caller is responsible for having held that time; this only pays for it. The
        /// director's job is to keep the corridor quiet while it runs, because grounding that
        /// gets interrupted is worse than no grounding at all.
        /// </summary>
        public bool Ground()
        {
            if (GroundingRemaining <= 0 || San >= Max) return false;

            GroundingRemaining--;
            SetSan(San + GroundingCalm, "reason.grounding");
            return true;
        }

        /// <summary>
        /// Treatment that did not come out of the first-aid box.
        ///
        /// Kept separate from <see cref="UseFirstAid"/> because that one is a scarce resource
        /// with a count on it, and this one is a quest outcome - being helped, resting, a job
        /// that went right. Neither can take the caretaker past full.
        /// </summary>
        public void Heal(int amount, string reasonKey)
        {
            if (amount <= 0) return;
            SetHp(Hp + amount, reasonKey);
        }

        /// <summary>Nerve returned by something other than grounding (v5.0 7.3).</summary>
        public void Calm(int amount, string reasonKey)
        {
            if (amount <= 0) return;
            SetSan(San + amount, reasonKey);
        }

        /// <summary>Naming a thing correctly off evidence that cannot lie (v5.0 7.3).</summary>
        public void NoteInvariantRead() { SetSan(San + InvariantReadCalm, "reason.read_correctly"); }

        /// <summary>
        /// An ordinary job, finished without incident.
        ///
        /// Two in a row are worth something and the night's total is capped, so the way to
        /// steady yourself is to do the work rather than to farm the smallest task on the list.
        /// </summary>
        public void NoteRoutineResolved(bool clean)
        {
            if (!clean) { _cleanRoutineStreak = 0; return; }

            _cleanRoutineStreak++;
            if (_cleanRoutineStreak < 2) return;

            _cleanRoutineStreak = 0;
            if (_routineCalmThisNight >= RoutineCalmPerNight) return;

            int calm = Math.Min(RoutineCalm, RoutineCalmPerNight - _routineCalmThisNight);
            _routineCalmThisNight += calm;
            SetSan(San + calm, "reason.routine_steady");
        }

        /// <summary>What the shift itself gives back, whatever happened during it.</summary>
        public void EndNight()
        {
            SetHp(Hp + (Hp < HurtBelow ? NightEndHealWhenHurt : NightEndHeal), "reason.shift_over");
            SetSan(San + (San < ShakenBelow ? NightEndCalmWhenShaken : NightEndCalm), "reason.shift_over");
        }

        // -----------------------------------------------------------------
        // what the numbers mean
        // -----------------------------------------------------------------

        public SanBand Band
        {
            get
            {
                if (San <= 0) return SanBand.Breakdown;
                if (San < 30) return SanBand.Critical;
                if (San < 50) return SanBand.Shaken;
                if (San < 75) return SanBand.Uneasy;
                return SanBand.Stable;
            }
        }

        public bool Collapsed { get { return Hp <= 0 || San <= 0; } }

        /// <summary>
        /// Whether the final reveal may be entered at all (v5.0 8.1).
        ///
        /// Checked before the grade rather than folded into it: no ending is not a fifth
        /// grade, it is having pushed the campaign to its last scene in a state that cannot
        /// carry it.
        /// </summary>
        public EndingGate Gate
        {
            get
            {
                if (Hp < EndingGateMinimum) return EndingGate.PhysicalCollapse;
                if (San < EndingGateMinimum) return EndingGate.MentalBreakdown;
                return EndingGate.Eligible;
            }
        }

        /// <summary>The score v5.0 8.2 grades on. HP counts for slightly more than SAN.</summary>
        public float ConditionScore { get { return Hp * 0.55f + San * 0.45f; } }

        // -----------------------------------------------------------------
        // save
        // -----------------------------------------------------------------

        public void LoadFrom(int hp, int san, int firstAidRemaining, int groundingRemaining)
        {
            Hp = Clamp(hp);
            San = Clamp(san);
            FirstAidRemaining = Math.Max(0, Math.Min(FirstAidKitsPerCampaign, firstAidRemaining));
            GroundingRemaining = Math.Max(0, Math.Min(GroundingPerNight, groundingRemaining));
            _routineCalmThisNight = 0;
            _cleanRoutineStreak = 0;
        }

        /// <summary>Mirrored onto a joining caretaker (v3.0 46.1). Silent: no collapse events.</summary>
        public void ApplyMirror(int hp, int san, int firstAidRemaining, int groundingRemaining)
        {
            LoadFrom(hp, san, firstAidRemaining, groundingRemaining);
        }

        // -----------------------------------------------------------------

        static int Clamp(int value) { return value < 0 ? 0 : (value > Max ? Max : value); }

        void SetHp(int next, string reasonKey)
        {
            int clamped = Clamp(next);
            if (clamped == Hp) return;

            int previous = Hp;
            Hp = clamped;

            var cb = OnHpChanged;
            if (cb != null) cb(previous, Hp, reasonKey);
            EventBus.Publish(new VitalChangedEvent(true, previous, Hp, reasonKey));

            Log.Info("Vital", "HP " + previous + " -> " + Hp + " (" + reasonKey + ")");
            if (Hp <= 0) RaiseCollapse(true);
        }

        void SetSan(int next, string reasonKey)
        {
            int clamped = Clamp(next);
            if (clamped == San) return;

            int previous = San;
            San = clamped;

            var cb = OnSanChanged;
            if (cb != null) cb(previous, San, reasonKey);
            EventBus.Publish(new VitalChangedEvent(false, previous, San, reasonKey));

            Log.Info("Vital", "SAN " + previous + " -> " + San + " (" + reasonKey + ")");
            if (San <= 0) RaiseCollapse(false);
        }

        void RaiseCollapse(bool physical)
        {
            var cb = OnCollapse;
            if (cb != null) cb(physical);
            EventBus.Publish(new VitalCollapseEvent(physical));
        }
    }
}
