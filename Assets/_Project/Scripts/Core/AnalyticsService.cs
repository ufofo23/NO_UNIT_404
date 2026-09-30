using System.Collections.Generic;
using System.Text;

namespace NO404.Core
{
    /// <summary>
    /// Local-only QA analytics (GDD 20.22). No SDK, no personal data - events are written to
    /// the log buffer and can be exported by the dev console so playtests are measurable
    /// whether or not a real analytics backend is ever added.
    /// </summary>
    public sealed class AnalyticsService
    {
        public static class Events
        {
            public const string TutorialStarted = "tutorial_started";
            public const string TutorialCompleted = "tutorial_completed";
            public const string CaseStarted = "case_started";
            public const string CaseCompleted = "case_completed";
            public const string DecisionSelected = "decision_selected";
            public const string EvidenceAcquired = "evidence_acquired";
            public const string EvidenceMissed = "evidence_missed";
            public const string NightCompleted = "night_completed";
            public const string DeathOrFailure = "death_or_failure";
            public const string EndingReached = "ending_reached";
            public const string AccessibilityChanged = "settings_accessibility_changed";
            public const string DemoCompleted = "demo_completed";
        }

        readonly List<string> _lines = new List<string>(256);

        public void Track(string eventName, string param = null, int value = 0)
        {
            var line = eventName + (string.IsNullOrEmpty(param) ? "" : "|" + param) +
                       (value != 0 ? "|" + value : "");
            _lines.Add(line);
            Log.Trace("Analytics", line);
        }

        public string Export()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _lines.Count; i++) sb.AppendLine(_lines[i]);
            return sb.ToString();
        }

        public int Count { get { return _lines.Count; } }
        public void Clear() { _lines.Clear(); }
    }
}
