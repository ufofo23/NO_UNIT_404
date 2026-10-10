using System;
using System.Collections.Generic;
using NO404.ContentData;
using NO404.Core;

namespace NO404.CCTV
{
    /// <summary>
    /// Channel logic, the 60 second rewind buffer, anomaly scheduling and snapshots
    /// (GDD 12). Rendering lives in CctvRig; this class never touches cameras so it can be
    /// unit tested and so unrendered channels still run their event scripts (GDD 20.13).
    /// </summary>
    public sealed class CctvService
    {
        public const int RewindSeconds = 60;

        /// <summary>
        /// How long after an event ends the player may still name it. Matched to the rewind
        /// buffer on purpose: if the footage is still reachable, the claim should be too
        /// (GDD 12.4 - core anomalies must be re-checkable).
        /// </summary>
        public const int ReportGraceSeconds = RewindSeconds;

        // What naming it right, naming it wrong, and crying wolf are each worth.
        public const int ReportCorrectPerformance = 2;
        public const int ReportWrongCategoryPerformance = -1;
        public const int ReportNothingTherePerformance = -2;

        /// <summary>
        /// The night of the fire (GDD 4.2). GDD 12.3 #19 and #36 put it back on the timestamp,
        /// and GDD 2.3 hook #8 is every channel reading it at once on the last night.
        /// </summary>
        public const string ArchiveDate = "2009-11-07";
        static readonly int[] ArchiveStampTypes = { 19, 36 };

        public sealed class Channel
        {
            public CctvChannelDefinition Definition;
            public FeedState State = FeedState.Live;
            public string ActiveAnomalyId = string.Empty;
            public int AnomalyEndsAtGameSecond;
            public bool Motion;
            /// <summary>Set when a P0 anomaly fired while the player was in another app (GDD 12.4).</summary>
            public bool UnreviewedMotion;
            public readonly Queue<FeedSample> Rewind = new Queue<FeedSample>(RewindSeconds + 4);

            public string CameraId { get { return Definition != null ? Definition.cameraId : string.Empty; } }
        }

        readonly ContentDatabase _content;
        readonly GameClock _clock;
        readonly List<Channel> _channels = new List<Channel>(12);
        readonly Dictionary<string, Channel> _byId = new Dictionary<string, Channel>(12);
        readonly List<CctvSnapshot> _snapshots = new List<CctvSnapshot>();
        readonly HashSet<string> _firedAnomalies = new HashSet<string>();
        readonly List<Sighting> _sightings = new List<Sighting>(16);

        /// <summary>
        /// GDD 12.3 catalogue numbers the player has correctly filed at least once, across the
        /// whole playthrough (GDD 3.2 "이상 기록부").
        ///
        /// This is the only thing in the game that visibly accumulates. Thirty-six anomaly
        /// types were authored and none of them were ever shown to the player as a set, so a
        /// caretaker who filed one had no way of knowing whether they had seen two of these or
        /// thirty - which turns the whole surveillance half of the game into a chore with no
        /// horizon. A list with gaps in it is a reason to keep looking; a list you cannot see
        /// is not.
        ///
        /// It survives the night. It is playthrough progress, not shift progress.
        /// </summary>
        readonly HashSet<int> _catalogued = new HashSet<int>();

        public int CataloguedCount { get { return _catalogued.Count; } }
        public bool IsCatalogued(int typeNumber) { return _catalogued.Contains(typeNumber); }
        public IEnumerable<int> Catalogued { get { return _catalogued; } }

        int _lastSampledSecond = -1;
        int _snapshotCounter;

        public CctvService(ContentDatabase content, GameClock clock)
        {
            _content = content;
            _clock = clock;
            BuildChannels();
        }

        void BuildChannels()
        {
            _channels.Clear();
            _byId.Clear();

            foreach (var definition in _content.CctvChannels)
            {
                if (definition == null || string.IsNullOrEmpty(definition.cameraId)) continue;
                var channel = new Channel { Definition = definition };
                _channels.Add(channel);
                _byId[definition.cameraId] = channel;
            }
        }

        public IReadOnlyList<Channel> Channels { get { return _channels; } }
        public IReadOnlyList<CctvSnapshot> Snapshots { get { return _snapshots; } }

