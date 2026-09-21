using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Applies a real Material (built from the actual baked VARGO texture,
    // extracted via Blender since the FBX round-trip left Unity with a flat
    // white default material and no surface detail at all) to the placed
    // statue figure in the real Lobby_Graybox scene.
    public static class ApplyStatueMaterial
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string BaseColorPath = "Assets/Art/Statue/Figure1_Standing_BaseColor.png";
        private const string NormalPath = "Assets/Art/Statue/Figure1_Standing_Normal.png";
        private const string MaterialOutPath = "Assets/Art/Statue/M_Figure1_Standing.mat";

        [MenuItem("RuleGhost/Graybox/Apply Statue Material")]
        public static void Run()
        {
            var normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (baseColor == null)
            {
                Debug.LogError($"[ApplyStatueMaterial] Could not load {BaseColorPath}");
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialOutPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialOutPath);
            }
            else
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseColor);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseColor);
            if (normal != null)
            {
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var figure = GameObject.Find("CenterStatue/Statue_Figure1");
            if (figure == null)
            {
                Debug.LogError("[ApplyStatueMaterial] Could not find Statue_Figure1 in the scene.");
                return;
            }

            var renderers = figure.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
            }
            Debug.Log($"[ApplyStatueMaterial] Applied material to {renderers.Length} renderer(s).");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ApplyStatueMaterial] Scene saved.");
        }
    }
}
