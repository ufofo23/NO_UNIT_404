using System.Collections.Generic;
using UnityEngine;
using NO404.Cases;
using NO404.CCTV;
using NO404.ContentData;
using NO404.Dialogue;
using NO404.Endings;
using NO404.Evidence;
using NO404.Facility;
using NO404.Anomalies;
using NO404.Gameplay;
using NO404.Manual;
using NO404.Phone;
using NO404.Pressure;
using NO404.Residents;
using NO404.Save;
using NO404.Threat;
using NO404.Visitors;

namespace NO404.Core
{
    /// <summary>
    /// Explicit service registry. Deliberately not a DI container (GDD 20.3 forbids one):
    /// it is a flat set of properties assigned once, in the fixed order from GDD 20.6.
    /// Nothing here uses GameObject.Find or string lookups.
    /// </summary>
    public static class ServiceHub
    {
        public static bool Ready { get; private set; }

        public static SettingsService Settings { get; private set; }
        public static LocalizationService Localization { get; private set; }
        public static SaveService Save { get; private set; }
        public static SteamService Steam { get; private set; }
        public static AudioService Audio { get; private set; }
        public static SceneService Scene { get; private set; }
        public static GameStateService State { get; private set; }

        /// <summary>The caretaker's body and nerve, and the gate they put on the ending (v5.0 6-8).</summary>
        public static VitalService Vitals { get; private set; }
        public static GameClock Clock { get; private set; }
        public static ContentDatabase Content { get; private set; }
        public static CaseService Cases { get; private set; }

        /// <summary>Which of a night's fifteen quests actually happen (v5.0 4).</summary>
        public static Cases.NightPoolService NightPool { get; private set; }
        public static EvidenceService Evidence { get; private set; }
        public static ResidentDatabase Residents { get; private set; }
        public static ResidentTrafficService Traffic { get; private set; }
        public static AccessLogService AccessLog { get; private set; }
        public static FacilityMeterService Facility { get; private set; }
        public static CctvService Cctv { get; private set; }
        public static DialogueService Dialogue { get; private set; }
        public static InterphoneService Interphone { get; private set; }

        /// <summary>
        /// The people who are already inside (v3.0 38). Built after the interphone because
        /// a grant hands straight over to it.
        /// </summary>
        public static ActiveVisitorService ActiveVisitors { get; private set; }
        public static PhoneService Phone { get; private set; }
        public static EndingService Endings { get; private set; }
        public static ThreatService Threat { get; private set; }
        public static NightPowerService Power { get; private set; }
        public static NightPressureService Pressure { get; private set; }
        public static ZoneStreamer Zones { get; private set; }
        /// <summary>Where the player is in the stairwell (v2.1 spec 0.8).</summary>
        public static StairNavigator Stairs { get; private set; }
        /// <summary>Floor risk, distortion exposure and the manual violation count (spec 0.10).</summary>
        public static RiskService Risk { get; private set; }
        /// <summary>The night response manual (spec 0.9).</summary>
        public static ManualService Manual { get; private set; }
        /// <summary>The M01..M18 anomaly events (spec 22).</summary>
        public static ManualEventService ManualEvents { get; private set; }
        /// <summary>The A01..A05 anomalous tools (spec 23).</summary>
        public static AnomalyToolService AnomalyTools { get; private set; }
        /// <summary>M07 holding the fifth floor and the shaft above it (spec 0.8.4).</summary>
        public static CorridorLoopDirector CorridorLoop { get; private set; }
        /// <summary>What the caretaker is carrying, made visible (spec 0.10.4).</summary>
        public static DistortionDirector Distortion { get; private set; }
        public static HintService Hints { get; private set; }
        public static CaptionService Captions { get; private set; }
        public static AnalyticsService Analytics { get; private set; }
        public static PlayerTracker Player { get; private set; }