        /// <summary>Channel currently shown large. Drives the render resolution tiers.</summary>
        public string SelectedCameraId { get; private set; }
        public bool GridMode { get; private set; } = true;
        /// <summary>Index of the visible 6-tile page in grid mode.</summary>
        public int GridPage { get; private set; }

        public Channel Find(string cameraId)
        {
            if (string.IsNullOrEmpty(cameraId)) return null;
            Channel channel;
            return _byId.TryGetValue(cameraId, out channel) ? channel : null;
        }

        /// <summary>
        /// Puts a channel on this caretaker's monitor.
        ///
        /// Which tile somebody is looking at is theirs alone - three people watching three
        /// different corridors is most of what a co-op shift is for - so the selection stays
        /// local and only the consequences of having looked go to the host.
        /// </summary>
        public void Select(string cameraId)
        {
            SelectedCameraId = cameraId;
            var channel = Find(cameraId);
            if (channel == null) return;

            channel.UnreviewedMotion = false;
            ServiceHub.Player.ViewChannel(cameraId);

            Net.NetShift.Request(Net.NetShift.ShiftAct.ViewChannel, cameraId);
        }

        /// <summary>
        /// Somebody on shift pulled this channel up (v3.0 46.1).
        ///
        /// Everything here writes state the whole shift reads - a case objective, a door check,
        /// an anomaly's observed flag - so it runs on the host and comes back in the mirror. It
        /// deliberately does not ask *which* caretaker looked: GDD 13.2 wants two independent
        /// facts checked before a caller is judged, and a fact one of them checked is checked.
        /// </summary>
        public void NoteChannelViewed(string cameraId)
        {
            var channel = Find(cameraId);
            if (channel == null) return;

            channel.UnreviewedMotion = false;
            ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.ViewCctvChannel, cameraId);

            // Half the visitor checks in the game point at CCTV as where to confirm them
            // (GDD 13.2), but looking never counted - only the resident database and the
            // access log did. A camera route the player actually pulled up is a check.
            ServiceHub.Interphone.MarkChecked("cctv." + cameraId);

            // Observing an active anomaly counts as seeing it.
            if (!string.IsNullOrEmpty(channel.ActiveAnomalyId)) RegisterObservation(channel, channel.ActiveAnomalyId);
        }

        public void SetGridMode(bool grid) { GridMode = grid; }

        public void SetGridPage(int page)
        {
            int pages = Math.Max(1, (_channels.Count + 5) / 6);
            GridPage = ((page % pages) + pages) % pages;
        }

        /// <summary>The six channels drawn in the current grid page.</summary>
        public List<Channel> VisibleGridChannels()
        {
            var result = new List<Channel>(6);
            int start = GridPage * 6;
            for (int i = start; i < start + 6 && i < _channels.Count; i++) result.Add(_channels[i]);
            return result;
        }

        /// <summary>
        /// An endless run replays the authored anomaly nights, so the "already fired" set has
        /// to be forgotten each shift or night 7 onwards would be perfectly, silently calm.
        /// The campaign keeps its memory: an anomaly fires once per playthrough.
        /// </summary>
        public void BeginNight(int nightIndex)
        {
            _sightings.Clear();

            // A blackout belongs to the shift that scripted it. Clearing it here rather than
            // trusting whoever cut it to put it back means no later night can ever open on a
            // wall that a previous one left dead.
            FeedsCut = false;
            _cutAtGameSecond = -1;
            _blackoutSeconds = 0;
            for (int i = 0; i < _channels.Count; i++) _channels[i].State = FeedState.Live;

            if (ServiceHub.State.EndlessMode) _firedAnomalies.Clear();
        }

        // ---- per-frame ----------------------------------------------------

        public void Tick()
        {
            int now = _clock.GameSecond;
            if (now == _lastSampledSecond) return;
            _lastSampledSecond = now;

            TryFireAnomalies(now);
            PruneSightings(now);
            RefreshArchiveStamp();

            for (int i = 0; i < _channels.Count; i++)
            {
                var channel = _channels[i];

                if (!string.IsNullOrEmpty(channel.ActiveAnomalyId) && now >= channel.AnomalyEndsAtGameSecond)
                    EndAnomaly(channel);
            }

            RecordSamples(now);
        }

