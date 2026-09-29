using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Replaces CheckPoint_ThermoHygrometer's flat blue placeholder (M_Thermo) with 3 baked-texture
    // materials generated via the VARGO workflow (digital hygrometer housing, one state each) --
    // see HygrometerDisplay.cs, which swaps between them at runtime instead of the old floating
    // TextMesh + MaterialPropertyBlock tint. Assigns the Normal material as the live scene's
    // default so the device shows a plausible reading even when no Routine/Anomaly state is active
    // (previously it showed nothing at all in that case).
    public static class SetupHygrometerMaterials
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string ArtDir = "Assets/Art/Checkpoints/Hygrometer";

        private static readonly (string textureName, string materialName)[] Variants =
        {
            ("Hygrometer_normal_v1", "M_Hygrometer_Normal"),
            ("Hygrometer_routine_v1", "M_Hygrometer_Routine"),
            ("Hygrometer_anomaly_v1", "M_Hygrometer_Anomaly"),
        };

        [MenuItem("RuleGhost/Graybox/Setup Hygrometer Materials")]
        public static void Run()
        {
            Material normalMat = null;

            foreach (var (textureName, materialName) in Variants)
            {
                var texPath = $"{ArtDir}/{textureName}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    Debug.LogError($"[SetupHygrometerMaterials] Could not find texture at {texPath}.");
                    continue;
                }

                var matPath = $"{ArtDir}/{materialName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                // CheckPoint_ThermoHygrometer's box (a default CreatePrimitive(Cube)) has its -Z
                // face's V axis running opposite to the source image's -- the droplet icon and
                // buttons rendered bottom/top-swapped (droplet at the bottom, buttons at the top)
                // even though left/right order was untouched. Flipping V (not U) on this face's
                // tiling is cheaper than regenerating or re-exporting the source images.
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTextureScale("_BaseMap", new Vector2(-1f, -1f));
                    mat.SetTextureOffset("_BaseMap", new Vector2(1f, 1f));
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTextureScale("_MainTex", new Vector2(-1f, -1f));
                    mat.SetTextureOffset("_MainTex", new Vector2(1f, 1f));
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
                EditorUtility.SetDirty(mat);

                if (materialName == "M_Hygrometer_Normal") normalMat = mat;
            }

            AssetDatabase.SaveAssets();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var go = GameObject.Find("CheckPoint_ThermoHygrometer");
            if (go == null)
            {
                Debug.LogError("[SetupHygrometerMaterials] Could not find CheckPoint_ThermoHygrometer in the scene.");
                return;
            }
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null || normalMat == null)
            {
                Debug.LogError("[SetupHygrometerMaterials] Missing renderer or Normal material.");
                return;
            }
            renderer.sharedMaterial = normalMat;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SetupHygrometerMaterials] 3 materials built, CheckPoint_ThermoHygrometer set to Normal. Scene saved.");
        }
    }
}
