using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEditor;
using UnityEngine;
using NO404.Net;

namespace NO404.EditorTools
{
    /// <summary>
    /// Answers the one question nobody can answer by reading code: is this project actually
    /// wired up to Unity Gaming Services?
    ///
    /// Relay will not hand out a join code to a project that is not linked and does not have
    /// the service switched on in the dashboard, and the failure arrives in the player as a
    /// button that appears to do nothing. Those are two different problems - one is fixed in a
    /// browser and one is fixed by reconnecting to wifi - so it is worth being able to ask
    /// before a playtest rather than during one.
    /// </summary>
    public static class NetDiagnostics
    {
        [MenuItem("Tools/NO404/Net/Check Online Services", priority = 61)]
        public static async void Check()
        {
            Debug.Log("[NO404-NET] cloud project id: " +
                      (string.IsNullOrEmpty(Application.cloudProjectId)
                          ? "(none - link the project in Project Settings > Services)"
                          : Application.cloudProjectId));

            var ok = await MultiplayerBootstrap.EnsureSignedInAsync();

            if (!ok)
            {
                Debug.LogError("[NO404-NET] sign-in FAILED (" + MultiplayerBootstrap.LastErrorKey + ")\n" +
                               MultiplayerBootstrap.LastErrorDetail +
                               "\nCheck: Unity Dashboard > this project > Multiplayer > Relay is enabled, " +
                               "and Project Settings > Services shows the project linked.");
                return;
            }

            Debug.Log("[NO404-NET] services OK. state=" + UnityServices.State +
                      " signedIn=" + AuthenticationService.Instance.IsSignedIn +
                      " playerId=" + AuthenticationService.Instance.PlayerId);

            await TryRoom();
        }

        /// <summary>
        /// Opens a room, prints the code, and closes it again.
        ///
        /// This is the whole feature end to end: if a code comes back, two people on two
        /// machines can use one. If it throws, the reason is in the log rather than in a
        /// playtest.
        /// </summary>
        static async Task TryRoom()
        {
            Debug.Log("[NO404-NET] opening a test room...");

            if (!await MultiplayerSessionService.HostAsync())
            {
                Debug.LogError("[NO404-NET] could not open a room (" +
                               MultiplayerSessionService.LastErrorKey + ")\n" +
                               MultiplayerSessionService.LastErrorDetail);
                return;
            }

            Debug.Log("[NO404-NET] ROOM OPENED. Join code: " + MultiplayerSessionService.JoinCode +
                      "  (" + MultiplayerSessionService.PlayerCount + "/" +
                      MultiplayerSessionService.Capacity + ")");

            await MultiplayerSessionService.LeaveAsync();
            Debug.Log("[NO404-NET] test room closed. Relay is working.");
        }
    }
}
