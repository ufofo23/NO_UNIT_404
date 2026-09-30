using System.Collections.Generic;
using UnityEngine;
using NO404.Cases;
using NO404.CCTV;
using NO404.Core;
using NO404.Dialogue;
using NO404.Endings;
using NO404.Evidence;
using NO404.Facility;
using NO404.Manual;
using NO404.Anomalies;
using NO404.Phone;
using NO404.Residents;
using NO404.Visitors;

namespace NO404.ContentData
{
    /// <summary>
    /// Single lookup point for every authored definition.
    ///
    /// Content resolution order:
    ///   1. A ContentCatalog asset at Resources/NO404/ContentCatalog, if the designer has
    ///      baked one (Tools > NO404 > Data > Bake Seed Content To Assets).
    ///   2. Otherwise the code-authored seed in SeedContent, so the project is playable
    ///      from a fresh clone with no manual asset wiring.
    /// </summary>
    public sealed class ContentDatabase
    {
        public const string CatalogResourcePath = "NO404/ContentCatalog";

        readonly Dictionary<string, CaseDefinition> _cases = new Dictionary<string, CaseDefinition>();
        readonly Dictionary<string, EvidenceDefinition> _evidence = new Dictionary<string, EvidenceDefinition>();
        readonly Dictionary<string, ResidentDefinition> _residents = new Dictionary<string, ResidentDefinition>();
        readonly Dictionary<string, DialogueDefinition> _dialogues = new Dictionary<string, DialogueDefinition>();
        readonly Dictionary<string, VisitorDefinition> _visitors = new Dictionary<string, VisitorDefinition>();
        readonly Dictionary<string, AnomalyDefinition> _anomalies = new Dictionary<string, AnomalyDefinition>();
        readonly Dictionary<string, PhoneCallDefinition> _calls = new Dictionary<string, PhoneCallDefinition>();
        readonly Dictionary<string, EndingDefinition> _endings = new Dictionary<string, EndingDefinition>();
        readonly Dictionary<string, ManualPage> _manualPages = new Dictionary<string, ManualPage>();
        readonly Dictionary<string, ManualEventDefinition> _manualEvents = new Dictionary<string, ManualEventDefinition>();
        readonly Dictionary<string, AnomalyToolDefinition> _anomalyTools = new Dictionary<string, AnomalyToolDefinition>();

        readonly List<CctvChannelDefinition> _channels = new List<CctvChannelDefinition>();
        readonly List<MeterSeries> _meterSeries = new List<MeterSeries>();

        /// <summary>
        /// Ids that were registered more than once, in "kind:id" form.
        ///
        /// Every collection here is a dictionary keyed by id, so a second definition sharing
        /// an id does not collide - it silently replaces the first, and which one survives is
        /// whichever the seed happened to register last. T03 was authored on two different
        /// nights from v1.5 to v1.7 and the loader quietly threw one of them away; nothing in
        /// the project could have noticed, because by the time any validator or test looks at
        /// Cases the duplicate is already gone.
        ///
        /// So the loader records the collision as it happens. DataValidator turns this into
        /// an error (GDD 21.6).
        /// </summary>
        public IReadOnlyList<string> DuplicateIds { get { return _duplicateIds; } }
        readonly List<string> _duplicateIds = new List<string>();

        void NoteDuplicate(string kind, string id)
        {
            _duplicateIds.Add(kind + ":" + id);
            Log.Error("Content", "duplicate " + kind + " id " + id +
                                 " - the earlier definition was discarded");
        }

        public bool LoadedFromCatalog { get; private set; }

        public IReadOnlyCollection<CaseDefinition> Cases { get { return _cases.Values; } }
        public IReadOnlyCollection<EvidenceDefinition> Evidence { get { return _evidence.Values; } }
        public IReadOnlyCollection<ResidentDefinition> Residents { get { return _residents.Values; } }
        public IReadOnlyCollection<DialogueDefinition> Dialogues { get { return _dialogues.Values; } }
        public IReadOnlyCollection<VisitorDefinition> Visitors { get { return _visitors.Values; } }
        public IReadOnlyCollection<AnomalyDefinition> Anomalies { get { return _anomalies.Values; } }
        public IReadOnlyCollection<PhoneCallDefinition> PhoneCalls { get { return _calls.Values; } }
        public IReadOnlyCollection<EndingDefinition> Endings { get { return _endings.Values; } }
        /// <summary>Pages of the night response manual (v2.1 spec 0.9).</summary>
        public IReadOnlyCollection<ManualPage> ManualPages { get { return _manualPages.Values; } }
        /// <summary>The M01..M18 anomaly events (v2.1 spec 22).</summary>
        public IReadOnlyCollection<ManualEventDefinition> ManualEvents { get { return _manualEvents.Values; } }
        /// <summary>The A01..A05 anomalous tools (v2.1 spec 23).</summary>
        public IReadOnlyCollection<AnomalyToolDefinition> AnomalyTools { get { return _anomalyTools.Values; } }
        public IReadOnlyList<CctvChannelDefinition> CctvChannels { get { return _channels; } }
        public IReadOnlyList<MeterSeries> MeterSeries { get { return _meterSeries; } }

