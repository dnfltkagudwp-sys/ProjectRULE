using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Close-up on each East wall painting to verify the new abstract textures
    // (ApplyPaintingEastTextures) read correctly and right-side up in the scene.
    public static class PaintingEastCloseupTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Paintings/EastCheck";

        [MenuItem("RuleGhost/Tests/Painting East Closeup")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            CaptureFor("Painting_East_1");
            CaptureFor("Painting_East_2");
            CaptureFor("Painting_East_3");

            Debug.Log($"[PaintingEastCloseupTest] Renders written to {OutDir}");
        }

        private static void CaptureFor(string objectName)
        {
            var painting = GameObject.Find(objectName);
            Vector3 target = painting != null ? painting.transform.position : Vector3.zero;
            Capture(target + new Vector3(-2.5f, 0.3f, 0.3f), target, 35f, objectName.ToLowerInvariant());
        }

        private static void Capture(Vector3 eyePos, Vector3 target, float fov, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;

            const int width = 900, height = 700;
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
            Object.DestroyImmediate(camGO);
        }
    }
}
