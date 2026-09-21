using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Figure1_Standing.fbx has the same known Blender->FBX axis bug as
    // Frame_Blender_v1.fbx (height ends up on local Z instead of Y). This
    // tries a couple of corrective root rotations and logs the resulting
    // world-space bounds so the right one can be picked before actually
    // placing the statue in the real scene.
    public static class StatueAxisFixDiagTest
    {
        [MenuItem("RuleGhost/Tests/Statue Axis Fix Diag")]
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Statue/Figure1_Standing.fbx");
            if (prefab == null)
            {
                Debug.LogError("[StatueAxisFixDiagTest] Could not load Figure1_Standing.fbx");
                return;
            }

            TryRotation(prefab, new Vector3(-90f, 0f, 0f));
            TryRotation(prefab, new Vector3(90f, 0f, 0f));
            TryRotation(prefab, Vector3.zero);
        }

        private static void TryRotation(GameObject prefab, Vector3 euler)
        {
            var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(euler));
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError($"[StatueAxisFixDiagTest] euler={euler}: no renderers");
                Object.DestroyImmediate(instance);
                return;
            }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Debug.Log($"[StatueAxisFixDiagTest] euler={euler} bounds.size={bounds.size} bounds.center={bounds.center}");
            Object.DestroyImmediate(instance);
        }
    }
}
