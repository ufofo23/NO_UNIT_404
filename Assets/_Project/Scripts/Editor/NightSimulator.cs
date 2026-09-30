using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using NO404.Cases;
using NO404.Core;
using NO404.Visitors;
using NO404.Facility;
using NO404.Pressure;
using NO404.Residents;

namespace NO404.EditorTools
{
    /// <summary>
    /// Plays a shift without a player and reports what the night did (GDD 25.4).
    ///
    /// GDD 15.4 fixes its numbers against a claim - "night six spent doing nothing arrives
    /// exactly once" - and that claim is only checkable against the real content volumes:
    /// how many contradictory crossings a night actually schedules, how many authored
    /// anomalies fire, how many callers reach the door. Those come out of
    /// ResidentTrafficService and the content database, not out of an estimate.
    ///
    /// So this boots the real services, builds the real night, and runs the real
    /// NightPressureService and NightPowerService against a few plausible players.
    /// </summary>
    public static class NightSimulator
    {
        const int StepSeconds = 30;

        /// <summary>How long after a sighting the player has stopped being able to file it.</summary>
        const int ReportWindowSeconds = 90;

        sealed class Policy
        {
            public string Name;
            /// <summary>Fraction of contradictory crossings the player catches and files.</summary>
            public float TrafficCatch;
            /// <summary>Fraction of authored anomalies the player catches and files.</summary>
            public float AnomalyCatch;
            /// <summary>Fraction of callers judged correctly.</summary>
            public float VisitorCorrect;
            /// <summary>Fraction of the night's cases closed as Correct rather than timing out.</summary>
            public float CaseCorrect;
            /// <summary>True when the player sits on one full-screen channel all shift.</summary>
            public bool WatchesSingleChannel;
            /// <summary>
            /// True when the player answers the knocking by bolting the office door (GDD 15.6).
            /// This is the counterplay the ladder is supposed to have, so it has to be measured
            /// - including its cost, which is every caller left standing outside.
            /// </summary>
            public bool BoltsTheDoor;
        }

        static readonly Policy[] Policies =
        {
            new Policy { Name = "방치",     TrafficCatch = 0.00f, AnomalyCatch = 0.00f, VisitorCorrect = 0.00f, CaseCorrect = 0.00f, WatchesSingleChannel = false },
            new Policy { Name = "초보",     TrafficCatch = 0.10f, AnomalyCatch = 0.35f, VisitorCorrect = 0.50f, CaseCorrect = 0.50f, WatchesSingleChannel = false },
            new Policy { Name = "보통",     TrafficCatch = 0.25f, AnomalyCatch = 0.65f, VisitorCorrect = 0.75f, CaseCorrect = 0.80f, WatchesSingleChannel = false },
            new Policy { Name = "성실",     TrafficCatch = 0.55f, AnomalyCatch = 0.90f, VisitorCorrect = 1.00f, CaseCorrect = 1.00f, WatchesSingleChannel = true  },
            new Policy { Name = "보통+잠금", TrafficCatch = 0.25f, AnomalyCatch = 0.65f, VisitorCorrect = 0.75f, CaseCorrect = 0.80f, WatchesSingleChannel = false, BoltsTheDoor = true },
        };

