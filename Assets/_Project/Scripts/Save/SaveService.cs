using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NO404.Core;

namespace NO404.Save
{
    public struct SaveResult
    {
        public bool Success;
        public int Slot;
        public string Error;
    }

    public struct LoadResult
    {
        public bool Success;
        public int Slot;
        public string Error;
    }

    public interface ISaveService
    {
        bool HasSave { get; }
        Task<SaveResult> SaveAsync(SaveReason reason, CancellationToken ct);
        Task<LoadResult> LoadAsync(CancellationToken ct);
        Task DeleteSaveAsync(CancellationToken ct);
    }

    /// <summary>
    /// Atomic, checksummed save files (GDD 20.17).
    ///
    /// Slots: 0-2 rotating autosaves, 3 manual, 4 night-start backup.
    /// The file is written to a .tmp and then replaced, so a crash mid-write can never
    /// destroy the previous save. A corrupt file falls back to the newest intact one.
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        public const int AutosaveSlotCount = 3;
        public const int ManualSlot = 3;
        public const int NightBackupSlot = 4;
        public const int SlotCount = 5;

        /// <summary>Supplied by GameLoop so the service never searches the scene.</summary>
        public Func<PlayerSaveEntry> CapturePlayer;
        public Action<PlayerSaveEntry> RestorePlayer;
        public Func<List<DoorSaveEntry>> CaptureDoors;
        public Action<List<DoorSaveEntry>> RestoreDoors;

        int _nextAutosaveSlot;
        bool _autosaveQueued;
        SaveReason _queuedReason;
        float _lastAutosaveRealtime;

        public bool IsBusy { get; private set; }
        public event Action<SaveReason> OnSaved;

        /// <summary>
        /// True while this sitting must not write a save file (the developer start, GDD 20.21).
        ///
        /// A shift opened on night 4 with the earlier nights filled in by hand is not a
        /// campaign anybody played, and its autosaves would rotate through the same three
        /// slots a real one lives in. Loading is untouched: the switch stops this sitting
        /// leaving anything behind, not reading what is already there.
        /// </summary>
        public bool WritesSuspended { get; set; }

        static string Dir { get { return Path.Combine(Application.persistentDataPath, "saves"); } }
        static string PathForSlot(int slot) { return Path.Combine(Dir, "slot" + slot + ".json"); }

        public bool HasSave
        {
            get
            {
                for (int i = 0; i < SlotCount; i++) if (File.Exists(PathForSlot(i))) return true;
                return false;
            }
        }

        // ---- autosave scheduling -----------------------------------------

        /// <summary>
        /// Requests an autosave. Coalesced so a burst of triggers (evidence + zone change)
        /// writes one file, and never during a blocking conversation.
        /// </summary>
        public void RequestAutosave(SaveReason reason)
        {
            if (WritesSuspended) return;

            _autosaveQueued = true;
            _queuedReason = reason;
        }

