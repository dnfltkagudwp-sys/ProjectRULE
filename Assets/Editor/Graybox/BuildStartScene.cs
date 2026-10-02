using System.Linq;
using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.EditorTools
{
    // Builds Assets/Scenes/Start.unity (title screen) from scratch and puts it first in the build
    // settings, ahead of Lobby_Graybox. Safe to re-run: it rebuilds the Start scene each time and
    // only rewrites the build settings scene list. Same legacy-uGUI + OS dynamic Korean font
    // convention as BuildRoundIntroUI.cs.
    public static class BuildStartScene
    {
        private const string StartScenePath = "Assets/Scenes/Start.unity";
        private const string GameScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string KoreanFontName = "Malgun Gothic";

        [MenuItem("RuleGhost/UI/Build Start Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;

            var font = Font.CreateDynamicFontFromOSFont(KoreanFontName, 24);

            var canvasGO = new GameObject("StartCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var titleGroup = new GameObject("TitleGroup", typeof(RectTransform), typeof(CanvasGroup));
            titleGroup.transform.SetParent(canvasGO.transform, false);
            Stretch(titleGroup.GetComponent<RectTransform>());
            var title = CreateText(titleGroup.transform, "Title", font, 120, TextAnchor.MiddleCenter, "야간순찰");
            title.color = new Color(0.9f, 0.9f, 0.92f);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(0f, 200f);
            titleRect.anchoredPosition = new Vector2(0f, 80f);

            var hintGroup = new GameObject("HintGroup", typeof(RectTransform), typeof(CanvasGroup));
            hintGroup.transform.SetParent(canvasGO.transform, false);
            Stretch(hintGroup.GetComponent<RectTransform>());
            var hint = CreateText(hintGroup.transform, "Hint", font, 32, TextAnchor.MiddleCenter,
                "아무 키나 눌러 시작\n<size=22>Esc  종료</size>");
            hint.supportRichText = true;
            hint.color = new Color(0.75f, 0.75f, 0.78f);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0.5f);
            hintRect.anchorMax = new Vector2(1f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(0f, 120f);
            hintRect.anchoredPosition = new Vector2(0f, -220f);

            var fadeGO = new GameObject("FadeOut", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            fadeGO.transform.SetParent(canvasGO.transform, false);
            Stretch(fadeGO.GetComponent<RectTransform>());
            fadeGO.GetComponent<Image>().color = Color.black;
            var fadeGroup = fadeGO.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;

            // The Start scene's own sound slots (title music, start sound) -- fill in the Inspector.
            new GameObject("SoundBank").AddComponent<RuleGhost.Anomalies.SoundBank>();

            var ui = canvasGO.AddComponent<StartScreenUI>();
            ui.EditorConfigure(titleGroup.GetComponent<CanvasGroup>(), hintGroup.GetComponent<CanvasGroup>(),
                fadeGroup, title, hint);

            EditorSceneManager.SaveScene(scene, StartScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StartScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            }.Concat(EditorBuildSettings.scenes.Where(s => s.path != StartScenePath && s.path != GameScenePath && s.path != "Assets/Scenes/SampleScene.unity")).ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildStartScene] Start scene built; build settings: Start (0), Lobby_Graybox (1).");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, Font font, int fontSize, TextAnchor anchor, string content)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
