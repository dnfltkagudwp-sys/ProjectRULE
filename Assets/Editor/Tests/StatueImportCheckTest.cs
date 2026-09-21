using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off check: the earlier Frame_Blender_v1.fbx import rendered as a
    // squashed sliver in batch-mode captures despite correct bounds/rotation,
    // so any newly imported 3D asset gets a quick visual sanity render before
    // being trusted, rather than assuming a clean AssetDatabase import means
    // the mesh actually displays correctly.
    public static class StatueImportCheckTest
    {
        private const string OutDir = "RawAssets/Statue/ImportCheck";

        [MenuItem("RuleGhost/Tests/Statue Import Check")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            CheckAsset("Assets/Art/Statue/Figure1_Standing.fbx", "standing");
            CheckAsset("Assets/Art/Statue/Figure1_TurnedLeft.fbx", "turnedleft");

            AssetDatabase.SaveAssets();
            Debug.Log($"[StatueImportCheckTest] Renders written to {OutDir}");
        }

        private static void CheckAsset(string path, string label)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[StatueImportCheckTest] Could not load {path}");
                return;
            }

            var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError($"[StatueImportCheckTest] {label}: no renderers found on instantiated asset.");
                Object.DestroyImmediate(instance);
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Debug.Log($"[StatueImportCheckTest] {label}: renderers={renderers.Length} bounds.center={bounds.center} bounds.size={bounds.size}");

            var lightGO = new GameObject("Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var camGO = new GameObject("Cam");
            var cam = camGO.AddComponent<Camera>();
            float dist = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 1.8f + 0.5f;
            camGO.transform.position = bounds.center + new Vector3(0, 0, -dist);
            camGO.transform.LookAt(bounds.center);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = dist * 4f;

            const int width = 500, height = 700;
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
            File.WriteAllBytes(Path.Combine(fullDir, $"{label}.png"), outputTex.EncodeToPNG());

            Object.DestroyImmediate(outputTex);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(lightGO);
            Object.DestroyImmediate(instance);
        }
    }
}
