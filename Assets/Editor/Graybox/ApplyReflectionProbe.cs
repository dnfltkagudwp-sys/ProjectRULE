using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Fixes the floor "glowing" bug: with Smoothness 0.75 and no reflection
    // probe in the scene, the floor mirrored RenderSettings.ambientLight
    // uniformly across its entire surface -- constant regardless of distance
    // from any real light, and stronger at grazing angles (Fresnel), so the
    // floor farthest from every spotlight looked the brightest. Lowers the
    // floor's smoothness and bakes a real reflection probe so the "sky" it
    // reflects is the room's own dark walls/ceiling instead of a flat color.
    public static class ApplyReflectionProbe
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string FloorMaterialPath = "Assets/Art/Architecture/M_Floor.mat";

        [MenuItem("RuleGhost/Graybox/Apply Reflection Probe Fix")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (floorMat != null)
            {
                if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.35f);
                if (floorMat.HasProperty("_Glossiness")) floorMat.SetFloat("_Glossiness", 0.35f);
                EditorUtility.SetDirty(floorMat);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogError("[ApplyReflectionProbe] Could not find M_Floor material.");
            }

            var existing = GameObject.Find("LobbyReflectionProbe");
            if (existing != null) Object.DestroyImmediate(existing);

            var probeGO = new GameObject("LobbyReflectionProbe");
            var probe = probeGO.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            probe.size = new Vector3(16f, 5.2f, 22f);
            probeGO.transform.position = new Vector3(0f, 1.5f, 0f);
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.intensity = 1f;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            bool baked = Lightmapping.BakeReflectionProbe(probe, "Assets/Art/Architecture/LobbyReflectionProbe.exr");
            Debug.Log($"[ApplyReflectionProbe] Floor smoothness lowered, reflection probe added. Bake result: {baked}. Scene saved.");
        }
    }
}
