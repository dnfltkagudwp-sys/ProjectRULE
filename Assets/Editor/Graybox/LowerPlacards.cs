using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // AdjustPortraitEyeLevel.cs lowered the 9 paintings by 0.15 but missed the museum placards
    // built independently under each one (AddMuseumDetails.AddPlacards) -- they're plain standalone
    // props, not parented to their painting, so they didn't move with it. Shifts each by the same
    // 0.15 to keep the painting-to-placard gap the same as before.
    public static class LowerPlacards
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float YOffset = -0.15f;

        [MenuItem("RuleGhost/Graybox/Lower Placards To Match Paintings")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var placardsGroup = GameObject.Find("Placards");
            if (placardsGroup == null)
            {
                Debug.LogWarning("[LowerPlacards] Could not find 'Placards' group.");
                return;
            }

            int moved = 0;
            foreach (Transform child in placardsGroup.transform)
            {
                child.position += new Vector3(0f, YOffset, 0f);
                moved++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[LowerPlacards] Moved {moved} placards down by {-YOffset}. Scene saved.");
        }
    }
}
