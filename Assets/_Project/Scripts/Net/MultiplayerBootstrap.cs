using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using NO404.Core;

namespace NO404.Net
{
    /// <summary>
    /// Signing this copy of the game in to Unity Gaming Services.
    ///
    /// Relay will not hand out a join code to a player it does not recognise, so everything in
    /// <see cref="MultiplayerSessionService"/> is gated behind this. It runs once, lazily -
    /// there is no reason to make a single-player caretaker wait on a network round trip
    /// before the main menu appears, and a player who never opens a session never signs in.
    ///
    /// Anonymous sign-in is deliberate for now. It gives a stable player id per machine with
    /// no account, no password and nothing for a playtester to set up; when Steam comes back
    /// into this the sign-in swaps for the Steam ticket and nothing above this class changes.
    /// </summary>
    public static class MultiplayerBootstrap
    {
        static Task<bool> _pending;

        public enum Status { Offline, Working, Ready, Failed }

        public static Status State { get; private set; } = Status.Offline;

        /// <summary>Localisation key describing the last failure, or null.</summary>
        public static string LastErrorKey { get; private set; }

        /// <summary>The raw exception text, for the log and a bug report. Never shown raw in UI.</summary>
        public static string LastErrorDetail { get; private set; }

        public static bool IsReady { get { return State == Status.Ready; } }

        public static event Action OnStateChanged;

        /// <summary>
        /// Initialise and sign in, or return the attempt already in flight.
        ///
        /// Idempotent on purpose: the menu polls this, and two buttons can both want it. A
        /// second caller joins the first call's task rather than starting a competing one,
        /// which is the bug that produces two anonymous accounts and a very confusing session.
        /// </summary>
        public static Task<bool> EnsureSignedInAsync()
        {
            if (State == Status.Ready) return Task.FromResult(true);
            if (_pending != null) return _pending;

            _pending = SignInAsync();
            return _pending;
        }

        static async Task<bool> SignInAsync()
        {
            Set(Status.Working, null, null);

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                    Log.Info("Multiplayer", "Unity Services initialized");
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Log.Info("Multiplayer", "Authentication success, player id " +
                                            AuthenticationService.Instance.PlayerId);
                }

                Set(Status.Ready, null, null);
                return true;
            }
            catch (Exception e)
            {
                // Swallowing this was never an option: the symptom is "방 만들기 does nothing",
                // and without the reason on screen there is no way for a player to tell an
                // unconfigured project from a dropped wifi connection.
                Log.Error("Multiplayer", "sign-in failed: " + e);
                Set(Status.Failed, ErrorKeyFor(e), e.Message);

                // Cleared so a player who fixes their connection can press the button again.
                _pending = null;
                return false;
            }
        }

        /// <summary>
        /// Turns an exception into something a player can act on.
        ///
        /// The most likely failure by far is a project with no Unity Gaming Services linked -
        /// which is a thing the developer fixes in the dashboard, not something the player did
        /// wrong - so it gets its own message rather than hiding inside "connection failed".
        /// </summary>
        static string ErrorKeyFor(Exception e)
        {
            var text = e.ToString();

            if (text.Contains("project") && (text.Contains("not linked") || text.Contains("unlinked")))
                return "ui.net.error.no_project";
            if (text.Contains("RequestTimedOut") || text.Contains("timed out"))
                return "ui.net.error.timeout";
            if (text.Contains("NetworkError") || text.Contains("Unreachable"))
                return "ui.net.error.offline";

            return "ui.net.error.signin_failed";
        }

        static void Set(Status state, string errorKey, string detail)
        {
            State = state;
            LastErrorKey = errorKey;
            LastErrorDetail = detail;

            var cb = OnStateChanged;
            if (cb != null) cb();
        }

        /// <summary>Forgets a failure so the menu can offer the button again.</summary>
        public static void ClearError()
        {
            if (State != Status.Failed) return;
            Set(Status.Offline, null, null);
        }
    }
}
