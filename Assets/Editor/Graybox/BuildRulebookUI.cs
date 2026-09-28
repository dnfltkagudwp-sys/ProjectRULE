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
    //
    // The body sits in a masked viewport with a ContentSizeFitter-driven content object rather
    // than a Unity ScrollRect -- this project has no EventSystem anywhere (every interaction is
    // Keyboard.current/Mouse.current polling, see GrayboxTestController/DoorTestInteraction/etc.),
    // and ScrollRect's mouse-wheel/drag handling needs one. RulebookUI moves the content rect
    // directly instead, matching how the rest of the project already handles input.
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

            // Bigger than before (was 1100x700) -- 9 rules of rule text plain overflowed the old
            // panel with nothing to clip or scroll it, spilling raw text past the black background.
            // Height has real slack (measured content ~720px against a first pass at 750px
            // viewport left only ~30px spare, which real Play Mode font rendering ate into and
            // clipped the last line) -- deliberately generous now instead of sized to the exact
            // edit-time measurement, since dynamic-font metrics aren't identical between an
            // editor-script layout pass and actual Play Mode rendering.
            var panelGO = new GameObject("RulebookPanel", typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImage = panelGO.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.85f);
            var panelRect = panelGO.GetComponent<RectTransform>();
            SetAnchors(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-650f, -520f), new Vector2(650f, 520f));
            panelGO.SetActive(false);

            var title = CreateText(panelGO.transform, "TitleText", font, 32, TextAnchor.UpperLeft);
            title.text = "규칙서";
            title.fontStyle = FontStyle.Bold;
            SetAnchors(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -70f), new Vector2(-40f, -10f));

            var scrollHint = CreateText(panelGO.transform, "ScrollHintText", font, 18, TextAnchor.UpperLeft);
            scrollHint.text = "↑↓ 또는 마우스 휠로 스크롤";
            scrollHint.color = new Color(1f, 1f, 1f, 0.5f);
            SetAnchors(scrollHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -98f), new Vector2(-40f, -74f));

            // Viewport: clips the content, doesn't move itself. RectMask2D needs no Image to work.
            var viewportGO = new GameObject("Viewport", typeof(RectMask2D));
            viewportGO.transform.SetParent(panelGO.transform, false);
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            SetAnchors(viewportRect, Vector2.zero, Vector2.one, new Vector2(40f, 40f), new Vector2(-40f, -110f));

            // Content: the thing that actually scrolls. Pivot pinned to its own top so
            // ContentSizeFitter grows it downward from a fixed top edge instead of from center.
            var body = CreateText(viewportGO.transform, "BodyText", font, 26, TextAnchor.UpperLeft);
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = Vector2.zero;
            // A freshly added RectTransform keeps its default sizeDelta (100x100) even after the
            // anchors above are set to fully stretch horizontally -- left unset, that 100 pads the
            // rect 50px wider than the viewport on each side, and the mask then clips those margins
            // off the actual text. Zeroing x here is what makes the stretch anchors take effect;
            // ContentSizeFitter still owns y.
            bodyRect.sizeDelta = new Vector2(0f, bodyRect.sizeDelta.y);
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rulebook = canvasGO.AddComponent<RulebookUI>();
            rulebook.EditorConfigure(panelGO, body, bodyRect, viewportRect);

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
