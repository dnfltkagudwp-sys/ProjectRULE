using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // The gap between the player's eye height (1.6m) and the north-wall portraits' own eye level
    // (~2.74m, via their EyeAnchor child) made looking a portrait in the eye require a steep upward
    // tilt. Splits the difference instead of moving either all the way: paintings down a bit, camera
    // up a bit. Only the 9 paintings' Y position changes (X/Z, rotation, tilt/flip state untouched);
    // EyeAnchor children follow automatically since they're parented with a local offset. Frame bars
    // (AddPaintingFrames) are NOT parented to their painting -- they're separately positioned in
    // world space at build time -- so AddPaintingFrames.Run() must be re-run after this to
    // realign them; this script deliberately does not do that itself, to keep the two concerns
    // (moving paintings vs. rebuilding frames) as separate, re-runnable steps.
    public static class AdjustPortraitEyeLevel
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        private const float OldPaintingY = 2.5f;
        private const float NewPaintingY = 2.35f;
        private const float NewCameraY = 1.75f;

        private static readonly string[] PaintingNames =
        {
            "Painting_North_1", "Painting_North_2", "Painting_North_3",
            "Painting_West_1", "Painting_West_2", "Painting_West_3",
            "Painting_East_1", "Painting_East_2", "Painting_East_3",
        };

        [MenuItem("RuleGhost/Graybox/Adjust Portrait Eye Level")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int moved = 0;
            foreach (var name in PaintingNames)
            {
                var painting = GameObject.Find(name);
                if (painting == null)
                {
                    Debug.LogWarning($"[AdjustPortraitEyeLevel] Could not find {name}, skipping.");
                    continue;
                }

                var pos = painting.transform.position;
                if (!Mathf.Approximately(pos.y, OldPaintingY))
                {
                    Debug.LogWarning($"[AdjustPortraitEyeLevel] {name} is at y={pos.y}, not the expected {OldPaintingY} -- " +
                                      "moving it anyway, but double-check it wasn't already adjusted.");
                }
                painting.transform.position = new Vector3(pos.x, NewPaintingY, pos.z);
                moved++;
            }

            var cameraGO = GameObject.Find("Main Camera");
            if (cameraGO != null)
            {
                var camPos = cameraGO.transform.localPosition;
                cameraGO.transform.localPosition = new Vector3(camPos.x, NewCameraY, camPos.z);
            }
            else
            {
                Debug.LogWarning("[AdjustPortraitEyeLevel] Could not find 'Main Camera'.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[AdjustPortraitEyeLevel] Moved {moved}/{PaintingNames.Length} paintings to y={NewPaintingY} " +
                      $"and camera to y={NewCameraY}. Scene saved. Re-run 'Add Painting Frames' next to realign the frame bars.");
        }
    }
}
