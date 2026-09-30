using System.Collections.Generic;

namespace NO404.Core
{
    /// <summary>
    /// What the game needs from Steam, stated without naming Steam.
    ///
    /// Steamworks.NET restricts its assembly to the platforms Valve ships a native library
    /// for. NO404.Runtime is not restricted - the EditMode tests and the tools build for
    /// anything - so it cannot reference that assembly directly without breaking every other
    /// platform. The dependency therefore points the other way: NO404.Steam references this,
    /// implements it, and registers itself. Nothing in gameplay code changes either way.
    /// </summary>
    /// <summary>
    /// How a Steam start-up ended. "Did not initialise" is two very different situations and
    /// collapsing them into a bool produced a player log that said "restarting through Steam"
    /// and "Steam is not running" one line apart.
    /// </summary>
    public enum SteamInitResult
    {
        /// <summary>Steam is up and the API is live.</summary>
        Ready,
        /// <summary>Steam is absent or declined. The game plays on and records locally.</summary>
        Unavailable,
        /// <summary>
        /// Steam is relaunching this game through the client and this process is already on
        /// its way out. Nothing further should be started.
        /// </summary>
        Restarting
    }

    public interface ISteamBackend
    {
        SteamInitResult Initialize();
        void RunCallbacks();
        void Unlock(string achievementId);
        void SetRichPresence(string statusKey);

        /// <summary>
        /// Whether Steam Cloud is on for both this app and this account. Saves themselves go
        /// through Auto-Cloud rather than the API (see Docs/Release/STEAM.md), so this exists
        /// to tell the player when their progress is not being backed up - which is the one
        /// thing Auto-Cloud cannot do for itself.
        /// </summary>
        bool CloudEnabled { get; }

        void Shutdown();
    }

    /// <summary>
    /// Steam facade (GDD 20.19).
    ///
    /// Unlocks are always recorded locally, whether or not Steam is running: the save file
    /// carries them (GDD 20.17) and the ending gallery reads them, so a player who bought the
    /// game on Steam but launched the exe directly still keeps their progress. When a backend
    /// is present the same unlock is forwarded to it.
    /// </summary>
    public sealed class SteamService
    {
        /// <summary>
        /// Set by NO404.Steam at load. Assigned before ServiceHub.Initialize runs, because
        /// RuntimeInitializeLoadType.SubsystemRegistration is earlier than any scene.
        /// </summary>
        public static ISteamBackend Backend;

        readonly HashSet<string> _unlocked = new HashSet<string>();

        ISteamBackend _backend;

        public SteamService() { ForwardToSteam = true; }

        /// <summary>True only when Steam is actually running and initialised.</summary>
        public bool Available { get; private set; }

        /// <summary>
        /// When false, unlocks are still recorded locally but never sent to Steam.
        ///
        /// Anything that plays the game without a person driving it - the smoke tests, the
        /// night simulator, a dev-console run - would otherwise grant achievements against
        /// whatever app id the developer's Steam client is signed into. That makes the smoke
        /// test's result depend on whether Steam happens to be open, and it quietly awards a
        /// real account achievements nobody earned.
        /// </summary>
        public bool ForwardToSteam { get; set; }

        /// <summary>False when Steam is absent, and also when the player has cloud saves off.</summary>
        public bool CloudEnabled { get { return Available && _backend != null && _backend.CloudEnabled; } }

        /// <summary>
        /// True once Steam has told us this process is being replaced by one it launched.
        /// Boot should stop rather than finish setting up a game that is closing.
        /// </summary>
        public bool IsRestarting { get; private set; }

        public void Initialize()
        {
            _backend = Backend;

            if (_backend == null)
            {
                Available = false;
                Log.Info("Steam", "no backend registered; achievements are recorded locally only");
                return;
            }

            var result = _backend.Initialize();
            Available = result == SteamInitResult.Ready;

            if (result == SteamInitResult.Restarting)
            {
                IsRestarting = true;
                Log.Info("Steam", "handing off to a copy launched by Steam; this process is closing");
                return;
            }

            if (!Available)
            {
                // Not an error. Running the exe outside Steam is a supported way to play, and
                // there is more than one reason to be here - the launcher is closed, or the
                // build has no app id of its own yet. The backend has already logged which,
                // so this line says what it can stand behind rather than guessing.
                Log.Info("Steam", "Steam integration inactive; achievements are recorded locally");
                return;
            }

            if (!_backend.CloudEnabled)
                Log.Warn("Steam", "cloud saves are disabled for this app or account");
        }

        /// <summary>
        /// Pumps Steam's callback queue. Must run every frame while Steam is up, or nothing
        /// Valve sends back - overlay state, cloud results - is ever delivered.
        /// </summary>
        public void Tick()
        {
            if (Available && _backend != null) _backend.RunCallbacks();
        }

        public bool IsUnlocked(string achievementId) { return _unlocked.Contains(achievementId); }

        public void Unlock(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) return;
            if (!_unlocked.Add(achievementId)) return;

            if (Available && ForwardToSteam && _backend != null) _backend.Unlock(achievementId);

            Log.Info("Steam", "achievement unlocked: " + achievementId);
            EventBus.Publish(new NotificationEvent("ui.notify.achievement", NotificationSeverity.Info));
        }

        public void SetRichPresence(string statusKey)
        {
            if (Available && _backend != null) _backend.SetRichPresence(statusKey);
            else Log.Trace("Steam", "rich presence: " + statusKey);
        }

        public IEnumerable<string> Unlocked { get { return _unlocked; } }

        /// <summary>
        /// Restores the local record from a save. Deliberately does not push these to Steam:
        /// Steam already knows what it has granted, and re-granting on every load would fire
        /// the overlay toast again for achievements the player earned weeks ago.
        /// </summary>
        public void LoadFrom(IEnumerable<string> unlocked)
        {
            _unlocked.Clear();
            if (unlocked == null) return;
            foreach (var id in unlocked) _unlocked.Add(id);
        }

        public void Shutdown()
        {
            if (Available && _backend != null) _backend.Shutdown();
            Available = false;
        }
    }
}
