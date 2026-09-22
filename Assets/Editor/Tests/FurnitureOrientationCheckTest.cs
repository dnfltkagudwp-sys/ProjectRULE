using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Isolated multi-angle renders of the generated desk/chair models (front, side, top) so their
    // actual front-facing direction and which local axis is "width" vs "depth" can be read off
    // visually, instead of guessed at -- the guess was wrong (SwapGuardRoomFurnitureModels placed
    // them clipping into the wall and facing an arbitrary direction).
    public static class FurnitureOrientationCheckTest
    {
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Furniture Orientation Check")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            RenderIsolated("Assets/Art/Furniture/GuardRoom_Desk.glb", "desk_glb");
            RenderIsolated("Assets/Art/Furniture/GuardRoom_Chair.glb", "chair_glb");

            Debug.Log($"[FurnitureOrientationCheckTest] Renders written to {OutDir}");
        }

        private static void RenderIsolated(string modelPath, string label)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null)
            {
                Debug.LogError($"[FurnitureOrientationCheckTest] Could not load {modelPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var renderer = instance.GetComponentInChildren<Renderer>();
            var bounds = renderer.bounds;
            Vector3 center = bounds.center;
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float dist = maxDim * 2.2f;

            // A basic light so the isolated object isn't pitch black (no scene lighting here).
            var lightGO = new GameObject("TempLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // +Z looking down -Z (front, assuming +Z is "forward" in world space), +X (side), +Y top-down.
            Capture(center + new Vector3(0, bounds.size.y * 0.3f, dist), center, $"{label}_front_plusZ");
            Capture(center + new Vector3(0, bounds.size.y * 0.3f, -dist), center, $"{label}_back_minusZ");
            Capture(center + new Vector3(dist, bounds.size.y * 0.3f, 0), center, $"{label}_side_plusX");
            Capture(center + new Vector3(0, dist * 1.3f, 0.001f), center, $"{label}_top");

            Object.DestroyImmediate(lightGO);
            Object.DestroyImmediate(instance);
        }

        private static void Capture(Vector3 eyePos, Vector3 target, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(target);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.01f;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            const int width = 700, height = 700;
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