        /// <summary>
        /// Where every caretaker on shift is standing (v3.0 34). In a one-handed shift
        /// this is one entry and behaves exactly as <see cref="Player"/> used to.
        /// </summary>
        public static PlayerPresence Presence { get; private set; }
        /// <summary>Called once by Bootstrap. Order matches GDD 20.6.</summary>
        public static void Initialize(MonoBehaviour runner, Transform persistentRoot)
        {
            if (Ready) return;

            Log.MinimumLevel = Application.isEditor ? LogLevel.Trace : LogLevel.Info;
            Log.Info("Boot", "service initialization started");

            Settings = new SettingsService();
            Settings.Load();

            Localization = new LocalizationService();
            Localization.Initialize(Settings.Current.language);

            Save = new SaveService();

            Steam = new SteamService();
            Steam.Initialize();

            Audio = new AudioService(persistentRoot, Settings);
            Scene = new SceneService(runner);
            Zones = new ZoneStreamer(runner);

            State = new GameStateService();
            Vitals = new VitalService();
            Clock = new GameClock();
            Presence = new PlayerPresence();
            Player = new PlayerTracker();

            Content = new ContentDatabase();
            Content.Load();

            Evidence = new EvidenceService(Content);
            Residents = new ResidentDatabase(Content);
            Traffic = new ResidentTrafficService();
            AccessLog = new AccessLogService();
            Facility = new FacilityMeterService(Content);
            Cctv = new CctvService(Content, Clock);
            Dialogue = new DialogueService(Content);
            Interphone = new InterphoneService(Content);
            ActiveVisitors = new ActiveVisitorService();
            Phone = new PhoneService(Content, Clock);
            Endings = new EndingService(Content);
            Threat = new ThreatService();

            // The two services that act without being asked (GDD 15.4 / 15.5). Power is built
            // first because the pressure ladder reads the reserve every game minute.
            Power = new NightPowerService();
            Pressure = new NightPressureService();

            // v2.1. Risk and the manual are built before CaseService because a case
            // consequence can raise a floor's risk or hand over a manual page the moment it
            // applies, and the very first one can fire on the opening night's first minute.
            Stairs = new StairNavigator();
            Risk = new RiskService(State);
            Manual = new ManualService(Content, State);
            Hints = new HintService();
            Captions = new CaptionService();
            NightPool = new Cases.NightPoolService();
            Cases = new CaseService(Content, Clock, State);
            ManualEvents = new ManualEventService(Content, Clock, State, Manual, Risk);
            AnomalyTools = new AnomalyToolService(Content, Clock, State);
            Distortion = new DistortionDirector(Risk, Clock);
            CorridorLoop = new CorridorLoopDirector(Stairs, Risk, Distortion);
            CorridorLoop.Enable();
            Analytics = new AnalyticsService();

            Settings.OnApplied += ignored => Audio.ApplySettings();

            Ready = true;
            Log.Info("Boot", "service initialization complete");
        }

        public static void Shutdown()
        {
            if (!Ready) return;
            if (Steam != null) Steam.Shutdown();
            if (CorridorLoop != null) CorridorLoop.Disable();
            EventBus.Clear();
            Ready = false;
        }

        /// <summary>Resets per-playthrough state. Settings, Steam and content survive.</summary>
        public static void ResetPlaythrough()
        {
            // A conversation left open would otherwise survive into the new playthrough and
            // keep blocking case scheduling and phone calls.
            Dialogue.End();

            State.ResetToNewGame();
            Vitals.ResetToNewGame();
            Clock.SetGameSecond(GameClock.ShiftStartSecond);
            Player.Reset();
            Presence.Reset();
            Evidence.Reset();
            Cases.Reset();
            NightPool.Reset();
            AccessLog.Reset();
            Facility.Reset();
            Cctv.Reset();
            Interphone.Reset();
            ActiveVisitors.Reset();
            Residents.Reset();
            Traffic.Reset();
            Phone.Reset();
            Endings.Reset();
            Threat.Reset();
            Power.Reset();
            Pressure.Reset();
            Stairs.Reset();
            if (CorridorLoop != null) CorridorLoop.Disable();
            Manual.Reset();
            ManualEvents.Reset();
            AnomalyTools.Reset();
            Distortion.Reset();
            if (CorridorLoop != null) CorridorLoop.Enable();
            Hints.Reset();
            Captions.Clear();

            // World objects (evidence pickups, one-shot dialogue triggers) live in the
            // always-resident office/lobby scene and survive this reset; they clear their own
            // latched state in response to this event instead of being re-created.
            EventBus.Publish(new PlaythroughResetEvent());
        }
    }

