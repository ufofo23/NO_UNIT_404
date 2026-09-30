using System;
using System.Collections.Generic;

namespace NO404.Core
{
    /// <summary>
    /// Authoritative holder of shift index, stats and flags (GDD 7, 25.1).
    /// Nothing else may write these values directly - everything goes through
    /// AddStat/SetFlag so that change events and clamping stay in one place.
    /// </summary>
    public sealed class GameStateService
    {
        readonly Dictionary<string, int> _stats = new Dictionary<string, int>(8);
        readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>(16);

        /// <summary>
        /// The choices that have a value rather than a yes/no (v5.0 5.4).
        ///
        /// SUNJA_CARE is GOOD, DELAYED or DENIED and a later night needs to know which. Three
        /// booleans could say all three at once; one id with one value cannot.
        /// </summary>
        readonly Dictionary<string, string> _choices = new Dictionary<string, string>(12);
        readonly HashSet<AccessLevel> _access = new HashSet<AccessLevel>();

        public int NightIndex { get; private set; }

        /// <summary>
        /// True during an endless run: shifts repeat past night 6, the story cases stay out of
        /// it, and the shift only stops when the caretaker is dismissed. Deliberately not
        /// saved - an endless run is a single sitting and a score, not a campaign.
        /// </summary>
        public bool EndlessMode { get; private set; }

        public void SetEndlessMode(bool endless)
        {
            EndlessMode = endless;
            if (endless) ResetRunTally();
        }

        // ---- endless run tally ------------------------------------------------

        /// <summary>
        /// What an endless run is scored on. Counted in every mode because it costs nothing
        /// and the campaign's night summary can use it later; only the endless result screen
        /// reads it today.
        /// </summary>
        public int ReportsCorrect { get; private set; }
        public int ReportsWrong { get; private set; }
        public int VisitorsCorrect { get; private set; }
        public int VisitorsWrong { get; private set; }

        public void ResetRunTally()
        {
            ReportsCorrect = 0;
            ReportsWrong = 0;
            VisitorsCorrect = 0;
            VisitorsWrong = 0;
        }

        public void NoteReport(bool correct)
        {
            if (correct) ReportsCorrect++; else ReportsWrong++;
        }

        public void NoteVisitor(bool correct)
        {
            if (correct) VisitorsCorrect++; else VisitorsWrong++;
        }

        public GameStateService()
        {
            ResetToNewGame();
        }

        public void ResetToNewGame()
        {
            EndlessMode = false;
            ResetRunTally();
            _stats.Clear();
            _flags.Clear();
            _choices.Clear();
            _access.Clear();

            // v5.0 5.2 initial values. Performance is not in v5.0's table and is kept as it
            // was: the shift score is still what the chairman reads, it simply no longer
            // decides the ending. ASSUMPTION: v5.0 replaces the ending grade with HP/SAN and
            // says nothing about retiring Performance, so it stays where the rest of the game
            // already reads it.
            _stats[StatIds.Performance] = 60;
            _stats[StatIds.CommunityTrust] = 50;
            _stats[StatIds.BuildingSafety] = 70;
            _stats[StatIds.ArchiveIntegrity] = 50;
            _stats[StatIds.ChairmanAlert] = 0;
            _stats[StatIds.HarinResonance] = 0;

            _access.Add(AccessLevel.Staff1);
            NightIndex = 0;
        }

        // ---- stats -------------------------------------------------------

        public int GetStat(string statId)
        {
            int value;
            return _stats.TryGetValue(statId, out value) ? value : 0;
        }

        public void SetStat(string statId, int value, string reason = "set")
        {
            int max = StatIds.MaxOf(statId);
            int clamped = value < 0 ? 0 : (value > max ? max : value);
            int previous = GetStat(statId);
            if (previous == clamped) return;

            _stats[statId] = clamped;
            EventBus.Publish(new StatChangedEvent(statId, previous, clamped, reason));
            Log.Info("State", statId + " " + previous + " -> " + clamped + " (" + reason + ")");
        }

        public void AddStat(string statId, int delta, string reason = "delta")
        {
            if (delta == 0) return;
            SetStat(statId, GetStat(statId) + delta, reason);
        }

