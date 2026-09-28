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

            // ApplyDramaticLighting darkens RenderSettings.ambientLight/ambientMode, but never
            // touches RenderSettings.skybox itself -- any ray escaping the room through the
            // entrance gap (or now, transparent glass -- see MakeGlassTransparent.cs) still hit
            // Unity's default procedural sky and baked it straight into this probe, showing up as
            // a bright sun/sky reflection on the floor and glass despite the room reading as dark
            // everywhere else. There's no exterior built for this level -- the museum is a closed
            // box at night -- so there's nothing a skybox should ever contribute here.
            RenderSettings.skybox = null;

            var existing = GameObject.Find("LobbyReflectionProbe");
            if (existing != null) Object.DestroyImmediate(existing);

            var probeGO = new GameObject("LobbyReflectionProbe");
            var probe = probeGO.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            // Room + 2m padding each axis (was 14x20 -> 16x22; now 18x26 after the 2026-09-28
            // resize, see ResizeLobby.cs), height unchanged (room height 5m + 0.2m padding).
            probe.size = new Vector3(20f, 5.2f, 28f);
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
