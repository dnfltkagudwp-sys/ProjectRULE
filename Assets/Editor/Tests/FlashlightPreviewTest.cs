using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Renders from a walking-eye-height viewpoint with a temporary spotlight
    // matching the camera (simulating the player's flashlight) so the
    // darker-overall + flashlight combo can be checked without actually
    // playing. The real flashlight lives on Player_TestController/Main
    // Camera for actual play -- this just previews the same effect from a
    // fixed batch-render camera.
    public static class FlashlightPreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Flashlight Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            Capture(new Vector3(3f, 1.6f, -3f), new Vector3(0.5f, 1.6f, 2f), "flashlight_view_entrance");
            Capture(new Vector3(-3f, 1.6f, 5f), new Vector3(-6.95f, 2.2f, 5f), "flashlight_view_painting");

            Debug.Log($"[FlashlightPreviewTest] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;

            // Flashlight matching the camera, same as AddPlayerFlashlight's setup.
            var flashGO = new GameObject("PreviewFlashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            var light = flashGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.92f, 0.95f, 1f);
            light.intensity = 13f;
            light.range = 10f;
            light.spotAngle = 40f;
            light.innerSpotAngle = 20f;

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