        public IReadOnlyDictionary<string, int> AllStats { get { return _stats; } }

        // ---- flags -------------------------------------------------------

        public bool GetFlag(string flagId)
        {
            bool value;
            return _flags.TryGetValue(flagId, out value) && value;
        }

        public void SetFlag(string flagId, bool value)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            if (GetFlag(flagId) == value && _flags.ContainsKey(flagId)) return;

            _flags[flagId] = value;
            EventBus.Publish(new FlagChangedEvent(flagId, value));
            Log.Info("State", "flag " + flagId + " = " + value);
        }

        public IReadOnlyDictionary<string, bool> AllFlags { get { return _flags; } }

        // ---- choices (v5.0 5.4) ------------------------------------------

        /// <summary>
        /// Records which way a choice went. Writing it again overwrites: a resident was cared
        /// for or delayed or refused, and the night that asks later wants one answer.
        /// </summary>
        public void SetChoice(string choiceId, string value)
        {
            if (string.IsNullOrEmpty(choiceId)) return;

            string previous;
            _choices.TryGetValue(choiceId, out previous);
            if (previous == value) return;

            _choices[choiceId] = value ?? string.Empty;
            EventBus.Publish(new ChoiceRecordedEvent(choiceId, value ?? string.Empty));
            Log.Info("State", "choice " + choiceId + " = " + value);
        }

        /// <summary>The recorded value, or null when the choice has not come up yet.</summary>
        public string GetChoice(string choiceId)
        {
            string value;
            return _choices.TryGetValue(choiceId ?? string.Empty, out value) ? value : null;
        }

        public bool ChoiceIs(string choiceId, string value)
        {
            return string.Equals(GetChoice(choiceId), value, StringComparison.Ordinal);
        }

        public IReadOnlyDictionary<string, string> AllChoices { get { return _choices; } }

        // ---- debt (v5.0 5.3) ---------------------------------------------
        //
        // Debts are stats, so they clamp, save and publish like everything else. These two
        // wrappers exist so a caller says what it means rather than remembering that a debt
        // happens to be stored as a number between zero and five.

        public int GetDebt(string debtId) { return GetStat(debtId); }

        public void AddDebt(string debtId, int amount, string reasonKey)
        {
            if (amount == 0 || !DebtIds.IsDebt(debtId)) return;
            AddStat(debtId, amount, reasonKey);
        }

        // ---- access ------------------------------------------------------

        public bool HasAccess(AccessLevel level)
        {
            return level == AccessLevel.None || _access.Contains(level);
        }

        public void GrantAccess(AccessLevel level)
        {
            if (level == AccessLevel.None || !_access.Add(level)) return;
            Log.Info("State", "access granted: " + level);
        }

        public IEnumerable<AccessLevel> GrantedAccess { get { return _access; } }

        // ---- night -------------------------------------------------------

        public void BeginNight(int nightIndex)
        {
            NightIndex = nightIndex;
            EventBus.Publish(new NightStartedEvent(nightIndex));
            Log.Info("State", "night " + nightIndex + " started");
        }

        public void EndNight()
        {
            EventBus.Publish(new NightEndedEvent(NightIndex));
            Log.Info("State", "night " + NightIndex + " ended");
        }

        // ---- save hooks --------------------------------------------------

        public void LoadFrom(int nightIndex,
                             IEnumerable<KeyValuePair<string, int>> stats,
                             IEnumerable<KeyValuePair<string, bool>> flags,
                             IEnumerable<int> accessLevels,
                             IEnumerable<KeyValuePair<string, string>> choices = null)
        {
            NightIndex = nightIndex;

            _stats.Clear();
            if (stats != null) foreach (var kv in stats) _stats[kv.Key] = kv.Value;

            _flags.Clear();
            if (flags != null) foreach (var kv in flags) _flags[kv.Key] = kv.Value;

            _choices.Clear();

            if (choices != null) foreach (var kv in choices) _choices[kv.Key] = kv.Value;

            _access.Clear();
            if (accessLevels != null)
                foreach (var level in accessLevels) _access.Add((AccessLevel)level);
            if (_access.Count == 0) _access.Add(AccessLevel.Staff1);
        }
    }
}
