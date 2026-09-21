using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Applies the portrait texture (closed/open) directly onto the real Painting_North_1
    // placeholder inside the actual Lobby_Graybox scene, then renders it from a few plausible
    // patrol-viewing distances. Uses the real lobby lighting/geometry instead of an isolated
    // test scene, and avoids the FBX-imported frame entirely (its scale-on-import bug is a
    // separate issue) by texturing the existing graybox Cube placeholder.
    public static class PortraitLegibilityInLobbyTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string BaseTexPath = "Assets/Art/Paintings/Portrait/Portrait_base_v1.png";
        private const string OpenTexPath = "Assets/Art/Paintings/Portrait/Portrait_eyesopen_v1.png";
        private const string OutDir = "RawAssets/Paintings/Portrait/LegibilityTest";

        [MenuItem("RuleGhost/Tests/Portrait Legibility In Lobby")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var painting = GameObject.Find("Lobby_Graybox/Paintings/Painting_North_1");
            if (painting == null)
            {
                painting = GameObject.Find("Painting_North_1");
            }
            if (painting == null)
            {
                Debug.LogError("[PortraitLegibilityInLobbyTest] Could not find Painting_North_1 in the scene.");
                return;
            }

            var renderer = painting.GetComponent<Renderer>();
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexPath);
            var openTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OpenTexPath);
            if (renderer == null || baseTex == null || openTex == null)
            {
                Debug.LogError("[PortraitLegibilityInLobbyTest] Missing renderer or textures.");
                return;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            // Player eye height ~1.6m (matches GrayboxTestController camera), standing on the
            // lobby's center line looking straight down +Z at the north wall painting.
            Vector3 paintingPos = painting.transform.position;
            float[] distances = { 3f, 6f, 9f };

            foreach (var distance in distances)
            {
                CaptureWithTexture(renderer, baseTex, paintingPos, distance, $"lobby_{distance:0}m_closed");
                CaptureWithTexture(renderer, openTex, paintingPos, distance, $"lobby_{distance:0}m_open");
            }

            renderer.SetPropertyBlock(null);
            Debug.Log($"[PortraitLegibilityInLobbyTest] Renders written to {OutDir}");
        }

        private static void CaptureWithTexture(Renderer renderer, Texture2D tex, Vector3 paintingPos, float distance, string outName)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", tex);
            block.SetTexture("_MainTex", tex);
            // Unity's built-in Cube primitive's front-face V is flipped relative to the source
            // image; correct it here rather than baking a flip into the source texture.
            var flipV = new Vector4(1, -1, 0, 1);
            block.SetVector("_BaseMap_ST", flipV);
            block.SetVector("_MainTex_ST", flipV);
            renderer.SetPropertyBlock(block);

            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            Vector3 eyePos = new Vector3(paintingPos.x, 1.6f, paintingPos.z - distance);
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(paintingPos);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;

            const int width = 640, height = 800;
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
