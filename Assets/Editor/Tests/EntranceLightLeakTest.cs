using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Looks straight at the entrance gap and the guard room doorway to
    // confirm the solid-black camera background actually closes off the
    // "sky visible through the opening" light leak the user spotted.
    public static class EntranceLightLeakTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Entrance Light Leak Check")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            // Facing the entrance gap (south wall opening, z=-10) from inside.
            Capture(new Vector3(0f, 1.6f, -8.5f), new Vector3(0f, 1.6f, -10f), "leak_check_entrance");
            // Facing into the guard room doorway (west wall opening).
            Capture(new Vector3(-4f, 1.6f, -8f), new Vector3(-8.7f, 1.6f, -8f), "leak_check_guardroom");
            Capture(new Vector3(-3f, 1.9f, -5f), new Vector3(-8.7f, 3.5f, -8f), "leak_check_guardroom_top");

            Debug.Log($"[EntranceLightLeakTest] Renders written to {OutDir}");
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
