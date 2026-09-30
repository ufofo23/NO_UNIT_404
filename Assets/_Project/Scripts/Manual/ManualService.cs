using System.Collections.Generic;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Manual
{
    /// <summary>
    /// The caretaker's copy of 야간 특이상황 대응 지침 (spec 0.9).
    ///
    /// Holds which pages have been obtained and hands them to the manual UI. It deliberately
    /// does no judging: whether the player followed a page is <see cref="Anomalies"/>' problem,
    /// and whether breaking it counts as a violation is RiskService's. This class answers one
    /// question - what can the caretaker read right now.
    ///
    /// Spec 0.9.4: pages are procedures, not explanations. Nothing here is ever consulted to
    /// decide the truth of a main case.
    /// </summary>
    public sealed class ManualService
    {
        readonly ContentDatabase _content;
        readonly GameStateService _state;
        readonly HashSet<string> _unlocked = new HashSet<string>();

        public ManualService(ContentDatabase content, GameStateService state)
        {
            _content = content;
            _state = state;
            Reset();
        }

        /// <summary>Page ids the player can read, in the order the manual prints them.</summary>
        public IEnumerable<ManualPage> UnlockedPages
        {
            get
            {
                foreach (var page in OrderedPages())
                    if (_unlocked.Contains(page.pageId)) yield return page;
            }
        }

        public int UnlockedCount { get { return _unlocked.Count; } }

        public bool IsUnlocked(string pageId)
        {
            return !string.IsNullOrEmpty(pageId) && _unlocked.Contains(pageId);
        }

        /// <summary>Has the player been told how to handle this event yet?</summary>
        public bool HasPageFor(string eventId)
        {
            var page = FindPageFor(eventId);
            return page != null && _unlocked.Contains(page.pageId);
        }

        public ManualPage Find(string pageId)
        {
            if (string.IsNullOrEmpty(pageId)) return null;
            foreach (var page in _content.ManualPages)
                if (page != null && page.pageId == pageId) return page;
            return null;
        }

        public ManualPage FindPageFor(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            foreach (var page in _content.ManualPages)
                if (page != null && page.eventId == eventId) return page;
            return null;
        }

        // ---- unlocking --------------------------------------------------------

        /// <summary>
        /// Add a page to the binder. Returns false if it was already there, so a note the
        /// player picks up twice does not announce itself twice.
        /// </summary>
        public bool Unlock(string pageId, string reason = null)
        {
            var page = Find(pageId);
            if (page == null)
            {
                Log.Error("Manual", "cannot unlock unknown page " + pageId);
                return false;
            }
            if (!_unlocked.Add(pageId)) return false;

            Log.Info("Manual", "page " + pageId + " unlocked" +
                               (reason == null ? "" : " (" + reason + ")"));
            EventBus.Publish(new ManualPageUnlockedEvent(pageId, page.eventId));
            EventBus.Publish(new NotificationEvent("notify.manual.page_added", NotificationSeverity.Info));
            return true;
        }

        public bool UnlockFor(string eventId, string reason = null)
        {
            var page = FindPageFor(eventId);
            return page != null && Unlock(page.pageId, reason);
        }

        /// <summary>
        /// Called when a night begins and whenever a flag changes: hands over every page whose
        /// stated source has now happened.
        ///
        /// Pages carrying an <see cref="ManualPage.unlockFlagId"/> wait for that flag even
        /// once their night has arrived - a note Dongsik left behind a meter cover is
        /// obtainable on night 2, not automatic on night 2.
        /// </summary>
        public void RefreshAvailability()
        {
            foreach (var page in OrderedPages())
            {
                if (_unlocked.Contains(page.pageId)) continue;
                if (_state.NightIndex < page.nightIndex) continue;
                if (!string.IsNullOrEmpty(page.unlockFlagId) && !_state.GetFlag(page.unlockFlagId)) continue;

                Unlock(page.pageId, "availability");
            }
        }

        // ---- lifecycle ---------------------------------------------------------

        public void Reset()
        {
            _unlocked.Clear();
            // Spec 0.9.1: the prologue starts with the first three rules already in the drawer.
            foreach (var page in OrderedPages())
                if (page.source == ManualUnlockSource.Preloaded && page.nightIndex <= 0)
                    _unlocked.Add(page.pageId);
        }

        public void LoadFrom(IEnumerable<string> pageIds)
        {
            _unlocked.Clear();
            if (pageIds == null) return;
            foreach (var id in pageIds) if (Find(id) != null) _unlocked.Add(id);
        }

        public IEnumerable<string> UnlockedIds { get { return _unlocked; } }

        // ---- ordering -----------------------------------------------------------

        /// <summary>
        /// Pages in printed order: the general rules first, then M01..M18, then the tools.
        ///
        /// Sorting by id gets this right for free because the spec numbered them that way, and
        /// the general rules carry a MANUAL_R prefix that sorts ahead of MANUAL_M.
        /// </summary>
        IEnumerable<ManualPage> OrderedPages()
        {
            var pages = new List<ManualPage>(_content.ManualPages);
            pages.Sort((a, b) =>
            {
                if (a == null) return b == null ? 0 : 1;
                if (b == null) return -1;
                return string.CompareOrdinal(a.pageId, b.pageId);
            });
            return pages;
        }
    }
}