        [MenuItem("Tools/NO404/Data/Simulate Nights", priority = 51)]
        public static void Simulate()
        {
            var root = new GameObject("NightSimulator");
            try
            {
                ServiceHub.Initialize(root.AddComponent<SimulationRunner>(), root.transform);

                var report = new StringBuilder();
                report.AppendLine("=== NO404 night simulation (GDD 25.4) ===");

                ReportVolumes(report);
                ReportPressure(report);
                ReportPower(report);

                Debug.Log(report.ToString());
            }
            finally
            {
                ServiceHub.Shutdown();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        sealed class SimulationRunner : MonoBehaviour { }

        // ---- what a night is actually made of --------------------------------

        struct NightVolume
        {
            public int TrafficTotal;
            public int TrafficWrong;
            public int Anomalies;
            public int Visitors;
            public int RefusableVisitors;
            public int DeadlineCases;
            public List<int> WrongCrossingTimes;
        }

        static NightVolume MeasureNight(int nightIndex)
        {
            var volume = new NightVolume { WrongCrossingTimes = new List<int>() };

            ServiceHub.State.ResetToNewGame();
            ServiceHub.State.BeginNight(nightIndex);
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond);
            ServiceHub.Traffic.BeginNight(nightIndex);

            foreach (var movement in ServiceHub.Traffic.Scheduled)
            {
                volume.TrafficTotal++;
                if (movement.Kind == TrafficKind.Ordinary) continue;

                volume.TrafficWrong++;
                volume.WrongCrossingTimes.Add(movement.GameSecond);
            }

            foreach (var anomaly in ServiceHub.Content.Anomalies)
                if (anomaly != null && anomaly.nightIndex == nightIndex) volume.Anomalies++;

            foreach (var visitor in ServiceHub.Content.Visitors)
            {
                if (visitor == null || visitor.nightIndex != nightIndex) continue;

                volume.Visitors++;
                if (visitor.ShouldBeRefused) volume.RefusableVisitors++;
            }

            foreach (var definition in ServiceHub.Content.Cases)
            {
                if (definition == null || definition.nightIndex != nightIndex) continue;
                if (definition.failSafe != null && definition.failSafe.enabled &&
                    definition.failSafe.triggerGameSecond > 0) volume.DeadlineCases++;
            }

            return volume;
        }

        static void ReportVolumes(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("-- what each night is made of --");
            report.AppendLine("night | traffic | wrong | anomalies | visitors | refusable | deadline cases");

            // Night 0 is not simulated because night 0 is not played (GDD 9.1).
            for (int night = 1; night <= 6; night++)
            {
                var v = MeasureNight(night);
                report.AppendLine(string.Format("{0,5} | {1,7} | {2,5} | {3,9} | {4,8} | {5,9} | {6,14}",
                    night, v.TrafficTotal, v.TrafficWrong, v.Anomalies, v.Visitors,
                    v.RefusableVisitors, v.DeadlineCases));
            }
        }

        // ---- the pressure run -------------------------------------------------

        struct Event
        {
            public int GameSecond;
            public Action<NightPressureService> Apply;
            /// <summary>Callers are one of the two events a bolted door changes the outcome of.</summary>
            public bool IsVisitor;
            /// <summary>The other: case work is on the far side of the office door (GDD 15.6).</summary>
            public bool IsCase;

            public Event(int gameSecond, Action<NightPressureService> apply,
                         bool isVisitor = false, bool isCase = false)
            {
                GameSecond = gameSecond; Apply = apply; IsVisitor = isVisitor; IsCase = isCase;
            }
        }

        static void ReportPressure(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("-- pressure at 06:00, and confrontations on the way --");
            report.AppendLine("night |        방치 |        초보 |        보통 |        성실 |    보통+잠금");

            for (int night = 1; night <= 6; night++)
            {
                var volume = MeasureNight(night);
                var row = new StringBuilder(string.Format("{0,5} |", night));

                for (int p = 0; p < Policies.Length; p++)
                {
                    int confrontations;
                    int peak;
                    int final = RunNight(night, volume, Policies[p], out confrontations, out peak);
                    row.Append(string.Format(" {0,3} (x{1}, ^{2,3}) |", final, confrontations, peak));
                }

                report.AppendLine(row.ToString());
            }

            report.AppendLine("   값 = 06:00 압박, x = 대면 횟수, ^ = 최고점");
        }

        static int RunNight(int nightIndex, NightVolume volume, Policy policy,
                            out int confrontations, out int peak)
        {
            // Seeded per night and policy so a re-run reports the same night.
            var random = new System.Random(unchecked(nightIndex * 7919 + policy.Name.GetHashCode()));

            var pressure = new NightPressureService();
            ServiceHub.State.ResetToNewGame();
            ServiceHub.State.BeginNight(nightIndex);
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond);
            pressure.BeginNight(nightIndex);

            var events = BuildEvents(volume, policy, random);
            events.Sort((a, b) => a.GameSecond.CompareTo(b.GameSecond));

            peak = 0;
            int next = 0;

            while (ServiceHub.Clock.GameSecond < GameClock.ShiftEndSecond)
            {
                ServiceHub.Clock.AdvanceSeconds(StepSeconds);
                int now = ServiceHub.Clock.GameSecond;

                while (next < events.Count && events[next].GameSecond <= now)
                {
                    // Bolted, the front-door release is dead: a caller who arrives now is left
                    // on the step rather than judged either way (GDD 15.6).
                    if (pressure.OfficeDoorLocked && events[next].IsVisitor) pressure.NoteGenuineRefused();
                    else if (pressure.OfficeDoorLocked && events[next].IsCase) pressure.NoteDeadlineMissed();
                    else events[next].Apply(pressure);

                    next++;
                }

                pressure.Tick();

                // The player answers the knocking, and stops hiding once it is quiet again.
                if (policy.BoltsTheDoor)
                {
                    if (pressure.Stage >= PressureStage.AtTheDoor) pressure.OfficeDoorLocked = true;
                    else if (pressure.Value < NightPressureService.NearAt) pressure.OfficeDoorLocked = false;
                }

                if (pressure.Value > peak) peak = pressure.Value;
            }

            confrontations = pressure.ConfrontationCount;
            return pressure.Value;
        }

