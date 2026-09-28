using System.IO;
using RuleGhost.Anomalies;
using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.EditorTools
{
    // One-off visual check for BuildRulebookUI's scroll viewport -- renders the panel with the
    // real full rule text (read straight off PatrolRuntimeController's own serialized profile
    // list, the same data RulebookUI.Refresh() uses at runtime) to confirm the body is actually
    // clipped by the viewport instead of spilling past the panel, both scrolled to the top and
    // scrolled to the bottom. Doesn't touch or save the scene.
    public static class RulebookLayoutPreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Rulebook Layout Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            var canvasGO = GameObject.Find("RulebookCanvas");
            if (canvasGO == null)
            {
                Debug.LogError("[RulebookLayoutPreviewTest] RulebookCanvas not found -- run Build Rulebook UI first.");
                return;
            }

            var runtimeGO = GameObject.Find("PatrolRuntimeController");
            var controller = runtimeGO != null ? runtimeGO.GetComponent<PatrolRuntimeController>() : null;
            if (controller == null)
            {
                Debug.LogError("[RulebookLayoutPreviewTest] Could not find PatrolRuntimeController in scene.");
                return;
            }

            var so = new SerializedObject(controller);
            var sequenceProp = so.FindProperty("patrolSequence");
            var profiles = new System.Collections.Generic.List<PatrolProfile>();
            for (int i = 0; i < sequenceProp.arraySize; i++)
            {
                profiles.Add(sequenceProp.GetArrayElementAtIndex(i).objectReferenceValue as PatrolProfile);
            }

            string text = RulebookTextBuilder.BuildAll(profiles);
            Debug.Log($"[RulebookLayoutPreviewTest] Built text length: {text.Length} chars from {profiles.Count} profiles.");

            var panelGO = canvasGO.transform.Find("RulebookPanel").gameObject;
            panelGO.SetActive(true);

            var bodyText = canvasGO.transform.Find("RulebookPanel/Viewport/BodyText").GetComponent<Text>();
            bodyText.text = text;
            var contentRect = bodyText.rectTransform;
            var viewportRect = canvasGO.transform.Find("RulebookPanel/Viewport").GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            Debug.Log($"[RulebookLayoutPreviewTest] Content height: {contentRect.rect.height:F0}px, " +
                      $"Viewport height: {viewportRect.rect.height:F0}px, " +
                      $"Overflow: {Mathf.Max(0f, contentRect.rect.height - viewportRect.rect.height):F0}px");

            // Screen Space - Camera only for this preview render, so the overlay canvas actually
            // shows up in a RenderTexture (Screen Space - Overlay draws straight to the display,
            // not through any camera). Scene is never saved, so this doesn't stick.
            var canvas = canvasGO.GetComponent<Canvas>();
            var camGO = new GameObject("PreviewCam");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGO.transform.position = new Vector3(0f, 0f, -10f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;

            contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, 0f);
            Capture(cam, "rulebook_scrolled_top");

            float maxScroll = Mathf.Max(0f, contentRect.rect.height - viewportRect.rect.height);
            contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, maxScroll);
            Capture(cam, "rulebook_scrolled_bottom");

            Object.DestroyImmediate(camGO);
            Debug.Log($"[RulebookLayoutPreviewTest] Renders written to {OutDir}");
        }

        private static void Capture(Camera cam, string outName)
        {
            const int width = 1200, height = 900;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();

            RenderTexture.active = rt;
            var outputTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            outputTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outputTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();

            string fullDir = Path.Combine(Application.dataPath, "..", OutDir);
            File.WriteAllBytes(Path.Combine(fullDir, outName + ".png"), outputTex.EncodeToPNG());
            Object.DestroyImmediate(outputTex);
        }
    }
}
