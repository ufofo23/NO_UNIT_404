using UnityEngine;
using UnityEngine.Rendering;
using NO404.Interaction;

namespace NO404.Core
{
    /// <summary>
    /// The material every greybox surface in this game is a tinted copy of.
    ///
    /// This exists because of a bug that only appeared in a build: the whole world rendered
    /// magenta. Nothing failed and nothing was logged, because nothing had actually gone
    /// wrong from Unity's point of view - the world was asking
    /// <c>GameObject.CreatePrimitive(...).sharedMaterial</c> for its base material, and that
    /// hands back the legacy built-in default, which the Universal Render Pipeline cannot
    /// draw. A shader that is missing says so; a shader that is present and wrong renders
    /// magenta in silence.
    ///
    /// So the material is taken from the render pipeline itself. That is both correct and the
    /// thing that makes it survive a build: the pipeline asset is referenced by
    /// GraphicsSettings, so its default material and shader cannot be stripped, which a
    /// <c>Shader.Find</c> by name absolutely can be.
    ///
    /// Every fallback below logs which one it took. If this is ever magenta again, the build
    /// log will say why rather than leaving somebody to guess a second time.
    /// </summary>
    public static class GreyboxMaterial
    {
        static Material _cached;

        /// <summary>
        /// A material the active render pipeline can draw. Never null.
        /// </summary>
        public static Material Default
        {
            get
            {
                if (_cached != null) return _cached;
                _cached = Resolve();
                return _cached;
            }
        }

        /// <summary>
        /// Where the generated material lives. Kept in step with RenderAssetSetup, which is
        /// the editor script that writes it.
        /// </summary>
        public const string ResourcePath = "NO404/Render/Greybox";

        static Material Resolve()
        {
            // 1. A real material asset under Resources. This is the only lookup that works in
            //    a player, and the ordering is the whole lesson of this bug: both of the
            //    obvious alternatives below succeed in the editor and return null in a build,
            //    so testing in the editor proved nothing and the world shipped magenta.
            //
            //    Resources ships whatever is in it, and shipping the material drags its
            //    shader in - which is also what stops the shader being stripped.
            var shipped = Resources.Load<Material>(ResourcePath);
            if (shipped != null)
            {
                Log.Info("Render", "greybox material from Resources (" + shipped.shader.name + ")");
                return shipped;
            }

            // 2. The pipeline's own default. URP implements this through its *editor*
            //    resources, so it is null in a player - useful in the editor only.
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                var fromPipeline = pipeline.defaultMaterial;
                if (fromPipeline != null)
                {
                    Log.Warn("Render", "no material at Resources/" + ResourcePath +
                                       "; using the pipeline default (" + fromPipeline.shader.name +
                                       "). Run Tools > NO404 > Net > Rebuild Greybox Material.");
                    return fromPipeline;
                }
            }

            // 3. By name. Only finds anything if some other asset kept the shader alive.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                Log.Warn("Render", "falling back to " + lit.name + " by name");
                return new Material(lit);
            }

            // 4. What the world used to do, kept as a last resort so a broken pipeline gives
            //    a visible building rather than none at all.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = probe.GetComponent<MeshRenderer>().sharedMaterial;

            if (Application.isPlaying) Object.Destroy(probe);
            else Object.DestroyImmediate(probe);

            Log.Error("Render", "no render pipeline material available; the world will draw " +
                                "with the built-in default and will look magenta under URP");
            return material;
        }

        /// <summary>
        /// A tinted copy. Callers own the result, so this never hands back the shared asset -
        /// writing a colour onto the pipeline's default material would repaint every surface
        /// in the game, including ones that have not been built yet.
        /// </summary>
        public static Material Tinted(Color color)
        {
            var material = new Material(Default);

            if (material.HasProperty(ShaderIds.BaseColor)) material.SetColor(ShaderIds.BaseColor, color);
            material.color = color;

            return material;
        }

        /// <summary>Drops the cached lookup. Only useful when the pipeline is swapped at runtime.</summary>
        public static void Forget() { _cached = null; }
    }
}
