using System.Collections.Generic;

namespace NO404.Core
{
    /// <summary>
    /// Who is standing where, across every caretaker on shift (v3.0 34, 37.3).
    ///
    /// The whole game was written against `ServiceHub.Player.CurrentZone`, which is a sound
    /// assumption right up until there are two of you. Then it silently means "wherever the
    /// person holding this copy of the game is standing", and every rule built on it quietly
    /// stops working for the other one: a colleague at the lobby glass sees nothing on your
    /// screen, and a visitor being walked upstairs by your friend counts as unescorted.
    ///
    /// So the questions the rules actually want to ask are asked here instead, and they are
    /// asked about the shift rather than about you. <see cref="AnyoneIn"/> is the one that
    /// matters: the building does not care which caretaker is in the room.
    ///
    /// Zones the local player experiences alone - streaming, the stalker, staged anomalies on
    /// a lens - deliberately keep reading <c>ServiceHub.Player</c>. Those are not facts about
    /// the building, they are facts about one person's night, and a second player must not
    /// make the first one's corridor stop loading.
    /// </summary>
    public sealed class PlayerPresence
    {
        /// <summary>The id used when nobody has joined. Single player is a one-caretaker shift.</summary>
        public const string SoloPlayerId = "P1";

        /// <summary>
        /// What one caretaker is drawing (GDD 15.5).
        ///
        /// The night reserve is one meter for one building, and two of the four things that
        /// pull on it - a torch and a camera wall on a screen - are held by a person rather
        /// than by the building. With three caretakers on shift the service was reading one
        /// of each and charging for one of each, so two torches down a dark corridor were free
        /// and the whole point of the reserve went with them.
        /// </summary>
        public struct Load
        {
            /// <summary>0 not watching, 1 the grid, 2 one channel full-screen.</summary>
            public int Screens;
            public bool Torch;
        }

        readonly Dictionary<string, string> _zoneByPlayer = new Dictionary<string, string>();
        readonly Dictionary<string, Load> _loadByPlayer = new Dictionary<string, Load>();

        public PlayerPresence()
        {
            LocalPlayerId = SoloPlayerId;
            _zoneByPlayer[SoloPlayerId] = ZoneIds.Office;
        }

        /// <summary>Which caretaker this copy of the game is. Set by the session on join.</summary>
        public string LocalPlayerId { get; private set; }

        public int Count { get { return _zoneByPlayer.Count; } }

        public IEnumerable<KeyValuePair<string, string>> All { get { return _zoneByPlayer; } }

        public void SetLocalPlayerId(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == LocalPlayerId) return;

            string zone;
            _zoneByPlayer.TryGetValue(LocalPlayerId, out zone);
            _zoneByPlayer.Remove(LocalPlayerId);

            LocalPlayerId = playerId;
            _zoneByPlayer[playerId] = zone ?? ZoneIds.Office;
        }

        /// <summary>
        /// Where somebody is now. Called for the local player by their own tracker and for
        /// everybody else by whatever is carrying their position across the wire.
        /// </summary>
        public void Report(string playerId, string zoneId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(zoneId)) return;
            _zoneByPlayer[playerId] = zoneId;
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _zoneByPlayer.Remove(playerId);
            _loadByPlayer.Remove(playerId);
        }

        /// <summary>What this caretaker is currently pulling out of the reserve.</summary>
        public void ReportLoad(string playerId, bool torch, int screens)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _loadByPlayer[playerId] = new Load { Torch = torch, Screens = screens };
        }

        public Load LoadOf(string playerId)
        {
            Load load;
            return _loadByPlayer.TryGetValue(playerId ?? string.Empty, out load) ? load : default(Load);
        }

        public int TorchCount
        {
            get
            {
                int count = 0;
                foreach (var pair in _loadByPlayer) if (pair.Value.Torch) count++;
                return count;
            }
        }

        /// <summary>How many caretakers have the wall up, at each of its two costs.</summary>
        public void CountScreens(out int grid, out int fullscreen)
        {
            grid = 0;
            fullscreen = 0;

            foreach (var pair in _loadByPlayer)
            {
                if (pair.Value.Screens == 1) grid++;
                else if (pair.Value.Screens == 2) fullscreen++;
            }
        }

        public string ZoneOf(string playerId)
        {
            string zone;
            return _zoneByPlayer.TryGetValue(playerId ?? string.Empty, out zone) ? zone : null;
        }

        /// <summary>
        /// True when any caretaker on shift is in this zone.
        ///
        /// This is the question almost every rule was really asking. A tell only visible
        /// through the lobby glass is visible because somebody is at the glass - it does not
        /// matter whose hands are on which keyboard, and in a two-handed shift it is usually
        /// not the person who will end up pressing the button.
        /// </summary>
        public bool AnyoneIn(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return false;

            foreach (var pair in _zoneByPlayer)
                if (pair.Value == zoneId) return true;

            return false;
        }

        /// <summary>The id of somebody standing in this zone, or null. Used for blame and escorts.</summary>
        public string WhoIsIn(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;

            foreach (var pair in _zoneByPlayer)
                if (pair.Value == zoneId) return pair.Key;

            return null;
        }

        public void Reset()
        {
            _zoneByPlayer.Clear();
            _loadByPlayer.Clear();
            _zoneByPlayer[LocalPlayerId] = ZoneIds.Office;
        }
    }
}
