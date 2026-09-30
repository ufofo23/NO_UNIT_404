using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NO404.Core
{
    /// <summary>
    /// Minimal key/value localization built on a CSV string table (GDD 20.18 key rules).
    /// The table lives at Resources/NO404/strings.csv with the columns: key,ko,en.
    ///
    /// This is deliberately a thin layer with the same call shape as Unity Localization
    /// (`Loc.T("ui.home.title")`) so that swapping in the package later touches this file only.
    /// No user-visible string is hard-coded anywhere else in the project.
    /// </summary>
    public sealed class LocalizationService
    {
        public const string ResourcePath = "NO404/strings";
        public const string DefaultLanguage = "ko";

        static LocalizationService _instance;
        public static LocalizationService Instance { get { return _instance; } }

        readonly Dictionary<string, string[]> _rows = new Dictionary<string, string[]>(512);
        readonly List<string> _languages = new List<string>();
        int _languageColumn;

        public string Language { get; private set; }
        public event Action OnLanguageChanged;
        public IReadOnlyList<string> AvailableLanguages { get { return _languages; } }

        /// <summary>Keys requested but not present. Surfaced by the data validator.</summary>
        public readonly HashSet<string> MissingKeys = new HashSet<string>();

        public void Initialize(string language)
        {
            _instance = this;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Log.Error("Loc", "String table not found at Resources/" + ResourcePath + ".csv");
            }
            else
            {
                Parse(asset.text);
            }

            SetLanguage(string.IsNullOrEmpty(language) ? DefaultLanguage : language);
        }

        public void SetLanguage(string language)
        {
            int index = _languages.IndexOf(language);
            if (index < 0)
            {
                index = _languages.IndexOf(DefaultLanguage);
                if (index < 0) index = 0;
                language = _languages.Count > 0 ? _languages[index] : DefaultLanguage;
            }

            Language = language;
            _languageColumn = index < 0 ? 0 : index;

            var cb = OnLanguageChanged;
            if (cb != null) cb();
            Log.Info("Loc", "language = " + Language);
        }

        public bool HasKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _rows.ContainsKey(key);
        }

        /// <summary>
        /// Resolve a key. In the editor an unknown key returns a loud placeholder so it is
        /// visible during play; in a release build it falls back to the key's leaf token
        /// and logs a warning (GDD 20.12 safety rules).
        /// </summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            string[] columns;
            if (_rows.TryGetValue(key, out columns))
            {
                if (_languageColumn < columns.Length && !string.IsNullOrEmpty(columns[_languageColumn]))
                    return columns[_languageColumn];
                // Fall back to the source language column.
                if (columns.Length > 0 && !string.IsNullOrEmpty(columns[0])) return columns[0];
            }

            if (MissingKeys.Add(key)) Log.Warn("Loc", "missing key: " + key);
#if UNITY_EDITOR
            return "#" + key + "#";
#else
            int dot = key.LastIndexOf('.');
            return dot >= 0 && dot < key.Length - 1 ? key.Substring(dot + 1) : key;
#endif
        }

        /// <summary>
        /// Formatted lookup. Concatenating translated fragments is forbidden by the GDD, so
        /// every composed sentence must come from one key with numbered placeholders.
        /// </summary>
        public string Get(string key, params object[] args)
        {
            var pattern = Get(key);
            if (args == null || args.Length == 0) return pattern;
            try { return string.Format(pattern, args); }
            catch (FormatException) { return pattern; }
        }

        void Parse(string csv)
        {
            _rows.Clear();
            _languages.Clear();

            var lines = SplitLines(csv);
            if (lines.Count == 0) return;

            var header = SplitCsvLine(lines[0]);
            for (int i = 1; i < header.Count; i++) _languages.Add(header[i].Trim());

            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                var cells = SplitCsvLine(line);
                if (cells.Count < 2) continue;

                var key = cells[0].Trim();
                if (key.Length == 0) continue;

                var values = new string[_languages.Count];
                for (int c = 0; c < values.Length; c++)
                    values[c] = (c + 1) < cells.Count ? cells[c + 1] : string.Empty;

                _rows[key] = values;
            }

            Log.Info("Loc", "loaded " + _rows.Count + " keys, languages: " + string.Join(",", _languages.ToArray()));
        }

        static List<string> SplitLines(string text)
        {
            var result = new List<string>(512);
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"') { inQuotes = !inQuotes; sb.Append(c); continue; }
                if (!inQuotes && (c == '\n' || c == '\r'))
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    result.Add(sb.ToString());
                    sb.Length = 0;
                    continue;
                }
                sb.Append(c);
            }

            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        static List<string> SplitCsvLine(string line)
        {
            var cells = new List<string>(4);
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { cells.Add(sb.ToString()); sb.Length = 0; }
                    else sb.Append(c);
                }
            }

            cells.Add(sb.ToString());
            return cells;
        }
    }

    /// <summary>Short call site helper: <c>Loc.T("ui.home.title")</c>.</summary>
    public static class Loc
    {
        public static string T(string key)
        {
            var svc = LocalizationService.Instance;
            return svc != null ? svc.Get(key) : key;
        }

        public static string T(string key, params object[] args)
        {
            var svc = LocalizationService.Instance;
            return svc != null ? svc.Get(key, args) : key;
        }
    }
}
