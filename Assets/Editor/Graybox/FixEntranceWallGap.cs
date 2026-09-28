using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // ResizeLobby.cs moved the west/east wall corners out to +/-9 but left Wall_South_Left/Right
    // at their old 5.5m width (matching the old +/-7 corner), leaving a 2m gap on each side of the
    // entrance wall where it used to meet the corner. Widens both to span the new corner (+/-9) to
    // the same 3m entrance gap (+/-1.5) they've always kept. One-off, not meant to be re-run.
    public static class FixEntranceWallGap
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Graybox/Fix Entrance Wall Gap")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            SetLocal("Wall_South_Left", new Vector3(-5.25f, 2.5f, -13.1f), new Vector3(7.5f, 5f, 0.2f));
            SetLocal("Wall_South_Right", new Vector3(5.25f, 2.5f, -13.1f), new Vector3(7.5f, 5f, 0.2f));

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[FixEntranceWallGap] Entrance wall segments widened to meet the new corners. Scene saved.");
        }

        private static void SetLocal(string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[FixEntranceWallGap] Could not find '{name}' -- skipping.");
                return;
            }
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
        }
    }
}
