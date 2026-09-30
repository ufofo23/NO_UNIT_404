using System;
using System.Collections.Generic;
using UnityEngine;
using NO404.Core;

namespace NO404.Visitors
{
    /// <summary>
    /// How far into the building somebody is allowed to come (v3.0 G-01 / 38.2).
    ///
    /// This replaces `문 열기 / 거절`. The old pair asked one question - is this person real -
    /// and the answer ended the event, which is exactly what v3.0 B-02 forbids: a visitor the
    /// player stops caring about the moment the button is pressed. Seven levels ask a better
    /// question, and it is a question with no safe default. Refusing a legitimate carer costs
    /// the third floor; handing a stranger FloorPass puts them on a landing with no camera.
    ///
    /// Order here is deliberate and is NOT the risk order - use <see cref="VisitorAccess.RiskRank"/>
    /// for that. Escorted is more permissive than LobbyOnly in reach and less dangerous than
    /// FloorPass in practice, because somebody is walking next to them.
    /// </summary>
    public enum VisitorAccessLevel
    {
        /// <summary>No decision yet. Not a grant.</summary>
        Pending = 0,
        /// <summary>Turned away at the gate.</summary>
        Reject = 1,
        /// <summary>Left standing outside while the caretaker checks something.</summary>
        Hold = 2,
        /// <summary>Into the vestibule between the two doors - close enough to look at properly.</summary>
        Vestibule = 3,
        /// <summary>As far as the lobby. Parcels, handovers, the post boxes.</summary>
        LobbyOnly = 4,
        /// <summary>Anywhere, but only while a member of staff is with them.</summary>
        Escorted = 5,
        /// <summary>Up to their stated floor, unaccompanied.</summary>
        FloorPass = 6,
        /// <summary>The run of the building for a while. Very rarely the right answer.</summary>
        FullTemporary = 7
    }

    /// <summary>Where a visitor is in their evening (v3.0 38.1).</summary>
    public enum VisitorState
    {
        Approaching = 0,
        AtGate = 1,
        Interviewing = 2,
        Waiting = 3,
        Rejected = 4,
        Inside = 5,
        EnRoute = 6,
        AtDestination = 7,
        Deviating = 8,
        Leaving = 9,
        Exited = 10
    }

    /// <summary>
    /// Things that can be true of a visitor on top of where they are (v3.0 38.1).
    /// A flag set, because several of these happen at once and none of them replaces the state.
    /// </summary>
    [Flags]
    public enum VisitorFlags
    {
        None = 0,
        /// <summary>Seen in two places at once. The night's job is to work out which is real.</summary>
        DuplicateObserved = 1 << 0,
        /// <summary>Nothing has seen them for long enough that the last position is a guess.</summary>
        Untracked = 1 << 1,
        /// <summary>Behind a door the caretaker closed on them.</summary>
        LockedIn = 1 << 2,
        /// <summary>Currently within reach of the member of staff who took them on.</summary>
        Escorted = 1 << 3,
        /// <summary>The escort condition has been broken for long enough to matter.</summary>
        EscortBroken = 1 << 4,
        /// <summary>Doing something the building will regret.</summary>
        HostileIntent = 1 << 5,
        /// <summary>Impossible, and not a threat.</summary>
        AnomalousButSafe = 1 << 6,
        /// <summary>Not happening now. Happened, and is being played back at somebody.</summary>
        HistoricalReplay = 1 << 7
    }

    /// <summary>
    /// The pass itself (v3.0 45.3).
    ///
    /// Revocable from the desk. Revoking does not teleport anybody out of a room they are
    /// already standing in - it stops the next door from opening, which is the honest thing a
    /// real access system does and a much better scene.
    /// </summary>
    [Serializable]
    public sealed class AccessToken
    {
        public string tokenId;
        public string visitorId;
        public VisitorAccessLevel level;
        public string[] allowedZones = new string[0];
        [Tooltip("Game second the pass stops working. 0 = no expiry.")]
        public int expireSecond;
        public bool escortRequired;
        public bool revoked;
        [Tooltip("Which member of staff granted it. Multiplayer keeps the blame honest.")]
        public string grantedByPlayerId;
        public int grantedSecond;

        public bool Allows(string zoneId)
        {
            if (revoked || string.IsNullOrEmpty(zoneId)) return false;
            if (expireSecond > 0 && ServiceHub.Clock != null &&
                ServiceHub.Clock.GameSecond >= expireSecond) return false;

            for (int i = 0; i < allowedZones.Length; i++)
                if (allowedZones[i] == zoneId) return true;

            return false;
        }
    }