        static List<Event> BuildEvents(NightVolume volume, Policy policy, System.Random random)
        {
            var events = new List<Event>();

            // How many admissions this night can still go wrong; see the door loop below.
            int refusableRemaining = volume.RefusableVisitors;

            // Contradictory crossings (GDD 12.6). Dozens per night on the late shifts, which
            // is exactly why their weight has to be measured rather than assumed.
            for (int i = 0; i < volume.WrongCrossingTimes.Count; i++)
            {
                int at = volume.WrongCrossingTimes[i];

                if (random.NextDouble() < policy.TrafficCatch)
                    events.Add(new Event(at, p => p.NoteTrafficReported()));
                else
                    events.Add(new Event(at + ReportWindowSeconds, p => p.NoteTrafficMissed()));
            }

            // Authored anomalies (GDD 12.3), spread across the shift.
            for (int i = 0; i < volume.Anomalies; i++)
            {
                int at = GameClock.ShiftStartSecond + (int)((i + 0.5f) / Mathf.Max(1, volume.Anomalies) * 8 * 3600);

                if (random.NextDouble() < policy.AnomalyCatch)
                    events.Add(new Event(at, p => p.NoteReportCorrect()));
                else
                    events.Add(new Event(at + ReportWindowSeconds, p => p.NoteSightingExpired()));
            }

            // Callers at the front door (GDD 13.4).
            //
            // Getting a caller wrong is two different mistakes and they do not cost the same.
            // Turning away someone who lives here costs trust and a little pressure; letting
            // in someone who should have been refused puts a body in the building that then
            // crosses the cameras four times (GDD 15.4). Only refusable callers can produce
            // the second kind, so the number of intruders a night can contain is capped by how
            // many refusable callers it has - not by how many callers there are.
            //
            // This used to charge every misjudgement as an admitted intruder, which meant the
            // model punished volume itself: tripling the ordinary traffic at the door tripled
            // the simulated intruders even though the number of people who could actually be
            // let in wrongly had not changed. That made the measured curve in GDD 25.4 unusable
            // for judging any change to the roster size, which is the main thing it is for.
            for (int i = 0; i < volume.Visitors; i++)
            {
                int at = GameClock.ShiftStartSecond + (int)((i + 0.5f) / Mathf.Max(1, volume.Visitors) * 6 * 3600);

                if (random.NextDouble() < policy.VisitorCorrect)
                {
                    events.Add(new Event(at, p => p.NoteVisitorCorrect(), true));
                    continue;
                }

                // The refusable ones are scheduled first, so the first misjudgements are the
                // admissions and the rest are residents left on the step.
                if (refusableRemaining <= 0)
                {
                    events.Add(new Event(at, p => p.NoteGenuineRefused(), true));
                    continue;
                }

                refusableRemaining--;
                events.Add(new Event(at, p => p.NoteWrongAdmit("sim_visitor_" + at), true));

                // And then they walk: four unrecorded crossings the player has to find.
                for (int c = 0; c < ResidentTrafficService.IntruderCrossings; c++)
                {
                    int crossing = at + ResidentTrafficService.IntruderFirstDelaySeconds
                                      + c * ResidentTrafficService.IntruderGapSeconds;
                    if (crossing >= GameClock.ShiftEndSecond) break;

                    bool caught = random.NextDouble() < policy.TrafficCatch;
                    string id = "sim_visitor_" + at;

                    if (caught) events.Add(new Event(crossing, p => p.ClearIntruder(id)));
                    else events.Add(new Event(crossing + ReportWindowSeconds, p => p.NoteIntruderMissed()));
                }
            }

            // Cases with a deadline (GDD 15.7).
            for (int i = 0; i < volume.DeadlineCases; i++)
            {
                int at = GameClock.ShiftStartSecond + (int)((i + 1f) / (volume.DeadlineCases + 1f) * 7 * 3600);

                if (random.NextDouble() < policy.CaseCorrect)
                    events.Add(new Event(at, p => p.NoteCaseClosedWell(), false, true));
                else
                    events.Add(new Event(at, p => p.NoteDeadlineMissed(), false, true));
            }

            return events;
        }

