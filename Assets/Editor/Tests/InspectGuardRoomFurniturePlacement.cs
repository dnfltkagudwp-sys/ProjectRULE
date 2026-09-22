using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Read-only: prints the current desk/chair transforms and world-space bounds so a manual
    // placement (done by hand in the Editor) can be checked for overlap/wall-clipping without
    // touching or re-running the placement logic itself.
    public static class InspectGuardRoomFurniturePlacement
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Tests/Inspect Guard Room Furniture Placement")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Inspect("GuardRoom_Desk");
            Inspect("GuardRoom_Chair");
        }

        private static void Inspect(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogError($"[InspectGuardRoomFurniturePlacement] Could not find {name}.");
                return;
            }

            var t = go.transform;
            var renderers = go.GetComponentsInChildren<Renderer>();
            Bounds? bounds = null;
            foreach (var r in renderers)
            {
                if (bounds == null) bounds = r.bounds;
                else { var b = bounds.Value; b.Encapsulate(r.bounds); bounds = b; }
            }

            string boundsStr = bounds.HasValue
                ? $"worldBoundsCenter={bounds.Value.center} worldBoundsSize={bounds.Value.size} min={bounds.Value.min} max={bounds.Value.max}"
                : "no renderers";

            Debug.Log($"[InspectGuardRoomFurniturePlacement] {name}: localPos={t.localPosition} worldPos={t.position} " +
                      $"localEuler={t.localEulerAngles} localScale={t.localScale} {boundsStr}");
        }
    }
}
