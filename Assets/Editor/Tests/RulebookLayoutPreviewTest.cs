using System.Collections.Generic;
using System.IO;
using RuleGhost.Anomalies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.EditorTools
{
    // One-off visual check for BuildRulebookUI's paged paper reader: renders every page of the
    // real rulebook (built from PatrolRuntimeController's own profile list, the same data
    // RulebookUI uses at runtime) to confirm the text fits the sheet. Doesn't save the scene.
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
            var controller = Object.FindFirstObjectByType<PatrolRuntimeController>();
            if (canvasGO == null || controller == null)
            {
                Debug.LogError("[RulebookLayoutPreviewTest] Missing RulebookCanvas or PatrolRuntimeController.");
                return;
            }

            var so = new SerializedObject(controller);
            var sequenceProp = so.FindProperty("patrolSequence");
            var profiles = new List<PatrolProfile>();
            for (int i = 0; i < sequenceProp.arraySize; i++)
            {
                profiles.Add(sequenceProp.GetArrayElementAtIndex(i).objectReferenceValue as PatrolProfile);
            }

            var pages = RulebookTextBuilder.BuildPages(profiles);
            Debug.Log($"[RulebookLayoutPreviewTest] {pages.Count} pages from {profiles.Count} profiles.");

            canvasGO.transform.Find("RulebookPanel").gameObject.SetActive(true);
            var body = canvasGO.transform.Find("RulebookPanel/Paper/BodyText").GetComponent<Text>();
            var indicator = canvasGO.transform.Find("RulebookPanel/Paper/PageIndicator").GetComponent<Text>();

            var canvas = canvasGO.GetComponent<Canvas>();
            var camGO = new GameObject("PreviewCam");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            // Far outside the lobby so no scene geometry (statue, walls) shows behind the overlay.
            camGO.transform.position = new Vector3(0f, 5000f, -10f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;

            for (int p = 0; p < pages.Count; p++)
            {
                body.text = pages[p];
                indicator.text = $"- {p + 1} / {pages.Count} -";
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
                float textHeight = body.preferredHeight;
                float boxHeight = body.rectTransform.rect.height;
                Debug.Log($"[RulebookLayoutPreviewTest] page {p + 1}: text {textHeight:F0}px in box {boxHeight:F0}px (overflow {Mathf.Max(0f, textHeight - boxHeight):F0}px)");
                Capture(cam, $"rulebook_page_{p + 1}");
            }

            Object.DestroyImmediate(camGO);
            Debug.Log($"[RulebookLayoutPreviewTest] Renders written to {OutDir}");
        }

        private static void Capture(Camera cam, string outName)
        {
            const int width = 1920, height = 1080;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();

            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", OutDir, outName + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
