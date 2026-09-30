using System;
using System.Collections.Generic;
using UnityEngine;
using NO404.Cases;

namespace NO404.Core
{
    /// <summary>
    /// Command processor for the developer console (GDD 20.21). Disabled in release builds:
    /// every command returns "disabled" so a shipped binary cannot be driven through it.
    /// </summary>
    public static class DevConsole
    {
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Set by GameLoop so scene.goto can move the player.</summary>
        public static Action<string> TeleportToZone;

        public static string Execute(string commandLine)
        {
            if (!Enabled) return "console disabled in release build";
            if (string.IsNullOrEmpty(commandLine)) return string.Empty;

            var parts = commandLine.Trim().Split(' ');
            var command = parts[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "help": return Help();

                    case "night.set":
                        ServiceHub.State.BeginNight(int.Parse(parts[1]));
                        ServiceHub.Cases.BeginNight(int.Parse(parts[1]));
                        return "night = " + parts[1] + " (state and cases only - " +
                               "use night.start to actually run the shift)";

                    case "night.start":
                    {
                        var loop = GameLoop.Instance;
                        if (loop == null) return "no game running; start or continue one first";

                        if (!loop.DebugStartNight(int.Parse(parts[1])))
                            return "no world yet - use New Game or Continue first";

                        return "night " + parts[1] + " running: visitors, traffic, CCTV, " +
                               "pressure and the night reserve are all live";
                    }

                    case "pressure.add":
                    {
                        var pressure = ServiceHub.Pressure;
                        pressure.Add(int.Parse(parts[1]), "reason.pressure_drift");
                        return "pressure = " + pressure.Value + " (" + pressure.Stage + ")";
                    }

                    case "pressure.show":
                    {
                        var pressure = ServiceHub.Pressure;
                        return "pressure = " + pressure.Value + " (" + pressure.Stage + "), " +
                               "intruders = " + pressure.IntruderCount + ", " +
                               "missed crossings = " + pressure.TrafficMissedSoFar + "/" +
                               Pressure.NightPressureService.TrafficMissedCapPerNight + ", " +
                               "confrontations = " + pressure.ConfrontationCount;
                    }

                    case "power.drain":
                    {
                        var power = ServiceHub.Power;
                        power.Drain(int.Parse(parts[1]) * 10, "reason.power_draw");
                        return "reserve = " + power.ReservePercent + "%";
                    }

                    case "power.show":
                    {
                        var power = ServiceHub.Power;
                        return "reserve = " + power.ReservePercent + "%, draw = " +
                               (power.CurrentDrawPerHour() / 10f).ToString("0.0") + "%/h, " +
                               "breaker resets left = " + power.BreakerResetsRemaining + ", " +
                               "dark channels = " + power.DarkChannels.Count;
                    }

                    case "clock.set":
                    {
                        var hhmm = parts[1].Split(':');
                        ServiceHub.Clock.SetTime(int.Parse(hhmm[0]), int.Parse(hhmm[1]));
                        return "clock = " + ServiceHub.Clock.ToClockString();
                    }

                    case "case.start":
                        return ServiceHub.Cases.TryStartCase(parts[1])
                            ? "started " + parts[1]
                            : "could not start " + parts[1] + " (" + WhyNot(parts[1]) + ")";

                    case "case.complete":
                    {
                        var evidence = new List<string>();
                        foreach (var pair in ServiceHub.Evidence.Owned) evidence.Add(pair.Key);
                        var result = ServiceHub.Cases.SubmitDecision(parts[1], parts[2], evidence);
                        return result.Accepted ? "resolved " + parts[1] + " as " + parts[2]
                                               : "rejected: " + result.RejectReason;
                    }

                    case "evidence.give":
                        return ServiceHub.Evidence.Acquire(parts[1], Evidence.EvidenceSource.Unknown) != null
                            ? "granted " + parts[1] : "unknown evidence " + parts[1];

                    case "stat.set":
                        ServiceHub.State.SetStat(parts[1], int.Parse(parts[2]), "console");
                        return parts[1] + " = " + ServiceHub.State.GetStat(parts[1]);

                    case "flag.set":
                        ServiceHub.State.SetFlag(parts[1], bool.Parse(parts[2]));
                        return parts[1] + " = " + parts[2];

                    case "shift.why":
                    {
                        // Why the clock-off button will not go.
                        //
                        // The button already names its reason, but it names one reason - the
                        // most actionable - and when a caretaker is looking at a task list
                        // reading 6/6 the answer they need is which of the other four things
                        // is holding the shift. Every one of them is invisible from the home
                        // screen: a conversation left open three rooms away, a handset still
                        // live, somebody at the door who was never judged, a caller the night
                        // has booked for later.
                        var loop = GameLoop.Instance;
                        if (loop == null) return "no game running";

                        var report = new System.Text.StringBuilder();
                        report.AppendLine("clock-off: " + loop.CurrentShiftBlocker);
                        report.AppendLine("  conversation open   " + ServiceHub.Dialogue.IsActive);
                        report.AppendLine("  phone ringing/live  " + (ServiceHub.Phone.IsRinging ||
                                                                      ServiceHub.Phone.InCall));
                        report.AppendLine("  visitor at the door " + ServiceHub.Interphone.HasWaitingVisitor);
                        report.AppendLine("  callers still due   " + loop.PendingVisitorCount);
                        report.AppendLine("  shift over (06:00)  " + ServiceHub.Clock.ShiftOver);

                        int night = ServiceHub.State.NightIndex;
                        report.AppendLine("  cases tonight:");
                        foreach (var runtime in ServiceHub.Cases.AllCases)
                        {
                            if (runtime.Definition.nightIndex != night) continue;
                            report.AppendLine("    " + runtime.CaseId + "  " + runtime.State +
                                              (runtime.State.IsResolved() ? "" : "   <- holding"));
                        }
                        return report.ToString();
                    }

                    // ---- v2.1 QA (spec 30.2) --------------------------------
                    //
                    // stat.set reaches these counters too, and must not be used for them: it
                    // writes the number straight into GameStateService, so the band never
                    // changes hands and FloorRiskChangedEvent never fires. The corridor stays
                    // clean, the signs stay honest, and the tester concludes the dressing does
                    // not work. Everything below goes through RiskService instead, which is
                    // what the game itself uses.

                    case "risk.exposure":
                    {
                        int target = int.Parse(parts[1]);
                        ServiceHub.Risk.AddExposure(target - ServiceHub.Risk.Exposure, "console");
                        return "DistortionExposure = " + ServiceHub.Risk.Exposure +
                               " (" + ServiceHub.Risk.Band + ")";
                    }

                    case "risk.floor":
                    {
                        var floorId = parts[1].ToUpperInvariant();
                        if (!Gameplay.FloorPlan.Exists(floorId))
                            return "no floor " + floorId + " (spec 0.7.1)";

                        int target = int.Parse(parts[2]);
                        ServiceHub.Risk.AddFloorRisk(floorId, target - ServiceHub.Risk.FloorRisk(floorId),
                                                     "console");
                        return floorId + " risk = " + ServiceHub.Risk.FloorRisk(floorId) +
                               " (" + ServiceHub.Risk.TierOf(floorId) + ")";
                    }

                    case "risk.show":
                    {
                        var risk = ServiceHub.Risk;
                        var text = "exposure " + risk.Exposure + " (" + risk.Band + ")" +
                                   "  violations " + risk.ViolationCount +
                                   "  memory " + risk.MemoryDebt + "  tools " + risk.ToolDebt + "\n";

                        var order = Gameplay.FloorPlan.Order;
                        for (int i = 0; i < order.Length; i++)
                        {
                            int value = risk.FloorRisk(order[i]);
                            if (value <= 0) continue;
                            text += "  " + order[i] + " = " + value + " (" + risk.TierOf(order[i]) + ")\n";
                        }
                        return text;
                    }

                    case "manual.list":
                    {
                        var text = string.Empty;
                        foreach (var runtime in ServiceHub.ManualEvents.AllEvents)
                        {
                            if (runtime.State == Anomalies.ManualEventState.Dormant) continue;
                            text += "  " + runtime.Definition.eventId + "  " + runtime.State +
                                    (ServiceHub.Manual.IsUnlocked(runtime.Definition.manualPageId)
                                        ? "  [page issued]" : "  [no page]") + "\n";
                        }
                        return text.Length == 0 ? "nothing in flight" : text;
                    }

                    case "manual.start":
                        return ServiceHub.ManualEvents.Begin(parts[1])
                            ? parts[1] + " started"
                            : parts[1] + " could not start (already running, resolved, or unknown)";

                    case "manual.page":
                    {
                        // Spec 30.2 path 1: the run where the caretaker read the rule first.
                        if (parts[1] == "all")
                        {
                            int count = 0;
                            for (int i = 0; i < ManualEventIds.All.Length; i++)
                                if (ServiceHub.Manual.UnlockFor(ManualEventIds.All[i], "console")) count++;
                            return count + " page(s) issued";
                        }

                        return ServiceHub.Manual.UnlockFor(parts[1], "console")
                            ? "page for " + parts[1] + " issued"
                            : "no page for " + parts[1] + " (or it was already in the binder)";
                    }

                    case "manual.forget":
                        // Spec 30.2 path 2: the run where they work it out from the room alone.
                        // A wrong answer after this is a mistake and not a violation (0.9.2).
                        //
                        // Reset rather than a blanket wipe, because spec 0.9.1 puts the general
                        // rules in the drawer before the first shift and taking those away would
                        // be a state the game never has. No anomaly's own page survives it, which
                        // is what path 2 actually needs.
                        ServiceHub.Manual.Reset();
                        return "binder reset to its opening state (" +
                               ServiceHub.Manual.UnlockedCount + " page(s) left, spec 0.9.1)";

                    case "manual.counter":
                        ServiceHub.ManualEvents.SetCounter(parts[1], parts[2], int.Parse(parts[3]));
                        return parts[1] + "." + parts[2] + " = " +
                               ServiceHub.ManualEvents.Counter(parts[1], parts[2]);

                    case "manual.step":
                        return ServiceHub.ManualEvents.CompleteObjective(parts[1], parts[2])
                            ? parts[1] + " step " + parts[2] + " done"
                            : "no such step, or the event is not running";

                    case "tool.wake":
                    {
                        if (parts[1] == "all")
                        {
                            for (int i = 0; i < ManualEventIds.Tools.Length; i++)
                                ServiceHub.AnomalyTools.ForceWake(ManualEventIds.Tools[i]);
                            return "every machine is awake";
                        }

                        return ServiceHub.AnomalyTools.ForceWake(parts[1])
                            ? parts[1] + " is awake" : "unknown tool " + parts[1];
                    }

                    case "hints.set":
                    {
                        // Spec 30.2 path 10: the same anomaly with the accessibility hints on.
                        HintMode mode;
                        switch (parts[1].ToLowerInvariant())
                        {
                            case "off": mode = HintMode.Off; break;
                            case "delayed": mode = HintMode.Delayed; break;
                            case "always": mode = HintMode.Always; break;
                            default: return "hints.set off|delayed|always";
                        }

                        ServiceHub.Settings.Current.hintMode = (int)mode;
                        ServiceHub.Settings.Apply();
                        ServiceHub.Hints.Reset();
                        return "hints = " + mode;
                    }

                    case "scene.goto":
                    {
                        var cb = TeleportToZone;
                        if (cb == null) return "no teleport handler";
                        cb(parts[1]);
                        return "moved to " + parts[1];
                    }

                    case "cctv.trigger":
                        return ServiceHub.Cctv.TriggerAnomaly(parts[1], parts[2])
                            ? "triggered " + parts[2] + " on " + parts[1]
                            : "unknown camera or anomaly";

                    case "call.force":
                        return ServiceHub.Phone.ForceCall(parts[1])
                            ? "ringing " + parts[1] : "unknown call " + parts[1];

                    case "call.answer":
                        ServiceHub.Phone.Answer();
                        return ServiceHub.Phone.InCall ? "answered" : "nothing to answer";

                    case "circuit.set":
                        return ServiceHub.Facility.TrySetCircuit(parts[1], bool.Parse(parts[2]))
                            ? parts[1] + " = " + parts[2] + " (" + ServiceHub.Facility.ActiveCircuitCount +
                              "/" + CircuitIds.MaxSimultaneous + ")"
                            : "refused: unknown circuit or the budget is full";

                    case "ending.eval":
                    {
                        var ending = ServiceHub.Endings.Evaluate();
                        return ending != null ? "would reach " + ending.endingId : "no ending matched";
                    }

                    case "save.now":
                        ServiceHub.Save.SaveAsync(Save.SaveReason.Manual, System.Threading.CancellationToken.None);
                        return "saving...";

                    case "save.corrupt_test":
                        return CorruptTest();

                    case "log.dump":
                        Debug.Log(Log.Dump());
                        return "log written to the Unity console";

                    case "analytics.dump":
                        Debug.Log(ServiceHub.Analytics.Export());
                        return ServiceHub.Analytics.Count + " events written to the Unity console";

                    case "dev.mainonly":
                    {
                        // The whole night except its spine, off. MainOnlyMode says what
                        // survives it, why it is not saved, and why it needs a night.start.
                        if (parts.Length > 1)
                            MainOnlyMode.Set(parts[1] == "on" || parts[1] == "true" || parts[1] == "1");

                        return "main-quest-only is " + (MainOnlyMode.Active ? "on" : "off") +
                               " - run night.start N to rebuild the shift with it";
                    }

                    case "loc.missing":
                        return string.Join(", ", new List<string>(ServiceHub.Localization.MissingKeys).ToArray());

                    default:
                        return "unknown command: " + command;
                }
            }
            catch (Exception e)
            {
                return "error: " + e.Message;
            }
        }

