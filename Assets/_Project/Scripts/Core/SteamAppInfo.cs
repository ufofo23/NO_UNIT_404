namespace NO404.Core
{
    /// <summary>
    /// The Steam app id, in the one place both halves of the project can see it.
    ///
    /// It used to live as a const inside NO404.Steam. That assembly is platform-restricted, so
    /// nothing else could read it - including the release check that is supposed to catch this
    /// exact mistake. The id now lives in NO404.Runtime, SteamworksBackend reads it from here,
    /// and <c>ReleaseSetup.ReleaseReadinessReport</c> refuses to pass while it is still the
    /// placeholder.
    ///
    /// Why this is worth a file of its own: 480 is Valve's public test app (Spacewar). It
    /// accepts every achievement call and records none of them, so a build shipped on 480
    /// looks completely healthy and silently awards nothing. The failure is invisible in play
    /// and only shows up as players asking why achievements do not unlock.
    ///
    /// TODO-STEAM: replace <see cref="AppId"/> with the id from the Steamworks partner site,
    /// and put the same number in <c>steam_appid.txt</c> at the project root. Docs/Release/STEAM.md
    /// lists everything else keyed to it.
    /// </summary>
    public static class SteamAppInfo
    {
        /// <summary>Valve's Spacewar test app. Anything shipped on this records nothing.</summary>
        public const uint PlaceholderAppId = 480;

        /// <summary>The app this build talks to.</summary>
        public const uint AppId = PlaceholderAppId;

        /// <summary>True while the project is still pointed at Valve's test app.</summary>
        public static bool IsPlaceholder { get { return AppId == PlaceholderAppId; } }
    }
}
