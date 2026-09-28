using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuleGhost.EditorTools
{
    // M_EntranceGlass and M_GuardRoomWindowGlass were both created with GetOrCreateMaterial's
    // plain opaque setup (AddEntranceDoor.cs / DecorateGuardRoom.cs) -- a pale, smooth, but fully
    // OPAQUE URP/Lit material. That's the actual reason neither ever read as glass: it isn't
    // transparency-plus-a-bad-texture, there was no transparency at all. Flips both to the URP/Lit
    // Transparent surface type with a reflective tint instead -- no new texture asset needed, and
    // the room now has a baked reflection probe (see ApplyReflectionProbe.cs) for it to reflect.
    public static class MakeGlassTransparent
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Graybox/Make Glass Transparent")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int updated = 0;
            updated += ConvertAt("Assets/Art/Architecture/M_EntranceGlass.mat") ? 1 : 0;
            updated += ConvertAt("Assets/Art/Architecture/M_GuardRoomWindowGlass.mat") ? 1 : 0;

            AssetDatabase.SaveAssets();
            Debug.Log($"[MakeGlassTransparent] Converted {updated}/2 glass materials to transparent.");
        }

        private static bool ConvertAt(string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogWarning($"[MakeGlassTransparent] Could not find material at {path}.");
                return false;
            }

            // URP/Lit's own Transparent surface type -- matches what the shader's inspector does
            // when you switch Surface Type to Transparent by hand (Blend Mode: Alpha).
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;

            // Mostly see-through with a faint tint, high smoothness + a touch of metallic so the
            // reflection probe actually shows up on it instead of just looking like tinted plastic.
            const float alpha = 0.28f;
            var baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            baseColor.a = alpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);

            EditorUtility.SetDirty(mat);
            return true;
        }
    }
}