        // ---- the reserve run --------------------------------------------------

        static void ReportPower(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("-- night reserve at 06:00, no breaker trips --");
            report.AppendLine("night | 아무것도 안 켬 | 그리드 상주 | 단일 채널 상주 | 단일 채널 소진 시각");

            for (int night = 1; night <= 6; night++)
            {
                int idle = RunPower(night, false, false);
                int grid = RunPower(night, true, true);
                int single = RunPower(night, true, false);
                string emptied = RunPowerUntilEmpty(night);

                report.AppendLine(string.Format("{0,5} | {1,14} | {2,11} | {3,14} | {4}",
                    night, idle + "%", grid + "%", single + "%", emptied));
            }
        }

        static int RunPower(int nightIndex, bool watching, bool gridMode)
        {
            var power = PreparePower(nightIndex, watching, gridMode);

            while (ServiceHub.Clock.GameSecond < GameClock.ShiftEndSecond)
            {
                ServiceHub.Clock.AdvanceSeconds(StepSeconds);
                power.Tick();
            }

            return power.ReservePercent;
        }

        static string RunPowerUntilEmpty(int nightIndex)
        {
            var power = PreparePower(nightIndex, true, false);

            while (ServiceHub.Clock.GameSecond < GameClock.ShiftEndSecond)
            {
                ServiceHub.Clock.AdvanceSeconds(StepSeconds);
                power.Tick();
                if (power.IsDead) return ServiceHub.Clock.ToClockString();
            }

            return "-";
        }

        static NightPowerService PreparePower(int nightIndex, bool watching, bool gridMode)
        {
            ServiceHub.State.ResetToNewGame();
            ServiceHub.State.BeginNight(nightIndex);
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond);

            ServiceHub.Player.Reset();
            ServiceHub.Player.SetPcMode(watching);
            if (watching) ServiceHub.Player.OpenApp(AppIds.Cctv);
            ServiceHub.Cctv.SetGridMode(gridMode);

            var power = new NightPowerService();
            power.BeginNight(nightIndex);
            return power;
        }
    }
}
