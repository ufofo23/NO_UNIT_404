using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NO404.Gameplay;

namespace NO404.Tests
{
    /// <summary>
    /// The seam the art pass lands on (v2.1 spec 29, GDD 17.10).
    ///
    /// A seam nothing has ever gone through is not a seam, it is a hope - so this builds a real
    /// prefab asset in the real Resources folder, puts a prop through it, and takes it away
    /// again. Everything it asserts is a promise made to whoever eventually authors the models:
    /// the file name is the prop name, the model is not scaled, there is always a collider, and
    /// a project with no art in it behaves exactly as it does today.
    /// </summary>
    public sealed class PropArtSeamTests
    {
        const string PropName = "ZZ_SeamProbe";
        static readonly string Folder = "Assets/_Project/Resources/" + PropArt.ResourceFolder.TrimEnd('/');
        static string Path { get { return Folder + "/" + PropName + ".prefab"; } }

        GameObject _parent;
        bool _createdFolder;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("SeamParent");
            PropArt.Enabled = true;
            PropArt.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            if (_parent != null) Object.DestroyImmediate(_parent);

            if (File.Exists(Path)) AssetDatabase.DeleteAsset(Path);
            if (_createdFolder && Directory.Exists(Folder) &&
                Directory.GetFiles(Folder, "*.prefab").Length == 0)
                AssetDatabase.DeleteAsset(Folder);

            AssetDatabase.Refresh();
            PropArt.Enabled = true;
            PropArt.ClearCache();
        }

