using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off cleanup: separate batch-process runs of AnomalyApplierSmokeTest each created their
    // own "HumidityReadout" GameObject (a fresh AnomalyRuntimeApplier instance per process has no
    // memory of one from a previous process), leaving duplicates stacked in the saved scene.
    // Removes all but one so the scene matches what a single continuous Play session would ever
    // actually produce.
    public static class CleanupDuplicateHumidityLabels
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Tests/Cleanup Duplicate Humidity Labels")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int removed = 0;
            bool keptOne = false;
            foreach (var go in all)
            {
                if (go.name != "HumidityReadout")
                {
                    continue;
                }

                if (!keptOne)
                {
                    keptOne = true;
                    go.SetActive(false); // matches the reset (inactive) baseline state
                    continue;
                }

                Object.DestroyImmediate(go);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[CleanupDuplicateHumidityLabels] Removed {removed} duplicate(s), kept 1 (inactive). Scene saved.");
        }
    }
}