        /// <summary>
        /// Fills the rewind buffer and nothing else. What a client runs instead of Tick.
        ///
        /// The sixty seconds behind each channel is not simulation - it is a record of states
        /// that arrived over the wire - so every copy can and must build its own. Without it a
        /// joining caretaker scrubs the slider on a dead buffer, which on night 1 means the
        /// CAM-06 snapshot C01 asks for cannot be aimed at anything (GDD 12.4).
        /// </summary>
        public void SampleFeedsOnly()
        {
            int now = _clock.GameSecond;
            if (now == _lastSampledSecond) return;
            _lastSampledSecond = now;

            RecordSamples(now);
        }

        void RecordSamples(int now)
        {
            for (int i = 0; i < _channels.Count; i++)
            {
                var channel = _channels[i];
                channel.Rewind.Enqueue(new FeedSample(now, channel.State, channel.ActiveAnomalyId, channel.Motion));
                while (channel.Rewind.Count > RewindSeconds) channel.Rewind.Dequeue();
            }
        }

        void TryFireAnomalies(int now)
        {
            // Nothing may happen on a channel that is showing nothing. An anomaly spent
            // against the blackout would be an anomaly the player could not have seen, which
            // is exactly what GDD 12.4 forbids.
            if (FeedsCut) return;

            foreach (var anomaly in _content.Anomalies)
            {
                if (anomaly == null || _firedAnomalies.Contains(anomaly.anomalyId)) continue;
                if (anomaly.nightIndex != AnomalyNight()) continue;

                // dev.mainonly: the wall shows what the main quest put there and nothing else.
                // Not marked as fired, so the same anomaly is still waiting on the same
                // channel the next time the shift runs without the switch on.
                if (MainOnlyMode.Suppresses(anomaly.caseId)) continue;

                // Some anomalies are the result of something the caretaker did, and have no
                // business on the wall until they have done it.
                string unmet;
                if (!Cases.ConditionEvaluator.EvaluateAll(anomaly.conditions, out unmet)) continue;

                // Time the wall was dark is time the window did not spend.
                int windowEnd = anomaly.windowEnd > 0 ? anomaly.windowEnd + _blackoutSeconds : 0;

                if (now < anomaly.windowBegin) continue;
                if (windowEnd > 0 && now > windowEnd) continue;

                var channel = Find(anomaly.cameraId);
                if (channel == null || !string.IsNullOrEmpty(channel.ActiveAnomalyId)) continue;

                // GDD 12.4: an anomaly the player had no chance to see is not a fair anomaly.
                // The window is a window, not a stopwatch - hold the event until the channel is
                // actually on screen, and only fire it unwatched once the window is running out.
                if (!IsWatching(channel) && windowEnd > 0 && now < windowEnd) continue;

                StartAnomaly(channel, anomaly, now);
            }
        }

        /// <summary>
        /// Which night's anomaly schedule to run. An endless run has no night 7, so it cycles
        /// the authored nights 1-6 rather than falling silent the moment the campaign's worth
        /// of anomalies runs out. Resident traffic needs no equivalent - it is generated.
        /// </summary>
        int AnomalyNight()
        {
            int night = ServiceHub.State.NightIndex;
            if (!ServiceHub.State.EndlessMode) return night;
            return 1 + (Math.Max(0, night) % 6);
        }

        /// <summary>
        /// True when this channel's picture is in front of the player right now. Grid mode only
        /// counts when the channel is on the page being drawn - the grid shows six of twelve.
        /// </summary>
        bool IsWatching(Channel channel)
        {
            if (!ServiceHub.Player.InPcMode || ServiceHub.Player.CurrentAppId != AppIds.Cctv) return false;
            if (!GridMode) return SelectedCameraId == channel.CameraId;

            int index = _channels.IndexOf(channel);
            if (index < 0) return false;

            int start = GridPage * 6;
            return index >= start && index < start + 6;
        }

