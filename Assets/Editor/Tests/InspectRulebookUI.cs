using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.EditorTools
{
    // Read-only structural check for BuildRulebookUI's output -- confirms the hierarchy exists,
    // the panel starts hidden, and RulebookUI's serialized references actually resolved, without
    // needing Play mode (RulebookUI.Update only runs while playing, so this can't exercise the
    // Tab-toggle behavior itself, only that the wiring it depends on is correct).
    public static class InspectRulebookUI
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Tests/Inspect Rulebook UI")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvasGO = GameObject.Find("RulebookCanvas");
            if (canvasGO == null)
            {
                Debug.LogError("[InspectRulebookUI] RulebookCanvas not found -- run RuleGhost/UI/Build Rulebook UI first.");
                return;
            }

            // GameObject.Find only searches active objects, and the panel starts inactive by
            // design -- walk the hierarchy directly instead so an inactive panel doesn't read as
            // "missing".
            var rulebook = canvasGO.GetComponent<RulebookUI>();
            var panelTransform = canvasGO.transform.Find("RulebookPanel");
            var hintTransform = canvasGO.transform.Find("HintText");
            var panel = panelTransform != null ? panelTransform.gameObject : null;
            var hint = hintTransform != null ? hintTransform.gameObject : null;
            var body = panelTransform != null ? panelTransform.Find("BodyText")?.GetComponent<Text>() : null;

            Debug.Log($"[InspectRulebookUI] Canvas found: {canvasGO != null}, RulebookUI component: {rulebook != null}, " +
                      $"Panel found: {panel != null}, Panel active (should be false): {panel != null && panel.activeSelf}, " +
                      $"HintText found: {hint != null}, BodyText found: {body != null}");

            if (rulebook != null)
            {
                var so = new SerializedObject(rulebook);
                var panelRootProp = so.FindProperty("panelRoot");
                var bodyTextProp = so.FindProperty("bodyText");
                Debug.Log($"[InspectRulebookUI] RulebookUI.panelRoot wired: {panelRootProp.objectReferenceValue != null}, " +
                          $"RulebookUI.bodyText wired: {bodyTextProp.objectReferenceValue != null}");
            }

            if (body != null)
            {
                Debug.Log($"[InspectRulebookUI] BodyText.font: {(body.font != null ? body.font.name : "NULL")}");
            }
        }
    }
}
