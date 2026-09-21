using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Wide overview renders of the whole lobby after applying the new
    // architecture materials (floor/wall/pedestal/door), so floor, walls,
    // and the inspection door can all be checked in context, not just the
    // statue cluster close-ups from StatuePlacementRenderTest.
    public static class RoomOverviewRenderTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Room Overview Render")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            // High, angled overview from near the entrance looking north.
            Capture(new Vector3(0, 4f, -9f), new Vector3(0, 1.2f, 6f), 70f, "overview_entrance_high");
            // Closer, eye-height shot toward the inspection door (east wall, z~9).
            Capture(new Vector3(3f, 1.6f, 6f), new Vector3(6.9f, 1.3f, 9f), 55f, "overview_inspection_door");
            // Floor/wall close read from a low, near angle.
            Capture(new Vector3(-3f, 1.2f, -6f), new Vector3(0f, 0.8f, -2f), 60f, "overview_floor_wall");

            Debug.Log($"[RoomOverviewRenderTest] Renders written to {OutDir}");
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
