using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Brings the guard room into the same visual language as the museum: its walls/ceiling/floor
    // currently all share one flat yellow-tan placeholder (M_GuardRoom, 0.65/0.6/0.4), which reads
    // as an unrelated room regardless of lighting. Reuses the museum's own floor/ceiling materials
    // directly (guaranteed match, zero new assets), and derives a brighter variant of the museum
    // wall tone for the guard room's walls (same cool-toned family, just less dimmed) instead of
    // inventing an unrelated color. Geometry is untouched -- this only reassigns materials.
    public static class UnifyGuardRoomAtmosphere
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string ArtDir = "Assets/Art/Architecture";

        private const string WallSourcePath = ArtDir + "/Wall_base_v1.png";
        private const string GuardWallOutputPath = ArtDir + "/Wall_tinted_guardroom_v1.png";
        private const string GuardWallMaterialPath = ArtDir + "/M_GuardRoomWall.mat";
        private const string FloorMaterialPath = ArtDir + "/M_Floor.mat";
        private const string CeilingMaterialPath = "Assets/_Graybox/Materials/M_Ceiling.mat";

        // Same cool-leaning ratio as AdjustWallTone's M_Wall tint (0.62, 0.66, 0.76) but much
        // milder, so the result is "one step brighter, same family" rather than a different hue.
        private static readonly Color GuardWallTint = new Color(0.80f, 0.83f, 0.90f);

        // The window's LeftMargin/RightMargin are deliberately excluded -- DecorateGuardRoom now
        // gives them the dark trim material (matching the door frame's casing) instead of the wall
        // color, so they shouldn't be retinted back to wall tone here.
        private static readonly string[] WallObjects =
        {
            "GuardRoom_Wall_West", "GuardRoom_Wall_South", "GuardRoom_Wall_North",
            "GuardRoom_DoorFillWall_Bottom", "GuardRoom_DoorFillWall_Top", "GuardRoom_DoorHeader",
        };

        [MenuItem("RuleGhost/Graybox/Unify Guard Room Atmosphere")]
        public static void Run()
        {
            var guardWallMat = BuildTintedWallMaterial();
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            var ceilingMat = AssetDatabase.LoadAssetAtPath<Material>(CeilingMaterialPath);
            if (floorMat == null || ceilingMat == null)
            {
                Debug.LogError("[UnifyGuardRoomAtmosphere] Could not find the museum's M_Floor/M_Ceiling materials.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var path in WallObjects)
            {
                ApplyTo(path, guardWallMat);
            }
            ApplyTo("GuardRoom_Floor", floorMat);
            ApplyTo("GuardRoom_Ceiling", ceilingMat);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[UnifyGuardRoomAtmosphere] Guard room walls/fill panel retinted, floor/ceiling reuse the museum's own materials. Scene saved.");
        }

        private static Material BuildTintedWallMaterial()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(WallSourcePath);
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(WallSourcePath);
            var pixels = src.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                pixels[i] = new Color(
                    Mathf.Clamp01(c.r * GuardWallTint.r),
                    Mathf.Clamp01(c.g * GuardWallTint.g),
                    Mathf.Clamp01(c.b * GuardWallTint.b));
            }

            var outTex = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
            outTex.SetPixels(pixels);
            outTex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", GuardWallOutputPath), outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            AssetDatabase.ImportAsset(GuardWallOutputPath);
            var tintedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GuardWallOutputPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GuardWallMaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, GuardWallMaterialPath);
            }
            // Smaller walls than the main gallery -- a gentler tiling than M_Wall's 3x so the
            // plaster grain doesn't repeat as densely across a 3m panel.
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tintedTex);
                mat.SetTextureScale("_BaseMap", new Vector2(1.5f, 1.5f));
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tintedTex);
                mat.SetTextureScale("_MainTex", new Vector2(1.5f, 1.5f));
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void ApplyTo(string objectPath, Material mat)
        {
            var go = GameObject.Find(objectPath);
            if (go == null)
            {
                Debug.LogError($"[UnifyGuardRoomAtmosphere] Could not find object: {objectPath}");
                return;
            }
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError($"[UnifyGuardRoomAtmosphere] No renderer on: {objectPath}");
                return;
            }
            renderer.sharedMaterial = mat;
        }
    }
}