        void StartAnomaly(Channel channel, AnomalyDefinition anomaly, int now)
        {
            _firedAnomalies.Add(anomaly.anomalyId);

            channel.ActiveAnomalyId = anomaly.anomalyId;
            channel.State = anomaly.feedState;
            channel.Motion = true;

            // Core anomalies stay on screen at least 4 seconds (GDD 12.4).
            float duration = anomaly.durationSeconds < 4f ? 4f : anomaly.durationSeconds;
            channel.AnomalyEndsAtGameSecond = now + (int)(duration * GameClock.GameSecondsPerRealSecond);

            // An anomaly can leave a trace behind in the world (GDD 9.1: the red slipper).
            if (!string.IsNullOrEmpty(anomaly.setsFlagId)) ServiceHub.State.SetFlag(anomaly.setsFlagId, true);

            // The player may now name this, on this channel, as this family (GDD 12.3).
            RegisterSighting(channel.CameraId, anomaly.category,
                             (int)(duration * GameClock.GameSecondsPerRealSecond),
                             anomaly.anomalyId, anomaly.evidenceId, 1);

            bool playerLooking = IsWatching(channel);

            // GDD 24.2: on the hardest setting the system nags less.
            if (!playerLooking && !DifficultyProfile.ReducedCctvAlerts) channel.UnreviewedMotion = true;

            EventBus.Publish(new CctvAnomalyEvent(channel.CameraId, anomaly.anomalyId, true));
            Log.Info("CCTV", "anomaly " + anomaly.anomalyId + " on " + channel.CameraId +
                             (playerLooking ? " (observed)" : " (unwatched)"));

            if (playerLooking) RegisterObservation(channel, anomaly.anomalyId);
        }

        void EndAnomaly(Channel channel)
        {
            var id = channel.ActiveAnomalyId;
            channel.ActiveAnomalyId = string.Empty;
            channel.State = FeedState.Live;
            channel.Motion = false;
            channel.AnomalyEndsAtGameSecond = 0;
            EventBus.Publish(new CctvAnomalyEvent(channel.CameraId, id, false));
        }

        void RegisterObservation(Channel channel, string anomalyId)
        {
            var anomaly = _content.FindAnomaly(anomalyId);
            if (anomaly == null) return;

            if (!string.IsNullOrEmpty(anomaly.objectiveId) && !string.IsNullOrEmpty(anomaly.caseId))
                ServiceHub.Cases.TryAdvanceObjective(anomaly.caseId, anomaly.objectiveId);
        }

        /// <summary>Manual trigger used by scripted cases and the dev console.</summary>
        public bool TriggerAnomaly(string cameraId, string anomalyId)
        {
            var channel = Find(cameraId);
            var anomaly = _content.FindAnomaly(anomalyId);
            if (channel == null || anomaly == null) return false;

            _firedAnomalies.Remove(anomalyId);
            StartAnomaly(channel, anomaly, _clock.GameSecond);
            return true;
        }

        // ---- timestamps ------------------------------------------------------

        /// <summary>True while the whole wall of monitors is dated 2009 (GDD 2.3 hook #8).</summary>
        public bool ArchiveTimestamp { get; private set; }

        /// <summary>
        /// One channel slipping into the past drags every other channel with it. That is the
        /// shot the trailer wants and it is also the honest reading: it is not the camera that
        /// moved, it is the building.
        /// </summary>
        void RefreshArchiveStamp()
        {
            bool active = false;

            for (int i = 0; i < _channels.Count && !active; i++)
            {
                var id = _channels[i].ActiveAnomalyId;
                if (string.IsNullOrEmpty(id)) continue;

                var anomaly = _content.FindAnomaly(id);
                if (anomaly == null) continue;

                for (int t = 0; t < ArchiveStampTypes.Length; t++)
                    if (anomaly.typeNumber == ArchiveStampTypes[t]) { active = true; break; }
            }

            ArchiveTimestamp = active;
        }

        /// <summary>
        /// What the burnt-in clock on a feed reads. GDD 12.2 forbids labelling feed state, so
        /// the date is the only tell there is - and it is never explained.
        /// </summary>
        public string TimestampFor(int gameSecond)
        {
            if (Cases.SelectedMainQuestRules.Active("N5-M01")) return ArchiveDate + "  02:08";
            return ArchiveTimestamp
                ? ArchiveDate + "  " + GameClock.FormatSecond(gameSecond)
                : GameClock.FormatSecond(gameSecond);
        }

        // ---- reporting -------------------------------------------------------

