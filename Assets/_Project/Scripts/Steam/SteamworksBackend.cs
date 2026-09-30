using Steamworks;
using UnityEngine;
using NO404.Core;

namespace NO404.SteamIntegration
{
    /// <summary>
    /// The real Steam backend (GDD 20.19).
    ///
    /// This assembly exists only so that Steamworks.NET's platform restrictions stay out of
    /// NO404.Runtime. It registers itself into SteamService and is never referenced by name
    /// from gameplay code, which is why deleting the whole folder leaves a game that still
    /// builds and still records achievements locally.
    ///
    /// Steamworks.NET ships a SteamManager MonoBehaviour for projects with no service layer.
    /// This project has one, and CLAUDE.md forbids the singleton-in-a-scene pattern it uses,
    /// so that file is removed and the API is driven from here instead.
    /// </summary>
    public sealed class SteamworksBackend : ISteamBackend
    {
        /// <summary>
        /// The app this build talks to. The number itself lives in NO404.Runtime so that the
        /// release check can see it - this assembly is platform-restricted and nothing else
        /// can read a const declared in here. See <see cref="SteamAppInfo"/>.
        /// </summary>
        public const uint AppId = SteamAppInfo.AppId;

        bool _running;

        /// <summary>
        /// Registers the backend before any scene loads. SubsystemRegistration runs ahead of
        /// ServiceHub.Initialize, which is the only ordering requirement here.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            SteamService.Backend = new SteamworksBackend();
        }

        public SteamInitResult Initialize()
        {
            // No real app id yet means no Steam at all (TODO-STEAM in SteamAppInfo).
            //
            // This is not caution, it is a bug fix. RestartAppIfNecessary(480) asks Steam to
            // relaunch the game as app 480 - Valve's public Spacewar test app - and Steam
            // obliges: the player double-clicks NO_UNIT_404.exe and Steam shows them playing
            // "SteamworksExample". The overlay attaches to the wrong game, and a playtester
            // sent this build gets a library entry for something they never asked for.
            //
            // Skipping is also free. 480 accepts every achievement call and records none of
            // them, so nothing is lost that was not already being thrown away, and the local
            // achievement path below is a supported way to play. The moment SteamAppInfo.AppId
            // becomes a real id this guard stops applying on its own.
            if (SteamAppInfo.IsPlaceholder)
            {
                Log.Info("Steam", "no app id yet (still Valve's test app " +
                                  SteamAppInfo.PlaceholderAppId + "), so Steam is left alone; " +
                                  "achievements are recorded locally");
                return SteamInitResult.Unavailable;
            }

            if (!Packsize.Test())
            {
                Log.Error("Steam", "Steamworks.NET packsize mismatch - wrong platform build");
                return SteamInitResult.Unavailable;
            }

            if (!DllCheck.Test())
            {
                Log.Error("Steam", "the wrong steam_api binary is next to the player");
                return SteamInitResult.Unavailable;
            }

            try
            {
                // Relaunches the game through Steam when it was started from the exe directly.
                // Only true in a shipped build with no steam_appid.txt beside it, which is
                // exactly the arrangement BuildScript produces.
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
                {
                    Application.Quit();
                    return SteamInitResult.Restarting;
                }
            }
            catch (System.DllNotFoundException e)
            {
                Log.Error("Steam", "steam_api64.dll is missing: " + e.Message);
                return SteamInitResult.Unavailable;
            }

            _running = SteamAPI.Init();
            if (!_running) return SteamInitResult.Unavailable;

            // No RequestCurrentStats call here. Older integrations needed one before any
            // SetAchievement would take, and every tutorial still shows it; Steamworks SDK
            // 1.6x removed it and delivers the current user's stats with Init. RequestUserStats
            // remains, but that is for reading *other* players and is not what this needs.

            Log.Info("Steam", "initialised as " + SteamFriends.GetPersonaName() +
                              " (app " + AppId + ")");
            return SteamInitResult.Ready;
        }

        public void RunCallbacks()
        {
            if (_running) SteamAPI.RunCallbacks();
        }

        public void Unlock(string achievementId)
        {
            if (!_running) return;

            if (!SteamUserStats.SetAchievement(achievementId))
            {
                // Almost always a name that is not registered on the partner site. Worth an
                // error: it is invisible in game and the achievement simply never appears.
                Log.Error("Steam", "SetAchievement rejected '" + achievementId +
                                   "'; is it declared on the partner site?");
                return;
            }

            SteamUserStats.StoreStats();
        }

        public void SetRichPresence(string statusKey)
        {
            if (!_running) return;

            // "steam_display" names a token in the app's rich presence localisation file;
            // sending a raw sentence here shows nothing at all.
            SteamFriends.SetRichPresence("steam_display", statusKey);
        }

        public bool CloudEnabled
        {
            get
            {
                if (!_running) return false;
                return SteamRemoteStorage.IsCloudEnabledForApp() &&
                       SteamRemoteStorage.IsCloudEnabledForAccount();
            }
        }

        public void Shutdown()
        {
            if (!_running) return;

            _running = false;
            SteamAPI.Shutdown();
        }
    }
}
