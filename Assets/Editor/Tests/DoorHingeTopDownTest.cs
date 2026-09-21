using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Top-down and eye-level shots of the door area to verify the hinge
    // swings into the room (not through the wall) and doesn't clip the
    // adjacent wall pieces or pilgrims at open angles.
    public static class DoorHingeTopDownTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Door Hinge Top-Down Check")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            Capture(new Vector3(6.9f, 6f, 9f), new Vector3(6.9f, 0f, 9f), 60f, "door_hinge_topdown");
            Capture(new Vector3(3.5f, 1.5f, 7.5f), new Vector3(6.9f, 1.2f, 9.2f), 60f, "door_hinge_eyelevel");

            Debug.Log($"[DoorHingeTopDownTest] Renders written to {OutDir}");
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
