using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Turns the guard room desk's existing paper prop (GuardRoom_RuleDocument, added by
    // DecorateGuardRoom as an explicit placeholder for "the future rule-sheet interactable") into
    // the pickup for the 규칙서: gives it a look-at collider and RulebookPickup. Nothing else about
    // the prop changes, so swapping in a real model later only needs the same collider + component
    // on the new object. Safe to re-run.
    public static class BuildRulebookPickup
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string DocumentName = "GuardRoom_RuleDocument";

        [MenuItem("RuleGhost/Graybox/Build Rulebook Pickup")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // An earlier pass built a separate primitive booklet on the desk, which ended up
            // overlapping this placeholder -- remove it if it's still there.
            var oldBook = GameObject.Find("Rulebook_Pickup");
            if (oldBook != null)
            {
                Object.DestroyImmediate(oldBook);
            }

            var doc = GameObject.Find(DocumentName);
            if (doc == null)
            {
                Debug.LogError($"[BuildRulebookPickup] {DocumentName} not found.");
                return;
            }

            var box = doc.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = doc.AddComponent<BoxCollider>();
            }
            // Local units (the prop is a scaled unit cube, 0.35 x 0.02 x 0.25 in the world): a bit
            // wider and much taller than the paper itself, so the camera-center ray has a
            // forgiving target -- a true 2cm slab is easy to miss from standing height.
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(1.3f, 4f, 1.3f);
            // Trigger, so the player's CharacterController walks through rather than snagging on
            // it (Physics.Raycast in RulebookPickup queries triggers explicitly).
            box.isTrigger = true;

            if (doc.GetComponent<RulebookPickup>() == null)
            {
                doc.AddComponent<RulebookPickup>();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[BuildRulebookPickup] {DocumentName} is now the rulebook pickup at {doc.transform.position}. Scene saved.");
        }
    }
}
