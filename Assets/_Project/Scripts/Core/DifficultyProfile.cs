namespace NO404.Core
{
    public enum Difficulty { Story = 0, Standard = 1, NightSupervisor = 2 }

    /// <summary>None / delayed / always (GDD 16.16 accessibility, 24.3).</summary>
    public enum HintMode { Off = 0, Delayed = 1, Always = 2 }

    /// <summary>
    /// Turns the difficulty option into the concrete multipliers GDD 24.2 specifies, so no
    /// system has to know what "이야기" means - it just asks for the number it needs.
    /// </summary>
    public static class DifficultyProfile
    {
        public static Difficulty Current
        {
            get
            {
                var settings = ServiceHub.Settings;
                return settings == null ? Difficulty.Standard : (Difficulty)settings.Current.difficulty;
            }
        }

        /// <summary>Fraction of the player's sprint a pursuer may reach (GDD 15.2: 85-95%).</summary>
        public static float ChaseSpeedFactor
        {
            get
            {
                if (ServiceHub.Settings != null && ServiceHub.Settings.Current.easierChase) return 0.75f;

                switch (Current)
                {
                    case Difficulty.Story: return 0.75f;
                    case Difficulty.NightSupervisor: return 0.95f;
                    default: return 0.9f;
                }
            }
        }

        /// <summary>Scales how long a routine task has before its fail-safe fires.</summary>
        public static float DeadlineFactor
        {
            get
            {
                switch (Current)
                {
                    case Difficulty.Story: return 1.5f;
                    case Difficulty.NightSupervisor: return 0.8f;
                    default: return 1f;
                }
            }
        }

        /// <summary>Scales the 3/6/9 minute hint ladder from GDD 24.3.</summary>
        public static float HintDelayFactor
        {
            get
            {
                switch (Current)
                {
                    case Difficulty.Story: return 0.6f;
                    case Difficulty.NightSupervisor: return 1.5f;
                    default: return 1f;
                }
            }
        }

        /// <summary>GDD 24.2: the night supervisor gets fewer unattended-motion nudges.</summary>
        public static bool ReducedCctvAlerts { get { return Current == Difficulty.NightSupervisor; } }

        /// <summary>GDD 24.2 / 16.16: story mode and the accessibility toggle remove timers.</summary>
        public static bool ChoiceTimersDisabled
        {
            get
            {
                if (Current == Difficulty.Story) return true;
                return ServiceHub.Settings != null && ServiceHub.Settings.Current.noChoiceTimers;
            }
        }

        /// <summary>GDD 24.2: story mode highlights the evidence that actually matters.</summary>
        public static bool HighlightKeyEvidence { get { return Current == Difficulty.Story; } }

        /// <summary>
        /// Scales everything that raises the night's counter-pressure (GDD 15.4). Relief is
        /// never scaled - an easier setting should not also be a slower recovery, or the
        /// player who chose it spends longer at the top of the ladder, not less.
        /// </summary>
        public static float PressureGainFactor
        {
            get
            {
                if (ServiceHub.Settings != null && ServiceHub.Settings.Current.calmNights) return 0.5f;

                switch (Current)
                {
                    case Difficulty.Story: return 0.6f;
                    case Difficulty.NightSupervisor: return 1.3f;
                    default: return 1f;
                }
            }
        }

        /// <summary>
        /// GDD 24.2 / 16.16: halves what a confrontation costs in time. The evidence and the
        /// performance still go - softening a consequence is not deleting it.
        /// </summary>
        public static bool SoftenedConsequences
        {
            get
            {
                if (Current == Difficulty.Story) return true;
                return ServiceHub.Settings != null && ServiceHub.Settings.Current.calmNights;
            }
        }

        public static HintMode Hints
        {
            get
            {
                var settings = ServiceHub.Settings;
                return settings == null ? HintMode.Delayed : (HintMode)settings.Current.hintMode;
            }
        }
    }
}
