using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off visual check for SetupHygrometerMaterials.cs -- confirms the new baked-texture
    // materials actually read correctly on CheckPoint_ThermoHygrometer's real box geometry (not
    // just the flat source image), and flags any obvious UV/aspect stretching.
    public static class HygrometerPreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Hygrometer Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            var go = GameObject.Find("CheckPoint_ThermoHygrometer");
            if (go == null)
            {
                Debug.LogError("[HygrometerPreviewTest] Could not find CheckPoint_ThermoHygrometer.");
                return;
            }

            Vector3 pos = go.transform.position;
            // The box is mounted on the north wall facing INTO the room (-Z), so the visible face
            // is the -Z side -- the camera needs to sit at a smaller z (inside the room), not a
            // larger one (which ends up behind the wall, looking at nothing).
            Vector3 eye = pos - new Vector3(0f, 0f, 0.7f);
            Capture(eye, pos, "hygrometer_normal_closeup");

            var renderer = go.GetComponent<Renderer>();
            var routineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Checkpoints/Hygrometer/M_Hygrometer_Routine.mat");
            var anomalyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Checkpoints/Hygrometer/M_Hygrometer_Anomaly.mat");

            renderer.sharedMaterial = routineMat;
            Capture(eye, pos, "hygrometer_routine_closeup");

            renderer.sharedMaterial = anomalyMat;
            Capture(eye, pos, "hygrometer_anomaly_closeup");

            // Restore Normal -- this test never saves the scene, but avoid leaving the in-memory
            // state on Anomaly in case something downstream reads it before a scene reload.
            var normalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Checkpoints/Hygrometer/M_Hygrometer_Normal.mat");
            renderer.sharedMaterial = normalMat;

            Debug.Log($"[HygrometerPreviewTest] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var flashGO = new GameObject("Flashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            var flashlight = flashGO.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.color = new Color(0.92f, 0.95f, 1f);
            // Much dimmer than the real player flashlight (8) -- purely to inspect the texture
            // itself at close range without blowing out, not to match in-game exposure.
            flashlight.intensity = 0.35f;
            flashlight.range = 10f;
            flashlight.spotAngle = 40f;
            flashlight.innerSpotAngle = 20f;

            const int width = 900, height = 900;
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