    /// <summary>
    /// Small record of what the player has done. Backs the PlayerEnteredZone / AppOpened /
    /// DialogueChoiceSelected conditions in GDD 20.10.
    /// </summary>
    public sealed class PlayerTracker
    {
        readonly HashSet<string> _visitedZones = new HashSet<string>();
        readonly HashSet<string> _openedApps = new HashSet<string>();
        readonly HashSet<string> _dialogueChoices = new HashSet<string>();
        readonly HashSet<string> _viewedRecords = new HashSet<string>();
        readonly HashSet<string> _viewedChannels = new HashSet<string>();

        public string CurrentZone { get; private set; }
        public bool InPcMode { get; private set; }
        public string CurrentAppId { get; private set; }

        public PlayerTracker()
        {
            CurrentZone = ZoneIds.Office;
            CurrentAppId = AppIds.Home;
        }

        public void EnterZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || CurrentZone == zoneId) return;
            CurrentZone = zoneId;
            _visitedZones.Add(zoneId);

            // The shift's roster is what the building's own rules read (v3.0 34).
            if (ServiceHub.Presence != null)
                ServiceHub.Presence.Report(ServiceHub.Presence.LocalPlayerId, zoneId);

            EventBus.Publish(new ZoneChangedEvent(zoneId));
        }

        public void SetPcMode(bool active)
        {
            if (InPcMode == active) return;
            InPcMode = active;
            EventBus.Publish(new PcModeChangedEvent(active));
        }

        public void OpenApp(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return;
            CurrentAppId = appId;
            _openedApps.Add(appId);
            EventBus.Publish(new AppOpenedEvent(appId));
        }

        public void ViewRecord(string residentId)
        {
            if (string.IsNullOrEmpty(residentId)) return;
            if (_viewedRecords.Add(residentId))
                EventBus.Publish(new ResidentRecordViewedEvent(residentId));
        }

        public void ViewChannel(string cameraId)
        {
            if (string.IsNullOrEmpty(cameraId)) return;
            _viewedChannels.Add(cameraId);
            // Published every time rather than only on the first view: the v2.1 prohibitions
            // are about looking now, not about having ever looked (spec 22 M01/M02).
            EventBus.Publish(new CctvChannelViewedEvent(cameraId));
        }

        public void SelectChoice(string conversationId, string choiceId)
        {
            if (string.IsNullOrEmpty(choiceId)) return;
            _dialogueChoices.Add(conversationId + "/" + choiceId);
            _dialogueChoices.Add(choiceId);
            EventBus.Publish(new DialogueChoiceSelectedEvent(conversationId, choiceId));
        }

        public bool HasVisited(string zoneId) { return _visitedZones.Contains(zoneId); }
        public bool HasOpenedApp(string appId) { return _openedApps.Contains(appId); }
        public bool HasChosen(string choiceKey) { return _dialogueChoices.Contains(choiceKey); }
        public bool HasViewedRecord(string residentId) { return _viewedRecords.Contains(residentId); }
        public bool HasViewedChannel(string cameraId) { return _viewedChannels.Contains(cameraId); }

        public IEnumerable<string> VisitedZones { get { return _visitedZones; } }
        public IEnumerable<string> OpenedApps { get { return _openedApps; } }
        public IEnumerable<string> DialogueChoices { get { return _dialogueChoices; } }

        public void Reset()
        {
            _visitedZones.Clear();
            _openedApps.Clear();
            _dialogueChoices.Clear();
            _viewedRecords.Clear();
            _viewedChannels.Clear();
            CurrentZone = ZoneIds.Office;
            CurrentAppId = AppIds.Home;
            InPcMode = false;
        }

        public void LoadFrom(IEnumerable<string> zones, IEnumerable<string> apps, IEnumerable<string> choices)
        {
            Reset();
            if (zones != null) foreach (var z in zones) _visitedZones.Add(z);
            if (apps != null) foreach (var a in apps) _openedApps.Add(a);
            if (choices != null) foreach (var c in choices) _dialogueChoices.Add(c);
        }
    }
}
