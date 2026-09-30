using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NO404.EditorTools
{
    /// <summary>
    /// Generates the one material asset the game needs to exist as a file.
    ///
    /// Everything in this project is built in code, and for materials that turned out to be
    /// impossible for a reason that only shows up in a build: the whole world rendered
    /// magenta, and two attempts to fix it from source failed because both of the obvious
    /// runtime lookups are lies outside the editor.
    ///
    ///   RenderPipelineAsset.defaultMaterial   URP implements this through its *editor*
    ///                                         resources. In a build it returns null.
    ///   Shader.Find("Universal Render Pipeline/Lit")
    ///                                         Finds nothing, because a shader no included
    ///                                         asset references is stripped from the build.
    ///
    /// Both are true and neither is reported. The only thing that survives is an actual
    /// material asset under Resources: Unity ships everything in Resources, and shipping the
    /// material drags its shader in with it.
    /// </summary>
    public static class RenderAssetSetup
    {
        public const string ResourcePath = "NO404/Render/Greybox";

        const string AssetFolder = "Assets/_Project/Resources/NO404/Render";
        const string AssetPath = AssetFolder + "/Greybox.mat";

        /// <summary>Tried in order; the first one that exists wins.</summary>
        static readonly string[] ShaderCandidates =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit"
        };

        [MenuItem("Tools/NO404/Net/Rebuild Greybox Material", priority = 62)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(AssetFolder);

            var shader = FindShader();
            if (shader == null)
            {
                Debug.LogError("[NO404] no URP shader found - is the Universal RP package installed?");
                return;
            }

            var material = new Material(shader) { name = "Greybox" };

            var existing = AssetDatabase.LoadAssetAtPath<Material>(AssetPath);
            if (existing != null)
            {
                existing.shader = shader;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(material, AssetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[NO404] greybox material written to " + AssetPath + " (" + shader.name + ")");
        }

        static Shader FindShader()
        {
            // The pipeline's own default is the most honest source while we are in the editor,
            // where it actually works.
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultMaterial != null &&
                pipeline.defaultMaterial.shader != null)
                return pipeline.defaultMaterial.shader;

            for (int i = 0; i < ShaderCandidates.Length; i++)
            {
                var shader = Shader.Find(ShaderCandidates[i]);
                if (shader != null) return shader;
            }

            return null;
        }

        /// <summary>Regenerates the material when it has gone missing, like the net prefabs.</summary>
        [InitializeOnLoadMethod]
        static void EnsureOnLoad()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(AssetPath) != null) return;
            EditorApplication.delayCall += Rebuild;
        }
    }
}
