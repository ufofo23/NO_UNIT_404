using System.Collections.Generic;
using NO404.Core;

namespace NO404.Facility
{
    public enum MeterKind { Power = 0, Water = 1, Heating = 2, Elevator = 3, Fire = 4, CctvNetwork = 5 }

    /// <summary>One hourly sample of a meter series. Axis units live in the localization table.</summary>
    public struct MeterSample
    {
        public int GameSecond;
        public float Value;
        public MeterSample(int gameSecond, float value) { GameSecond = gameSecond; Value = value; }
    }

    public sealed class MeterSeries
    {
        public string SeriesId;      // e.g. "803", "404"
        public string LabelKey;
        public MeterKind Kind;
        public string UnitKey;
        public bool Hidden;          // 404 series before the reveal
        public string RevealFlagId;
        /// <summary>Evidence granted the first time the player actually looks at this series.</summary>
        public string EvidenceId;
        public readonly List<MeterSample> Samples = new List<MeterSample>();

        public float Max
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < Samples.Count; i++) if (Samples[i].Value > max) max = Samples[i].Value;
                return max;
            }
        }

        public float ValueAt(int gameSecond)
        {
            float value = 0f;
            for (int i = 0; i < Samples.Count; i++)
            {
                if (Samples[i].GameSecond > gameSecond) break;
                value = Samples[i].Value;
            }
            return value;
        }
    }

    /// <summary>
    /// Backs the Facility app graphs (GDD 16.12) and the night-5 power budget (GDD C10).
    /// Series are plain data so the placeholder line renderer and a real chart widget can
    /// share the same source.
    /// </summary>
    public sealed class FacilityMeterService
    {
        readonly ContentData.ContentDatabase _content;
        readonly List<MeterSeries> _series = new List<MeterSeries>();
        readonly Dictionary<string, bool> _circuits = new Dictionary<string, bool>();

        public FacilityMeterService(ContentData.ContentDatabase content)
        {
            _content = content;
            BuildFromContent();
        }

        public IReadOnlyList<MeterSeries> AllSeries { get { return _series; } }

        /// <summary>Total load the building can carry during the night-5 outage.</summary>
        public float PowerBudget = 100f;

        void BuildFromContent()
        {
            _series.Clear();
            if (_content == null) return;

            var source = _content.MeterSeries;
            for (int i = 0; i < source.Count; i++) _series.Add(source[i]);
        }

        public void Register(MeterSeries series)
        {
            if (series == null) return;
            _series.Add(series);
        }

        public List<MeterSeries> VisibleSeries(MeterKind kind)
        {
            var result = new List<MeterSeries>();
            for (int i = 0; i < _series.Count; i++)
            {
                var s = _series[i];
                if (s.Kind != kind) continue;
                if (s.Hidden && (string.IsNullOrEmpty(s.RevealFlagId) || !ServiceHub.State.GetFlag(s.RevealFlagId)))
                    continue;
                result.Add(s);
            }
            return result;
        }

        public MeterSeries Find(MeterKind kind, string seriesId)
        {
            for (int i = 0; i < _series.Count; i++)
                if (_series[i].Kind == kind && _series[i].SeriesId == seriesId) return _series[i];
            return null;
        }

        // ---- circuits (night 5 power distribution) -----------------------

        public void RegisterCircuit(string circuitId, bool enabled)
        {
            if (string.IsNullOrEmpty(circuitId)) return;
            _circuits[circuitId] = enabled;
        }

        public bool HasCircuit(string circuitId)
        {
            return !string.IsNullOrEmpty(circuitId) && _circuits.ContainsKey(circuitId);
        }

        public bool IsCircuitOn(string circuitId)
        {
            bool on;
            return _circuits.TryGetValue(circuitId, out on) && on;
        }

        /// <summary>Circuits currently switched on. Night 5 allows at most three (GDD 9.6).</summary>
        public int ActiveCircuitCount
        {
            get
            {
                int n = 0;
                foreach (var pair in _circuits) if (pair.Value) n++;
                return n;
            }
        }

        /// <summary>
        /// Switches a circuit on within the night-5 budget. Returns false when the limit is
        /// reached, so the UI can tell the player something must be given up first.
        /// </summary>
        public bool TrySetCircuit(string circuitId, bool on)
        {
            if (!_circuits.ContainsKey(circuitId)) return false;
            if (on && !IsCircuitOn(circuitId) && ActiveCircuitCount >= CircuitIds.MaxSimultaneous) return false;

            _circuits[circuitId] = on;
            Log.Info("Facility", "circuit " + circuitId + " = " + on +
                                 " (" + ActiveCircuitCount + "/" + CircuitIds.MaxSimultaneous + ")");
            return true;
        }

        public void SetCircuit(string circuitId, bool on)
        {
            if (!_circuits.ContainsKey(circuitId)) return;
            _circuits[circuitId] = on;
            Log.Info("Facility", "circuit " + circuitId + " = " + on);
        }

        public IEnumerable<string> Circuits { get { return _circuits.Keys; } }

        public void Reset()
        {
            _circuits.Clear();
            BuildFromContent();
        }

        public void LoadFrom(IEnumerable<Save.CircuitSaveEntry> circuits)
        {
            if (circuits == null) return;
            foreach (var c in circuits) _circuits[c.circuitId] = c.on;
        }
    }
}
