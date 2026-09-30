using System.Collections.Generic;
using UnityEngine;

namespace NO404.Gameplay
{
    /// <summary>
    /// Maps zone ids to their scene roots. Populated by WorldBuilder so nothing in the
    /// project ever needs GameObject.Find (GDD 21.2 non-negotiable rules).
    /// </summary>
    public static class ZoneRegistry
    {
        static readonly Dictionary<string, Transform> Roots = new Dictionary<string, Transform>();
        static readonly Dictionary<string, Transform> Spawns = new Dictionary<string, Transform>();

        public static void Register(string zoneId, Transform root, Transform spawn)
        {
            if (string.IsNullOrEmpty(zoneId) || root == null) return;
            Roots[zoneId] = root;
            if (spawn != null) Spawns[zoneId] = spawn;
        }

        public static Transform Find(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            Transform root;
            return Roots.TryGetValue(zoneId, out root) ? root : null;
        }

        public static Transform FindSpawn(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            Transform spawn;
            return Spawns.TryGetValue(zoneId, out spawn) ? spawn : null;
        }

        /// <summary>Called when a streamed scene unloads so no id points at a dead object.</summary>
        public static void Unregister(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return;
            Roots.Remove(zoneId);
            Spawns.Remove(zoneId);
        }

        public static bool IsLoaded(string zoneId)
        {
            Transform root;
            return Roots.TryGetValue(zoneId, out root) && root != null;
        }

        public static void Clear()
        {
            Roots.Clear();
            Spawns.Clear();
        }
    }
}
