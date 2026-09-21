using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off diagnostic: renders the shared frame with the portrait texture (closed vs open
    // eyes) at a couple of patrol-realistic viewing distances, so the eye-open anomaly's
    // readability can be checked without a full Play-mode walkthrough.
    public static class PortraitLegibilityTest
    {
        private const string FramePath = "Assets/Art/Frame/Frame_Blender_v1.fbx";
        private const string BaseTexPath = "Assets/Art/Paintings/Portrait/Portrait_base_v1.png";
        private const string OpenTexPath = "Assets/Art/Paintings/Portrait/Portrait_eyesopen_v1.png";
        private const string OutDir = "RawAssets/Paintings/Portrait/LegibilityTest";

        [MenuItem("RuleGhost/Tests/Portrait Legibility Test")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGO = new GameObject("Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGO.transform.rotation = Quaternion.Euler(40f, -20f, 0f);

            var framePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FramePath);
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexPath);
            var openTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OpenTexPath);

            if (framePrefab == null || baseTex == null || openTex == null)
            {
                Debug.LogError("[PortraitLegibilityTest] Missing frame prefab or textures.");
                return;
            }

            var diag = Object.Instantiate(framePrefab);
            var rend = diag.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Debug.Log($"[PortraitLegibilityTest] DIAG frame renderer bounds center={rend.bounds.center} size={rend.bounds.size} " +
                          $"rendererGO={rend.gameObject.name} localPos={rend.transform.position} localRot={rend.transform.rotation.eulerAngles} " +
                          $"materials=[{string.Join(", ", System.Array.ConvertAll(rend.sharedMaterials, m => m ? m.name : "null"))}]");
            }
            else
            {
                Debug.LogError("[PortraitLegibilityTest] DIAG: no renderer found on instantiated frame at all.");
            }
            Object.DestroyImmediate(diag);

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            // Try both +Z and -Z camera sides since the FBX export axis conversion direction
            // isn't verified yet -- whichever one actually shows the canvas face wins.
            foreach (var side in new[] { 1f, -1f })
            {
                Capture(framePrefab, baseTex, 3f, side, $"side{side}_3m_closed");
                Capture(framePrefab, openTex, 3f, side, $"side{side}_3m_open");
                Capture(framePrefab, baseTex, 6f, side, $"side{side}_6m_closed");
                Capture(framePrefab, openTex, 6f, side, $"side{side}_6m_open");
            }

            Debug.Log($"[PortraitLegibilityTest] Renders written to {OutDir}");
        }

        private static void Capture(GameObject framePrefab, Texture2D tex, float distance, float side, string outName)
        {
            var frameInstance = Object.Instantiate(framePrefab, Vector3.zero, Quaternion.identity);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var renderers = frameInstance.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name.Contains("Canvas"))
                    {
                        var mat = new Material(shader);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                        mats[i] = mat;
                    }
                }
                r.sharedMaterials = mats;
            }

            var camGO = new GameObject("Cam");
            var cam = camGO.AddComponent<Camera>();
            var frameRend = frameInstance.GetComponentInChildren<Renderer>();
            Vector3 target = frameRend != null ? frameRend.bounds.center : frameInstance.transform.position;
            camGO.transform.position = target + new Vector3(0, 0, distance * side);
            camGO.transform.LookAt(target);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f);

            const int width = 640, height = 900;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var outputTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            outputTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outputTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();

            byte[] bytes = outputTex.EncodeToPNG();
            string fullDir = Path.Combine(Application.dataPath, "..", OutDir);
            File.WriteAllBytes(Path.Combine(fullDir, outName + ".png"), bytes);

            Object.DestroyImmediate(outputTex);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(frameInstance);
        }
    }
}