        /// <summary>Called every frame by GameLoop.</summary>
        public void Tick()
        {
            if (!_autosaveQueued || IsBusy) return;
            if (Time.realtimeSinceStartup - _lastAutosaveRealtime < 5f) return;
            if (ServiceHub.Dialogue != null && ServiceHub.Dialogue.IsBlocking) return;

            _autosaveQueued = false;
            _lastAutosaveRealtime = Time.realtimeSinceStartup;

            int slot = _nextAutosaveSlot;
            _nextAutosaveSlot = (_nextAutosaveSlot + 1) % AutosaveSlotCount;
            var reason = _queuedReason;

            var task = SaveToSlotAsync(slot, reason, CancellationToken.None);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted) Log.Error("Save", "autosave failed: " + t.Exception);
            }, TaskScheduler.Default);
        }

        // ---- public API ---------------------------------------------------

        public Task<SaveResult> SaveAsync(SaveReason reason, CancellationToken ct)
        {
            int slot = reason == SaveReason.Manual ? ManualSlot
                     : reason == SaveReason.NightStart ? NightBackupSlot
                     : _nextAutosaveSlot;

            if (reason != SaveReason.Manual && reason != SaveReason.NightStart)
                _nextAutosaveSlot = (_nextAutosaveSlot + 1) % AutosaveSlotCount;

            return SaveToSlotAsync(slot, reason, ct);
        }

        public async Task<SaveResult> SaveToSlotAsync(int slot, SaveReason reason, CancellationToken ct)
        {
            if (WritesSuspended)
            {
                // Said out loud for the one caller that is a person pressing a button.
                if (reason == SaveReason.Manual)
                    EventBus.Publish(new NotificationEvent("ui.dev.notify.saves_off",
                                                           NotificationSeverity.Warning));
                Log.Info("Save", "skipped (" + reason + "): writes are suspended for this sitting");
                return new SaveResult { Success = false, Slot = slot, Error = "suspended" };
            }

            if (IsBusy) return new SaveResult { Success = false, Slot = slot, Error = "busy" };
            IsBusy = true;

            try
            {
                // Capture runs on the main thread; only the file write is offloaded.
                // PathForSlot touches Application.persistentDataPath, a main-thread-only
                // Unity API, so it must be resolved here rather than inside Task.Run.
                var data = Capture(slot, reason);
                var payload = Serialize(data);
                var path = PathForSlot(slot);

                await Task.Run(() => WriteAtomic(path, payload), ct);

                Log.Info("Save", "saved slot " + slot + " (" + reason + ")");
                EventBus.Publish(new GameSavedEvent(reason, slot));

                var cb = OnSaved;
                if (cb != null) cb(reason);

                return new SaveResult { Success = true, Slot = slot };
            }
            catch (Exception e)
            {
                Log.Error("Save", "save failed: " + e.Message);
                return new SaveResult { Success = false, Slot = slot, Error = e.Message };
            }
            finally
            {
                IsBusy = false;
            }
        }

        public Task<LoadResult> LoadAsync(CancellationToken ct)
        {
            return LoadFromSlotAsync(NewestSlot(), ct);
        }

        public async Task<LoadResult> LoadFromSlotAsync(int slot, CancellationToken ct)
        {
            if (slot < 0) return new LoadResult { Success = false, Slot = slot, Error = "no_save" };

            string text = null;
            var path = PathForSlot(slot);
            try
            {
                text = await Task.Run(() => ReadVerified(path), ct);
            }
            catch (Exception e)
            {
                Log.Error("Save", "read failed: " + e.Message);
            }

            if (text == null)
            {
                int fallback = NewestSlot(exclude: slot);
                if (fallback >= 0)
                {
                    Log.Warn("Save", "slot " + slot + " unreadable, falling back to slot " + fallback);
                    return await LoadFromSlotAsync(fallback, ct);
                }
                return new LoadResult { Success = false, Slot = slot, Error = "corrupt" };
            }

            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(text); }
            catch (Exception e) { return new LoadResult { Success = false, Slot = slot, Error = e.Message }; }

            if (data == null || !SaveMigrations.TryMigrate(data))
                return new LoadResult { Success = false, Slot = slot, Error = "migration_failed" };

            Restore(data);
            Log.Info("Save", "loaded slot " + slot);
            return new LoadResult { Success = true, Slot = slot };
        }

        public Task DeleteSaveAsync(CancellationToken ct)
        {
            var paths = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++) paths[i] = PathForSlot(i);

            return Task.Run(() =>
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    try { if (File.Exists(paths[i])) File.Delete(paths[i]); }
                    catch (Exception e) { Log.Warn("Save", "delete failed for slot " + i + ": " + e.Message); }
                }
            }, ct);
        }

        public SaveHeader ReadHeader(int slot)
        {
            var header = new SaveHeader { Slot = slot };
            var path = PathForSlot(slot);
            if (!File.Exists(path)) return header;

            try
            {
                var text = ReadVerified(path);
                if (text == null) return header;

                var data = JsonUtility.FromJson<SaveData>(text);
                if (data == null) return header;

                header.Exists = true;
                header.NightIndex = data.nightIndex;
                header.GameSecond = data.gameSecond;
                header.SaveUtc = data.saveUtc;
                header.Reason = (SaveReason)data.reason;
            }
            catch (Exception e)
            {
                Log.Warn("Save", "header read failed for slot " + slot + ": " + e.Message);
            }

            return header;
        }

        public int NewestSlot(int exclude = -1)
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;

            for (int i = 0; i < SlotCount; i++)
            {
                if (i == exclude) continue;
                var path = PathForSlot(i);
                if (!File.Exists(path)) continue;

                var stamp = File.GetLastWriteTimeUtc(path);
                if (stamp <= bestTime) continue;
                bestTime = stamp;
                best = i;
            }

            return best;
        }

        // ---- file primitives ---------------------------------------------

        public static string Serialize(SaveData data)
        {
            var json = JsonUtility.ToJson(data, false);
            return Checksum(json) + "\n" + json;
        }

        static void WriteAtomic(string path, string payload)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, payload, Encoding.UTF8);

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>Returns the JSON body if the checksum matches, otherwise null.</summary>
        public static string ReadVerified(string path)
        {
            if (!File.Exists(path)) return null;

            var text = File.ReadAllText(path, Encoding.UTF8);
            int newline = text.IndexOf('\n');
            if (newline <= 0) { Log.Error("Save", "malformed save file: " + path); return null; }

            var expected = text.Substring(0, newline).Trim();
            var body = text.Substring(newline + 1);

            if (Checksum(body) != expected)
            {
                Log.Error("Save", "checksum mismatch: " + path);
                return null;
            }

            return body;
        }

        /// <summary>FNV-1a. Detects truncation and corruption; it is not a security measure.</summary>
        public static string Checksum(string text)
        {
            unchecked
            {
                const uint prime = 16777619u;
                uint hash = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= prime;
                }
                return hash.ToString("x8");
            }
        }

        // ---- capture / restore -------------------------------------------

        public SaveData Capture(int slot, SaveReason reason)
        {
            var data = new SaveData
            {
                schemaVersion = SaveData.CurrentSchemaVersion,
                buildVersion = Application.version,
                saveUtc = DateTime.UtcNow.ToString("o"),
                slot = slot,
                reason = (int)reason,
                nightIndex = ServiceHub.State.NightIndex,
                gameSecond = ServiceHub.Clock.GameSecond
            };

            foreach (var kv in ServiceHub.State.AllStats)
                data.stats.Add(new StatSaveEntry { key = kv.Key, value = kv.Value });

            foreach (var kv in ServiceHub.State.AllFlags)
                data.flags.Add(new FlagSaveEntry { key = kv.Key, value = kv.Value });

            foreach (var kv in ServiceHub.State.AllChoices)
                data.choices.Add(new ChoiceSaveEntry { key = kv.Key, value = kv.Value });

            data.campaignSeed = ServiceHub.NightPool.CampaignSeed;
            foreach (var id in ServiceHub.Cases.DrawnTonight) data.selectedQuests.Add(id);
            foreach (var id in ServiceHub.NightPool.PreviousNight) data.previousNightQuests.Add(id);

            data.hp = ServiceHub.Vitals.Hp;
            data.san = ServiceHub.Vitals.San;
            data.firstAidRemaining = ServiceHub.Vitals.FirstAidRemaining;
            data.groundingRemaining = ServiceHub.Vitals.GroundingRemaining;

            foreach (var level in ServiceHub.State.GrantedAccess) data.accessLevels.Add((int)level);

            foreach (var runtime in ServiceHub.Cases.AllCases)
            {
                if (runtime.State == Cases.CaseState.Dormant) continue;
                var entry = new CaseSaveEntry
                {
                    caseId = runtime.CaseId,
                    state = (int)runtime.State,
                    decisionId = runtime.ChosenDecisionId,
                    startedAt = runtime.StartedAtGameSecond,
                    resolvedAt = runtime.ResolvedAtGameSecond,
                    failSafeFired = runtime.FailSafeFired
                };
                foreach (var o in runtime.CompletedObjectives) entry.completedObjectives.Add(o);
                for (int i = 0; i < runtime.AttachedEvidence.Count; i++)
                    entry.attachedEvidence.Add(runtime.AttachedEvidence[i]);
                data.cases.Add(entry);
            }

            foreach (var pair in ServiceHub.Evidence.Owned)
            {
                var runtime = pair.Value;
                data.evidence.Add(new EvidenceSaveEntry
                {
                    evidenceId = runtime.EvidenceId,
                    source = (int)runtime.Source,
                    acquiredAt = runtime.AcquiredAtGameSecond,
                    boardX = runtime.BoardX,
                    boardY = runtime.BoardY,
                    placed = runtime.Placed
                });
            }

            var links = ServiceHub.Evidence.Links;
            for (int i = 0; i < links.Count; i++)
                data.evidenceLinks.Add(new EvidenceLinkSaveEntry
                {
                    a = links[i].A, b = links[i].B, relation = (int)links[i].Relation
                });

            foreach (var kv in ServiceHub.Residents.StatusOverrides)
                data.residents.Add(new ResidentSaveEntry { residentId = kv.Key, status = (int)kv.Value });

            foreach (var kv in ServiceHub.Interphone.Decisions)
                data.visitors.Add(new VisitorSaveEntry { visitorId = kv.Key, decision = (int)kv.Value });

            var snapshots = ServiceHub.Cctv.Snapshots;
            for (int i = 0; i < snapshots.Count; i++)
                data.snapshots.Add(new SnapshotSaveEntry
                {
                    snapshotId = snapshots[i].snapshotId,
                    cameraId = snapshots[i].cameraId,
                    gameSecond = snapshots[i].gameSecond,
                    anomalyId = snapshots[i].anomalyId,
                    attachedCaseId = snapshots[i].attachedCaseId
                });

            // GDD 15.4 / 15.5.
            data.pressure = ServiceHub.Pressure.Value;
            data.pressureNightIndex = ServiceHub.State.NightIndex;
            foreach (var id in ServiceHub.Pressure.Intruders) data.pressureIntruders.Add(id);
            data.powerReserve = ServiceHub.Power.Reserve;
            data.corridorLightsOn = ServiceHub.Power.CorridorLightsOn;
            data.breakerResetsUsed = ServiceHub.Power.BreakerResetsUsed;

            foreach (var id in ServiceHub.Cctv.FiredAnomalies) data.firedAnomalies.Add(id);
            foreach (var n in ServiceHub.Cctv.Catalogued) data.cataloguedAnomalies.Add(n);
            foreach (var id in ServiceHub.Phone.AnsweredCalls) data.answeredCalls.Add(id);
            foreach (var id in ServiceHub.Phone.MissedCalls) data.missedCalls.Add(id);

            var log = ServiceHub.AccessLog.Entries;
            for (int i = 0; i < log.Count; i++)
                data.accessLog.Add(new AccessLogSaveEntry
                {
                    gameSecond = log[i].GameSecond,
                    subject = (int)log[i].Subject,
                    nameKey = log[i].NameKey,
                    cardId = log[i].CardId,
                    cameraId = log[i].CameraId,
                    locationKey = log[i].LocationKey,
                    inbound = log[i].Inbound,
                    contradictionGroup = log[i].ContradictionGroup
                });

            foreach (var circuitId in ServiceHub.Facility.Circuits)
                data.circuits.Add(new CircuitSaveEntry
                {
                    circuitId = circuitId, on = ServiceHub.Facility.IsCircuitOn(circuitId)
                });

            foreach (var z in ServiceHub.Player.VisitedZones) data.visitedZones.Add(z);
            foreach (var a in ServiceHub.Player.OpenedApps) data.openedApps.Add(a);
            foreach (var c in ServiceHub.Player.DialogueChoices) data.dialogueChoices.Add(c);
            foreach (var a in ServiceHub.Steam.Unlocked) data.unlockedAchievements.Add(a);

            if (ServiceHub.Dialogue.IsActive && ServiceHub.Dialogue.CurrentLine != null)
            {
                data.CurrentDialogueId = ServiceHub.Dialogue.Current.conversationId;
                data.CurrentDialogueNodeId = ServiceHub.Dialogue.CurrentLine.NodeId;
            }

            // v2.1: the manual, the anomaly events in flight, and the stairwell.
            foreach (var pageId in ServiceHub.Manual.UnlockedIds) data.manualPages.Add(pageId);

            foreach (var runtime in ServiceHub.ManualEvents.AllEvents)
            {
                if (runtime.State == Anomalies.ManualEventState.Dormant) continue;
                var entry = new ManualEventSaveEntry
                {
                    eventId = runtime.Definition.eventId,
                    state = (int)runtime.State,
                    startedAt = runtime.StartedAt,
                    resolvedAt = runtime.ResolvedAt,
                    wrongAttempts = runtime.WrongAttempts,
                    failSafeFired = runtime.FailSafeFired
                };
                foreach (var o in runtime.CompletedObjectives) entry.completedObjectives.Add(o);
                foreach (var counter in runtime.Counters)
                {
                    entry.counterKeys.Add(counter.Key);
                    entry.counterValues.Add(counter.Value);
                }
                data.manualEvents.Add(entry);
            }

            foreach (var tool in ServiceHub.AnomalyTools.AllTools)
            {
                if (!tool.Awake && tool.UseCount == 0 && !tool.HoldOpen) continue;
                data.anomalyTools.Add(new AnomalyToolSaveEntry
                {
                    toolId = tool.ToolId,
                    awake = tool.Awake,
                    useCount = tool.UseCount,
                    holdOpen = tool.HoldOpen,
                    holdExpired = tool.HoldExpired,
                    holdEndsAt = tool.HoldEndsAtGameSecond,
                    heldFlagId = tool.HeldFlagId,
                    warningsFired = tool.WarningsFired
                });
            }

            foreach (var deferred in ServiceHub.Cases.NextNightQueue)
                if (deferred != null) data.deferredConsequences.Add(deferred);

            var stairs = ServiceHub.Stairs;
            data.stairs = new StairSaveEntry
            {
                entryFloor = stairs.EntryFloor,
                currentLanding = stairs.CurrentLanding,
                directionHint = (int)stairs.DirectionHint,
                variant = (int)stairs.Variant,
                inStairwell = stairs.InStairwell,
                flightsWalked = stairs.FlightsWalked
            };

            if (CapturePlayer != null) data.player = CapturePlayer();
            if (CaptureDoors != null) data.doors = CaptureDoors();

            return data;
        }

        /// <summary>
        /// Replaces this copy's shift with somebody else's (v3.0 46.1).
        ///
        /// A joining caretaker's screens have to agree with the host's about every single
        /// thing the night has decided - which cases are open, which objectives are ticked,
        /// what is in the evidence tray, how much reserve is left, which anomalies have
        /// already fired. That is precisely the set of things the save file exists to write
        /// down, so the mirror is a save: the host captures one and the client applies it
        /// here, and the two can never drift apart in the way a hand-written list of synced
        /// fields drifts the first time somebody adds a service and forgets.
        ///
        /// What it deliberately does NOT touch is everything that is a fact about one person
        /// rather than about the shift: where they are standing, which floors they have
        /// walked, which apps they have opened, what the stairwell has done to them, and their
        /// own achievements. Those stay theirs. Doors and the clock have their own immediate
        /// sync in NetShift, which arrives faster than this does.
        ///
        /// <see cref="Restore"/> is not reusable for this: it resets the playthrough, moves
        /// the player and resumes a conversation, none of which may happen twice a second.
        /// </summary>
        public void ApplyShiftMirror(SaveData data)
        {
            if (data == null) return;

            var stats = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < data.stats.Count; i++)
                stats.Add(new KeyValuePair<string, int>(data.stats[i].key, data.stats[i].value));

            var flags = new List<KeyValuePair<string, bool>>();
            for (int i = 0; i < data.flags.Count; i++)
                flags.Add(new KeyValuePair<string, bool>(data.flags[i].key, data.flags[i].value));

            ServiceHub.State.LoadFrom(data.nightIndex, stats, flags, data.accessLevels, Choices(data));

            // HP and SAN are deliberately not mirrored - ShiftMirror strips them, and this
            // copy's caretaker keeps the body they earned.

            // Which case the HUD is following is a local choice - two caretakers may well be
            // working different halves of the night - and CaseService.LoadFrom resets it to
            // the first active case. So it is taken out and put back around the rebuild.
            var tracked = ServiceHub.Cases.TrackedCase;
            var trackedId = tracked != null ? tracked.CaseId : null;

            ServiceHub.Evidence.LoadFrom(data.evidence, data.evidenceLinks);
            ServiceHub.Cases.LoadFrom(data.cases);
            ServiceHub.Cases.LoadNextNightQueue(data.deferredConsequences);
            ServiceHub.Cases.RebuildNightChain(data.nightIndex);
            if (!string.IsNullOrEmpty(trackedId)) ServiceHub.Cases.SetTracked(trackedId);

            ServiceHub.Residents.LoadFrom(data.residents);
            ServiceHub.Interphone.LoadFrom(data.visitors);
            ServiceHub.Cctv.LoadFrom(data.snapshots, data.firedAnomalies);
            ServiceHub.Cctv.LoadCatalogue(data.cataloguedAnomalies);
            ServiceHub.Phone.LoadFrom(data.answeredCalls, data.missedCalls);
            ServiceHub.AccessLog.LoadFrom(data.accessLog);
            ServiceHub.Facility.LoadFrom(data.circuits);

            // Power before pressure, for the same reason Restore does it in this order: the
            // ladder reads the reserve.
            ServiceHub.Power.LoadFrom(data.powerReserve, data.corridorLightsOn, data.breakerResetsUsed);
            ServiceHub.Pressure.LoadFrom(data.pressure, data.pressureNightIndex, data.pressureIntruders);

            ServiceHub.Manual.LoadFrom(data.manualPages);
            ServiceHub.ManualEvents.LoadFrom(data.manualEvents);
            ServiceHub.AnomalyTools.LoadFrom(data.anomalyTools);

            // The same announcement a load makes, and it means the same thing here: the state
            // underneath the world has been replaced, so anything drawing itself from that
            // state - a picked-up document that should no longer be on its desk, a gated prop -
            // has to look again. Every one of those handlers is a resync, not an effect, which
            // is what makes it safe to say twice a second.
            EventBus.Publish(new SaveRestoredEvent());
        }

        static List<KeyValuePair<string, string>> Choices(SaveData data)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (data.choices == null) return list;

            for (int i = 0; i < data.choices.Count; i++)
                list.Add(new KeyValuePair<string, string>(data.choices[i].key, data.choices[i].value));

            return list;
        }

        public void Restore(SaveData data)
        {
            ServiceHub.ResetPlaythrough();

            var stats = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < data.stats.Count; i++)
                stats.Add(new KeyValuePair<string, int>(data.stats[i].key, data.stats[i].value));

            var flags = new List<KeyValuePair<string, bool>>();
            for (int i = 0; i < data.flags.Count; i++)
                flags.Add(new KeyValuePair<string, bool>(data.flags[i].key, data.flags[i].value));

            ServiceHub.State.LoadFrom(data.nightIndex, stats, flags, data.accessLevels, Choices(data));
            ServiceHub.Vitals.LoadFrom(data.hp, data.san,
                                       data.firstAidRemaining, data.groundingRemaining);
            ServiceHub.Clock.SetGameSecond(data.gameSecond);

            ServiceHub.Evidence.LoadFrom(data.evidence, data.evidenceLinks);
            ServiceHub.Cases.LoadFrom(data.cases);
            // After LoadFrom, which resets the service and would otherwise drop the queue.
            ServiceHub.Cases.LoadNextNightQueue(data.deferredConsequences);

            // v5.0 4.4 step 11: the night is replayed, never re-drawn.
            ServiceHub.NightPool.BeginCampaign(data.campaignSeed);
            ServiceHub.NightPool.Restore(data.selectedQuests, data.previousNightQuests);
            ServiceHub.Cases.RestoreDraw(data.selectedQuests);
            // The night queue is derived from the content rather than saved, so a reload has
            // to put it back too - without this the dashboard and the clock-off button read an
            // empty chain for the rest of the shift.
            ServiceHub.Cases.RebuildNightChain(data.nightIndex);
            ServiceHub.Residents.LoadFrom(data.residents);
            ServiceHub.Interphone.LoadFrom(data.visitors);
            ServiceHub.Cctv.LoadFrom(data.snapshots, data.firedAnomalies);
            ServiceHub.Cctv.LoadCatalogue(data.cataloguedAnomalies);
            ServiceHub.Phone.LoadFrom(data.answeredCalls, data.missedCalls);
            ServiceHub.AccessLog.LoadFrom(data.accessLog);
            ServiceHub.Facility.LoadFrom(data.circuits);

            // Power before pressure: the ladder reads the reserve, so restoring it the other
            // way round would evaluate the first minute against a full tank.
            ServiceHub.Power.LoadFrom(data.powerReserve, data.corridorLightsOn, data.breakerResetsUsed);
            ServiceHub.Pressure.LoadFrom(data.pressure, data.pressureNightIndex, data.pressureIntruders);
            ServiceHub.Player.LoadFrom(data.visitedZones, data.openedApps, data.dialogueChoices);
            ServiceHub.Steam.LoadFrom(data.unlockedAchievements);

            // v2.1. The manual loads before the events so an event restoring mid-procedure can
            // still be asked whether its page was ever issued (spec 0.9.2).
            ServiceHub.Manual.LoadFrom(data.manualPages);
            ServiceHub.ManualEvents.LoadFrom(data.manualEvents);
            ServiceHub.AnomalyTools.LoadFrom(data.anomalyTools);
            if (data.stairs != null)
                ServiceHub.Stairs.LoadFrom(data.stairs.entryFloor, data.stairs.currentLanding,
                                           data.stairs.directionHint, data.stairs.variant,
                                           data.stairs.inStairwell, data.stairs.flightsWalked);

            if (RestoreDoors != null) RestoreDoors(data.doors);
            if (RestorePlayer != null && data.player != null) RestorePlayer(data.player);

            if (!string.IsNullOrEmpty(data.CurrentDialogueId))
                ServiceHub.Dialogue.Resume(data.CurrentDialogueId, data.CurrentDialogueNodeId);

            // Evidence is only known now that LoadFrom above has run, so world pickups sync
            // their hidden/visible state here rather than off ResetPlaythrough's earlier event.
            EventBus.Publish(new SaveRestoredEvent());
        }
    }
}
