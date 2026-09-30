using System.Collections.Generic;
using UnityEngine;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Where finished art gets into the game (GDD 17.10, v2.1 spec 29).
    ///
    /// Every solid thing in this building is a tinted primitive that <see cref="WorldBuilder"/>
    /// makes at runtime. That was the right call for a greybox and it left the art pass with
    /// nowhere to land: replacing one swing set meant editing C#, and there are the better part
    /// of two hundred boxes. ZoneSceneBuilder offered a way out at the scale of a whole floor,
    /// which is far too coarse to work a prop at a time.
    ///
    /// So this is the seam, and it is deliberately the smallest one that works: before making
    /// a box, the builder asks whether a prefab of that name exists. Drop
    /// <c>Resources/NO404/Props/Swings.prefab</c> into the project and the swings are a model;
    /// touch nothing and they stay a box. Both states are shippable, every state in between is
    /// shippable, and no scene, prefab reference or code change is involved in moving from one
    /// to the other.
    ///
    /// The contract that makes that safe is the size. A prefab is instantiated unscaled and is
    /// expected to occupy the same volume as the box it replaces, because that volume is not
    /// decoration - patrol routes are drawn through the gaps between these props, interaction
    /// range is measured against them, and the player collides with them. A model that does not
    /// fit is reported rather than squashed to fit, because non-uniformly scaling somebody's
    /// mesh is not a fix.
    /// </summary>
    public static class PropArt
    {
        /// <summary>Prefabs live here, named exactly as the prop they replace.</summary>
        public const string ResourceFolder = "NO404/Props/";

        /// <summary>
        /// How far a model may be off the greybox volume before it is called out.
        ///
        /// A fifth is generous on purpose. A real swing set is not a box and its bounds will
        /// never match one exactly; what this catches is the model authored in centimetres, or
        /// at the wrong scale entirely, which is the mistake that actually happens.
        /// </summary>
        public const float SizeTolerance = 0.2f;

        /// <summary>
        /// Turns the whole seam off, so the world builds as greybox whatever is in Resources.
        ///
        /// For the tests that assert the greybox contract, and for anyone who needs to see the
        /// layout without the art on top of it.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

        /// <summary>Names already looked up and not found, so Resources is asked once each.</summary>
        static readonly HashSet<string> _absent = new HashSet<string>();

        /// <summary>Names whose model did not fit, so the warning is logged once each.</summary>
        static readonly HashSet<string> _reported = new HashSet<string>();

        public static int LoadedCount { get { return _prefabs.Count; } }

        /// <summary>Forget every lookup. For tests that add or remove prefabs mid-session.</summary>
        public static void ClearCache()
        {
            _prefabs.Clear();
            _absent.Clear();
            _reported.Clear();
        }

        public static bool HasArt(string name)
        {
            return Enabled && Find(name) != null;
        }

        static GameObject Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            GameObject prefab;
            if (_prefabs.TryGetValue(name, out prefab)) return prefab;
            if (_absent.Contains(name)) return null;

            prefab = Resources.Load<GameObject>(ResourceFolder + name);
            if (prefab == null) { _absent.Add(name); return null; }

            _prefabs[name] = prefab;
            return prefab;
        }

        /// <summary>
        /// Build the finished art for this prop, or null if there is none yet.
        ///
        /// <paramref name="size"/> is the volume the greybox would have occupied, and is used
        /// for two things: giving the instance a collider if the model brought none, and
        /// checking that the model is the size the building expects.
        /// </summary>
        public static GameObject TryBuild(Transform parent, string name, Vector3 localPosition,
                                          Vector3 size, bool wantsCollider = true)
        {
            if (!Enabled) return null;

            var prefab = Find(name);
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = name;
            instance.transform.localPosition = localPosition;

            // Deliberately not scaled. See the class comment: a model that is the wrong size is
            // a model to fix, not one to stretch.
            if (wantsCollider) EnsureCollider(instance, size);
            else StripColliders(instance);

            ReportIfWrongSize(instance, name, size);
            return instance;
        }

        /// <summary>
        /// Guarantee a collider on the root, because that is where everything looks for one.
        ///
        /// ManualProp switches a prop on and off through the collider on its own GameObject,
        /// and the interaction raycast needs something to hit. A model whose colliders are all
        /// in children would satisfy neither, so the root gets a box the size of the greybox -
        /// which is exactly the volume the rest of the building was built around.
        /// </summary>
        static void EnsureCollider(GameObject instance, Vector3 size)
        {
            if (instance.GetComponent<Collider>() != null) return;

            var box = instance.AddComponent<BoxCollider>();
            box.size = size;
            box.center = Vector3.zero;
        }

        /// <summary>
        /// Take every collider off a prop that exists for the lens only.
        ///
        /// Disabled first and destroyed second, which is not belt and braces: at runtime
        /// Destroy is deferred to the end of the frame, so a CCTV apparition would spend the
        /// rest of that frame as something a patrol could walk into and the interaction
        /// raycast could hit. Disabling closes that window. The destroy itself has to pick its
        /// call - Destroy does nothing outside play mode - or an editor tool that builds the
        /// world leaves the colliders behind.
        /// </summary>
        static void StripColliders(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                if (Application.isPlaying) Object.Destroy(colliders[i]);
                else Object.DestroyImmediate(colliders[i]);
            }
        }

        /// <summary>Put an instance and everything under it on one layer (the CCTV-only pass).</summary>
        public static void SetLayerRecursively(GameObject instance, int layer)
        {
            if (instance == null || layer < 0) return;

            instance.layer = layer;
            var children = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++) children[i].gameObject.layer = layer;
        }

        static void ReportIfWrongSize(GameObject instance, string name, Vector3 expected)
        {
            if (_reported.Contains(name)) return;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            var actual = bounds.size;
            if (Fits(actual.x, expected.x) && Fits(actual.y, expected.y) && Fits(actual.z, expected.z))
                return;

            _reported.Add(name);
            Log.Warn("Art", name + " is " + Format(actual) + " but the building expects " +
                            Format(expected) + ". Patrol routes, interaction range and " +
                            "collision were all laid out around the second number.");
        }

        static bool Fits(float actual, float expected)
        {
            // A prop can be flat in one axis - a sign, a floor mark - and a proportional test
            // against nearly zero is meaningless, so those fall back to an absolute slack.
            if (expected < 0.05f) return actual <= 0.05f + SizeTolerance;
            return Mathf.Abs(actual - expected) <= expected * SizeTolerance;
        }

        static string Format(Vector3 size)
        {
            return size.x.ToString("0.00") + " x " + size.y.ToString("0.00") +
                   " x " + size.z.ToString("0.00") + "m";
        }
    }
}
