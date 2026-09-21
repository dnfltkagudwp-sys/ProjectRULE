using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Shifts the existing VARGO wall texture toward a cool blue-gray gallery
    // tone via local pixel editing, instead of regenerating with VARGO.
    // Turns out the source texture is already a light warm-neutral plaster
    // (not dark at all) -- the wall only LOOKS dark in-game because of the
    // dim scene lighting, not the texture's own value. An additive lift
    // blew it out to solid white for exactly that reason. What actually
    // needs to change is hue (warm-neutral -> cool blue-gray), which a
    // multiplicative tint does without raising brightness into clipping --
    // the room stays just as dark wherever it's unlit.
    public static class AdjustWallTone
    {
        private const string SourcePath = "Assets/Art/Architecture/Wall_base_v1.png";
        private const string OutputPath = "Assets/Art/Architecture/Wall_tinted_v1.png";
        private const string MaterialPath = "Assets/Art/Architecture/M_Wall.mat";

        // Multiplicative tint: pulls red/green down a bit more than blue to
        // cool the hue, and dims slightly overall so it doesn't compete with
        // the paintings under their spotlights.
        private static readonly Color Tint = new Color(0.62f, 0.66f, 0.76f);

        [MenuItem("RuleGhost/Graybox/Adjust Wall Tone")]
        public static void Run()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SourcePath);
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            var pixels = src.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                pixels[i] = new Color(
                    Mathf.Clamp01(c.r * Tint.r),
                    Mathf.Clamp01(c.g * Tint.g),
                    Mathf.Clamp01(c.b * Tint.b));
            }

            var outTex = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
            outTex.SetPixels(pixels);
            outTex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", OutputPath), outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            AssetDatabase.ImportAsset(OutputPath);
            var tintedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null && tintedTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tintedTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tintedTex);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[AdjustWallTone] Wrote {OutputPath} and assigned it to M_Wall.");
        }
    }
}
