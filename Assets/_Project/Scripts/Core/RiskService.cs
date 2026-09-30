using NO404.Gameplay;

namespace NO404.Core
{
    /// <summary>
    /// What a floor's accumulated risk does to it (spec 0.10.3).
    ///
    /// The player never sees this as a number. It is read by dressing code - lighting, audio,
    /// which doors work - so that a floor the caretaker mishandled is recognisable by standing
    /// in it.
    /// </summary>
    public enum FloorRiskTier
    {
        /// <summary>0: normal.</summary>
        Clear = 0,
        /// <summary>1: sound and lighting change only.</summary>
        Unsettled = 1,
        /// <summary>2: false interphone calls, brief afterimages, objects that have moved.</summary>
        Deceptive = 2,
        /// <summary>3: one movement block, one repeated stair flight, a false door.</summary>
        Obstructive = 3,
        /// <summary>4: cases gain a step; one safe route is closed.</summary>
        Hostile = 4,
        /// <summary>5: a serious follow-on event; an NPC or a piece of evidence can be lost.</summary>
        Severe = 5
    }

    /// <summary>Bands of DistortionExposure (spec 0.10.4).</summary>
    public enum DistortionBand
    {
        /// <summary>0-24: normal.</summary>
        None = 0,
        /// <summary>25-49: a monitor glyph deforms for one frame; distant footsteps.</summary>
        Faint = 1,
        /// <summary>50-74: false task cards, wrong floor readouts, stairs repeat more often.</summary>
        Marked = 2,
        /// <summary>75-99: real and false clues become hard to tell apart.</summary>
        Severe = 3,
        /// <summary>100: not a game over - an EmergencyCorrection objective (spec 0.10.4).</summary>
        Critical = 4
    }

    /// <summary>
    /// The v2.1 risk model (spec 0.10): per-floor risk, distortion exposure, manual
    /// violations, and the two debts the anomalous tools charge.
    ///
    /// This owns no storage. Every counter is a <see cref="GameStateService"/> stat, so it
    /// clamps, saves, migrates and publishes StatChangedEvent exactly like Performance does.
    /// What lives here is the meaning: the bands, the tiers, and the rule that hitting 100
    /// exposure opens a correction procedure instead of killing the player.
    ///
    /// Spec 0.10.5 is a rule about *this* class as much as about content: nothing here can
    /// end a run. Wrongness accumulates into the world's behaviour and is always recoverable.
    /// </summary>
    public sealed class RiskService
    {
        readonly GameStateService _state;

        public RiskService(GameStateService state)
        {
            _state = state;
        }

        // ---- floor risk (spec 0.10.3) ----------------------------------------

        public int FloorRisk(string floorId)
        {
            return string.IsNullOrEmpty(floorId) ? 0 : _state.GetStat(StatIds.FloorRisk(floorId));
        }

        public FloorRiskTier TierOf(string floorId)
        {
            return (FloorRiskTier)FloorRisk(floorId);
        }

        /// <summary>
        /// Raise or lower a floor's risk. Silently ignores ids that are not floors, so an
        /// anomaly naming the thirteenth floor cannot create a risk counter for a storey the
        /// building does not have.
        /// </summary>
        public void AddFloorRisk(string floorId, int delta, string reason = "risk")
        {
            if (delta == 0) return;
            if (!FloorPlan.Exists(floorId))
            {
                Log.Error("Risk", "ignored FloorRisk change for non-floor '" + floorId + "'");
                return;
            }

            string statId = StatIds.FloorRisk(floorId);
            int previous = _state.GetStat(statId);
            _state.AddStat(statId, delta, reason);
            int current = _state.GetStat(statId);

            if (previous != current) EventBus.Publish(new FloorRiskChangedEvent(floorId, previous, current));
        }

        /// <summary>Risk on the floor a zone belongs to. Zero for the lift car and the shaft.</summary>
        public int RiskOfZone(string zoneId)
        {
            return FloorRisk(FloorPlan.FloorOfZone(zoneId));
        }

        /// <summary>
        /// True when this floor is bad enough to take a safe route away (spec 0.10.3 tier 4).
        /// Content asks this rather than comparing to a literal, so re-tuning the ladder is
        /// one edit here.
        /// </summary>
        public bool BlocksSafeRoute(string floorId) { return FloorRisk(floorId) >= 4; }

        /// <summary>Tier 3: one movement obstruction is due on this floor.</summary>
        public bool Obstructs(string floorId) { return FloorRisk(floorId) >= 3; }

        // ---- distortion exposure (spec 0.10.4) --------------------------------

        public int Exposure { get { return _state.GetStat(StatIds.DistortionExposure); } }

        public DistortionBand Band { get { return BandOf(Exposure); } }

        public static DistortionBand BandOf(int exposure)
        {
            if (exposure >= 100) return DistortionBand.Critical;
            if (exposure >= 75) return DistortionBand.Severe;
            if (exposure >= 50) return DistortionBand.Marked;
            if (exposure >= 25) return DistortionBand.Faint;
            return DistortionBand.None;
        }

