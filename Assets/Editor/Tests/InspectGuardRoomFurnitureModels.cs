using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off: prints the imported FBX models' actual render bounds so DecorateGuardRoom knows
    // what scale factor to apply when swapping them in for the placeholder cube desk/chair.
    public static class InspectGuardRoomFurnitureModels
    {
        [MenuItem("RuleGhost/Tests/Inspect Guard Room Furniture Models")]
        public static void Run()
        {
            Inspect("Assets/Art/Furniture/GuardRoom_Desk.fbx");
            Inspect("Assets/Art/Furniture/GuardRoom_Chair.fbx");
        }

        private static void Inspect(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[InspectGuardRoomFurnitureModels] Could not load {path} -- is it imported yet?");
                return;
            }

            var instance = Object.Instantiate(prefab);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[InspectGuardRoomFurnitureModels] {path}: no renderers found.");
                Object.DestroyImmediate(instance);
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            Debug.Log($"[InspectGuardRoomFurnitureModels] {path}: size={bounds.size} center={bounds.center} rendererCount={renderers.Length}");
            Object.DestroyImmediate(instance);
        }
    }
}
