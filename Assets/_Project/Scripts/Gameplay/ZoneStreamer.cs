using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Additive zone streaming (GDD 20.5).
    ///
    /// Rules taken straight from the design:
    ///  - the office and lobby are always resident, so the PC is never a load away
    ///  - a door preloads its destination while the player is looking at it and refuses to
    ///    open until the load has finished
    ///  - a failed load leaves the door shut and retryable rather than dropping the player
    ///    into nothing
    ///  - real loading and the horror "SIGNAL LOST" are different states; the CCTV app asks
    ///    for a zone and shows a spinner while it streams
    ///
    /// Groups are unloaded once nothing has asked for them for a few seconds and the player
    /// is somewhere else.
    /// </summary>
    public sealed class ZoneStreamer
    {
        public const float KeepAliveSeconds = 6f;

        // Unloading is not a nicety. SceneManager.UnloadSceneAsync finishes a frame or more
        // after it is asked, and without a state for that window a group that is still going
        // away reads as already gone - so the next request additively loads a second copy of
        // a scene Unity is in the middle of destroying.
        enum GroupStatus { Unloaded, Loading, Loaded, Unloading, Failed }

        sealed class GroupState
        {
            public GroupStatus Status = GroupStatus.Unloaded;
            public float RequestedUntilRealtime;
            public int FailureCount;
        }

        readonly Dictionary<string, GroupState> _groups = new Dictionary<string, GroupState>();
        readonly MonoBehaviour _runner;

        public ZoneStreamer(MonoBehaviour runner)
        {
            _runner = runner;
            for (int i = 0; i < ZoneGroups.All.Length; i++)
                _groups[ZoneGroups.All[i]] = new GroupState();
        }

        public event Action<string> OnGroupLoaded;
        public event Action<string> OnGroupUnloaded;

        public bool IsGroupLoaded(string group)
        {
            GroupState state;
            return _groups.TryGetValue(group, out state) && state.Status == GroupStatus.Loaded;
        }

        public bool IsZoneReady(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return false;
            return ZoneRegistry.IsLoaded(zoneId);
        }

        public bool HasFailed(string zoneId)
        {
            var group = ZoneGroups.GroupOf(zoneId);
            GroupState state;
            return group != null && _groups.TryGetValue(group, out state) && state.Status == GroupStatus.Failed;
        }

        // ---- requests --------------------------------------------------------

        /// <summary>
        /// Asks for a zone and keeps it alive for a few seconds. Callers renew by asking
        /// again, which is what looking at a door or watching a camera does every frame.
        /// </summary>
        public void RequestZone(string zoneId)
        {
            var group = ZoneGroups.GroupOf(zoneId);
            if (group == null) return;

            RequestGroup(group);
        }

        public void RequestGroup(string group)
        {
            GroupState state;
            if (!_groups.TryGetValue(group, out state)) return;

            state.RequestedUntilRealtime = Time.realtimeSinceStartup + KeepAliveSeconds;

            // Walking out of a floor and straight back into it is ordinary play - the keep
            // alive window is seconds, not minutes - so this path has to be correct rather
            // than unlikely.
            if (state.Status == GroupStatus.Unloaded || state.Status == GroupStatus.Failed
                || state.Status == GroupStatus.Unloading)
                _runner.StartCoroutine(LoadRoutine(group, state));
        }

        /// <summary>Loads the always-resident core group. Blocking on purpose: it is tiny.</summary>
        public void LoadCoreImmediate()
        {
            var state = _groups[ZoneGroups.Core];
            if (state.Status == GroupStatus.Loaded) return;

            SceneManager.LoadScene(ZoneGroups.Core, LoadSceneMode.Additive);
            state.Status = GroupStatus.Loaded;
            state.RequestedUntilRealtime = float.MaxValue;

            Log.Info("Stream", "core loaded");

            var cb = OnGroupLoaded;
            if (cb != null) cb(ZoneGroups.Core);
        }

        IEnumerator LoadRoutine(string group, GroupState state)
        {
            // Let an unload of this same group finish first. Loading over it is what produces
            // two copies of a corridor, and the second copy is solid.
            while (state.Status == GroupStatus.Unloading) yield return null;

            if (state.Status == GroupStatus.Loaded || state.Status == GroupStatus.Loading) yield break;

            state.Status = GroupStatus.Loading;
            Log.Info("Stream", "loading " + group);

            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(group, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                Log.Error("Stream", "load threw for " + group + ": " + e.Message);
            }

            if (op == null)
            {
                state.Status = GroupStatus.Failed;
                state.FailureCount++;
                Log.Error("Stream", group + " is not in Build Settings; the door stays locked");
                yield break;
            }

            while (!op.isDone) yield return null;

            // The scene's builder registers its zones in Awake, so they exist by now.
            bool ok = true;
            var zones = ZoneGroups.ZonesIn(group);
            for (int i = 0; i < zones.Length; i++)
                if (!ZoneRegistry.IsLoaded(zones[i])) ok = false;

            if (!ok)
            {
                state.Status = GroupStatus.Failed;
                state.FailureCount++;
                Log.Error("Stream", group + " loaded but registered no zones");
                yield break;
            }

            state.Status = GroupStatus.Loaded;
            Log.Info("Stream", group + " ready");

            var cb = OnGroupLoaded;
            if (cb != null) cb(group);
        }

        // ---- eviction --------------------------------------------------------

        public void Tick()
        {
            float now = Time.realtimeSinceStartup;
            string playerGroup = ZoneGroups.GroupOf(ServiceHub.Player.CurrentZone);

            for (int i = 0; i < ZoneGroups.Streamed.Length; i++)
            {
                var group = ZoneGroups.Streamed[i];
                var state = _groups[group];

                if (state.Status != GroupStatus.Loaded) continue;
                if (group == playerGroup) { state.RequestedUntilRealtime = now + KeepAliveSeconds; continue; }
                if (now < state.RequestedUntilRealtime) continue;

                Unload(group, state);
            }
        }

        void Unload(string group, GroupState state)
        {
            var scene = SceneManager.GetSceneByName(group);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                state.Status = GroupStatus.Unloaded;
                return;
            }

            state.Status = GroupStatus.Unloading;
            _runner.StartCoroutine(UnloadRoutine(group, state));
        }

        IEnumerator UnloadRoutine(string group, GroupState state)
        {
            AsyncOperation op = null;
            try
            {
                op = SceneManager.UnloadSceneAsync(group);
            }
            catch (Exception e)
            {
                Log.Error("Stream", "unload threw for " + group + ": " + e.Message);
            }

            if (op != null)
                while (!op.isDone) yield return null;

            state.Status = GroupStatus.Unloaded;
            Log.Info("Stream", "unloaded " + group);

            var cb = OnGroupUnloaded;
            if (cb != null) cb(group);
        }

        /// <summary>True while any streamed group is still loading or being torn down.</summary>
        public bool IsSettled
        {
            get
            {
                for (int i = 0; i < ZoneGroups.Streamed.Length; i++)
                {
                    var status = _groups[ZoneGroups.Streamed[i]].Status;
                    if (status == GroupStatus.Loading || status == GroupStatus.Unloading) return false;
                }

                return true;
            }
        }

        /// <summary>Drops every streamed group, e.g. when returning to the menu.</summary>
        public void UnloadAllStreamed()
        {
            for (int i = 0; i < ZoneGroups.Streamed.Length; i++)
            {
                var group = ZoneGroups.Streamed[i];
                var state = _groups[group];
                if (state.Status == GroupStatus.Loaded) Unload(group, state);
                state.RequestedUntilRealtime = 0f;
            }
        }

        public string DebugSummary()
        {
            var parts = new List<string>();
            foreach (var pair in _groups) parts.Add(pair.Key + "=" + pair.Value.Status);
            return string.Join(" ", parts.ToArray());
        }
    }
}
