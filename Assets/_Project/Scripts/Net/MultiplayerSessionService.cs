using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using NO404.Core;

namespace NO404.Net
{
    /// <summary>
    /// Opening a shift other people can walk into (v3.0 34).
    ///
    /// This is the only class in the game that talks to Unity's Multiplayer Services, and that
    /// is the entire point of it existing. The menu asks for "a room and a code"; what
    /// provides them - Relay today, a Steam lobby when the game ships - is a detail that stops
    /// here. Nothing above this layer knows the word Relay, and nothing below it knows what a
    /// caretaker is.
    ///
    /// The session SDK drives Netcode itself: creating a session with Relay networking
    /// configures the transport and calls NetworkManager.StartHost, and joining one calls
    /// StartClient. So this service never starts Netcode by hand - it only needs the
    /// NetworkManager to exist first, which <see cref="NetSession"/> guarantees at boot.
    /// </summary>
    public static class MultiplayerSessionService
    {
        /// <summary>v3.0 34.1. Four is the ceiling and one is a complete game.</summary>
        public const int MaxPlayers = 4;

        public enum Status { Idle, Working, Hosting, Joined, Failed }

        static ISession _session;

        public static Status State { get; private set; } = Status.Idle;

        /// <summary>The six characters somebody reads out over voice chat.</summary>
        public static string JoinCode
        {
            get { return _session != null ? _session.Code : null; }
        }

        public static int PlayerCount { get { return _session != null ? _session.PlayerCount : 1; } }
        public static int Capacity { get { return _session != null ? _session.MaxPlayers : MaxPlayers; } }
        public static bool InSession { get { return _session != null; } }
        public static bool IsHost { get { return _session != null && _session.IsHost; } }

        public static string LastErrorKey { get; private set; }
        public static string LastErrorDetail { get; private set; }

        public static event Action OnChanged;

        // -----------------------------------------------------------------
        // hosting
        // -----------------------------------------------------------------

        public static async Task<bool> HostAsync()
        {
            if (State == Status.Working || InSession) return false;

            Set(Status.Working, null, null);

            if (!await MultiplayerBootstrap.EnsureSignedInAsync())
            {
                Set(Status.Failed, MultiplayerBootstrap.LastErrorKey,
                    MultiplayerBootstrap.LastErrorDetail);
                return false;
            }

            try
            {
                Log.Info("Multiplayer", "Creating Relay Session");

                var options = new SessionOptions
                {
                    MaxPlayers = MaxPlayers,
                    IsPrivate = true          // reachable by code only; there is no browser
                }.WithRelayNetwork();

                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Subscribe(_session);

                Log.Info("Multiplayer", "Host created");
                Log.Info("Multiplayer", "Join Code: " + _session.Code);

                Set(Status.Hosting, null, null);
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Multiplayer", "could not create a session: " + e);
                Set(Status.Failed, ErrorKeyFor(e), e.Message);
                await CleanUpAsync();
                return false;
            }
        }

        // -----------------------------------------------------------------
        // joining
        // -----------------------------------------------------------------

        public static async Task<bool> JoinAsync(string rawCode)
        {
            if (State == Status.Working || InSession) return false;

            var code = Normalise(rawCode);
            if (string.IsNullOrEmpty(code))
            {
                Set(Status.Failed, "ui.net.error.empty_code", null);
                return false;
            }

            Set(Status.Working, null, null);

            if (!await MultiplayerBootstrap.EnsureSignedInAsync())
            {
                Set(Status.Failed, MultiplayerBootstrap.LastErrorKey,
                    MultiplayerBootstrap.LastErrorDetail);
                return false;
            }

            try
            {
                Log.Info("Multiplayer", "Joining session: " + code);

                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Subscribe(_session);

                Log.Info("Multiplayer", "Joined session " + _session.Id);
                Set(Status.Joined, null, null);
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Multiplayer", "could not join " + code + ": " + e);
                Set(Status.Failed, ErrorKeyFor(e), e.Message);
                await CleanUpAsync();
                return false;
            }
        }

        /// <summary>
        /// Codes get read out loud and typed back in, so they arrive with spaces, stray
        /// newlines from a paste, and whatever case the person felt like. Relay wants the
        /// canonical upper-case form.
        /// </summary>
        public static string Normalise(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            var trimmed = code.Trim().Replace(" ", string.Empty)
                              .Replace("\r", string.Empty).Replace("\n", string.Empty);

            return trimmed.Length == 0 ? null : trimmed.ToUpperInvariant();
        }

        // -----------------------------------------------------------------
        // leaving
        // -----------------------------------------------------------------

