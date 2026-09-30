using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using NO404.CCTV;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.EditorTools
{
    /// <summary>
    /// Measures the GDD 20.20 performance budget against the GDD 28.7 scenes.
    ///
    /// The budget has been written down since v1.0 and never measured once, which makes it a
    /// wish rather than a budget. This runs the scenes it names, samples frame time and
    /// allocation over a fixed window, and prints the result next to the number it is supposed
    /// to meet - so the answer is "12.4ms against a 12ms budget", not "feels fine".
    ///
    /// Run from the menu with the Game view open, or in batch mode **with graphics**:
    ///   Unity.exe -batchmode -projectPath . -executeMethod NO404.EditorTools.PerformanceHarness.RunBatch
    /// Never with -nographics. A frame time measured with no renderer attached is not a
    /// measurement of anything, and reporting one would be worse than reporting nothing.
    /// </summary>
    public static class PerformanceHarness
    {
        // ---- GDD 20.20 ------------------------------------------------------

        /// <summary>Main thread, 1080p High, ordinary play.</summary>
        public const float FrameBudgetMs = 12f;

        /// <summary>Main thread with the CCTV grid live - six cameras rendering at once.</summary>
        public const float CctvFrameBudgetMs = 16f;

        /// <summary>Per-frame managed allocation. The stated goal is zero in the steady state.</summary>
        public const int AllocBudgetBytes = 1024;

        /// <summary>Total memory in play.</summary>
        public const long MemoryBudgetBytes = 4L * 1024 * 1024 * 1024;

        /// <summary>MonoBehaviours carrying an Update.</summary>
        public const int UpdateBehaviourBudget = 200;

        const int WarmupFrames = 120;
        const int SampleFrames = 600;

        sealed class Scenario
        {
            public string Name;
            public string ZoneId;
            public bool CctvGrid;
            public float BudgetMs;
        }

        // GDD 28.7. Rain, smoke and the fire are art-pass effects that do not exist yet, so
        // those scenarios measure the space without them and will get heavier - which is the
        // point of measuring now rather than after.
        static readonly Scenario[] Scenarios =
        {
            new Scenario { Name = "office cctv 6-up",  ZoneId = ZoneIds.Office,  CctvGrid = true,  BudgetMs = CctvFrameBudgetMs },
            new Scenario { Name = "office idle",       ZoneId = ZoneIds.Office,  CctvGrid = false, BudgetMs = FrameBudgetMs },
            new Scenario { Name = "basement parking",  ZoneId = ZoneIds.Parking, CctvGrid = false, BudgetMs = FrameBudgetMs },
            new Scenario { Name = "lobby evacuation",  ZoneId = ZoneIds.Lobby,   CctvGrid = false, BudgetMs = FrameBudgetMs },
            new Scenario { Name = "unit 404",          ZoneId = ZoneIds.Unit404, CctvGrid = false, BudgetMs = FrameBudgetMs },
        };

        sealed class Result
        {
            public string Name;
            public float BudgetMs;
            public float MeanMs;
            public float P95Ms;
            public float WorstMs;
            public long AllocPerFrame;
            public long MemoryBytes;
            public int UpdateBehaviours;

            public bool WithinBudget { get { return P95Ms <= BudgetMs; } }
        }

        [MenuItem("Tools/NO404/Release/Measure Performance", priority = 152)]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "NO404",
                    "Enter Play mode first. Frame time cannot be measured in edit mode.",
                    "OK");
                return;
            }

            EditorCoroutineRunner.Start(Measure());
        }

        /// <summary>Survives the domain reload that entering play mode causes.</summary>
        const string PendingKey = "NO404.PerformanceHarness.Pending";

        /// <summary>Batch entry. Enters play mode, measures, writes the report, exits.</summary>
        public static void RunBatch()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("[NO404] performance run started with no graphics device. " +
                               "Drop -nographics; a frame time measured without a renderer is meaningless.");
                EditorApplication.Exit(2);
                return;
            }

            // Entering play mode reloads the domain, which throws away every delegate this
            // class has registered - including the one that would have driven the run. The
            // flag is what survives; Resume picks the work back up on the other side.
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

            SessionState.SetBool(PendingKey, false);
            EditorCoroutineRunner.Start(BatchRoutine());
        }

        static IEnumerator BatchRoutine()
        {
            while (!Application.isPlaying) yield return null;
            while (!ServiceHub.Ready) yield return null;
            while (GameLoop.Instance == null) yield return null;

            yield return Measure();

            EditorApplication.Exit(0);
        }

        static IEnumerator Measure()
        {
            var loop = GameLoop.Instance;
            if (loop == null)
            {
                Debug.LogError("[NO404] no GameLoop; start the game before measuring");
                yield break;
            }

            // A real shift, not an empty scene: the services that cost anything - traffic,
            // pressure, the reserve, the CCTV schedule - only run inside one.
            loop.NewGame();
            while (loop.Mode != GameMode.Playing) yield return null;

            var results = new List<Result>();

            foreach (var scenario in Scenarios)
            {
                ServiceHub.Zones.RequestZone(scenario.ZoneId);

                int waited = 0;
                while (!ZoneRegistry.IsLoaded(scenario.ZoneId) && ++waited < 600) yield return null;

                if (!ZoneRegistry.IsLoaded(scenario.ZoneId))
                {
                    Debug.LogWarning("[NO404] " + scenario.Name + ": zone " + scenario.ZoneId +
                                     " never loaded; skipped");
                    continue;
                }

                ServiceHub.Player.EnterZone(scenario.ZoneId);
                loop.OpenPc(scenario.CctvGrid);

                yield return Sample(scenario, results);
            }

            loop.OpenPc(false);
            Report(results);
        }

        static IEnumerator Sample(Scenario scenario, List<Result> results)
        {
            for (int i = 0; i < WarmupFrames; i++) yield return null;

            // The collection that would otherwise land mid-window and be reported as a spike
            // the game does not actually have.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            yield return null;

            var frames = new float[SampleFrames];
            long allocBefore = GC.GetTotalMemory(false);

            for (int i = 0; i < SampleFrames; i++)
            {
                frames[i] = Time.unscaledDeltaTime * 1000f;
                yield return null;
            }

            long allocAfter = GC.GetTotalMemory(false);

            Array.Sort(frames);

            double sum = 0.0;
            for (int i = 0; i < frames.Length; i++) sum += frames[i];

            results.Add(new Result
            {
                Name = scenario.Name,
                BudgetMs = scenario.BudgetMs,
                MeanMs = (float)(sum / frames.Length),
                P95Ms = frames[Mathf.Clamp((int)(frames.Length * 0.95f), 0, frames.Length - 1)],
                WorstMs = frames[frames.Length - 1],
                AllocPerFrame = Math.Max(0, (allocAfter - allocBefore) / SampleFrames),
                MemoryBytes = Profiler.GetTotalAllocatedMemoryLong(),
                UpdateBehaviours = CountUpdateBehaviours()
            });
        }

        /// <summary>
        /// GDD 20.20 caps MonoBehaviours with an Update at 200. Counted by reflection because
        /// the alternative is trusting that nobody added one, which is how the number got to
        /// be worth capping.
        /// </summary>
        static int CountUpdateBehaviours()
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly;

            var seen = new Dictionary<Type, bool>();
            int count = 0;

            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour == null) continue;

                var type = behaviour.GetType();

                bool hasUpdate;
                if (!seen.TryGetValue(type, out hasUpdate))
                {
                    hasUpdate = false;
                    for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                    {
                        if (t.GetMethod("Update", Flags) == null) continue;
                        hasUpdate = true;
                        break;
                    }
                    seen[type] = hasUpdate;
                }

                if (hasUpdate) count++;
            }

            return count;
        }

        static void Report(List<Result> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[NO404] performance — GDD 20.20 budget");
            sb.AppendLine("  device: " + SystemInfo.graphicsDeviceName);
            sb.AppendLine("  cpu:    " + SystemInfo.processorType);
            sb.AppendLine("  screen: " + Screen.width + "x" + Screen.height +
                          "  quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            sb.AppendLine();
            sb.AppendLine("  scenario            mean    p95   worst  budget  alloc/f  updates");

            bool over = false;
            foreach (var r in results)
            {
                over |= !r.WithinBudget;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,-18} {1,6:0.00} {2,6:0.00} {3,6:0.00} {4,6:0.0} {5,8} {6,8}{7}",
                    r.Name, r.MeanMs, r.P95Ms, r.WorstMs, r.BudgetMs,
                    r.AllocPerFrame + "B", r.UpdateBehaviours,
                    r.WithinBudget ? "" : "   OVER"));
            }

            if (results.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  memory: " + (results[0].MemoryBytes / 1048576) + " MB of a " +
                              (MemoryBudgetBytes / 1048576) + " MB budget");
            }

            sb.AppendLine();
            sb.AppendLine("  Measured on this machine, not on the GDD 20.2 minimum spec " +
                          "(i5-8400 / GTX 1060). Treat a pass here as necessary, not sufficient.");

            var text = sb.ToString();
            if (over) Debug.LogWarning(text); else Debug.Log(text);

            Directory.CreateDirectory("Builds");
            var path = Path.Combine("Builds", "performance_" +
                                    DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt");
            File.WriteAllText(path, text);
            Debug.Log("[NO404] performance report written to " + path);
        }
    }

    /// <summary>
    /// Runs a coroutine from the editor. The harness has to yield across frames to measure
    /// them, and an editor menu item has no MonoBehaviour to run on.
    /// </summary>
    public static class EditorCoroutineRunner
    {
        public static void Start(IEnumerator routine)
        {
            EditorApplication.CallbackFunction step = null;

            step = () =>
            {
                bool alive;
                try
                {
                    alive = routine.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogError("[NO404] performance run failed: " + e);
                    alive = false;
                }

                if (!alive) EditorApplication.update -= step;
            };

            EditorApplication.update += step;
        }
    }
}
