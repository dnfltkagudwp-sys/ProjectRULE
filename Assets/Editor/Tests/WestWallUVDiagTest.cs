using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off diagnostic: the West-wall painting cubes are scaled along a different thin axis
    // (X) than the North-wall cubes (Z), so the known North-wall UV V-flip fix may not carry
    // over as-is. Renders the same landscape texture on Painting_West_1 with a few candidate
    // ST transforms so the correct one can be picked by inspection.
    public static class WestWallUVDiagTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string TexPath = "Assets/Art/Paintings/Landscape3/Landscape3_person_v1.png";
        private const string OutDir = "RawAssets/Paintings/Landscape/LegibilityTest/UVDiag";

        [MenuItem("RuleGhost/Tests/West Wall UV Diag")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var painting = GameObject.Find("Painting_West_1");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            if (painting == null || tex == null)
            {
                Debug.LogError("[WestWallUVDiagTest] Missing painting or texture.");
                return;
            }

            var renderer = painting.GetComponent<Renderer>();
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            Debug.Log($"[WestWallUVDiagTest] DIAG painting={painting.name} " +
                      $"tex={tex.name} texSize=({tex.width}x{tex.height}) rendererSharedMat={renderer.sharedMaterial.name} " +
                      $"rendererSharedMatMainTex={(renderer.sharedMaterial.HasProperty("_BaseMap") ? renderer.sharedMaterial.GetTexture("_BaseMap")?.name : "n/a")}");

            var variants = new (string name, Vector4 st)[]
            {
                ("identity", new Vector4(1, 1, 0, 0)),
                ("vflip", new Vector4(1, -1, 0, 1)),
                ("uflip", new Vector4(-1, 1, 1, 0)),
                ("uvflip", new Vector4(-1, -1, 1, 1)),
            };

            Vector3 paintingPos = painting.transform.position;
            foreach (var (name, st) in variants)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetTexture("_BaseMap", tex);
                block.SetTexture("_MainTex", tex);
                block.SetVector("_BaseMap_ST", st);
                block.SetVector("_MainTex_ST", st);
                renderer.SetPropertyBlock(block);

                var camGO = new GameObject("TestCam");
                var cam = camGO.AddComponent<Camera>();
                Vector3 eyePos = new Vector3(paintingPos.x + 3f, 1.6f, paintingPos.z);
                camGO.transform.position = eyePos;
                camGO.transform.LookAt(paintingPos);
                cam.fieldOfView = 45f;
                cam.nearClipPlane = 0.05f;

                const int width = 500, height = 650;
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
                File.WriteAllBytes(Path.Combine(fullDir, $"west_{name}.png"), outputTex.EncodeToPNG());

                Object.DestroyImmediate(outputTex);
                Object.DestroyImmediate(camGO);
            }

            renderer.SetPropertyBlock(null);
            Debug.Log($"[WestWallUVDiagTest] Renders written to {OutDir}");
        }
    }
}
