using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Adds a small "EyeAnchor" child Transform to each North wall portrait, positioned roughly at
    // the portrait's eye level and just proud of its front face. GazeSensor uses this instead of
    // a collider raycast -- the 3 portraits sit close together on the same wall, and a collider
    // hit only tells you which painting's box was struck, not whether the player is precisely
    // centered on this one's eyes.
    public static class AddEyeAnchors
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        private static readonly string[] NorthPortraits =
        {
            "Painting_North_1", "Painting_North_2", "Painting_North_3",
        };

        // Local space of the painting Cube (unit cube, so [-0.5, 0.5] spans its own size):
        // slightly above center (upper-third, roughly where a headshot portrait's eyes sit) and
        // just past the front face. North wall paintings face -Z (into the room, per their
        // m_LocalPosition being just short of the north wall's higher Z).
        private static readonly Vector3 LocalEyeOffset = new Vector3(0f, 0.15f, -0.55f);

        [MenuItem("RuleGhost/Anomalies/Add Eye Anchors")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int added = 0;
            foreach (var name in NorthPortraits)
            {
                var painting = GameObject.Find(name);
                if (painting == null)
                {
                    Debug.LogWarning($"[AddEyeAnchors] Could not find {name}, skipping.");
                    continue;
                }

                var existing = painting.transform.Find("EyeAnchor");
                var anchorGO = existing != null ? existing.gameObject : new GameObject("EyeAnchor");
                anchorGO.transform.SetParent(painting.transform, false);
                anchorGO.transform.localPosition = LocalEyeOffset;
                anchorGO.transform.localRotation = Quaternion.identity;
                added++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[AddEyeAnchors] Added/updated EyeAnchor on {added}/{NorthPortraits.Length} portraits. Scene saved.");
        }
    }
}