        static string WhyNot(string caseId)
        {
            var runtime = ServiceHub.Cases.Find(caseId);
            if (runtime == null) return "unknown case";
            if (runtime.State != Cases.CaseState.Dormant) return "state is " + runtime.State;

            string reason;
            if (!Cases.ConditionEvaluator.EvaluateAll(runtime.Definition.startConditions, out reason)) return reason;
            if (runtime.Definition.nightIndex != ServiceHub.State.NightIndex)
                return "case belongs to night " + runtime.Definition.nightIndex;

            return "no reason recorded";
        }

        static string CorruptTest()
        {
            // Writes a deliberately broken payload so the checksum + fallback path can be
            // exercised without hand-editing files (GDD 28.5 save test matrix).
            var path = System.IO.Path.Combine(Application.persistentDataPath, "saves");
            System.IO.Directory.CreateDirectory(path);
            System.IO.File.WriteAllText(System.IO.Path.Combine(path, "slot0.json"), "deadbeef\n{ not json");
            return "slot0 corrupted; load should fall back to the next newest slot";
        }

        static string Help()
        {
            return "night.start N | night.set N | clock.set HH:MM | " +
                   "pressure.add N | pressure.show | power.drain N | power.show | " +
                   "case.start ID | case.complete ID DECISION | " +
                   "evidence.give ID | stat.set ID N | flag.set ID true|false | scene.goto ZONE | " +
                   "cctv.trigger CAM ANOMALY | call.force ID | call.answer | " +
                   "circuit.set ID true|false | ending.eval | save.now | save.corrupt_test | " +
                   "log.dump | analytics.dump | loc.missing | dev.mainonly on|off\n" +
                   "shift.why | " +
                   "v2.1 QA (spec 30.2): risk.exposure N | risk.floor FLOOR N | risk.show | " +
                   "manual.list | manual.start ID | manual.page ID|all | manual.forget | " +
                   "manual.counter ID NAME N | manual.step ID STEP | " +
                   "tool.wake ID|all | hints.set off|delayed|always";
        }
    }
}