    /// <summary>
    /// Static facts about the access model: what each level reaches, and how dangerous it is.
    /// </summary>
    public static class VisitorAccess
    {
        /// <summary>
        /// How much of the building a grant puts at risk, low to high.
        ///
        /// Not the enum order. Escorted reaches further than LobbyOnly and is safer than
        /// FloorPass, because the whole point of it is that somebody is standing there.
        /// </summary>
        public static int RiskRank(VisitorAccessLevel level)
        {
            switch (level)
            {
                case VisitorAccessLevel.Reject:        return 0;
                case VisitorAccessLevel.Hold:          return 1;
                case VisitorAccessLevel.Vestibule:     return 2;
                case VisitorAccessLevel.LobbyOnly:     return 3;
                case VisitorAccessLevel.Escorted:      return 4;
                case VisitorAccessLevel.FloorPass:     return 5;
                case VisitorAccessLevel.FullTemporary: return 6;
                default:                        return -1;
            }
        }

        /// <summary>True for the grants that actually put somebody inside the building.</summary>
        public static bool LetsThemIn(VisitorAccessLevel level)
        {
            return RiskRank(level) >= RiskRank(VisitorAccessLevel.Vestibule);
        }

        /// <summary>True for the grants that let somebody past the lobby on their own.</summary>
        public static bool Unaccompanied(VisitorAccessLevel level)
        {
            return level == VisitorAccessLevel.FloorPass || level == VisitorAccessLevel.FullTemporary;
        }

        /// <summary>
        /// Zones a level reaches, for a visitor whose stated destination is <paramref name="floorZone"/>.
        ///
        /// Vestibule is not a zone of its own - the building does not have one to stream - so
        /// it resolves to the lobby with the state machine holding them at the door. That is a
        /// deliberate simplification and it is invisible in play: what Vestibule buys is the
        /// close look (38.8), not the floor space.
        /// </summary>
        public static string[] ZonesFor(VisitorAccessLevel level, string floorZone)
        {
            switch (level)
            {
                case VisitorAccessLevel.Vestibule:
                case VisitorAccessLevel.LobbyOnly:
                    return new[] { ZoneIds.Lobby };

                case VisitorAccessLevel.FloorPass:
                    return string.IsNullOrEmpty(floorZone)
                        ? new[] { ZoneIds.Lobby, ZoneIds.Elevator, ZoneIds.Stairwell }
                        : new[] { ZoneIds.Lobby, ZoneIds.Elevator, ZoneIds.Stairwell, floorZone };

                case VisitorAccessLevel.Escorted:
                case VisitorAccessLevel.FullTemporary:
                    return AllPublicZones();

                default:
                    return new string[0];
            }
        }

        /// <summary>
        /// Everywhere a pass can reach at its most permissive. The plant rooms and the archive
        /// are not on it: nothing a visitor can claim justifies B2, and a caretaker who wants
        /// somebody down there walks them down (Escorted covers it through the escort rule
        /// rather than through the token).
        /// </summary>
        public static string[] AllPublicZones()
        {
            return new[]
            {
                ZoneIds.Lobby, ZoneIds.Elevator, ZoneIds.Stairwell,
                ZoneIds.Floor02, ZoneIds.Floor03, ZoneIds.Floor04,
                ZoneIds.Floor05, ZoneIds.Floor06,
                ZoneIds.Laundry, ZoneIds.Parking
            };
        }

        /// <summary>
        /// The levels the caretaker has been taught by a given night (v3.0 38.2).
        ///
        /// Showing all seven buttons on the first night is the same mistake as showing every
        /// cross-reference on the first caller: the player reads it as a menu to be optimised
        /// rather than a decision. The prologue teaches turning people away and letting them
        /// into the lobby, and the passes that hand over real floor arrive with the nights
        /// that need them.
        /// </summary>
        public static VisitorAccessLevel[] TaughtBy(int nightIndex)
        {
            var list = new List<VisitorAccessLevel>
            {
                VisitorAccessLevel.Reject, VisitorAccessLevel.Hold, VisitorAccessLevel.Vestibule, VisitorAccessLevel.LobbyOnly
            };

            if (nightIndex >= 1)
            {
                list.Add(VisitorAccessLevel.Escorted);
                list.Add(VisitorAccessLevel.FloorPass);
            }

            // The run of the building, unaccompanied, all night. It is almost never right, and
            // it is not offered until the week has shown the player why.
            if (nightIndex >= 4) list.Add(VisitorAccessLevel.FullTemporary);

            return list.ToArray();
        }

        public static string LabelKey(VisitorAccessLevel level)
        {
            return "ui.access." + level.ToString().ToLowerInvariant();
        }

        /// <summary>How long an unaccompanied pass lasts before the reader stops honouring it.</summary>
        public const int FloorPassSeconds = 45 * 60;
        public const int FullTemporarySeconds = 90 * 60;
    }
}
