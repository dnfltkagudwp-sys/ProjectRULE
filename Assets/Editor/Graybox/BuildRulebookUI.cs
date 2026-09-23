using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.EditorTools
{
    // Builds the 규칙서 (rulebook) HUD: a small always-on "Tab: 규칙서" hint plus a full panel,
    // hidden until Tab is pressed, that RulebookUI fills in from the active PatrolDuty/Anomalies.
    // Legacy uGUI Text with an OS-dynamic Korean font (Malgun Gothic) rather than TextMeshPro --
    // this project has never imported TMP's Essential Resources, and a dynamic OS font renders
    // Korean correctly with zero setup, which is all this panel needs.
    public static class BuildRulebookUI
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string KoreanFontName = "Malgun Gothic";

        [MenuItem("RuleGhost/UI/Build Rulebook UI")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find("RulebookCanvas");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var font = Font.CreateDynamicFontFromOSFont(KoreanFontName, 24);

            var canvasGO = new GameObject("RulebookCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var hint = CreateText(canvasGO.transform, "HintText", font, 20, TextAnchor.LowerLeft);
            hint.text = "Tab : 규칙서";
            hint.color = new Color(1f, 1f, 1f, 0.75f);
            SetAnchors(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(300f, 40f));

            var panelGO = new GameObject("RulebookPanel", typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImage = panelGO.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.85f);
            var panelRect = panelGO.GetComponent<RectTransform>();
            // Point anchor (0.5,0.5) means offsetMin/offsetMax are corner positions relative to
            // screen center, not a size from an origin -- symmetric +/-half-size actually centers
            // the box, instead of pinning its bottom-left corner to screen center like before.
            SetAnchors(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-550f, -350f), new Vector2(550f, 350f));
            panelGO.SetActive(false);

            var title = CreateText(panelGO.transform, "TitleText", font, 32, TextAnchor.UpperLeft);
            title.text = "규칙서";
            title.fontStyle = FontStyle.Bold;
            // Top-stretch anchor: offsetMax.y must be <= 0 (inset from the top edge), not positive
            // (which pushed the title above the panel's own bounds, off the visible canvas).
            SetAnchors(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -70f), new Vector2(-40f, -10f));

            var body = CreateText(panelGO.transform, "BodyText", font, 26, TextAnchor.UpperLeft);
            SetAnchors(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(-40f, -70f));

            var rulebook = canvasGO.AddComponent<RulebookUI>();
            rulebook.EditorConfigure(panelGO, body);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[BuildRulebookUI] Rulebook canvas built and wired.");
        }

        private static Text CreateText(Transform parent, string name, Font font, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // RectTransform anchors expressed as (anchorMin, anchorMax, offsetMin, offsetMax) rather
        // than a single position+size, since every element here is meant to stay pinned to a
        // screen edge or stretch to fill its parent regardless of resolution.
        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
