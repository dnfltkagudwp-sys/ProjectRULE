using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Applies the VARGO-generated floor/wall/pedestal/door textures to the
    // real Lobby_Graybox scene, replacing the flat placeholder colors.
    public static class ApplyArchitectureMaterials
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string ArtDir = "Assets/Art/Architecture";

        private static readonly string[] WallObjects =
        {
            "Wall_West_South", "Wall_West_North", "Wall_East",
            "Wall_North_Inner", "Wall_South_Left", "Wall_South_Right",
        };

        [MenuItem("RuleGhost/Graybox/Apply Architecture Materials")]
        public static void Run()
        {
            // Smoothness was 0.75 -- with no reflection probe in the scene,
            // that glossy a floor mirrored RenderSettings.ambientLight
            // uniformly across its whole surface regardless of distance from
            // any actual light, and grazing-angle Fresnel made it worse far
            // from the camera -- the floor looked brightest exactly where it
            // should be darkest. Lowered alongside adding a reflection probe
            // (see ApplyReflectionProbe.cs) so the floor still has a plausible
            // sheen without projecting a constant fake sky-color glow.
            var floorMat = BuildMaterial("M_Floor", ArtDir + "/Floor_base_v1.png", tiling: 6f, smoothness: 0.35f);
            // Wall_base_v1.png is a light, warm-neutral plaster -- it only
            // looked dark in-game because of the dim scene lighting, not the
            // texture itself. Wall_tinted_v1.png is a cool blue-gray
            // multiplicative recolor of it (see AdjustWallTone.cs) so the
            // wall reads as a gallery wall wherever it's actually lit,
            // without raising overall brightness or fighting the dark mood.
            var wallMat = BuildMaterial("M_Wall", ArtDir + "/Wall_tinted_v1.png", tiling: 3f, smoothness: 0.15f);
            var pedestalMat = BuildMaterial("M_Pedestal", ArtDir + "/Pedestal_base_v1.png", tiling: 2f, smoothness: 0.3f);
            var doorMat = BuildMaterial("M_Door", ArtDir + "/Door_base_v1.png", tiling: 1f, smoothness: 0.25f);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ApplyTo("Floor", floorMat);
            foreach (var wallName in WallObjects)
            {
                ApplyTo(wallName, wallMat);
            }
            ApplyTo("CenterStatue/Pedestal", pedestalMat);
            ApplyTo("CheckPoint_InspectionDoor", doorMat);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ApplyArchitectureMaterials] Applied and saved.");
        }

        private static Material BuildMaterial(string name, string texPath, float tiling, float smoothness)
        {
            string matPath = $"{ArtDir}/{name}.mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogError($"[ApplyArchitectureMaterials] Missing texture: {texPath}");
                return null;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            mat.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ApplyTo(string objectName, Material mat)
        {
            if (mat == null) return;
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogError($"[ApplyArchitectureMaterials] Could not find object: {objectName}");
                return;
            }
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError($"[ApplyArchitectureMaterials] No renderer on: {objectName}");
                return;
            }
            renderer.sharedMaterial = mat;
        }
    }
}