        public void Load()
        {
            Clear();

            var catalog = Resources.Load<ContentCatalog>(CatalogResourcePath);
            if (catalog != null && catalog.HasContent)
            {
                LoadedFromCatalog = true;
                Absorb(catalog.cases, catalog.evidence, catalog.residents, catalog.dialogues, catalog.visitors);
                _channels.AddRange(catalog.cctvChannels);
                for (int i = 0; i < catalog.anomalies.Length; i++) Register(catalog.anomalies[i]);
                for (int i = 0; i < catalog.phoneCalls.Length; i++) Register(catalog.phoneCalls[i]);
                for (int i = 0; i < catalog.endings.Length; i++) Register(catalog.endings[i]);
                for (int i = 0; i < catalog.manualPages.Length; i++) Register(catalog.manualPages[i]);
                for (int i = 0; i < catalog.manualEvents.Length; i++) Register(catalog.manualEvents[i]);
                for (int i = 0; i < catalog.anomalyTools.Length; i++) Register(catalog.anomalyTools[i]);
                _meterSeries.AddRange(SeedContent.BuildMeterSeries());
                Log.Info("Content", "loaded ContentCatalog asset");
            }
            else
            {
                LoadedFromCatalog = false;
                var seed = SeedContent.Build();
                Absorb(seed.Cases, seed.Evidence, seed.Residents, seed.Dialogues, seed.Visitors);
                _channels.AddRange(seed.Channels);
                for (int i = 0; i < seed.Anomalies.Length; i++) Register(seed.Anomalies[i]);
                for (int i = 0; i < seed.PhoneCalls.Length; i++) Register(seed.PhoneCalls[i]);
                for (int i = 0; i < seed.Endings.Length; i++) Register(seed.Endings[i]);
                for (int i = 0; i < seed.ManualPages.Length; i++) Register(seed.ManualPages[i]);
                for (int i = 0; i < seed.ManualEvents.Length; i++) Register(seed.ManualEvents[i]);
                for (int i = 0; i < seed.AnomalyTools.Length; i++) Register(seed.AnomalyTools[i]);
                _meterSeries.AddRange(seed.MeterSeries);
                Log.Info("Content", "loaded code seed content");
            }

            Log.Info("Content", _cases.Count + " cases, " + _evidence.Count + " evidence, " +
                                _residents.Count + " residents, " + _channels.Count + " cameras, " +
                                _anomalies.Count + " anomalies, " + _calls.Count + " calls, " +
                                _endings.Count + " endings, " + _manualPages.Count + " manual pages, " +
                                _manualEvents.Count + " manual events, " +
                                _anomalyTools.Count + " anomaly tools");
        }

        void Absorb(CaseDefinition[] cases, EvidenceDefinition[] evidence, ResidentDefinition[] residents,
                    DialogueDefinition[] dialogues, VisitorDefinition[] visitors)
        {
            if (cases != null)
                for (int i = 0; i < cases.Length; i++)
                    if (cases[i] != null && !string.IsNullOrEmpty(cases[i].caseId))
                    {
                        if (_cases.ContainsKey(cases[i].caseId)) NoteDuplicate("case", cases[i].caseId);
                        _cases[cases[i].caseId] = cases[i];
                    }

            if (evidence != null)
                for (int i = 0; i < evidence.Length; i++)
                    if (evidence[i] != null && !string.IsNullOrEmpty(evidence[i].evidenceId))
                    {
                        if (_evidence.ContainsKey(evidence[i].evidenceId))
                            NoteDuplicate("evidence", evidence[i].evidenceId);
                        _evidence[evidence[i].evidenceId] = evidence[i];
                    }

            if (residents != null)
                for (int i = 0; i < residents.Length; i++)
                    if (residents[i] != null && !string.IsNullOrEmpty(residents[i].residentId))
                    {
                        if (_residents.ContainsKey(residents[i].residentId))
                            NoteDuplicate("resident", residents[i].residentId);
                        _residents[residents[i].residentId] = residents[i];
                    }

            if (dialogues != null)
                for (int i = 0; i < dialogues.Length; i++)
                    if (dialogues[i] != null && !string.IsNullOrEmpty(dialogues[i].conversationId))
                    {
                        if (_dialogues.ContainsKey(dialogues[i].conversationId))
                            NoteDuplicate("dialogue", dialogues[i].conversationId);
                        _dialogues[dialogues[i].conversationId] = dialogues[i];
                    }

            if (visitors != null)
                for (int i = 0; i < visitors.Length; i++)
                    if (visitors[i] != null && !string.IsNullOrEmpty(visitors[i].visitorId))
                    {
                        if (_visitors.ContainsKey(visitors[i].visitorId))
                            NoteDuplicate("visitor", visitors[i].visitorId);
                        _visitors[visitors[i].visitorId] = visitors[i];
                    }
        }

