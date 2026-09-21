using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Adds a cap band just below the pedestal's top edge and a base band at
    // its foot, both protruding slightly outward -- turns the plain
    // rectangular pedestal into something closer to a real plinth (base -
    // body - cap) without changing its footprint, height, or top surface Y,
    // so the already-placed pilgrims (whose feet are set exactly at the
    // pedestal's top Y) aren't affected. Built as a 4-bar ring per band
    // (same technique as AddPaintingFrames.cs) rather than a filled box, so
    // the bands don't create a coplanar overlap with the pedestal's own top
    // face where it would z-fight.
    public static class AddPedestalDetail
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float Overhang = 0.06f;
        private const float BandThickness = 0.07f;

        [MenuItem("RuleGhost/Graybox/Add Pedestal Detail")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var pedestal = GameObject.Find("CenterStatue/Pedestal");
            if (pedestal == null)
            {
                Debug.LogError("[AddPedestalDetail] Could not find CenterStatue/Pedestal.");
                return;
            }

            var existing = GameObject.Find("PedestalDetail");
            if (existing != null) Object.DestroyImmediate(existing);
            var root = new GameObject("PedestalDetail");
            root.transform.SetParent(pedestal.transform.parent, false);

            var mat = GetOrCreateMaterial("M_PedestalDetail", new Color(0.06f, 0.06f, 0.065f), smoothness: 0.4f);

            Vector3 pos = pedestal.transform.position;
            Vector3 scale = pedestal.transform.localScale;
            float halfW = scale.x / 2f;
            float halfL = scale.z / 2f;
            float topY = pos.y + scale.y / 2f;
            float bottomY = pos.y - scale.y / 2f;

            // Cap band: sits just below the top edge, flush with the top
            // (not above it), so pilgrim feet at topY are unaffected.
            BuildRing(root.transform, mat, "Cap", pos, halfW, halfL, topY - BandThickness / 2f, BandThickness, Overhang);

            // Base band: sits at the floor, matching the pedestal's own
            // footprint, giving the plinth a visible "foot".
            BuildRing(root.transform, mat, "Base", pos, halfW, halfL, bottomY + BandThickness / 2f, BandThickness, Overhang);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AddPedestalDetail] Cap and base bands added. Scene saved.");
        }

        private static void BuildRing(Transform parent, Material mat, string label, Vector3 center, float halfW, float halfL, float y, float bandH, float overhang)
        {
            float outerHalfW = halfW + overhang;
            float outerHalfL = halfL + overhang;

            Block(parent, $"Pedestal{label}_North", mat, new Vector3(center.x, y, center.z + outerHalfL - overhang / 2f), new Vector3(outerHalfW * 2f, bandH, overhang));
            Block(parent, $"Pedestal{label}_South", mat, new Vector3(center.x, y, center.z - outerHalfL + overhang / 2f), new Vector3(outerHalfW * 2f, bandH, overhang));
            Block(parent, $"Pedestal{label}_West", mat, new Vector3(center.x - outerHalfW + overhang / 2f, y, center.z), new Vector3(overhang, bandH, halfL * 2f));
            Block(parent, $"Pedestal{label}_East", mat, new Vector3(center.x + outerHalfW - overhang / 2f, y, center.z), new Vector3(overhang, bandH, halfL * 2f));
        }

        private static void Block(Transform parent, string name, Material mat, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"Assets/Art/Architecture/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
