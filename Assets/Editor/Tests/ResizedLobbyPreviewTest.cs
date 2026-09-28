using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off visual check for the 2026-09-28 lobby resize (see ResizeLobby.cs) -- confirms the
    // corridor between the statue and the side walls actually reads as wider, and that nothing
    // (paintings, frames, placards, lighting) drifted out of alignment after the coordinate pass.
    public static class ResizedLobbyPreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Resized Lobby Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            // Standing in the corridor between the pedestal and the west wall, looking at
            // Painting_West_2 (the center one, now at x=-8.95, z=0) -- this is exactly the "viewed
            // from the side while patrolling" complaint's location.
            Capture(new Vector3(-4f, 1.75f, 0f), new Vector3(-8.95f, 2.35f, 0f), "resize_west_corridor");

            // Wide shot from just inside the entrance looking down the full new length toward the
            // north wall, to see the statue + both side walls' worth of extra room at once.
            Capture(new Vector3(0f, 1.75f, -11f), new Vector3(0f, 2f, 13f), "resize_full_length");

            // Close on the (now 6.5m-spaced) west paintings from a natural standing distance.
            Capture(new Vector3(-5.5f, 1.75f, -3.2f), new Vector3(-8.95f, 2.35f, -6.5f), "resize_west_paintings");

            // South-west corner, looking along the entrance wall toward the west wall corner --
            // checks Wall_South_Left actually meets Wall_West_South now (FixEntranceWallGap.cs).
            Capture(new Vector3(-3f, 1.75f, -11f), new Vector3(-9f, 2f, -13f), "resize_southwest_corner");

            Debug.Log($"[ResizedLobbyPreviewTest] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.05f;

            var flashGO = new GameObject("PreviewFlashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            var light = flashGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.92f, 0.95f, 1f);
            light.intensity = 13f;
            light.range = 12f;
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
