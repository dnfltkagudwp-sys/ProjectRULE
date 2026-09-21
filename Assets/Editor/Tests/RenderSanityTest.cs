using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    public static class RenderSanityTest
    {
        [MenuItem("RuleGhost/Tests/Render Sanity Test")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGO = new GameObject("Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGO.transform.rotation = Quaternion.Euler(40f, -20f, 0f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            GameObject MarkerCube(string name, Vector3 pos, Vector3 scale, Color color)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.position = pos;
                go.transform.localScale = scale;
                var m = new Material(shader);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                m.color = color;
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go;
            }

            // Axis markers so screen orientation can be read directly off the render:
            // red = +X, green = +Y (up), blue = +Z.
            MarkerCube("AxisX", new Vector3(0.5f, 0, 0), new Vector3(1f, 0.05f, 0.05f), Color.red);
            MarkerCube("AxisY", new Vector3(0, 0.5f, 0), new Vector3(0.05f, 1f, 0.05f), Color.green);
            MarkerCube("AxisZ", new Vector3(0, 0, 0.5f), new Vector3(0.05f, 0.05f, 1f), Color.blue);

            var framePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Frame/Frame_Blender_v1.fbx");
            if (framePrefab != null)
            {
                Object.Instantiate(framePrefab, Vector3.zero, Quaternion.identity);
            }

            var camGO = new GameObject("Cam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = new Vector3(0, 0, -5);
            camGO.transform.LookAt(Vector3.zero, Vector3.up);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f);

            Debug.Log($"[RenderSanityTest] cam pos={camGO.transform.position} forward={camGO.transform.forward} " +
                      $"cullingMask={cam.cullingMask} pipeline={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline}");

            const int width = 640, height = 480;
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

            string outDir = Path.Combine(Application.dataPath, "..", "RawAssets", "SanityTest");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "sanity_red_cube.png"), outputTex.EncodeToPNG());

            Debug.Log("[RenderSanityTest] Wrote RawAssets/SanityTest/sanity_red_cube.png");
        }
    }
}
