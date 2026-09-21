using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // The scene's single directional light had shadows set to None, so no
    // object grounds itself against the floor with a contact shadow --
    // objects that are geometrically flush with the floor (like the
    // inspection door) still read as floating with nothing darkening the
    // floor at their base. This is a correctness fix (turning shadows on),
    // separate from the deferred dramatic mood-lighting pass.
    public static class EnableSceneShadows
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Graybox/Enable Scene Shadows")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int fixedCount = 0;
            foreach (var light in lights)
            {
                if (light.type != LightType.Directional) continue;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 1f;
                // Defaults (bias 0.05, normalBias 0.4) peter-pan on thin
                // objects like the 0.08m door slab and 0.2m frame -- their
                // shadow detaches from their own base instead of grounding
                // them, which reads as floating even with shadows enabled.
                light.shadowBias = 0.01f;
                light.shadowNormalBias = 0.05f;
                fixedCount++;
                Debug.Log($"[EnableSceneShadows] Enabled soft shadows on '{light.gameObject.name}'.");
            }

            if (fixedCount == 0)
            {
                Debug.LogWarning("[EnableSceneShadows] No directional light found.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[EnableSceneShadows] Scene saved.");
        }
    }
}