        /// <summary>
        /// Declares that something reportable is on this channel. Called by the anomaly
        /// scheduler and by resident traffic; anything else that ever puts a lie on a screen
        /// should call it too, so the player has exactly one verb for "that is wrong".
        /// </summary>
        public void RegisterSighting(string cameraId, AnomalyCategory category, int visibleGameSeconds,
                                     string sourceId, string evidenceId = null, int resonance = 0,
                                     SightingOrigin origin = SightingOrigin.Anomaly)
        {
            if (string.IsNullOrEmpty(cameraId)) return;

            _sightings.Add(new Sighting
            {
                CameraId = cameraId,
                Category = category,
                Origin = origin,
                ExpiresAtGameSecond = _clock.GameSecond + Math.Max(0, visibleGameSeconds) + ReportGraceSeconds,
                SourceId = sourceId,
                EvidenceId = evidenceId,
                Resonance = resonance
            });
        }

        /// <summary>True while this channel has something the player could still name.</summary>
        public bool HasReportable(string cameraId)
        {
            return FindReportable(cameraId) != null;
        }

        Sighting FindReportable(string cameraId)
        {
            // Newest first: if two things happened on one camera, the player is describing the
            // one they just watched, not the one from a minute ago.
            for (int i = _sightings.Count - 1; i >= 0; i--)
            {
                var sighting = _sightings[i];
                if (sighting.Reported || sighting.CameraId != cameraId) continue;
                if (_clock.GameSecond > sighting.ExpiresAtGameSecond) continue;
                return sighting;
            }
            return null;
        }

        /// <summary>
        /// The player names a channel and a GDD 12.3 family. Each sighting can only be claimed
        /// once - including when the claim is wrong - so working through all five categories is
        /// not a strategy.
        /// </summary>
        public ReportOutcome Report(string cameraId, AnomalyCategory category)
        {
            var sighting = FindReportable(cameraId);
            ServiceHub.State.NoteReport(sighting != null && sighting.Category == category);

            if (sighting == null)
            {
                ServiceHub.State.AddStat(StatIds.Performance, ReportNothingTherePerformance,
                                         "reason.false_report");
                ServiceHub.Audio.PlayCue(AudioCue.ReportRejected);
                EventBus.Publish(new NotificationEvent("ui.notify.report_nothing", NotificationSeverity.Warning));
                ServiceHub.Analytics.Track("cctv_report", cameraId + ":nothing");
                if (ServiceHub.Pressure != null) ServiceHub.Pressure.NoteReportMiscategorised();
                return ReportOutcome.NothingThere;
            }

            sighting.Reported = true;

            if (sighting.Category != category)
            {
                ServiceHub.State.AddStat(StatIds.Performance, ReportWrongCategoryPerformance,
                                         "reason.misfiled_report");
                ServiceHub.Audio.PlayCue(AudioCue.ReportRejected);
                EventBus.Publish(new NotificationEvent("ui.notify.report_wrong", NotificationSeverity.Warning));
                ServiceHub.Analytics.Track("cctv_report", cameraId + ":miscategorised");
                if (ServiceHub.Pressure != null) ServiceHub.Pressure.NoteReportMiscategorised();
                return ReportOutcome.WrongCategory;
            }

            ServiceHub.State.AddStat(StatIds.Performance, ReportCorrectPerformance, "reason.logged_anomaly");
            NoteCatalogued(sighting);

            if (sighting.Resonance != 0)
                ServiceHub.State.AddStat(StatIds.HarinResonance, sighting.Resonance, "reason.noticed_contradiction");

            if (!string.IsNullOrEmpty(sighting.EvidenceId))
                ServiceHub.Evidence.Acquire(sighting.EvidenceId, Evidence.EvidenceSource.Cctv);

            var channel = Find(cameraId);
            if (channel != null) channel.UnreviewedMotion = false;

            // GDD 15.4: filing is the caretaker's main way of pushing the night back. A report
            // filed against a wrongly admitted visitor's own crossing is the one that names
            // them and stops the drip - any other correct report is just relief.
            NotePressureForReport(sighting);

            ServiceHub.Audio.PlayCue(AudioCue.ReportFiled);
            EventBus.Publish(new NotificationEvent("ui.notify.report_correct", NotificationSeverity.Task));
            ServiceHub.Analytics.Track("cctv_report", cameraId + ":correct:" + sighting.SourceId);
            Log.Info("CCTV", "reported " + sighting.SourceId + " on " + cameraId + " as " + category);
            return ReportOutcome.Correct;
        }