        void Register(AnomalyDefinition anomaly)
        {
            if (anomaly == null || string.IsNullOrEmpty(anomaly.anomalyId)) return;
            if (_anomalies.ContainsKey(anomaly.anomalyId)) NoteDuplicate("anomaly", anomaly.anomalyId);
            _anomalies[anomaly.anomalyId] = anomaly;
        }

        void Register(PhoneCallDefinition call)
        {
            if (call == null || string.IsNullOrEmpty(call.callId)) return;
            if (_calls.ContainsKey(call.callId)) NoteDuplicate("call", call.callId);
            _calls[call.callId] = call;
        }

        void Register(EndingDefinition ending)
        {
            if (ending == null || string.IsNullOrEmpty(ending.endingId)) return;
            if (_endings.ContainsKey(ending.endingId)) NoteDuplicate("ending", ending.endingId);
            _endings[ending.endingId] = ending;
        }

        void Register(ManualPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.pageId)) return;
            if (_manualPages.ContainsKey(page.pageId)) NoteDuplicate("manual page", page.pageId);
            _manualPages[page.pageId] = page;
        }

        void Register(ManualEventDefinition manualEvent)
        {
            if (manualEvent == null || string.IsNullOrEmpty(manualEvent.eventId)) return;
            if (_manualEvents.ContainsKey(manualEvent.eventId))
                NoteDuplicate("manual event", manualEvent.eventId);
            _manualEvents[manualEvent.eventId] = manualEvent;
        }

        void Register(AnomalyToolDefinition tool)
        {
            if (tool == null || string.IsNullOrEmpty(tool.toolId)) return;
            if (_anomalyTools.ContainsKey(tool.toolId)) NoteDuplicate("anomaly tool", tool.toolId);
            _anomalyTools[tool.toolId] = tool;
        }

        void Clear()
        {
            _duplicateIds.Clear();
            _calls.Clear();
            _endings.Clear();
            _cases.Clear();
            _evidence.Clear();
            _residents.Clear();
            _dialogues.Clear();
            _visitors.Clear();
            _anomalies.Clear();
            _channels.Clear();
            _meterSeries.Clear();
            _manualPages.Clear();
            _manualEvents.Clear();
            _anomalyTools.Clear();
        }

        public CaseDefinition FindCase(string id) { return Lookup(_cases, id); }
        public EvidenceDefinition FindEvidence(string id) { return Lookup(_evidence, id); }
        public ResidentDefinition FindResident(string id) { return Lookup(_residents, id); }
        public DialogueDefinition FindDialogue(string id) { return Lookup(_dialogues, id); }
        public VisitorDefinition FindVisitor(string id) { return Lookup(_visitors, id); }
        public AnomalyDefinition FindAnomaly(string id) { return Lookup(_anomalies, id); }
        public PhoneCallDefinition FindPhoneCall(string id) { return Lookup(_calls, id); }
        public EndingDefinition FindEnding(string id) { return Lookup(_endings, id); }
        public ManualPage FindManualPage(string id) { return Lookup(_manualPages, id); }
        public ManualEventDefinition FindManualEvent(string id) { return Lookup(_manualEvents, id); }
        public AnomalyToolDefinition FindAnomalyTool(string id) { return Lookup(_anomalyTools, id); }

        public CctvChannelDefinition FindChannel(string cameraId)
        {
            for (int i = 0; i < _channels.Count; i++)
                if (_channels[i].cameraId == cameraId) return _channels[i];
            return null;
        }

        static T Lookup<T>(Dictionary<string, T> map, string id) where T : class
        {
            if (string.IsNullOrEmpty(id)) return null;
            T value;
            return map.TryGetValue(id, out value) ? value : null;
        }
    }

    /// <summary>
    /// Optional baked asset. Empty by default; the editor bake tool fills it so designers can
    /// edit content in the inspector instead of in code.
    /// </summary>
    [CreateAssetMenu(menuName = "NO404/Content Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        public CaseDefinition[] cases = new CaseDefinition[0];
        public EvidenceDefinition[] evidence = new EvidenceDefinition[0];
        public ResidentDefinition[] residents = new ResidentDefinition[0];
        public DialogueDefinition[] dialogues = new DialogueDefinition[0];
        public VisitorDefinition[] visitors = new VisitorDefinition[0];
        public CctvChannelDefinition[] cctvChannels = new CctvChannelDefinition[0];
        public AnomalyDefinition[] anomalies = new AnomalyDefinition[0];
        public PhoneCallDefinition[] phoneCalls = new PhoneCallDefinition[0];
        public EndingDefinition[] endings = new EndingDefinition[0];
        public ManualPage[] manualPages = new ManualPage[0];
        public ManualEventDefinition[] manualEvents = new ManualEventDefinition[0];
        public AnomalyToolDefinition[] anomalyTools = new AnomalyToolDefinition[0];

        public bool HasContent { get { return cases != null && cases.Length > 0; } }
    }
}
