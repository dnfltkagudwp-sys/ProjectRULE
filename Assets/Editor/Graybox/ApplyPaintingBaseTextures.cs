using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // All 9 paintings currently share one flat placeholder material (M_Painting) -- the real
    // portrait/landscape textures were only ever applied transiently for the legibility render
    // tests (PortraitLegibilityInLobbyTest / LandscapeLegibilityInLobbyTest), never committed to
    // the live scene. This gives each North/West painting its own real material so AnomalyRuntime
    // Applier has a safe per-painting material to swap textures on later without affecting its
    // neighbors. East wall paintings stay on the placeholder -- no abstract-painting texture has
    // been generated yet (matches design note: no East-wall anomaly exists currently either).
    public static class ApplyPaintingBaseTextures
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "Assets/Art/Paintings";

        // North wall Cubes need a V-flip (confirmed by PortraitLegibilityInLobbyTest); West wall
        // Cubes use identity (confirmed by WestWallUVDiagTest -- different thickness axis).
        private static readonly Vector4 NorthFlipST = new Vector4(1, -1, 0, 1);
        private static readonly Vector4 WestIdentityST = new Vector4(1, 1, 0, 0);

        [MenuItem("RuleGhost/Anomalies/Apply Painting Base Textures")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ApplyOne("Painting_North_1", "Assets/Art/Paintings/Portrait/Portrait_base_v1.png", NorthFlipST, "M_Painting_North_1");
            ApplyOne("Painting_North_2", "Assets/Art/Paintings/Portrait2/Portrait2_base_v1.png", NorthFlipST, "M_Painting_North_2");
            ApplyOne("Painting_North_3", "Assets/Art/Paintings/Portrait3/Portrait3_base_v1.png", NorthFlipST, "M_Painting_North_3");
            ApplyOne("Painting_West_1", "Assets/Art/Paintings/Landscape1/Landscape1_base_v1.png", WestIdentityST, "M_Painting_West_1");
            ApplyOne("Painting_West_2", "Assets/Art/Paintings/Landscape2/Landscape2_base_v1.png", WestIdentityST, "M_Painting_West_2");
            ApplyOne("Painting_West_3", "Assets/Art/Paintings/Landscape3/Landscape3_base_v1.png", WestIdentityST, "M_Painting_West_3");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ApplyPaintingBaseTextures] North/West paintings given their own real-texture materials. Scene saved.");
        }

        private static void ApplyOne(string objectName, string texPath, Vector4 st, string matName)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogError($"[ApplyPaintingBaseTextures] Could not find {objectName}.");
                return;
            }
            var renderer = go.GetComponent<Renderer>();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (renderer == null || tex == null)
            {
                Debug.LogError($"[ApplyPaintingBaseTextures] Missing renderer or texture for {objectName} ({texPath}).");
                return;
            }

            string matPath = $"{OutDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", new Vector2(st.x, st.y));
                mat.SetTextureOffset("_BaseMap", new Vector2(st.z, st.w));
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
                mat.SetTextureScale("_MainTex", new Vector2(st.x, st.y));
                mat.SetTextureOffset("_MainTex", new Vector2(st.z, st.w));
            }
            EditorUtility.SetDirty(mat);

            renderer.sharedMaterial = mat;
        }
    }
}
