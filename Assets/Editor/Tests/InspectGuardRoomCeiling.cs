using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off diagnostic: lists every renderer whose bounds fall near the guard room
    // ceiling/doorway region, to identify a black shape reported visible from inside the
    // guard room looking up toward the ceiling/doorway corner.
    public static class InspectGuardRoomCeiling
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Tests/Inspect Guard Room Ceiling")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var region = new Bounds(new Vector3(-10.5f, 3.5f, -11f), new Vector3(6f, 3f, 6f));

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!region.Intersects(r.bounds)) continue;
                Debug.Log($"[InspectGuardRoomCeiling] {r.gameObject.name} | worldPos={r.transform.position} " +
                          $"| localPos={r.transform.localPosition} | localScale={r.transform.localScale} " +
                          $"| bounds.center={r.bounds.center} bounds.size={r.bounds.size} " +
                          $"| mat={r.sharedMaterial?.name} | parent={r.transform.parent?.name}");
            }

            Debug.Log("[InspectGuardRoomCeiling] Done.");
        }
    }
}
