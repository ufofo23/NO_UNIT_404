using System.Collections.Generic;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Residents
{
    /// <summary>
    /// Searchable resident directory backing the Residents app (GDD 16.10).
    /// The 404 record exists in content from the start but is filtered out of every query
    /// until its reveal flag is set on night 4 - the record itself is styled like any other.
    /// </summary>
    public sealed class ResidentDatabase
    {
        /// <summary>
        /// How long a queued glimpse stays on the list, in game seconds.
        ///
        /// Three real seconds at the base rate (GDD 6.2: one game minute is four real
        /// seconds). Game seconds rather than a real-time timer because CLAUDE.md gives the
        /// clock one unit and this has to survive a pause without quietly running out.
        /// </summary>
        public const int GlimpseGameSeconds = 45;

        readonly ContentDatabase _content;
        readonly Dictionary<string, ResidentStatus> _statusOverrides = new Dictionary<string, ResidentStatus>();

        string _glimpseResidentId;      // armed, waiting for the app to be opened
        string _glimpsingResidentId;    // on the list right now
        int _glimpseEndsAtGameSecond;

        public ResidentDatabase(ContentDatabase content) { _content = content; }

        // ---- the glimpse (GDD 16.10) ----------------------------------------
        //
        // The 404 record is the game's cover image and its title, and it used to be
        // unreachable until the night-4 sync - about three hours in, an hour past the point
        // where a Steam refund stops being free. It is still not searchable before night 4.
        // It simply arrives once, on its own, the night the player first proves someone is
        // living in a unit that does not exist, and is gone again three seconds later.
        //
        // Nothing mechanical rides on it: C01's own decision already pays the resonance, so
        // a player who blinks loses a moment and not a thread (GDD 14.4).

        /// <summary>Arms the glimpse. It shows the next time the residents app is opened.</summary>
        public void QueueGlimpse(string residentId)
        {
            if (string.IsNullOrEmpty(residentId)) return;
            if (_content.FindResident(residentId) == null) return;
            _glimpseResidentId = residentId;
        }

        public bool GlimpseQueued { get { return !string.IsNullOrEmpty(_glimpseResidentId); } }

        /// <summary>
        /// True while the glimpsed row is on the list. The app polls this so the row can go
        /// away under the player rather than waiting for them to type something.
        /// </summary>
        public bool GlimpseShowing
        {
            get
            {
                if (string.IsNullOrEmpty(_glimpsingResidentId)) return false;
                if (NowGameSecond() < _glimpseEndsAtGameSecond) return true;
                _glimpsingResidentId = null;
                return false;
            }
        }

        /// <summary>
        /// Called when the residents app opens. Starts the armed glimpse, if there is one, so
        /// the row is guaranteed to be on screen for its three seconds rather than expiring
        /// in an app nobody was looking at.
        /// </summary>
        public bool StartQueuedGlimpse()
        {
            if (string.IsNullOrEmpty(_glimpseResidentId)) return false;

            _glimpsingResidentId = _glimpseResidentId;
            _glimpseResidentId = null;
            _glimpseEndsAtGameSecond = NowGameSecond() + GlimpseGameSeconds;
            return true;
        }

        static int NowGameSecond()
        {
            return ServiceHub.Clock != null ? ServiceHub.Clock.GameSecond : 0;
        }

        public bool IsVisible(ResidentDefinition resident)
        {
            if (resident == null) return false;

            // Deleted is deleted. This comes before everything else, the glimpse included: a
            // row somebody removed does not flicker back because the sync once showed it.
            if (!string.IsNullOrEmpty(resident.hideFlagId) && ServiceHub.State.GetFlag(resident.hideFlagId))
                return false;

            if (!resident.hiddenUntilSync) return true;
            if (_glimpsingResidentId == resident.residentId && GlimpseShowing) return true;
            if (string.IsNullOrEmpty(resident.revealFlagId)) return false;
            return ServiceHub.State.GetFlag(resident.revealFlagId);
        }

        /// <summary>
        /// The name to put on the list. A row that is only on screen because the sync let it
        /// slip for three seconds shows its masked form - the list is not where anybody gets
        /// to read a name the campaign has not given them yet (v5.1 0.16).
        /// </summary>
        public string DisplayNameKey(ResidentDefinition resident)
        {
            if (resident == null) return string.Empty;

            bool revealed = !resident.hiddenUntilSync ||
                            (!string.IsNullOrEmpty(resident.revealFlagId) &&
                             ServiceHub.State.GetFlag(resident.revealFlagId));

            return revealed || string.IsNullOrEmpty(resident.glimpseNameKey)
                ? resident.nameKey
                : resident.glimpseNameKey;
        }

        /// <summary>What can be done to this row right now.</summary>
        public List<ResidentRecordAction> AvailableActions(ResidentDefinition resident)
        {
            var available = new List<ResidentRecordAction>();
            if (resident == null || resident.actions == null || !IsVisible(resident)) return available;

            for (int i = 0; i < resident.actions.Length; i++)
            {
                var action = resident.actions[i];
                if (action == null) continue;

                string unmet;
                if (Cases.ConditionEvaluator.EvaluateAll(action.availability, out unmet)) available.Add(action);
            }

            return available;
        }

        /// <summary>
        /// Does it. Host side: reached through NetShift's action funnel, so the row changes
        /// once for the whole shift whoever pressed the button.
        /// </summary>
        public bool Perform(string residentId, string actionId)
        {
            var resident = _content.FindResident(residentId);
            var action = resident != null ? resident.FindAction(actionId) : null;
            if (action == null || !IsVisible(resident)) return false;

            string unmet;
            if (!Cases.ConditionEvaluator.EvaluateAll(action.availability, out unmet))
            {
                Log.Info("Residents", actionId + " refused on " + residentId + ": " + unmet);
                return false;
            }

            ServiceHub.Cases.ApplyConsequences(action.consequences);
            Log.Info("Residents", actionId + " on " + residentId);
            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);
            return true;
        }

        public ResidentStatus StatusOf(ResidentDefinition resident)
        {
            if (resident == null) return ResidentStatus.Vacant;
            ResidentStatus overridden;
            return _statusOverrides.TryGetValue(resident.residentId, out overridden)
                ? overridden
                : resident.initialStatus;
        }

        public void SetStatus(string residentId, ResidentStatus status)
        {
            if (string.IsNullOrEmpty(residentId)) return;
            _statusOverrides[residentId] = status;
        }

        public ResidentDefinition FindById(string residentId)
        {
            var resident = _content.FindResident(residentId);
            return IsVisible(resident) ? resident : null;
        }

        public ResidentDefinition FindByUnit(string unitNumber)
        {
            foreach (var resident in _content.Residents)
            {
                if (resident == null || resident.unitNumber != unitNumber) continue;
                if (!IsVisible(resident)) continue;
                return resident;
            }
            return null;
        }

        /// <summary>
        /// Search over unit number, name, plate fragment, card id and last 4 phone digits
        /// (GDD 16.10 search fields). An empty query lists every visible record.
        /// </summary>
        public List<ResidentDefinition> Search(string query)
        {
            var results = new List<ResidentDefinition>();
            string q = query == null ? string.Empty : query.Trim().ToLowerInvariant();

            foreach (var resident in _content.Residents)
            {
                if (!IsVisible(resident)) continue;
                if (q.Length == 0 || MatchesQuery(resident, q)) results.Add(resident);
            }

            results.Sort((a, b) => string.CompareOrdinal(a.unitNumber, b.unitNumber));
            return results;
        }

        bool MatchesQuery(ResidentDefinition resident, string q)
        {
            if (resident.unitNumber != null && resident.unitNumber.ToLowerInvariant().Contains(q)) return true;
            if (resident.cardId != null && resident.cardId.ToLowerInvariant().Contains(q)) return true;
            if (resident.phoneLast4 != null && resident.phoneLast4.Contains(q)) return true;

            var displayName = Loc.T(resident.nameKey);
            if (displayName != null && displayName.ToLowerInvariant().Contains(q)) return true;

            if (resident.vehicles != null)
                for (int i = 0; i < resident.vehicles.Length; i++)
                {
                    var plate = resident.vehicles[i] != null ? resident.vehicles[i].plate : null;
                    if (plate != null && plate.ToLowerInvariant().Contains(q)) return true;
                }

            return false;
        }

        /// <summary>Looks up a plate across every visible record. Used by the parking task.</summary>
        public ResidentDefinition FindByPlate(string plate)
        {
            if (string.IsNullOrEmpty(plate)) return null;
            foreach (var resident in _content.Residents)
            {
                if (!IsVisible(resident) || resident.vehicles == null) continue;
                for (int i = 0; i < resident.vehicles.Length; i++)
                {
                    var v = resident.vehicles[i];
                    if (v != null && v.plate == plate) return resident;
                }
            }
            return null;
        }

        /// <summary>True when the resident pre-registered this visitor (GDD 13.2).</summary>
        public bool HasPreRegisteredVisitor(string unitNumber, string visitorId)
        {
            var resident = FindByUnit(unitNumber);
            if (resident == null || resident.recurringVisitors == null) return false;

            for (int i = 0; i < resident.recurringVisitors.Length; i++)
            {
                var v = resident.recurringVisitors[i];
                if (v != null && v.visitorId == visitorId) return v.preRegistered;
            }
            return false;
        }

        public void Reset()
        {
            _statusOverrides.Clear();
            _glimpseResidentId = null;
            _glimpsingResidentId = null;
            _glimpseEndsAtGameSecond = 0;
        }

        public void LoadFrom(IEnumerable<Save.ResidentSaveEntry> entries)
        {
            _statusOverrides.Clear();
            if (entries == null) return;
            foreach (var e in entries) _statusOverrides[e.residentId] = (ResidentStatus)e.status;
        }

        public IEnumerable<KeyValuePair<string, ResidentStatus>> StatusOverrides { get { return _statusOverrides; } }
    }
}
