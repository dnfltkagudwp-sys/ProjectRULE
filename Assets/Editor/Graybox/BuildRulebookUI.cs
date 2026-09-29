using System.IO;
using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.EditorTools
{
    // Builds the 규칙서 (rulebook) HUD: a page-of-paper reader (dimmed backdrop + a slightly tilted
    // sheet with the booklet text on it), the "Tab : 규칙서" hint and the "E : 규칙서 획득" pickup
    // prompt. RulebookUI hides the panel/hint/prompt until the physical rulebook has been picked up
    // from the guard room desk (BuildRulebookPickup). Legacy uGUI Text with OS dynamic Korean fonts
    // (a serif first, so it reads like a printed document) rather than TextMeshPro -- this project
    // has never imported TMP's Essential Resources. No ScrollRect: pages are turned directly.
    public static class BuildRulebookUI
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string PaperTexturePath = "Assets/Art/UI/Rulebook_paper_v1.png";

        private static readonly string[] SerifFonts = { "Batang", "Nanum Myeongjo", "Malgun Gothic" };
        private const string SansFont = "Malgun Gothic";

        private static readonly Color InkColor = new Color(0.13f, 0.09f, 0.07f);
        private static readonly Color PaperFallbackColor = new Color(0.86f, 0.80f, 0.66f);

        [MenuItem("RuleGhost/UI/Build Rulebook UI")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find("RulebookCanvas");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var serif = Font.CreateDynamicFontFromOSFont(SerifFonts, 28);
            var sans = Font.CreateDynamicFontFromOSFont(SansFont, 24);

            var canvasGO = new GameObject("RulebookCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var hint = CreateText(canvasGO.transform, "HintText", sans, 20, TextAnchor.LowerLeft);
            hint.text = "Tab : 규칙서";
            hint.color = new Color(1f, 1f, 1f, 0.75f);
            SetAnchors(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(300f, 40f));

            var prompt = CreateText(canvasGO.transform, "PickupPrompt", sans, 30, TextAnchor.MiddleCenter);
            prompt.text = "E : 규칙서 획득";
            prompt.color = new Color(1f, 1f, 1f, 0.9f);
            SetAnchors(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, -200f), new Vector2(300f, -150f));

            // Full-screen dim; the sheet sits on top of it.
            var panelGO = new GameObject("RulebookPanel", typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            SetAnchors(panelGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var paperGO = new GameObject("Paper", typeof(Image));
            paperGO.transform.SetParent(panelGO.transform, false);
            var paperImage = paperGO.GetComponent<Image>();
            var paperSprite = LoadPaperSprite();
            if (paperSprite != null)
            {
                paperImage.sprite = paperSprite;
                // The source scan is a bright ivory -- multiplied down so a full-height sheet
                // isn't glaring on a mostly dark screen. Tune this Image color in the Inspector.
                paperImage.color = new Color(0.75f, 0.72f, 0.66f);
            }
            else
            {
                paperImage.color = PaperFallbackColor;
            }
            var paperRect = paperGO.GetComponent<RectTransform>();
            // 758 x 1040 matches the cropped paper texture's aspect (711x977) so it isn't stretched.
            SetAnchors(paperRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-379f, -520f), new Vector2(379f, 520f));
            paperRect.localRotation = Quaternion.Euler(0f, 0f, -1.5f);

            // Sized so page 1 (document header + its 3 rules, the tallest page) fits with real
            // slack -- dynamic-font metrics differ a little between the editor layout pass and
            // actual Play Mode rendering, so don't size this to the exact edit-time measurement.
            var body = CreateText(paperGO.transform, "BodyText", serif, 26, TextAnchor.UpperLeft);
            body.color = InkColor;
            body.supportRichText = true;
            body.lineSpacing = 1.15f;
            SetAnchors(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(80f, 90f), new Vector2(-80f, -70f));

            var pageIndicator = CreateText(paperGO.transform, "PageIndicator", serif, 22, TextAnchor.LowerCenter);
            pageIndicator.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.7f);
            SetAnchors(pageIndicator.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(40f, 30f), new Vector2(-40f, 70f));

            var rulebook = canvasGO.AddComponent<RulebookUI>();
            rulebook.EditorConfigure(panelGO, body, pageIndicator, hint.gameObject, prompt.gameObject);

            panelGO.SetActive(false);
            hint.gameObject.SetActive(false);
            prompt.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[BuildRulebookUI] Rulebook canvas built and wired (paper texture: {(paperSprite != null ? "yes" : "fallback color")}).");
        }

        private static Sprite LoadPaperSprite()
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "..", PaperTexturePath)))
            {
                return null;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(PaperTexturePath);
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PaperTexturePath);
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
