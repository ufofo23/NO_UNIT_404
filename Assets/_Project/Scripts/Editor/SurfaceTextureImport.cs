using UnityEditor;
using UnityEngine;

namespace NO404.EditorTools
{
    /// <summary>
    /// Import settings for the painted surface tiles (Tools/GenerateReferenceTiles.py), which
    /// SurfaceArt loads from Resources/NO404/Surfaces.
    ///
    /// Two of them are not Unity's default and both matter:
    ///
    ///  - a <c>_normal</c> file has to be imported as a normal map. As a default texture it is
    ///    read as sRGB and every bump in the building leans the same wrong way;
    ///  - an <c>_albedo</c> file's alpha is smoothness, not transparency. Marking it as
    ///    transparency premultiplies the edges of every puddle into the colour.
    ///
    /// Applying this from an importer means regenerating the tiles lands them correctly
    /// without anyone clicking through the inspector.
    /// </summary>
    public sealed class SurfaceTextureImport : AssetPostprocessor
    {
        const string SurfaceFolder = "Assets/_Project/Resources/NO404/Surfaces/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SurfaceFolder)) return;

            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.png");

            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
        }
    }
}
