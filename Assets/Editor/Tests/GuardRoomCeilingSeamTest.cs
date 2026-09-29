using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off diagnostic for a black shape reported near the guard room ceiling by the user.
    // Renders with the scene's REAL lighting (untouched) and a flashlight matching the real
    // player's (see LobbyGrayboxBuilder.BuildPlayer: intensity 8, range 10, spotAngle 40/20)
    // instead of the brighter ad-hoc flashlights other preview tests use, so this reproduces
    // what the player actually sees as closely as possible.
    public static class GuardRoomCeilingSeamTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Guard Room Ceiling Seam")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            // Standing near the guard room's south-center, looking north-east toward the doorway
            // opening and the ceiling corner above it.
            Capture(new Vector3(-10.7f, 1.75f, -12f), new Vector3(-9.2f, 3.0f, -9.6f), "seam_from_south");

            // Standing right at the doorway threshold facing back west into the guard room ceiling.
            Capture(new Vector3(-9.6f, 1.75f, -11f), new Vector3(-11.5f, 3.1f, -11f), "seam_at_threshold");

            // Standing in the main lobby just outside the doorway, looking west at the seam from
            // the lobby side.
            Capture(new Vector3(-7f, 1.75f, -11f), new Vector3(-9.3f, 3.5f, -11f), "seam_from_lobby");

            // Directly under the north-east corner of the guard room ceiling, looking straight up
            // at the corner where GuardRoom_Ceiling / GuardRoom_Wall_North / the doorway fill meet.
            Capture(new Vector3(-10f, 1.75f, -10f), new Vector3(-9.1f, 3.1f, -9.4f), "seam_corner_closeup");

            Debug.Log($"[GuardRoomCeilingSeamTest] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // Matches the real player's flashlight exactly (LobbyGrayboxBuilder.BuildPlayer).
            var flashGO = new GameObject("Flashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            var flashlight = flashGO.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.color = new Color(0.92f, 0.95f, 1f);
            flashlight.intensity = 8f;
            flashlight.range = 10f;
            flashlight.spotAngle = 40f;
            flashlight.innerSpotAngle = 20f;

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
