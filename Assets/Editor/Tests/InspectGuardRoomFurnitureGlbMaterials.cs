using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off: checks whether the glTFast-imported GLB models actually carry real textures
    // (unlike the earlier FBX import, which came in with a flat gray placeholder material).
    public static class InspectGuardRoomFurnitureGlbMaterials
    {
        [MenuItem("RuleGhost/Tests/Inspect Guard Room Furniture GLB Materials")]
        public static void Run()
        {
            Inspect("Assets/Art/Furniture/GuardRoom_Desk.glb");
            Inspect("Assets/Art/Furniture/GuardRoom_Chair.glb");
        }

        private static void Inspect(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[InspectGuardRoomFurnitureGlbMaterials] Could not load {path}");
                return;
            }

            var renderers = prefab.GetComponentsInChildren<Renderer>();
            Debug.Log($"[InspectGuardRoomFurnitureGlbMaterials] {path}: {renderers.Length} renderer(s)");
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) { Debug.Log("  material: null"); continue; }
                    Debug.Log($"  material: {mat.name}, shader: {mat.shader.name}");
                    foreach (var texName in mat.GetTexturePropertyNames())
                    {
                        var tex = mat.GetTexture(texName);
                        Debug.Log($"    {texName}: {(tex != null ? $"{tex.name} ({tex.width}x{tex.height})" : "none")}");
                    }
                }
            }
        }
    }
}
