using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Verification renders for DecorateGuardRoom -- checks the door swings clear, and the
    // desk/chair/document read sensibly from both the lobby (through the doorway) and inside the
    // room itself.
    public static class GuardRoomFurnishingCheckTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Guard Room Furnishing Check")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            Capture(new Vector3(-4f, 1.6f, -8f), new Vector3(-9f, 1.4f, -8f), 60f, "guardroom_from_lobby");
            Capture(new Vector3(-8f, 1.6f, -6.8f), new Vector3(-9.9f, 1f, -8f), 65f, "guardroom_interior_desk");
            // Further back, narrower FOV, near head-on -- less wide-angle/oblique distortion than
            // the close-up shot above, for checking whether furniture is genuinely upright/aligned
            // versus just looking skewed from a wide lens close to it. Room interior is roughly
            // x in [-10.2,-7.1], z in [-9.5,-6.5], height under 3 -- both prior attempts had the
            // camera outside those bounds (past the north wall / above the ceiling).
            // User hand-placed the desk/chair near x=-7.66/-8.22, z=-7.44/-7.79 (east side, near
            // the door/window) -- not the west wall where the auto-placement pass put them.
            Capture(new Vector3(-9.5f, 1.6f, -6.9f), new Vector3(-7.9f, 0.6f, -7.6f), 45f, "guardroom_desk_chair_clean");
            // Offset from Point_GuardRoom's exact position (-8.7, 2, -8) -- sitting right at the
            // light previously produced a hard shadow artifact in the render.
            Capture(new Vector3(-9.3f, 2.9f, -7.6f), new Vector3(-9.3f, 0f, -8f), 50f, "guardroom_topdown");

            Debug.Log($"[GuardRoomFurnishingCheckTest] Renders written to {OutDir}");
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
