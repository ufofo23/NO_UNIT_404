using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NO404.Gameplay;

namespace NO404.EditorTools
{
    /// <summary>
    /// One-click project setup. Creates the shipping entry scene, registers it as build
    /// index 0 and applies the product settings from GDD 1.1 / 1.6.
    ///
    /// Bootstrap self-installs at runtime, so gameplay also works when you press Play from
    /// any other open scene - this menu item just makes builds deterministic.
    /// </summary>
    public static class ProjectSetup
    {
        const string SceneFolder = "Assets/_Project/Scenes";
        const string BootstrapScenePath = SceneFolder + "/SCN_Bootstrap.unity";

        [MenuItem("Tools/NO404/Setup/Create Entry Scene And Build Settings", priority = 0)]
        public static void Run()
        {
            EnsureFolders();
            CreateBootstrapScene();
            CreateZoneScenes();
            RegisterBuildScenes();
            ApplyProductSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            Debug.Log("[NO404] Setup complete. Press Play - the main menu appears automatically.");
        }

        static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/_Project",
                "Assets/_Project/Art",
                "Assets/_Project/Audio",
                "Assets/_Project/Data",
                "Assets/_Project/Data/Cases",
                "Assets/_Project/Data/Evidence",
                "Assets/_Project/Data/Dialogues",
                "Assets/_Project/Data/Residents",
                "Assets/_Project/Data/Visitors",
                "Assets/_Project/Prefabs",
                "Assets/_Project/Scenes",
                "Assets/_Project/Settings",
                "Assets/_Project/ThirdParty"
            };

            for (int i = 0; i < folders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(folders[i])) continue;

                var parent = Path.GetDirectoryName(folders[i]).Replace('\\', '/');
                var leaf = Path.GetFileName(folders[i]);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static void CreateBootstrapScene()
        {
            if (File.Exists(BootstrapScenePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The scene is intentionally empty: Bootstrap installs itself before the first
            // scene loads and builds the player and UI in code.
            var marker = new GameObject("SCN_Bootstrap (empty by design - see README)");
            marker.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        /// <summary>
        /// One additive scene per zone group (GDD 20.5). Each holds a single ZoneSceneBuilder;
        /// the greybox inside is generated at load time, so these assets stay small and merge
        /// cleanly until the art pass replaces a floor's contents with real meshes.
        /// </summary>
        static void CreateZoneScenes()
        {
            for (int i = 0; i < ZoneGroups.All.Length; i++)
            {
                var group = ZoneGroups.All[i];
                var path = SceneFolder + "/" + group + ".unity";
                if (File.Exists(path)) continue;

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var go = new GameObject(group);
                var builder = go.AddComponent<ZoneSceneBuilder>();
                builder.SetGroup(group);
                EditorUtility.SetDirty(builder);

                EditorSceneManager.SaveScene(scene, path);
                Debug.Log("[NO404] created " + path);
            }
        }

        static void RegisterBuildScenes()
        {
            // Bootstrap must stay first: it is the entry point of a build.
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };

            for (int i = 0; i < ZoneGroups.All.Length; i++)
                scenes.Add(new EditorBuildSettingsScene(SceneFolder + "/" + ZoneGroups.All[i] + ".unity", true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void ApplyProductSettings()
        {
            PlayerSettings.companyName = "ProjectCaretaker";
            PlayerSettings.productName = "NO UNIT 404";
            if (PlayerSettings.bundleVersion == "0.1" || string.IsNullOrEmpty(PlayerSettings.bundleVersion))
                PlayerSettings.bundleVersion = "0.1.0";

            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;
        }

        [MenuItem("Tools/NO404/Open Persistent Data Folder", priority = 200)]
        public static void OpenPersistentData()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        [MenuItem("Tools/NO404/Delete All Saves", priority = 201)]
        public static void DeleteSaves()
        {
            var dir = Path.Combine(Application.persistentDataPath, "saves");
            if (!Directory.Exists(dir))
            {
                Debug.Log("[NO404] No save folder to delete.");
                return;
            }

            if (!EditorUtility.DisplayDialog("NO UNIT 404", "Delete every save slot?", "Delete", "Cancel")) return;

            Directory.Delete(dir, true);
            Debug.Log("[NO404] Saves deleted.");
        }
    }
}
