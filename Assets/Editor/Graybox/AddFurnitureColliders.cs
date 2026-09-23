using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // GuardRoom_Desk and GuardRoom_Chair were originally DecorateGuardRoom.cs's own graybox
    // primitives (which get a collider for free from CreatePrimitive) but were later swapped for
    // imported .glb models (Assets/Art/Furniture/GuardRoom_Desk.glb, GuardRoom_Chair.glb) -- pure
    // visual meshes with no collider of their own, which is why the player currently walks straight
    // through both. Adds a BoxCollider fitted to each object's actual rendered bounds directly onto
    // the scene instance, since the imported asset itself can't be edited to add one.
    public static class AddFurnitureColliders
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Graybox/Add Guard Room Furniture Colliders")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            bool added = false;
            added |= AddFittedBoxCollider("GuardRoom_Desk");
            added |= AddFittedBoxCollider("GuardRoom_Chair");

            if (added)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            Debug.Log("[AddFurnitureColliders] Done.");
        }

        // Fits a BoxCollider to the object's actual rendered geometry by transforming each
        // renderer's world-space bounding-box CORNERS (not just its size) into the target's local
        // space and taking the axis-aligned min/max there -- correct for any rotation (the desk is
        // rotated -90 degrees around Y), unlike naively reusing Renderer.bounds.size directly.
        private static bool AddFittedBoxCollider(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[AddFurnitureColliders] Could not find '{name}'.");
                return false;
            }

            if (go.GetComponentInChildren<Collider>() != null)
            {
                Debug.Log($"[AddFurnitureColliders] '{name}' already has a collider -- skipping.");
                return false;
            }

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[AddFurnitureColliders] '{name}' has no renderers to fit a collider to.");
                return false;
            }

            Vector3 localMin = Vector3.positiveInfinity;
            Vector3 localMax = Vector3.negativeInfinity;
            foreach (var renderer in renderers)
            {
                var b = renderer.bounds;
                for (int xi = 0; xi < 2; xi++)
                {
                    for (int yi = 0; yi < 2; yi++)
                    {
                        for (int zi = 0; zi < 2; zi++)
                        {
                            var corner = new Vector3(
                                xi == 0 ? b.min.x : b.max.x,
                                yi == 0 ? b.min.y : b.max.y,
                                zi == 0 ? b.min.z : b.max.z);
                            var local = go.transform.InverseTransformPoint(corner);
                            localMin = Vector3.Min(localMin, local);
                            localMax = Vector3.Max(localMax, local);
                        }
                    }
                }
            }

            var collider = go.AddComponent<BoxCollider>();
            collider.center = (localMin + localMax) / 2f;
            collider.size = localMax - localMin;
            return true;
        }
    }
}
