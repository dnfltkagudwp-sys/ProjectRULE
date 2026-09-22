using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Swaps the desk/chair (currently the FBX import, flat gray material, manually placed/rotated
    // by hand in the Editor) for the glTFast-imported GLB versions of the exact same generated
    // models -- same mesh, but the GLB actually carries the baked PBR textures (baseColor/
    // metallicRoughness/normal) that the FBX export silently dropped. Preserves whatever position/
    // rotation/scale is currently on each placeholder instead of recomputing placement, since that
    // was hand-tuned after the last (buggy) auto-placement pass. Also nudges the chair a bit further
    // from the desk to clear a small remaining overlap between the two hand-placed objects.
    public static class SwapGuardRoomFurnitureToGlb
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string DeskGlbPath = "Assets/Art/Furniture/GuardRoom_Desk.glb";
        private const string ChairGlbPath = "Assets/Art/Furniture/GuardRoom_Chair.glb";

        // Resolves the desk/chair bounding-box overlap found via InspectGuardRoomFurniturePlacement
        // (X-overlap ~0.17m was the smallest axis) with a safety margin, pushed along the actual
        // desk->chair direction so it stays a minimal nudge rather than a reposition.
        private static readonly Vector3 ChairOverlapFix = new Vector3(-0.236f, 0f, -0.150f);

        [MenuItem("RuleGhost/Graybox/Swap Guard Room Furniture To GLB")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskGlbPath);
            var chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChairGlbPath);
            if (deskPrefab == null || chairPrefab == null)
            {
                Debug.LogError("[SwapGuardRoomFurnitureToGlb] Could not load desk/chair GLB models.");
                return;
            }

            SwapPreservingTransform("GuardRoom_Desk", deskPrefab, Vector3.zero);
            SwapPreservingTransform("GuardRoom_Chair", chairPrefab, ChairOverlapFix);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SwapGuardRoomFurnitureToGlb] Desk and chair swapped to textured GLB versions. Scene saved.");
        }

        private static void SwapPreservingTransform(string name, GameObject prefab, Vector3 positionNudge)
        {
            var existing = GameObject.Find(name);
            if (existing == null)
            {
                Debug.LogWarning($"[SwapGuardRoomFurnitureToGlb] Could not find '{name}' -- skipping.");
                return;
            }

            var parent = existing.transform.parent;
            var localPos = existing.transform.localPosition + positionNudge;
            var localRot = existing.transform.localRotation;
            var localScale = existing.transform.localScale;
            Object.DestroyImmediate(existing);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = localRot;
            instance.transform.localScale = localScale;
        }
    }
}