        /// <summary>
        /// GDD 15.4: a sighting whose window closed with nothing filed is the single most
        /// common way the night gains on the player. It is still not a dead end (GDD 12.4) -
        /// the rewind and the access log are both still there - it just is not free any more.
        /// </summary>
        /// <summary>
        /// GDD 15.4. The sighting id carries who it was: an intruder's crossings are registered
        /// with ResidentTrafficService.IntruderSourcePrefix, so naming one on camera is what
        /// ends it rather than any correct report happening to land first.
        /// </summary>
        static void NotePressureForReport(Sighting sighting)
        {
            var pressure = ServiceHub.Pressure;
            if (pressure == null) return;

            if (sighting.Origin == SightingOrigin.Intruder)
            {
                string source = sighting.SourceId;
                string prefix = Residents.ResidentTrafficService.IntruderSourcePrefix;

                if (!string.IsNullOrEmpty(source) && source.StartsWith(prefix))
                {
                    int start = prefix.Length;
                    int end = source.IndexOf(':', start);
                    string visitorId = end > start ? source.Substring(start, end - start)
                                                   : source.Substring(start);

                    if (pressure.ClearIntruder(visitorId)) return;
                }
            }

            if (sighting.Origin == SightingOrigin.Traffic) pressure.NoteTrafficReported();
            else pressure.NoteReportCorrect();
        }

        /// <summary>
        /// A correctly filed anomaly goes into the log. Only authored anomalies count - resident
        /// traffic and intruders are things to report, not species to collect.
        /// </summary>
        void NoteCatalogued(Sighting sighting)
        {
            if (sighting.Origin != SightingOrigin.Anomaly || string.IsNullOrEmpty(sighting.SourceId)) return;

            var anomaly = _content.FindAnomaly(sighting.SourceId);
            if (anomaly == null || anomaly.typeNumber <= 0) return;

            if (!_catalogued.Add(anomaly.typeNumber)) return;

            EventBus.Publish(new NotificationEvent("ui.notify.catalogued", NotificationSeverity.Task));
            Log.Info("CCTV", "catalogued type " + anomaly.typeNumber +
                             " (" + _catalogued.Count + "/" + AnomalyCatalogue.TypeCount + ")");
        }

        /// <summary>Restores the log from a save.</summary>
        public void LoadCatalogue(IEnumerable<int> typeNumbers)
        {
            _catalogued.Clear();
            if (typeNumbers == null) return;
            foreach (var n in typeNumbers) if (n > 0) _catalogued.Add(n);
        }

        void PruneSightings(int now)
        {
            for (int i = _sightings.Count - 1; i >= 0; i--)
            {
                bool expired = !_sightings[i].Reported && now > _sightings[i].ExpiresAtGameSecond;
                if (!_sightings[i].Reported && !expired) continue;

                if (expired && ServiceHub.Pressure != null)
                {
                    switch (_sightings[i].Origin)
                    {
                        case SightingOrigin.Anomaly:  ServiceHub.Pressure.NoteSightingExpired(); break;
                        case SightingOrigin.Intruder: ServiceHub.Pressure.NoteIntruderMissed(); break;
                        default:                      ServiceHub.Pressure.NoteTrafficMissed(); break;
                    }
                }

                _sightings.RemoveAt(i);
            }
        }

        /// <summary>
        /// Flags a channel as having had something on it the player did not see (GDD 12.4).
        /// Used by resident traffic, which produces far too many events for a popup each.
        /// </summary>
        public void MarkUnreviewedMotion(string cameraId)
        {
            if (DifficultyProfile.ReducedCctvAlerts) return;

            var channel = Find(cameraId);
            if (channel != null) channel.UnreviewedMotion = true;
        }

        /// <summary>
        /// Puts the host's wall on this copy's monitors (v3.0 46.1).
        ///
        /// Written straight onto the channel rather than through <see cref="SetFeedState"/>
        /// and <see cref="CutAllFeeds"/>, because those two are decisions - they refuse
        /// things, they charge the reserve, they announce themselves - and a client re-making
        /// a decision the host has already made is how one caretaker gets two "feeds restored"
        /// notifications for one basement board. This is the answer, not the argument.
        /// </summary>
        public void ApplyNetworkChannel(string cameraId, FeedState state, string anomalyId,
                                        int anomalyEndsAt, bool motion, bool unreviewed)
        {
            var channel = Find(cameraId);
            if (channel == null) return;

            channel.State = state;
            channel.ActiveAnomalyId = anomalyId ?? string.Empty;
            channel.AnomalyEndsAtGameSecond = anomalyEndsAt;
            channel.Motion = motion;
            channel.UnreviewedMotion = unreviewed;
        }