        /// <summary>A stand-in for a delivered model: a mesh in a child, like real art.</summary>
        void WriteProbePrefab(Vector3 size, bool withCollider)
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                _createdFolder = true;
                AssetDatabase.Refresh();
            }

            var root = new GameObject(PropName);
            try
            {
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.name = "Mesh";
                mesh.transform.SetParent(root.transform, false);
                mesh.transform.localScale = size;

                // Art arrives with its visuals in children and its colliders wherever the
                // artist put them, which is the whole reason the seam has to normalise both.
                var meshCollider = mesh.GetComponent<Collider>();
                if (meshCollider != null) Object.DestroyImmediate(meshCollider);

                if (withCollider)
                {
                    var box = root.AddComponent<BoxCollider>();
                    box.size = size;
                }

                PrefabUtility.SaveAsPrefabAsset(root, Path);
                AssetDatabase.Refresh();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            PropArt.ClearCache();
        }

        // ---- with no art at all ----------------------------------------------

        [Test]
        public void AProjectWithNoArtBehavesExactlyAsItDoesToday()
        {
            Assert.IsFalse(PropArt.HasArt(PropName));
            Assert.IsNull(PropArt.TryBuild(_parent.transform, PropName, Vector3.zero, Vector3.one),
                          "no prefab means the builder falls through to its box");
        }

        [Test]
        public void TheSeamCanBeSwitchedOffEntirely()
        {
            WriteProbePrefab(Vector3.one, withCollider: true);
            Assert.IsTrue(PropArt.HasArt(PropName));

            PropArt.Enabled = false;
            Assert.IsFalse(PropArt.HasArt(PropName), "greybox mode has to ignore the art");
            Assert.IsNull(PropArt.TryBuild(_parent.transform, PropName, Vector3.zero, Vector3.one));
        }

        // ---- with a model delivered ------------------------------------------

        [Test]
        public void DroppingAPrefabInResourcesReplacesTheBox()
        {
            WriteProbePrefab(Vector3.one, withCollider: true);

            var instance = PropArt.TryBuild(_parent.transform, PropName,
                                            new Vector3(1f, 2f, 3f), Vector3.one);

            Assert.IsNotNull(instance, "the prefab should have been found by its file name");
            Assert.AreEqual(PropName, instance.name, "and named as the prop it replaces");
            Assert.AreEqual(_parent.transform, instance.transform.parent);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), instance.transform.localPosition);
            Assert.IsNotNull(instance.transform.Find("Mesh"), "the model should have come with it");
        }

        [Test]
        public void TheModelIsNeverScaledToFit()
        {
            // Squashing somebody's mesh to make the numbers line up is not a fix, and a
            // silently stretched prop is worse than an obviously wrong one. The seam reports
            // a mismatch instead - see the next test.
            WriteProbePrefab(Vector3.one, withCollider: true);

            var instance = PropArt.TryBuild(_parent.transform, PropName, Vector3.zero,
                                            new Vector3(4f, 4f, 4f));

            Assert.AreEqual(Vector3.one, instance.transform.localScale);
        }

        [Test]
        public void AModelAtTheWrongScaleIsCalledOutRatherThanAccepted()
        {
            WriteProbePrefab(Vector3.one, withCollider: true);

            // Everything the building was laid out around says four metres; the model says one.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(PropName + ".*expects"));

            PropArt.TryBuild(_parent.transform, PropName, Vector3.zero, new Vector3(4f, 4f, 4f));
        }

        [Test]
        public void AModelOfTheRightSizePassesWithoutComplaint()
        {
            WriteProbePrefab(new Vector3(2f, 2f, 2f), withCollider: true);
            PropArt.TryBuild(_parent.transform, PropName, Vector3.zero, new Vector3(2f, 2f, 2f));
            // No LogAssert.Expect: an unexpected warning fails the test on its own.
        }

        // ---- the contract the rest of the game depends on --------------------

        [Test]
        public void AModelThatBroughtNoColliderStillGetsOne()
        {
            // ManualProp switches a prop on and off through the collider on its own
            // GameObject, and the interaction raycast needs something to hit. A model with its
            // colliders elsewhere would break both silently.
            WriteProbePrefab(new Vector3(2f, 2f, 2f), withCollider: false);

            var instance = PropArt.TryBuild(_parent.transform, PropName, Vector3.zero,
                                            new Vector3(2f, 2f, 2f));

            var collider = instance.GetComponent<BoxCollider>();
            Assert.IsNotNull(collider, "the root needs a collider whatever the artist did");
            Assert.AreEqual(new Vector3(2f, 2f, 2f), collider.size,
                            "and it is the volume the building was laid out around");
        }

        [Test]
        public void AModelThatBroughtItsOwnColliderKeepsIt()
        {
            WriteProbePrefab(new Vector3(2f, 2f, 2f), withCollider: true);

            var instance = PropArt.TryBuild(_parent.transform, PropName, Vector3.zero,
                                            new Vector3(2f, 2f, 2f));

            Assert.AreEqual(1, instance.GetComponents<Collider>().Length,
                            "the seam should not add a second collider over the artist's own");
        }

        [Test]
        public void ThingsThatMustNotBeTouchableComeBackWithNoColliderAtAll()
        {
            // The CCTV apparitions. They exist for the lens only: a collider on one would
            // block a patrol and swallow the interaction raycast.
            WriteProbePrefab(new Vector3(2f, 2f, 2f), withCollider: true);

            var instance = PropArt.TryBuild(_parent.transform, PropName, Vector3.zero,
                                            new Vector3(2f, 2f, 2f), wantsCollider: false);

            Assert.AreEqual(0, instance.GetComponentsInChildren<Collider>(true).Length);
        }

        [Test]
        public void EverythingUnderAModelGoesOntoTheCameraOnlyLayer()
        {
            WriteProbePrefab(Vector3.one, withCollider: false);

            var instance = PropArt.TryBuild(_parent.transform, PropName, Vector3.zero,
                                            Vector3.one, wantsCollider: false);

            const int layer = 9;
            PropArt.SetLayerRecursively(instance, layer);

            var all = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                Assert.AreEqual(layer, all[i].gameObject.layer,
                                all[i].name + " would still show on the player's camera");
        }

        [Test]
        public void AnUnconfiguredLayerLeavesEverythingAlone()
        {
            // Layers.CctvOnly is -1 in a project where the layer was never created, and
            // assigning -1 throws. The world has to build anyway.
            WriteProbePrefab(Vector3.one, withCollider: false);

            var instance = PropArt.TryBuild(_parent.transform, PropName, Vector3.zero,
                                            Vector3.one, wantsCollider: false);
            int before = instance.layer;

            PropArt.SetLayerRecursively(instance, -1);
            Assert.AreEqual(before, instance.layer);
        }
    }
}
