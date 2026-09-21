using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Five representative screenshots for a final look at the current
    // state: each of the four walls plus the central pilgrim crowd.
    public static class FinalPreviewShots
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Final Preview Shots")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            Capture(new Vector3(0f, 1.6f, 4f), new Vector3(0f, 2.3f, 9.95f), "wall_north");
            Capture(new Vector3(-2f, 1.6f, 2f), new Vector3(-6.95f, 2.3f, 2f), "wall_west");
            Capture(new Vector3(2f, 1.6f, -3f), new Vector3(6.95f, 2.3f, -3f), "wall_east");
            Capture(new Vector3(0f, 1.6f, -5f), new Vector3(0f, 1.6f, -9.9f), "wall_south_entrance");
            Capture(new Vector3(1.5f, 1.5f, -1f), new Vector3(0f, 2.5f, 1f), "center_pilgrims");

            Debug.Log($"[FinalPreviewShots] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var flashGO = new GameObject("PreviewFlashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            var light = flashGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.92f, 0.95f, 1f);
            light.intensity = 13f;
            light.range = 10f;
            light.spotAngle = 40f;
            light.innerSpotAngle = 20f;

            const int width = 1000, height = 750;
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