        /// <summary>
        /// Whether the wall is cut, as the host sees it.
        ///
        /// The channels themselves arrive alongside this, so nothing here touches them: this
        /// only keeps the flag every other rule reads - the one that stops a client putting a
        /// picture back on a wall the building has cut - agreeing with the host's.
        /// </summary>
        public void ApplyNetworkFeedCut(bool cut)
        {
            if (FeedsCut == cut) return;

            FeedsCut = cut;
            _cutAtGameSecond = cut ? (_clock != null ? _clock.GameSecond : GameClock.ShiftStartSecond) : -1;
        }

        public void SetFeedState(string cameraId, FeedState state)
        {
            // While the wall is cut nothing may quietly put a picture back on it - not the
            // power service restoring its own dark channels, not an anomaly ending.
            if (FeedsCut && state != FeedState.SignalLost) return;

            var channel = Find(cameraId);
            if (channel != null) channel.State = state;
        }

        // ---- the scripted blackout (GDD 9.2 cold open) ----------------------

        /// <summary>
        /// True while every feed is down because the building cut them, not because the night
        /// reserve ran out.
        ///
        /// The reserve cannot express this state and should not be made to: NightPowerService
        /// only starts dropping channels below its critical line, and killing twelve of them
        /// that way would need a negative reserve. More importantly the two mean different
        /// things. A reserve blackout is a bill the caretaker ran up. This is the shift
        /// opening on a building that is already wrong, and the only way out of it is the
        /// basement board - which is how the first ten minutes teach walking, interacting and
        /// leaving the office unattended without ever printing the word tutorial.
        /// </summary>
        public bool FeedsCut { get; private set; }

        /// <summary>
        /// Game seconds the wall spent cut this shift, so anomaly windows can be pushed past it.
        /// </summary>
        int _cutAtGameSecond = -1;
        int _blackoutSeconds;

        /// <summary>
        /// How long tonight's scripted blackout lasted, in game seconds.
        ///
        /// Anomaly windows are absolute times, and the cold open can hold the wall dark for
        /// most of an hour: a player who takes their time getting to the basement board could
        /// walk back into an office whose 23:30 window had already closed on a scare that was
        /// never allowed to fire. The child at the office door - and the slipper it leaves
        /// behind, which is evidence - would simply not exist that playthrough.
        ///
        /// So the blackout does not spend the window. TryFireAnomalies shifts every window on
        /// the cut night by however long the feeds were down, which is the same promise GDD
        /// 12.4 already makes: an anomaly the player had no chance to see is not a fair one.
        /// </summary>
        public int BlackoutSeconds { get { return _blackoutSeconds + CurrentCutSeconds(); } }

        int CurrentCutSeconds()
        {
            if (!FeedsCut || _cutAtGameSecond < 0) return 0;
            return Math.Max(0, _clock.GameSecond - _cutAtGameSecond);
        }

        public void CutAllFeeds()
        {
            if (FeedsCut) return;
            FeedsCut = true;
            _cutAtGameSecond = _clock != null ? _clock.GameSecond : GameClock.ShiftStartSecond;

            for (int i = 0; i < _channels.Count; i++)
            {
                var channel = _channels[i];
                channel.State = FeedState.SignalLost;
                channel.Motion = false;
                channel.ActiveAnomalyId = string.Empty;
                channel.AnomalyEndsAtGameSecond = 0;
            }

            Log.Warn("CCTV", "every feed is down");
        }

        /// <summary>Throwing the basement board is the only thing that calls this.</summary>
        public void RestoreCutFeeds()
        {
            if (!FeedsCut) return;
            _blackoutSeconds += CurrentCutSeconds();
            _cutAtGameSecond = -1;
            FeedsCut = false;

            for (int i = 0; i < _channels.Count; i++) _channels[i].State = FeedState.Live;

            // The reserve may still be holding channels of its own down there.
            if (ServiceHub.Power != null)
            {
                var dark = ServiceHub.Power.DarkChannels;
                for (int i = 0; i < dark.Count; i++) SetFeedState(dark[i], FeedState.SignalLost);
            }

            EventBus.Publish(new NotificationEvent("ui.notify.feeds_restored", NotificationSeverity.Info));
            Log.Info("CCTV", "feeds restored");
        }

