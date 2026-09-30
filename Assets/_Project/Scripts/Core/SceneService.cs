using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NO404.Core
{
    public static class SceneNames
    {
        public const string Bootstrap = "SCN_Bootstrap";
        public const string MainMenu = "SCN_MainMenu";
        public const string Building = "SCN_Building";
    }

    /// <summary>
    /// Owns every scene transition (GDD 20.5). Two rules from the GDD are enforced here:
    /// events never trigger a transition directly (callers must go through this service),
    /// and a horror "fake loading" screen is a separate concept from real load progress.
    /// </summary>
    public sealed class SceneService
    {
        readonly MonoBehaviour _runner;
        readonly HashSet<string> _additiveLoaded = new HashSet<string>();

        public bool IsBusy { get; private set; }
        public float Progress { get; private set; }
        public string ActiveGameplayScene { get; private set; }
        public event Action<string> OnSceneReady;

        public SceneService(MonoBehaviour coroutineRunner)
        {
            _runner = coroutineRunner;
        }

        public void LoadSingle(string sceneName, Action onDone = null)
        {
            if (IsBusy)
            {
                Log.Warn("Scene", "load requested while busy, ignored: " + sceneName);
                return;
            }

            _runner.StartCoroutine(LoadSingleRoutine(sceneName, onDone));
        }

        IEnumerator LoadSingleRoutine(string sceneName, Action onDone)
        {
            IsBusy = true;
            Progress = 0f;
            Log.Info("Scene", "loading " + sceneName);

            _additiveLoaded.Clear();

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Log.Fatal("Scene", "scene not in build settings: " + sceneName);
                IsBusy = false;
                yield break;
            }

            while (!op.isDone)
            {
                Progress = Mathf.Clamp01(op.progress / 0.9f);
                yield return null;
            }

            Progress = 1f;
            ActiveGameplayScene = sceneName;
            IsBusy = false;

            var cb = OnSceneReady;
            if (cb != null) cb(sceneName);
            if (onDone != null) onDone();

            Log.Info("Scene", "ready " + sceneName);
        }

        /// <summary>
        /// Additive preload used by doors and the elevator. The caller must not open the
        /// door until <paramref name="onReady"/> fires with true (GDD 20.5 loading strategy).
        /// </summary>
        public void PreloadAdditive(string sceneName, Action<bool> onReady)
        {
            if (_additiveLoaded.Contains(sceneName))
            {
                if (onReady != null) onReady(true);
                return;
            }

            _runner.StartCoroutine(PreloadRoutine(sceneName, onReady));
        }

        IEnumerator PreloadRoutine(string sceneName, Action<bool> onReady)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Log.Error("Scene", "additive load failed: " + sceneName);
                if (onReady != null) onReady(false);
                yield break;
            }

            while (!op.isDone) yield return null;

            _additiveLoaded.Add(sceneName);
            if (onReady != null) onReady(true);
        }

        public void UnloadAdditive(string sceneName)
        {
            if (!_additiveLoaded.Remove(sceneName)) return;
            SceneManager.UnloadSceneAsync(sceneName);
            Resources.UnloadUnusedAssets();
        }

        public bool IsAdditiveLoaded(string sceneName) { return _additiveLoaded.Contains(sceneName); }
    }
}
