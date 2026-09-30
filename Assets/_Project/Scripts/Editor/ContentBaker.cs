using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using NO404.Cases;
using NO404.ContentData;
using NO404.Dialogue;
using NO404.Evidence;
using NO404.Residents;
using NO404.Visitors;

namespace NO404.EditorTools
{
    /// <summary>
    /// Turns the code-authored seed content into inspector-editable ScriptableObjects plus a
    /// ContentCatalog in Resources. After baking, ContentDatabase loads the catalog instead
    /// of the code seed, so designers own the data from that point on.
    ///
    /// Baking is destructive for the target folders on purpose: it is a one-way handover,
    /// not a sync. Run it once, then edit the assets.
    /// </summary>
    public static class ContentBaker
    {
        const string DataRoot = "Assets/_Project/Data";
        const string CatalogFolder = "Assets/_Project/Resources/NO404";
        const string CatalogPath = CatalogFolder + "/ContentCatalog.asset";

        [MenuItem("Tools/NO404/Data/Bake Seed Content To Assets", priority = 51)]
        public static void Bake()
        {
            if (File.Exists(CatalogPath) &&
                !EditorUtility.DisplayDialog("NO UNIT 404",
                    "A ContentCatalog already exists. Re-baking overwrites the generated assets " +
                    "and discards inspector edits made to them.\n\nContinue?", "Overwrite", "Cancel"))
                return;

            EnsureFolder(CatalogFolder);

            var seed = SeedContent.Build();

            var cases = SaveAll(seed.Cases, DataRoot + "/Cases");
            var evidence = SaveAll(seed.Evidence, DataRoot + "/Evidence");
            var residents = SaveAll(seed.Residents, DataRoot + "/Residents");
            var dialogues = SaveAll(seed.Dialogues, DataRoot + "/Dialogues");
            var visitors = SaveAll(seed.Visitors, DataRoot + "/Visitors");

            // Everything below is a ScriptableObject the catalog holds directly, and every
            // one of them used to be dropped on the floor here. ContentDatabase reads a baked
            // catalog *instead of* the seed, so a designer who baked lost the phone calls, the
            // endings and the whole v2.1 manual system in one click - and the game came up
            // looking like it had simply never had them.
            var phoneCalls = SaveAll(seed.PhoneCalls, DataRoot + "/PhoneCalls");
            var endings = SaveAll(seed.Endings, DataRoot + "/Endings");
            var manualPages = SaveAll(seed.ManualPages, DataRoot + "/ManualPages");
            var manualEvents = SaveAll(seed.ManualEvents, DataRoot + "/ManualEvents");
            var anomalyTools = SaveAll(seed.AnomalyTools, DataRoot + "/AnomalyTools");

            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.cases = cases;
            catalog.evidence = evidence;
            catalog.residents = residents;
            catalog.dialogues = dialogues;
            catalog.visitors = visitors;
            catalog.cctvChannels = seed.Channels;
            catalog.anomalies = seed.Anomalies;
            catalog.phoneCalls = phoneCalls;
            catalog.endings = endings;
            catalog.manualPages = manualPages;
            catalog.manualEvents = manualEvents;
            catalog.anomalyTools = anomalyTools;

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[NO404] Baked " + cases.Length + " cases, " + evidence.Length + " evidence, " +
                      residents.Length + " residents, " + dialogues.Length + " dialogues, " +
                      visitors.Length + " visitors, " + phoneCalls.Length + " calls, " +
                      endings.Length + " endings, " + manualPages.Length + " manual pages, " +
                      manualEvents.Length + " manual events, " + anomalyTools.Length +
                      " anomaly tools into " + CatalogPath);
        }

        [MenuItem("Tools/NO404/Data/Revert To Code Seed Content", priority = 52)]
        public static void Revert()
        {
            if (!File.Exists(CatalogPath))
            {
                Debug.Log("[NO404] No catalog present; the code seed is already in use.");
                return;
            }

            if (!EditorUtility.DisplayDialog("NO UNIT 404",
                    "Delete the ContentCatalog so the game falls back to SeedContent?\n\n" +
                    "The generated .asset files under Data/ are left in place.", "Delete catalog", "Cancel"))
                return;

            AssetDatabase.DeleteAsset(CatalogPath);
            AssetDatabase.Refresh();
            Debug.Log("[NO404] Catalog deleted; code seed content is active again.");
        }

        static T[] SaveAll<T>(T[] instances, string folder) where T : ScriptableObject
        {
            EnsureFolder(folder);

            var saved = new List<T>(instances.Length);
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance == null) continue;

                var path = folder + "/" + SafeName(instance.name, i) + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<T>(path);

                if (existing != null)
                {
                    EditorUtility.CopySerialized(instance, existing);
                    EditorUtility.SetDirty(existing);
                    saved.Add(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(instance, path);
                    saved.Add(instance);
                }
            }

            return saved.ToArray();
        }

        static string SafeName(string name, int index)
        {
            if (string.IsNullOrEmpty(name)) return "Asset_" + index;

            var invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++) name = name.Replace(invalid[i], '_');
            return name;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
