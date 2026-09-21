using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Draws grout lines directly onto a copy of the existing marble floor
    // texture so the floor reads as laid tile instead of one continuous
    // photo -- pure local pixel editing, no VARGO generation needed (no
    // token/time cost), since the source photo is already approved.
    public static class GenerateFloorTilePattern
    {
        private const string SourcePath = "Assets/Art/Architecture/Floor_base_v1.png";
        private const string OutputPath = "Assets/Art/Architecture/Floor_tiled_v1.png";
        private const string MaterialPath = "Assets/Art/Architecture/M_Floor.mat";

        // Texture is tiled 6x across the floor in M_Floor, so N grid divisions
        // per texture gives N*6 tiles across the room -- 3 gives 18 tiles
        // along the room's length, a plausible large-format museum tile size.
        private const int Divisions = 3;
        private const int LineWidthPx = 6;
        // A darkened line barely showed against the already-dark stone under
        // dim mood lighting -- a lighter seam reads by contrast regardless of
        // overall scene brightness, so lighten instead of darken.
        private static readonly Color LineColor = new Color(0.5f, 0.52f, 0.55f);
        private const float LineBlend = 0.6f;

        [MenuItem("RuleGhost/Graybox/Generate Floor Tile Pattern")]
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
            int w = src.width, h = src.height;

            for (int div = 1; div < Divisions; div++)
            {
                DrawVerticalLine(pixels, w, h, w * div / Divisions);
                DrawHorizontalLine(pixels, w, h, h * div / Divisions);
            }
            // Outer border seam too.
            DrawVerticalLine(pixels, w, h, 0);
            DrawHorizontalLine(pixels, w, h, 0);

            var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
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
            var tiledTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null && tiledTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tiledTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tiledTex);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[GenerateFloorTilePattern] Wrote {OutputPath} and assigned it to M_Floor.");
        }

        private static void DrawVerticalLine(Color[] pixels, int w, int h, int centerX)
        {
            for (int dx = -LineWidthPx / 2; dx <= LineWidthPx / 2; dx++)
            {
                int x = ((centerX + dx) % w + w) % w;
                for (int y = 0; y < h; y++)
                {
                    Darken(pixels, x, y, w);
                }
            }
        }

        private static void DrawHorizontalLine(Color[] pixels, int w, int h, int centerY)
        {
            for (int dy = -LineWidthPx / 2; dy <= LineWidthPx / 2; dy++)
            {
                int y = ((centerY + dy) % h + h) % h;
                for (int x = 0; x < w; x++)
                {
                    Darken(pixels, x, y, w);
                }
            }
        }

        private static void Darken(Color[] pixels, int x, int y, int w)
        {
            int i = y * w + x;
            pixels[i] = Color.Lerp(pixels[i], LineColor, LineBlend);
        }
    }
}
