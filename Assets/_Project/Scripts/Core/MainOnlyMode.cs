namespace NO404.Core
{
    /// <summary>
    /// The dev.mainonly switch: a shift that runs its main quest and nothing else.
    ///
    /// A night is fifteen candidate quests, a visitor roster, a phone schedule, an anomaly
    /// schedule, the M-event chain and - from night 5 - a stalker, all layered over one fixed
    /// main (v5.0 4). That is the game. It is also six things happening at once when what you
    /// want to look at is whether the campaign's spine still walks from N1-M01 to N6-M01, so
    /// this takes the layers off and leaves the spine.
    ///
    /// Three things it deliberately is not:
    ///
    /// - <b>Not saved.</b> It is a switch on the sitting, not a fact about the campaign, so a
    ///   save written under it loads back into a normal night. A save that recorded which
    ///   parts of itself had been switched off would be a save nothing could trust.
    /// - <b>Not mirrored.</b> Every gate it drives sits in host-only simulation (v3.0 46.1),
    ///   so a client sees the same stripped shift without having to be told about it.
    /// - <b>Not reachable in a shipped build.</b> <see cref="Active"/> reads
    ///   <see cref="DevConsole.Enabled"/>, which compiles to false outside the editor and
    ///   development builds, so a release binary cannot enter this mode at all.
    ///
    /// What survives the switch is what the main quest owns: its own case, the callers it is
    /// made of, the calls and CCTV anomalies authored against its case id, and the night's
    /// opening state. Everything else is skipped where it would have been scheduled rather
    /// than hidden afterwards - nothing starts, so nothing needs cleaning up when the switch
    /// goes off again.
    /// </summary>
    public static class MainOnlyMode
    {
        static bool _on;

        /// <summary>True while the shift is running its main quest alone.</summary>
        public static bool Active { get { return _on && DevConsole.Enabled; } }

        /// <summary>
        /// Whether something authored against a case must not happen tonight.
        ///
        /// The rule the phone and the camera wall both follow, in one place so they cannot
        /// drift apart: a call or an anomaly naming one of tonight's drawn cases IS that case
        /// and rides in with it. Anything else - including anything naming no case at all - is
        /// the night around the main, which is exactly what this mode takes away.
        /// </summary>
        public static bool Suppresses(string caseId)
        {
            if (!Active) return false;

            return ServiceHub.Cases == null || !ServiceHub.Cases.IsRunningTonight(caseId);
        }

        /// <summary>
        /// Turns it on or off.
        ///
        /// Takes effect at the next StartNight. The schedules it gates - the draw, the caller
        /// beats, the phone, the anomaly wall, the M-event chain - are each built once at the
        /// top of a shift, and rebuilding them mid-night would mean deciding what to do with
        /// an event already open and a caller already at the door. Flip it, then night.start.
        /// </summary>
        public static void Set(bool on)
        {
            if (!DevConsole.Enabled || _on == on) return;

            _on = on;
            Log.Info("Dev", "main-quest-only " + (on ? "on" : "off") + "; takes effect next night");
        }
    }
}
