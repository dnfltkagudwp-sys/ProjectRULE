using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off visual check for BuildStartScene.cs. Overlay canvases don't render into a
    // Camera.Render() target, so this temporarily switches the canvas to ScreenSpaceCamera in
    // memory only (the scene is never saved).
    public static class StartScenePreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Start.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Start Scene Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            var cam = Object.FindFirstObjectByType<Camera>();
            var canvas = GameObject.Find("StartCanvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            const int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();

            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", OutDir, "start_scene.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[StartScenePreviewTest] Render written to {OutDir}/start_scene.png");
        }
    }
}
