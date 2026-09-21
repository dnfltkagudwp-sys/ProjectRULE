using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Renders the placed statue row from a couple of realistic patrol
    // viewpoints in the real Lobby_Graybox scene, to visually confirm the
    // forced-perspective procession (biggest at the front/entrance side,
    // shrinking toward the back) actually reads correctly in situ.
    public static class StatuePlacementRenderTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Statue/PlacementCheck";

        [MenuItem("RuleGhost/Tests/Statue Placement Render")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            var centerStatue = GameObject.Find("CenterStatue");
            if (centerStatue == null)
            {
                Debug.LogError("[StatuePlacementRenderTest] Could not find CenterStatue.");
                return;
            }

            var renderers = centerStatue.GetComponentsInChildren<Renderer>();
            bool any = false;
            Bounds bounds = default;
            foreach (var r in renderers)
            {
                if (!r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any)
            {
                Debug.LogError("[StatuePlacementRenderTest] No enabled renderers found under CenterStatue.");
                return;
            }
            Vector3 target = bounds.center;

            Capture(target, new Vector3(target.x, 1.6f, -9f), "pilgrims_from_entrance_9m", 60f);
            Capture(target, new Vector3(target.x, 1.6f, -6.5f), "pilgrims_from_south_edge", 70f);
            Capture(target, new Vector3(target.x - 5f, 1.6f, target.z), "pilgrims_from_west_side", 70f);
            Capture(target, new Vector3(target.x, 1.6f, 8f), "pilgrims_from_north_wall", 60f);

            Debug.Log($"[StatuePlacementRenderTest] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 target, Vector3 eyePos, string outName, float fov)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;

            const int width = 900, height = 800;
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
