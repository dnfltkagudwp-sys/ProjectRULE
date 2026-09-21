using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // The generated grout-line overlay (light-colored seams for contrast
    // against the dark mood lighting) ended up reading as if light were
    // leaking up through the floor -- reverts M_Floor back to the plain,
    // ungridded marble texture. Kept as a separate small script rather than
    // folding into GenerateFloorTilePattern so either direction is a single
    // menu click without re-running pixel generation.
    public static class RevertFloorToPlain
    {
        private const string PlainTexPath = "Assets/Art/Architecture/Floor_base_v1.png";
        private const string MaterialPath = "Assets/Art/Architecture/M_Floor.mat";

        [MenuItem("RuleGhost/Graybox/Revert Floor To Plain")]
        public static void Run()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PlainTexPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (tex == null || mat == null)
            {
                Debug.LogError("[RevertFloorToPlain] Missing texture or material.");
                return;
            }

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            Debug.Log("[RevertFloorToPlain] M_Floor reverted to plain marble (no grout overlay).");
        }
    }
}
