using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.EditorTools
{
    /// <summary>
    /// What the art pass actually has to make (v2.1 spec 29, GDD 17.10).
    ///
    /// Spec 29 lists assets per anomaly in prose, which is the right level for a designer and
    /// useless to a modeller: it does not say how big the swing set is, where it stands, or
    /// whether the player can walk into it. Those facts only exist in WorldBuilder, and writing
    /// them into a document by hand would produce a table that was wrong within a week.
    ///
    /// So the world is built here, in the editor, and asked. Every prop reports its name - which
    /// is the prefab file name that will replace it - its exact volume, the room it stands in,
    /// and whether it already has art. The result is a work order that cannot drift from the
    /// game, because it is generated from the game.
    /// </summary>
    public static class PropArtReport
    {
        const string OutputPath = "PropArtReport.csv";

        sealed class Entry
        {
            public string Name;
            public string Zone;
            public Vector3 Size;
            public Vector3 LocalPosition;
            public bool Interactable;
            public bool BelongsToAnomaly;
            public string EventId;
            public int Count;
            public bool HasArt;
        }

        [MenuItem("Tools/NO404/Art/Report Prop Coverage", priority = 60)]
        public static void Report()
        {
            var entries = Collect();

            int withArt = 0;
            foreach (var entry in entries.Values) if (entry.HasArt) withArt++;

            var sb = new StringBuilder();
            sb.AppendLine("[NO404] Prop art coverage: " + withArt + " of " + entries.Count +
                          " distinct props have a prefab");
            sb.AppendLine("  Drop a prefab at Resources/" + PropArt.ResourceFolder +
                          "<name>.prefab to replace one. Author it at the size below and do not");
            sb.AppendLine("  pre-scale it: the builder instantiates unscaled, and the volume is what");
            sb.AppendLine("  patrol routes, interaction range and collision were laid out around.");

            var missingAnomaly = new List<Entry>();
            foreach (var entry in entries.Values)
                if (!entry.HasArt && entry.BelongsToAnomaly) missingAnomaly.Add(entry);

            sb.AppendLine();
            sb.AppendLine("  " + missingAnomaly.Count + " of those belong to an M01-M18 anomaly, " +
                          "which spec 29 gates ContentReady on.");

            Debug.Log(sb.ToString());
            WriteCsv(entries);
        }

        static Dictionary<string, Entry> Collect()
        {
            // Built under one root in whatever scene is open and destroyed again, rather than
            // in a scene of its own: batch mode refuses to add a scene while the untitled one
            // is unsaved, and this tool has to run from the command line as well as the menu.
            // Only what hangs off this root is ever inspected, so nothing the developer had
            // open can be mistaken for a prop.
            var previous = PropArt.Enabled;
            var entries = new Dictionary<string, Entry>();
            GameObject root = null;

            try
            {
                // Greybox on purpose: the report is a list of what the boxes are, including the
                // ones a prefab is already standing in for. Building with the art in place
                // would hide exactly the dimensions a modeller needs for the rest.
                PropArt.Enabled = false;
                PropArt.ClearCache();

                root = new GameObject("PropArtProbe");

                // The office carries evidence pickups that ask the game whether they are
                // available yet, so the services have to exist before the core is built. A
                // null runner is fine: nothing here loads a scene.
                if (!ServiceHub.Ready) ServiceHub.Initialize(null, root.transform);

                var zones = new List<string>();
                zones.AddRange(ZoneGroups.ZonesIn(ZoneGroups.Core));
                for (int i = 0; i < ZoneGroups.Streamed.Length; i++)
                    zones.AddRange(ZoneGroups.ZonesIn(ZoneGroups.Streamed[i]));

                WorldBuilder.Create(root.transform).Build(zones.ToArray());

                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++) Record(entries, renderers[i].gameObject);

                for (int i = 0; i < zones.Count; i++) ZoneRegistry.Unregister(zones[i]);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                PropArt.Enabled = previous;
                PropArt.ClearCache();
            }

            return entries;
        }

        static void Record(Dictionary<string, Entry> entries, GameObject go)
        {
            var name = go.name;
            if (string.IsNullOrEmpty(name)) return;

            Entry entry;
            if (entries.TryGetValue(name, out entry)) { entry.Count++; return; }

            var anomalyProp = go.GetComponent<NO404.Anomalies.ManualProp>();

            entries[name] = new Entry
            {
                Name = name,
                Zone = ZoneOf(go.transform),
                Size = go.transform.localScale,
                LocalPosition = go.transform.localPosition,
                Interactable = go.GetComponent<NO404.Interaction.IInteractable>() != null,
                BelongsToAnomaly = anomalyProp != null,
                EventId = anomalyProp != null ? anomalyProp.EventId : string.Empty,
                Count = 1,
                HasArt = PropArtExists(name)
            };
        }

        /// <summary>
        /// Whether a prefab is already sitting in Resources for this name.
        ///
        /// Asked of the asset database rather than through PropArt, because the seam is
        /// switched off while the report builds and a Resources.Load during a build would
        /// answer for the wrong state.
        /// </summary>
        static bool PropArtExists(string name)
        {
            var path = "Assets/_Project/Resources/" + PropArt.ResourceFolder + name + ".prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        }

        static string ZoneOf(Transform t)
        {
            while (t != null)
            {
                for (int i = 0; i < ZoneIds.All.Length; i++)
                    if (t.name == ZoneIds.All[i] || t.name == "ZONE_" + ZoneIds.All[i])
                        return ZoneIds.All[i];
                t = t.parent;
            }
            return "(unplaced)";
        }

        static void WriteCsv(Dictionary<string, Entry> entries)
        {
            var sorted = new List<Entry>(entries.Values);
            sorted.Sort((a, b) =>
            {
                int byZone = string.CompareOrdinal(a.Zone, b.Zone);
                return byZone != 0 ? byZone : string.CompareOrdinal(a.Name, b.Name);
            });

            var sb = new StringBuilder();
            sb.AppendLine("prefab_name,zone,anomaly,interactable,instances,has_art," +
                          "size_x,size_y,size_z,local_x,local_y,local_z");

            foreach (var e in sorted)
            {
                sb.Append(e.Name).Append(',')
                  .Append(e.Zone).Append(',')
                  .Append(string.IsNullOrEmpty(e.EventId) ? "-" : e.EventId).Append(',')
                  .Append(e.Interactable ? "yes" : "no").Append(',')
                  .Append(e.Count).Append(',')
                  .Append(e.HasArt ? "yes" : "no").Append(',')
                  .Append(F(e.Size.x)).Append(',').Append(F(e.Size.y)).Append(',').Append(F(e.Size.z)).Append(',')
                  .Append(F(e.LocalPosition.x)).Append(',').Append(F(e.LocalPosition.y)).Append(',')
                  .Append(F(e.LocalPosition.z))
                  .AppendLine();
            }

            File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log("[NO404] Wrote " + sorted.Count + " props to " + Path.GetFullPath(OutputPath));
        }

        static string F(float value) { return value.ToString("0.###"); }
    }
}
