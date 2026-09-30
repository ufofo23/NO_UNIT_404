using System;
using System.Collections.Generic;
using NO404.Core;

namespace NO404.Residents
{
    public enum AccessSubject { Resident = 0, Visitor = 1, Staff = 2, Unknown = 3 }

    public sealed class AccessLogEntry
    {
        public int GameSecond;
        public AccessSubject Subject;
        public string NameKey;
        public string CardId;
        public string CameraId;
        public string LocationKey;
        /// <summary>Entering (true) or leaving (false).</summary>
        public bool Inbound;
        /// <summary>
        /// Authored contradictions stay silent in the list. GDD 16.11: the system never
        /// auto-warns; the player must select two rows and press Compare.
        /// </summary>
        public string ContradictionGroup;
    }

    /// <summary>
    /// Result of the Compare button. Reports raw facts only - no verdict, no highlighting
    /// of "the answer" (GDD 16.11).
    /// </summary>
    public readonly struct AccessComparison
    {
        public readonly int TimeDeltaSeconds;
        public readonly bool SameCard;
        public readonly bool SameLocation;
        /// <summary>Seconds a person actually needs to walk between the two locations.</summary>
        public readonly int TravelSeconds;
        public readonly bool PhysicallyPossible;

        public AccessComparison(int timeDelta, bool sameCard, bool sameLocation, int travelSeconds)
        {
            TimeDeltaSeconds = timeDelta;
            SameCard = sameCard;
            SameLocation = sameLocation;
            TravelSeconds = travelSeconds;
            PhysicallyPossible = sameLocation || Math.Abs(timeDelta) >= travelSeconds;
        }
    }

    public sealed class AccessLogService
    {
        readonly List<AccessLogEntry> _entries = new List<AccessLogEntry>(128);

        /// <summary>Walk times between logged locations, in game seconds (GDD 23.2).</summary>
        static readonly Dictionary<string, int> TravelTable = new Dictionary<string, int>
        {
            { "lobby|office", 25 },
            { "lobby|parking", 60 },
            { "lobby|floor04", 90 },
            { "lobby|floor08", 130 },
            { "lobby|floor13", 170 },
            { "office|parking", 70 },
            { "office|floor04", 100 },
            { "office|floor08", 140 },
            { "office|floor13", 180 },
            { "floor04|floor08", 60 },
            { "floor08|floor13", 70 },
            { "floor13|rooftop", 45 }
        };

        public IReadOnlyList<AccessLogEntry> Entries { get { return _entries; } }

        public void Add(AccessLogEntry entry)
        {
            if (entry == null) return;
            _entries.Add(entry);
            _entries.Sort((a, b) => a.GameSecond.CompareTo(b.GameSecond));
        }

        public void Add(int gameSecond, AccessSubject subject, string nameKey, string cardId,
                        string locationKey, string cameraId, bool inbound, string contradictionGroup = null)
        {
            Add(new AccessLogEntry
            {
                GameSecond = gameSecond,
                Subject = subject,
                NameKey = nameKey,
                CardId = cardId,
                LocationKey = locationKey,
                CameraId = cameraId,
                Inbound = inbound,
                ContradictionGroup = contradictionGroup
            });
        }

        public List<AccessLogEntry> Filter(AccessSubject? subject)
        {
            var result = new List<AccessLogEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (subject.HasValue && _entries[i].Subject != subject.Value) continue;
                result.Add(_entries[i]);
            }
            return result;
        }

        public AccessComparison Compare(AccessLogEntry a, AccessLogEntry b)
        {
            if (a == null || b == null) return new AccessComparison(0, false, false, 0);

            int delta = b.GameSecond - a.GameSecond;
            bool sameCard = !string.IsNullOrEmpty(a.CardId) && a.CardId == b.CardId;
            bool sameLocation = a.LocationKey == b.LocationKey;
            return new AccessComparison(delta, sameCard, sameLocation, TravelSeconds(a.LocationKey, b.LocationKey));
        }

        public static int TravelSeconds(string fromLocationKey, string toLocationKey)
        {
            if (string.IsNullOrEmpty(fromLocationKey) || string.IsNullOrEmpty(toLocationKey)) return 0;
            if (fromLocationKey == toLocationKey) return 0;

            string a = Leaf(fromLocationKey);
            string b = Leaf(toLocationKey);
            string forward = a + "|" + b;
            string backward = b + "|" + a;

            int seconds;
            if (TravelTable.TryGetValue(forward, out seconds)) return seconds;
            if (TravelTable.TryGetValue(backward, out seconds)) return seconds;
            return 120; // conservative default
        }

        static string Leaf(string locationKey)
        {
            int dot = locationKey.LastIndexOf('.');
            var leaf = dot >= 0 && dot < locationKey.Length - 1 ? locationKey.Substring(dot + 1) : locationKey;
            return leaf.ToLowerInvariant();
        }

        public void Reset() { _entries.Clear(); }

        public void LoadFrom(IEnumerable<Save.AccessLogSaveEntry> entries)
        {
            _entries.Clear();
            if (entries == null) return;

            foreach (var e in entries)
                _entries.Add(new AccessLogEntry
                {
                    GameSecond = e.gameSecond,
                    Subject = (AccessSubject)e.subject,
                    NameKey = e.nameKey,
                    CardId = e.cardId,
                    LocationKey = e.locationKey,
                    CameraId = e.cameraId,
                    Inbound = e.inbound,
                    ContradictionGroup = e.contradictionGroup
                });
        }
    }
}
