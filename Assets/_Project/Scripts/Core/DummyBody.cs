using UnityEngine;

namespace NO404.Core
{
    /// <summary>
    /// The stand-in body every person in this building is drawn with.
    ///
    /// There is no character art in the project, so a colleague down a corridor has always
    /// been a capsule with a torch on it - built inline in <see cref="Net.NetPlayer"/>, and
    /// therefore something only a second player ever saw. Visitors had no body at all: a
    /// stranger admitted at the door existed as a row on a tracking board and nothing else,
    /// which made the twelve cameras the wrong tool for the one question the Access system
    /// asks the player to answer ("where did they go?").
    ///
    /// So the shape lives here once and everybody uses it. The cap on top is the only thing
    /// that separates the two kinds of person - white for a caretaker, red for a visitor -
    /// because at thirty metres on a 480x270 feed a tint on the body itself is not a signal,
    /// it is a guess. It is a greybox readability aid, not a game mechanic: GDD 12.4 forbids
    /// anomalies that can only be told apart by colour, and nothing here is an anomaly.
    /// </summary>
    public static class DummyBody
    {
        /// <summary>Total height in metres, matching PlayerController.StandHeight closely enough.</summary>
        public const float Height = 1.8f;

        /// <summary>Everybody's body is the same grey. Only the cap identifies them.</summary>
        public static readonly Color BodyGrey = new Color(0.55f, 0.58f, 0.62f);

        /// <summary>A caretaker, yours or a colleague's.</summary>
        public static readonly Color CaretakerCap = new Color(0.95f, 0.96f, 0.97f);

        /// <summary>Somebody the door let in.</summary>
        public static readonly Color VisitorCap = new Color(0.82f, 0.13f, 0.13f);

        /// <summary>
        /// Builds one body under <paramref name="parent"/>, standing on the parent's floor.
        ///
        /// <paramref name="cctvOnly"/> puts the whole thing on the CCTV-only layer, which the
        /// player camera culls (GameLoop.CreatePlayer). That is how the local caretaker can
        /// appear on their own monitors without a capsule filling their view - and it is why
        /// the shadow goes off with it, since a shadow the eye can see cast by a body the eye
        /// cannot is worse than either.
        /// </summary>
        public static GameObject Build(Transform parent, Color capColor, bool torch, bool cctvOnly,
                                       string name = "Dummy")
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            // A capsule's y scale is a half-height, so 0.9 is 1.8m tall standing on the floor.
            BuildPart(root.transform, PrimitiveType.Capsule, "Shape",
                      new Vector3(0f, Height * 0.5f, 0f),
                      new Vector3(0.5f, Height * 0.5f, 0.5f),
                      BodyGrey, cctvOnly);

            // A cylinder's y scale is a half-height too: a 16cm disc sitting on the head.
            BuildPart(root.transform, PrimitiveType.Cylinder, "Cap",
                      new Vector3(0f, Height + 0.08f, 0f),
                      new Vector3(0.46f, 0.08f, 0.46f),
                      capColor, cctvOnly);

            if (torch) BuildTorch(root.transform);

            return root;
        }

        static void BuildPart(Transform parent, PrimitiveType shape, string name,
                              Vector3 localPosition, Vector3 localScale, Color color, bool cctvOnly)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            // A collider here would push the player around, block the interaction raycast and
            // wedge two people in a 1.55m corridor (GDD 17.4). This is a picture of somebody.
            Destroy(part.GetComponent<Collider>());

            // Same trap as the rest of the world: a primitive's own material is the legacy
            // built-in one, which URP draws magenta. See GreyboxMaterial.
            var renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GreyboxMaterial.Tinted(color);
                if (cctvOnly)
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (cctvOnly && Layers.CctvOnly >= 0) part.layer = Layers.CctvOnly;
        }

        /// <summary>
        /// What actually makes a person legible in an unlit corridor. Caretakers carry one;
        /// visitors do not, which is its own tell on a dark feed.
        /// </summary>
        static void BuildTorch(Transform parent)
        {
            var torch = new GameObject("Torch");
            torch.transform.SetParent(parent, false);
            torch.transform.localPosition = new Vector3(0f, 1.55f, 0.25f);

            var light = torch.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 14f;
            light.spotAngle = 50f;
            light.intensity = 2f;
        }

        /// <summary>
        /// Editor tooling builds bodies outside play mode, where Destroy never runs and the
        /// collider would survive into whatever the tool is measuring.
        /// </summary>
        static void Destroy(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