        // ---- rewind and snapshots -----------------------------------------

        /// <summary>Sample at <paramref name="offsetSeconds"/> back from now, clamped to the buffer.</summary>
        public FeedSample Rewind(string cameraId, int offsetSeconds)
        {
            var channel = Find(cameraId);
            int now = _clock.GameSecond;
            if (channel == null) return new FeedSample(now, FeedState.SignalLost, string.Empty, false);

            int target = now - Math.Max(0, Math.Min(offsetSeconds, RewindSeconds));
            FeedSample best = new FeedSample(now, channel.State, channel.ActiveAnomalyId, channel.Motion);

            foreach (var sample in channel.Rewind)
            {
                if (sample.GameSecond > target) break;
                best = sample;
            }
            return best;
        }

        public CctvSnapshot TakeSnapshot(string cameraId, int rewindOffsetSeconds = 0)
        {
            var channel = Find(cameraId);
            if (channel == null) return null;

            var sample = rewindOffsetSeconds > 0
                ? Rewind(cameraId, rewindOffsetSeconds)
                : new FeedSample(_clock.GameSecond, channel.State, channel.ActiveAnomalyId, channel.Motion);

            _snapshotCounter++;
            var snapshot = new CctvSnapshot
            {
                snapshotId = "SNAP_" + _snapshotCounter.ToString("000"),
                cameraId = cameraId,
                gameSecond = sample.GameSecond,
                anomalyId = sample.AnomalyId
            };
            _snapshots.Add(snapshot);

            EventBus.Publish(new CctvSnapshotCreatedEvent(cameraId, snapshot.gameSecond, snapshot.snapshotId));
            ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.TakeSnapshot, cameraId);
            Log.Info("CCTV", "snapshot " + snapshot.snapshotId + " on " + cameraId +
                             " at " + GameClock.FormatSecond(snapshot.gameSecond));

            // A snapshot of an anomaly that carries evidence grants it.
            var anomaly = _content.FindAnomaly(sample.AnomalyId);
            if (anomaly != null && !string.IsNullOrEmpty(anomaly.evidenceId))
                ServiceHub.Evidence.Acquire(anomaly.evidenceId, Evidence.EvidenceSource.Cctv);

            return snapshot;
        }

        public bool AttachSnapshotToCase(string snapshotId, string caseId)
        {
            for (int i = 0; i < _snapshots.Count; i++)
            {
                if (_snapshots[i].snapshotId != snapshotId) continue;
                _snapshots[i].attachedCaseId = caseId;
                return true;
            }
            return false;
        }

        public int UnreviewedMotionCount()
        {
            int count = 0;
            for (int i = 0; i < _channels.Count; i++) if (_channels[i].UnreviewedMotion) count++;
            return count;
        }

        public void Reset()
        {
            _snapshots.Clear();
            _firedAnomalies.Clear();
            _sightings.Clear();
            _snapshotCounter = 0;
            _lastSampledSecond = -1;
            SelectedCameraId = _channels.Count > 0 ? _channels[0].CameraId : string.Empty;
            GridMode = true;
            GridPage = 0;
            FeedsCut = false;
            _cutAtGameSecond = -1;
            _blackoutSeconds = 0;
            _catalogued.Clear();   // playthrough progress: cleared here, never on a night change
            BuildChannels();
        }

        public void LoadFrom(IEnumerable<Save.SnapshotSaveEntry> snapshots, IEnumerable<string> firedAnomalies)
        {
            _snapshots.Clear();
            _firedAnomalies.Clear();
            _snapshotCounter = 0;

            if (snapshots != null)
                foreach (var s in snapshots)
                {
                    _snapshots.Add(new CctvSnapshot
                    {
                        snapshotId = s.snapshotId,
                        cameraId = s.cameraId,
                        gameSecond = s.gameSecond,
                        anomalyId = s.anomalyId,
                        attachedCaseId = s.attachedCaseId
                    });
                    _snapshotCounter++;
                }

            if (firedAnomalies != null) foreach (var id in firedAnomalies) _firedAnomalies.Add(id);
        }

        public IEnumerable<string> FiredAnomalies { get { return _firedAnomalies; } }
    }
}
