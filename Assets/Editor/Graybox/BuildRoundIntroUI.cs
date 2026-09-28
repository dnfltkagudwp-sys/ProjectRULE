using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.EditorTools
{
    // Builds the shared full-screen black overlay (hidden, alpha 0) used by both the round-start
    // intro (day/time label, RoundIntroUI) and the death sequence (DeathSequenceUI) -- see
    // ScreenFadeController for why they share one CanvasGroup instead of one each. Same legacy-uGUI
    // + OS dynamic Korean font convention as BuildRulebookUI.cs -- see that file's comment for why
    // (no TMP Essential Resources imported in this project).
    public static class BuildRoundIntroUI
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string KoreanFontName = "Malgun Gothic";

        [MenuItem("RuleGhost/UI/Build Round Intro UI")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find("RoundIntroCanvas");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var font = Font.CreateDynamicFontFromOSFont(KoreanFontName, 24);

            var canvasGO = new GameObject("RoundIntroCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above RulebookCanvas's default sort so the intro screen wins during the round-start
            // moment even if the rulebook panel is somehow left open from the previous round.
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // CanvasGroup doesn't require a RectTransform, so without explicitly adding one here
            // this GameObject would only get a plain Transform -- its children would then anchor
            // against a degenerate (zero-size) parent rect instead of stretching to the canvas.
            var groupGO = new GameObject("FadeGroup", typeof(RectTransform), typeof(CanvasGroup));
            groupGO.transform.SetParent(canvasGO.transform, false);
            var groupRect = groupGO.GetComponent<RectTransform>();
            groupRect.anchorMin = Vector2.zero;
            groupRect.anchorMax = Vector2.one;
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;
            var canvasGroup = groupGO.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Shared between RoundIntroUI and DeathSequenceUI -- see ScreenFadeController's own
            // comment for why sharing one overlay instead of one each avoids a hand-off seam
            // between a death sequence's black hold and the next round's intro fading in.
            var screenFade = groupGO.AddComponent<ScreenFadeController>();
            screenFade.EditorConfigure(canvasGroup);

            var panelGO = new GameObject("BlackBackground", typeof(Image));
            panelGO.transform.SetParent(groupGO.transform, false);
            var panelImage = panelGO.GetComponent<Image>();
            panelImage.color = Color.black;
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var label = CreateText(groupGO.transform, "Label", font, 48, TextAnchor.MiddleCenter);
            label.color = Color.white;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            // Its own group, nested inside the shared blackout, so the day/time text can fade in
            // separately from the black behind it -- see RoundIntroUI for why that matters.
            var labelGroup = label.gameObject.AddComponent<CanvasGroup>();
            labelGroup.alpha = 0f;
            labelGroup.blocksRaycasts = false;
            labelGroup.interactable = false;

            var intro = canvasGO.AddComponent<RoundIntroUI>();
            intro.EditorConfigure(screenFade, label, labelGroup);

            var death = canvasGO.AddComponent<DeathSequenceUI>();
            death.EditorConfigure(screenFade);

            // Editor-only preview keys (1-6) for the death stings -- see the component's comment.
            canvasGO.AddComponent<RuleGhost.Debugging.DeathSequenceTestKeys>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[BuildRoundIntroUI] Round intro + death sequence canvas built and wired.");
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