        public void AddExposure(int delta, string reason = "distortion")
        {
            if (delta == 0) return;

            var previousBand = Band;
            _state.AddStat(StatIds.DistortionExposure, delta, reason);
            var currentBand = Band;

            if (previousBand == currentBand) return;

            EventBus.Publish(new DistortionBandChangedEvent((int)previousBand, (int)currentBand, Exposure));

            // Spec 0.10.4: 100 does not end the run. It adds a job.
            if (currentBand == DistortionBand.Critical)
            {
                _state.SetFlag(FlagIds.EmergencyCorrectionDue, true);
                EventBus.Publish(new NotificationEvent(
                    "notify.risk.emergency_correction", NotificationSeverity.Urgent));
            }
        }

        /// <summary>
        /// The correction procedure was carried out at the office desk. Clears the outstanding
        /// job and drops exposure back to the top of the Severe band, so the player keeps the
        /// consequences of the night without being pinned at Critical forever.
        /// </summary>
        public void ApplyEmergencyCorrection()
        {
            if (!_state.GetFlag(FlagIds.EmergencyCorrectionDue)) return;

            _state.SetFlag(FlagIds.EmergencyCorrectionDue, false);
            _state.SetStat(StatIds.DistortionExposure, 60, "reason.emergency_correction");
            EventBus.Publish(new DistortionBandChangedEvent(
                (int)DistortionBand.Critical, (int)DistortionBand.Marked, Exposure));
            Log.Info("Risk", "emergency correction applied");
        }

        public bool EmergencyCorrectionDue { get { return _state.GetFlag(FlagIds.EmergencyCorrectionDue); } }

        /// <summary>
        /// Whether false clues may be staged right now (spec 0.10.4, 50+).
        ///
        /// The companion rule matters more than this one: staging is allowed to add noise, and
        /// is never allowed to change which answer is correct (spec 32).
        /// </summary>
        public bool AllowsFalseClues { get { return Band >= DistortionBand.Marked; } }

        // ---- manual violations (spec 0.10.2 B) --------------------------------

        public int ViolationCount { get { return _state.GetStat(StatIds.ManualViolationCount); } }

        /// <summary>
        /// Record a breach of a rule the player had been given, and apply its standard cost.
        ///
        /// Only call this for a rule the player could have known - spec 0.9.2 forbids
        /// punishing an answer nothing in the game had told them. An event's first surprise is
        /// a wrong answer, not a violation.
        /// </summary>
        public void RecordViolation(string eventId, string ruleKey, int exposureDelta = 15,
                                    string floorId = null, int floorRiskDelta = 1)
        {
            _state.AddStat(StatIds.ManualViolationCount, 1, "reason.manual_violation");
            AddExposure(exposureDelta, "reason.manual_violation");
            if (!string.IsNullOrEmpty(floorId)) AddFloorRisk(floorId, floorRiskDelta, "reason.manual_violation");

            EventBus.Publish(new ManualViolationEvent(eventId, ruleKey, ViolationCount));
            Log.Info("Risk", "manual violation " + eventId + "/" + ruleKey +
                             " (total " + ViolationCount + ")");
        }

        // ---- tool debts (spec 23) ---------------------------------------------

        public int MemoryDebt { get { return _state.GetStat(StatIds.MemoryDebt); } }
        public int ToolDebt { get { return _state.GetStat(StatIds.ToolDebt); } }

        public void AddMemoryDebt(int delta, string reason = "reason.memory_debt")
        {
            _state.AddStat(StatIds.MemoryDebt, delta, reason);
        }

        public void AddToolDebt(int delta, string reason = "reason.tool_debt")
        {
            _state.AddStat(StatIds.ToolDebt, delta, reason);
        }

        /// <summary>
        /// Spec 31 C14: the debts make the last night harder to survive and never decide the
        /// ending. Callers that pick an ending must not consult them; callers that stage the
        /// escape should.
        /// </summary>
        public int EscapeDifficultyModifier
        {
            get { return MemoryDebt + ToolDebt + FloorRisk(FloorPlan.F4) + FloorRisk(FloorPlan.F1); }
        }

        // ---- night boundary ----------------------------------------------------

        /// <summary>
        /// Spec 0.10: exposure is a night's accumulated strain, not a permanent scar. Risk is
        /// the opposite - a floor stays mishandled until something in the world fixes it - so
        /// only exposure is relieved between shifts, and only partly.
        /// </summary>
        public void OnNightEnded()
        {
            int exposure = Exposure;
            if (exposure <= 0) return;

            int recovered = exposure >= 50 ? 20 : 10;
            var previousBand = Band;
            _state.AddStat(StatIds.DistortionExposure, -recovered, "reason.night_rest");
            if (Band != previousBand)
                EventBus.Publish(new DistortionBandChangedEvent((int)previousBand, (int)Band, Exposure));
        }
    }
}