        public static async Task LeaveAsync()
        {
            if (_session == null)
            {
                Set(Status.Idle, null, null);
                return;
            }

            try
            {
                await _session.LeaveAsync();
                Log.Info("Multiplayer", "Session closed");
            }
            catch (Exception e)
            {
                // Leaving is best-effort. A session we cannot say goodbye to still has to stop
                // being ours locally, or the menu is stuck in a room that no longer exists.
                Log.Warn("Multiplayer", "leaving was not clean: " + e.Message);
            }

            await CleanUpAsync();
            Set(Status.Idle, null, null);
        }

        static async Task CleanUpAsync()
        {
            Unsubscribe(_session);
            _session = null;
            _dropRequested = false;

            // The SDK starts Netcode, but it does not always stop it - and a NetworkManager
            // left listening will refuse the next session with an error that names nothing.
            if (NetSession.Instance != null) NetSession.Instance.ShutdownTransport();

            await Task.Yield();
        }

        // -----------------------------------------------------------------
        // session events
        // -----------------------------------------------------------------

        static void Subscribe(ISession session)
        {
            if (session == null) return;

            session.PlayerJoined += OnPlayerJoined;
            session.PlayerHasLeft += OnPlayerLeft;
            session.Deleted += OnDeleted;
            session.RemovedFromSession += OnRemoved;
            session.Changed += Raise;
        }

        static void Unsubscribe(ISession session)
        {
            if (session == null) return;

            session.PlayerJoined -= OnPlayerJoined;
            session.PlayerHasLeft -= OnPlayerLeft;
            session.Deleted -= OnDeleted;
            session.RemovedFromSession -= OnRemoved;
            session.Changed -= Raise;
        }

        static void OnPlayerJoined(string playerId)
        {
            Log.Info("Multiplayer", "player joined the session (" + PlayerCount + "/" + Capacity + ")");
            Raise();
        }

        static void OnPlayerLeft(string playerId)
        {
            Log.Info("Multiplayer", "player left the session (" + PlayerCount + "/" + Capacity + ")");
            Raise();
        }

        // The session's own callbacks can arrive on a background thread, and tearing down a
        // NetworkManager from one is a crash rather than an error. So the handler records what
        // happened and NetSession.Update does the work where Unity allows it.
        static volatile bool _dropRequested;
        static string _dropReasonKey;

        static void OnDeleted()
        {
            Log.Info("Multiplayer", "the host closed the session");
            RequestDrop("ui.net.error.host_left");
        }

        static void OnRemoved()
        {
            Log.Info("Multiplayer", "removed from the session");
            RequestDrop("ui.net.error.host_left");
        }

        static void RequestDrop(string reasonKey)
        {
            _dropReasonKey = reasonKey;
            _dropRequested = true;
        }

        /// <summary>
        /// Applies anything a background callback asked for. Called once a frame from
        /// <see cref="NetSession"/>, which is a MonoBehaviour and therefore on the main thread.
        /// </summary>
        public static void PumpMainThread()
        {
            if (!_dropRequested) return;
            _dropRequested = false;

            var reason = _dropReasonKey;
            _dropReasonKey = null;

            Unsubscribe(_session);
            _session = null;

            if (NetSession.Instance != null) NetSession.Instance.ShutdownTransport();

            Set(Status.Failed, reason, null);
        }

        /// <summary>
        /// Turns an SDK exception into a sentence a player can act on.
        ///
        /// Matched on text rather than on typed error codes because the SDK surfaces several
        /// unrelated exception types for these cases; the alternative is one generic message
        /// for "wrong code", "room full" and "no internet", which are three different problems
        /// with three different fixes.
        /// </summary>
        static string ErrorKeyFor(Exception e)
        {
            var text = e.ToString();

            if (text.Contains("NotFound") || text.Contains("not found") || text.Contains("InvalidJoinCode"))
                return "ui.net.error.bad_code";
            if (text.Contains("SessionFull") || text.Contains("is full") || text.Contains("NoOpenSlot"))
                return "ui.net.error.full";
            if (text.Contains("RequestTimedOut") || text.Contains("timed out"))
                return "ui.net.error.timeout";
            if (text.Contains("NetworkError") || text.Contains("Unreachable"))
                return "ui.net.error.offline";
            if (text.Contains("project") && text.Contains("not linked"))
                return "ui.net.error.no_project";

            return "ui.net.error.join_failed";
        }

        static void Set(Status state, string errorKey, string detail)
        {
            State = state;
            LastErrorKey = errorKey;
            LastErrorDetail = detail;
            Raise();
        }

        static void Raise() { var cb = OnChanged; if (cb != null) cb(); }

        public static void ClearError()
        {
            if (State != Status.Failed) return;
            Set(Status.Idle, null, null);
        }
    }
}
