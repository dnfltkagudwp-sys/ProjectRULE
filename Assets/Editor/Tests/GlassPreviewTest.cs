using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off visual check for MakeGlassTransparent.cs -- confirms the entrance door/sidelights
    // and the guard room window actually read as glass (transparent + reflective) instead of a
    // pale opaque panel.
    public static class GlassPreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Glass Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            // Standing a few meters inside the lobby, looking back at the entrance door.
            Capture(new Vector3(0f, 1.75f, -9f), new Vector3(0f, 1.6f, -13f), "glass_entrance_door");

            // Standing in the lobby corridor (east of the west wall, which now sits at x=-9 after
            // the resize), looking west at the guard room window through the wall.
            Capture(new Vector3(-6f, 1.6f, -11f), new Vector3(-9.1f, 1.5f, -11f), "glass_guardroom_window");

            Debug.Log($"[GlassPreviewTest] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            // Matches the real player camera (see LobbyGrayboxBuilder.BuildPlayer) -- without this,
            // anything transparent (the glass this test exists to check) shows Unity's default sky
            // through it instead of the solid-black night the real game actually renders.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var flashGO = new GameObject("PreviewFlashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            var light = flashGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.92f, 0.95f, 1f);
            light.intensity = 15f;
            light.range = 14f;
            light.spotAngle = 45f;
            light.innerSpotAngle = 20f;

            const int width = 1100, height = 750;
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
