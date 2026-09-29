using System.IO;
using RuleGhost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off check for BuildRulebookPickup: where the rulebook sits on the desk as seen from the
    // guard room, and whether RulebookPickup.IsLookingAt passes/fails as expected.
    public static class RulebookPickupPreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Rulebook Pickup Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            var book = GameObject.Find("GuardRoom_RuleDocument");
            if (book == null)
            {
                Debug.LogError("[RulebookPickupPreviewTest] GuardRoom_RuleDocument not found.");
                return;
            }
            Debug.Log($"[RulebookPickupPreviewTest] stray old book present: {GameObject.Find("Rulebook_Pickup") != null}");
            var col = book.GetComponent<Collider>();
            Physics.SyncTransforms();
            Vector3 bookPos = book.transform.position;

            // Player-ish eye near the guard room center, looking at the book.
            var eye = new Vector3(-11.3f, 1.65f, -10.44f);
            Capture(eye, bookPos, "rulebook_pickup_view");
            Capture(new Vector3(-10.4f, 1.65f, -11.6f), bookPos, "rulebook_pickup_view_b");

            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eye;
            camGO.transform.LookAt(bookPos);
            Debug.Log($"[RulebookPickupPreviewTest] look at book -> {RulebookPickup.IsLookingAt(cam, col, 2.5f)} (expect True)");
            camGO.transform.rotation = Quaternion.LookRotation(Vector3.left);
            Debug.Log($"[RulebookPickupPreviewTest] look away -> {RulebookPickup.IsLookingAt(cam, col, 2.5f)} (expect False)");
            camGO.transform.position = eye + new Vector3(-2f, 0f, 0f);
            camGO.transform.LookAt(bookPos);
            Debug.Log($"[RulebookPickupPreviewTest] too far (3.3m) -> {RulebookPickup.IsLookingAt(cam, col, 2.5f)} (expect False)");
            Object.DestroyImmediate(camGO);
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
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();

            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", OutDir, outName + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
        }
    }
}
