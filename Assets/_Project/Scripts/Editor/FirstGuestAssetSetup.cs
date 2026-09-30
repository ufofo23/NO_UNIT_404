using System.IO;
using UnityEditor;
using UnityEngine;
using NO404.Visitors;

namespace NO404.EditorTools
{
    public sealed class FirstGuestModelImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith("/FirstGuest/Worker_BaseColor.png")) return;
            var texture = (TextureImporter)assetImporter;
            texture.maxTextureSize = 4096;
            texture.sRGBTexture = true;
            texture.mipmapEnabled = true;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            texture.alphaSource = TextureImporterAlphaSource.None;
        }

        void OnPreprocessModel()
        {
            if (assetPath != FirstGuestAssetSetup.ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.optimizeGameObjects = false;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
            importer.normalSmoothingAngle = 180f;
            importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.FromAngle;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    public static class FirstGuestAssetSetup
    {
        const string Folder = "Assets/_Project/Resources/NO404/Characters/FirstGuest";
        public const string ModelPath = Folder + "/FirstGuest.fbx";

        [MenuItem("Tools/NO404/Art/Rebuild First Visitor")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new System.InvalidOperationException("Generate FirstGuest.fbx first.");
            var root = new GameObject("FirstGuestCharacter");
            try
            {
                var instance = Object.Instantiate(model, root.transform, false);
                instance.name = "FirstGuestModel";
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Worker_BaseColor.png");
                if (albedo == null) throw new System.InvalidOperationException("Reference worker texture is missing.");
                foreach (var animator in instance.GetComponentsInChildren<Animator>()) animator.enabled = false;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var source = materials[i];
                        if (source == null) continue;
                        string path = Folder + "/ReferenceWorker.mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null)
                        {
                            material = new Material(NO404.Core.GreyboxMaterial.Default);
                            AssetDatabase.CreateAsset(material, path);
                        }
                        material.name = "ReferenceWorker";
                        material.color = Color.white;
                        material.SetColor("_BaseColor", Color.white);
                        material.SetTexture("_BaseMap", albedo);
                        material.SetTexture("_MainTex", albedo);
                        material.SetFloat("_Smoothness", .16f);
                        material.SetFloat("_Metallic", 0f);
                        // Rodin marks its mesh double-sided in glTF. Preserve that
                        // contract for the thin pocket flaps, cap brim and face patches.
                        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                        material.doubleSidedGI = true;
                        material.SetFloat("_ReceiveShadows", 1f);
                        material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                        EditorUtility.SetDirty(material);
                        materials[i] = material;
                    }
                    renderer.sharedMaterials = materials;
                    renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        skin.quality = SkinQuality.Bone4;
                        skin.updateWhenOffscreen = true; // CCTV can become visible between player-camera frames.
                    }
                }
                root.AddComponent<FirstGuestMotion>();
                var bounds = new Bounds(); bool first = true;
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    // Skin renderer bounds include Unity's animation safety padding.
                    // Validate the deformed vertices, not that padded culling volume.
                    if (r is SkinnedMeshRenderer skin)
                    {
                        var baked = new Mesh();
                        try
                        {
                            skin.BakeMesh(baked);
                            foreach (var vertex in baked.vertices)
                            {
                                var point = skin.transform.TransformPoint(vertex);
                                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                                else bounds.Encapsulate(point);
                            }
                        }
                        finally { Object.DestroyImmediate(baked); }
                    }
                    else if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (bounds.size.y < 1.65f || bounds.size.y > 1.9f || Mathf.Abs(bounds.min.y) > .05f)
                    throw new System.InvalidOperationException("Unexpected guest scale/origin: " + bounds);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/FirstGuestCharacter.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("[FirstGuest] Prefab ready. Bounds " + bounds);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
