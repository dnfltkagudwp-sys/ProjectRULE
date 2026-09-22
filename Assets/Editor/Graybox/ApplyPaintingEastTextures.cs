using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // East wall paintings shared the placeholder M_Painting material until now (see
    // ApplyPaintingBaseTextures) because no abstract-painting texture had been generated.
    // East Cubes share West's rotation and thin-axis (X) scale exactly, so they take the
    // same identity UV mapping as West -- no flip needed (unlike North, whose thin axis is Z).
    public static class ApplyPaintingEastTextures
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "Assets/Art/Paintings";

        private static readonly Vector4 EastIdentityST = new Vector4(1, 1, 0, 0);

        [MenuItem("RuleGhost/Anomalies/Apply Painting East Textures")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ApplyOne("Painting_East_1", "Assets/Art/Paintings/Abstract1/Abstract1_base_v1.png", "M_Painting_East_1");
            ApplyOne("Painting_East_2", "Assets/Art/Paintings/Abstract2/Abstract2_base_v1.png", "M_Painting_East_2");
            ApplyOne("Painting_East_3", "Assets/Art/Paintings/Abstract3/Abstract3_base_v1.png", "M_Painting_East_3");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ApplyPaintingEastTextures] East paintings given their own real-texture materials. Scene saved.");
        }

        private static void ApplyOne(string objectName, string texPath, string matName)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogError($"[ApplyPaintingEastTextures] Could not find {objectName}.");
                return;
            }
            var renderer = go.GetComponent<Renderer>();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (renderer == null || tex == null)
            {
                Debug.LogError($"[ApplyPaintingEastTextures] Missing renderer or texture for {objectName} ({texPath}).");
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
                mat.SetTextureScale("_BaseMap", new Vector2(EastIdentityST.x, EastIdentityST.y));
                mat.SetTextureOffset("_BaseMap", new Vector2(EastIdentityST.z, EastIdentityST.w));
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
                mat.SetTextureScale("_MainTex", new Vector2(EastIdentityST.x, EastIdentityST.y));
                mat.SetTextureOffset("_MainTex", new Vector2(EastIdentityST.z, EastIdentityST.w));
            }
            EditorUtility.SetDirty(mat);

            renderer.sharedMaterial = mat;
        }
    }
}
