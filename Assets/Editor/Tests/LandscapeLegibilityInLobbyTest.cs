using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Same approach as PortraitLegibilityInLobbyTest: texture the real West-wall painting
    // placeholders in the actual Lobby_Graybox scene and render each landscape's base/person
    // pair from a few patrol-realistic viewing distances, so the "person appears" anomaly's
    // readability can be checked without a full Play-mode walkthrough.
    public static class LandscapeLegibilityInLobbyTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Paintings/Landscape/LegibilityTest";

        private struct Pair
        {
            public string PaintingName;
            public string BaseTexPath;
            public string PersonTexPath;
            public string Label;
        }

        [MenuItem("RuleGhost/Tests/Landscape Legibility In Lobby")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var pairs = new[]
            {
                new Pair
                {
                    PaintingName = "Painting_West_1",
                    BaseTexPath = "Assets/Art/Paintings/Landscape1/Landscape1_base_v1.png",
                    PersonTexPath = "Assets/Art/Paintings/Landscape1/Landscape1_person_v2.png",
                    Label = "landscape1_lake",
                },
                new Pair
                {
                    PaintingName = "Painting_West_2",
                    BaseTexPath = "Assets/Art/Paintings/Landscape2/Landscape2_base_v1.png",
                    PersonTexPath = "Assets/Art/Paintings/Landscape2/Landscape2_person_v2.png",
                    Label = "landscape2_forest",
                },
                new Pair
                {
                    PaintingName = "Painting_West_3",
                    BaseTexPath = "Assets/Art/Paintings/Landscape3/Landscape3_base_v1.png",
                    PersonTexPath = "Assets/Art/Paintings/Landscape3/Landscape3_person_v1.png",
                    Label = "landscape3_street",
                },
            };

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            float[] distances = { 3f, 6f, 9f };

            foreach (var pair in pairs)
            {
                var painting = GameObject.Find("Lobby_Graybox/Paintings/" + pair.PaintingName);
                if (painting == null)
                {
                    painting = GameObject.Find(pair.PaintingName);
                }
                if (painting == null)
                {
                    Debug.LogError($"[LandscapeLegibilityInLobbyTest] Could not find {pair.PaintingName} in the scene.");
                    continue;
                }

                var renderer = painting.GetComponent<Renderer>();
                var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.BaseTexPath);
                var personTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.PersonTexPath);
                if (renderer == null || baseTex == null || personTex == null)
                {
                    Debug.LogError($"[LandscapeLegibilityInLobbyTest] Missing renderer or textures for {pair.PaintingName} " +
                                    $"(renderer={renderer != null}, base={baseTex != null}, person={personTex != null}).");
                    continue;
                }

                // West wall paintings face +X into the room; the camera stands further along
                // +X from the wall looking back at it (mirrors the north-wall test's -Z offset).
                Vector3 paintingPos = painting.transform.position;
                foreach (var distance in distances)
                {
                    CaptureWithTexture(renderer, baseTex, paintingPos, distance, $"{pair.Label}_{distance:0}m_base");
                    CaptureWithTexture(renderer, personTex, paintingPos, distance, $"{pair.Label}_{distance:0}m_person");
                }

                renderer.SetPropertyBlock(null);
            }

            Debug.Log($"[LandscapeLegibilityInLobbyTest] Renders written to {OutDir}");
        }

        private static void CaptureWithTexture(Renderer renderer, Texture2D tex, Vector3 paintingPos, float distance, string outName)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", tex);
            block.SetTexture("_MainTex", tex);
            // Unlike the North-wall Cubes (thin along Z), the West-wall Cubes are thin along X,
            // so their front face is a different mesh face with its own default UV orientation
            // that needs no ST correction (confirmed via WestWallUVDiagTest against an
            // asymmetric source image -- identity was the only untransformed, unmirrored match).
            var identity = new Vector4(1, 1, 0, 0);
            block.SetVector("_BaseMap_ST", identity);
            block.SetVector("_MainTex_ST", identity);
            renderer.SetPropertyBlock(block);

            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            Vector3 eyePos = new Vector3(paintingPos.x + distance, 1.6f, paintingPos.z);
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
