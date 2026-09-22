using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off: tries to extract any embedded textures from the generated FBX models, to check
    // whether their gray/untextured look in-scene is a missing-extraction-step problem or the
    // models genuinely have no embedded texture at all.
    public static class ExtractGuardRoomFurnitureTextures
    {
        [MenuItem("RuleGhost/Tests/Extract Guard Room Furniture Textures")]
        public static void Run()
        {
            TryExtract("Assets/Art/Furniture/GuardRoom_Desk.fbx");
            TryExtract("Assets/Art/Furniture/GuardRoom_Chair.fbx");
        }

        private static void TryExtract(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[ExtractGuardRoomFurnitureTextures] Could not get ModelImporter for {path}");
                return;
            }

            string folder = System.IO.Path.GetDirectoryName(path);
            bool result = importer.ExtractTextures(folder);
            Debug.Log($"[ExtractGuardRoomFurnitureTextures] {path}: ExtractTextures returned {result}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderer = prefab != null ? prefab.GetComponentInChildren<Renderer>() : null;
            if (renderer != null)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) { Debug.Log($"  material: null"); continue; }
                    Debug.Log($"  material: {mat.name}, shader: {mat.shader.name}, mainTexture: {(mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap")?.name : "n/a")} / {(mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex")?.name : "n/a")}, color: {(mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").ToString() : mat.color.ToString())}");
                }
            }
        }
    }
}
