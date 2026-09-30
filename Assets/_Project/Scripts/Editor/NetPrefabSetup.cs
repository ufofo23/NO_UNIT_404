using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using NO404.Net;

namespace NO404.EditorTools
{
    /// <summary>
    /// Generates the two prefab assets the co-op layer cannot do without.
    ///
    /// Everything else in this project is built in code, and that has paid for itself: there
    /// is no prefab to go stale, no scene to re-wire, and a fresh clone runs. Netcode is the
    /// one place it does not work. A NetworkObject's GlobalObjectIdHash is stamped from the
    /// asset GUID during import, so an object created with `new GameObject()` has a hash of
    /// zero and the library refuses to spawn it - clients would have no way to know what to
    /// instantiate, and the failure is silent.
    ///
    /// So there are exactly two, they are generated rather than hand-made, and adding a third
    /// should need a reason. Nothing about the player rig lives in the player prefab: the
    /// camera, input and interaction stay on the local player object singleplayer already
    /// builds, and the prefab is only the thing the network agrees exists.
    /// </summary>
    public static class NetPrefabSetup
    {
        const string AssetFolder = "Assets/_Project/Resources/NO404/Net";
        const string ShiftAsset = AssetFolder + "/ShiftObject.prefab";
        const string PlayerAsset = AssetFolder + "/NetPlayer.prefab";

        [MenuItem("Tools/NO404/Net/Rebuild Net Prefabs", priority = 60)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(AssetFolder);

            BuildShift();
            BuildPlayer();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void BuildShift()
        {
            var temp = new GameObject("ShiftObject");
            try
            {
                temp.AddComponent<NetworkObject>();
                temp.AddComponent<NetShift>();
                Save(temp, ShiftAsset);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static void BuildPlayer()
        {
            var temp = new GameObject("NetPlayer");
            try
            {
                var netObject = temp.AddComponent<NetworkObject>();

                // Player objects are destroyed with the connection that owns them, which is
                // what makes a colleague's body disappear when they alt-F4.
                netObject.DontDestroyWithOwner = false;

                var netTransform = temp.AddComponent<NetworkTransform>();
                netTransform.InLocalSpace = false;
                netTransform.Interpolate = true;

                // Scale never changes and pitch is not worth a byte: a capsule tipped forward
                // because somebody looked at their shoes reads as a bug.
                netTransform.SyncScaleX = false;
                netTransform.SyncScaleY = false;
                netTransform.SyncScaleZ = false;
                netTransform.SyncRotAngleX = false;
                netTransform.SyncRotAngleZ = false;

                temp.AddComponent<NetPlayer>();
                Save(temp, PlayerAsset);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static void Save(GameObject source, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);

            if (prefab == null)
            {
                Debug.LogError("[NO404] could not write " + path);
                return;
            }

            var netObject = prefab.GetComponent<NetworkObject>();
            Debug.Log("[NO404] wrote " + path +
                      " (hash " + (netObject != null ? netObject.PrefabIdHash.ToString() : "?") + ")");
        }

        /// <summary>
        /// Regenerates whichever prefab has gone missing.
        ///
        /// They are generated artefacts, so a clone that has never run the menu item still
        /// works, and a stale one cannot survive a rename of the component it carries.
        /// </summary>
        [InitializeOnLoadMethod]
        static void EnsureOnLoad()
        {
            bool missing = AssetDatabase.LoadAssetAtPath<GameObject>(ShiftAsset) == null ||
                           AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAsset) == null;

            if (missing) EditorApplication.delayCall += Rebuild;
        }
    }
}
