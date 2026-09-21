using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Adds a simple 4-bar frame around each of the 9 paintings so they read
    // as framed artwork instead of flat panels stuck to the wall. Reads each
    // painting's own transform rather than hardcoding wall-specific
    // positions, and detects orientation generically (the thickness axis is
    // whichever of X/Z has the smaller scale) so it works for the north wall
    // (thickness on Z) and the west/east walls (thickness on X) alike.
    public static class AddPaintingFrames
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float FrameBorder = 0.08f;
        private const float FrameDepthAdd = 0.03f;

        private static readonly string[] PaintingNames =
        {
            "Painting_North_1", "Painting_North_2", "Painting_North_3",
            "Painting_West_1", "Painting_West_2", "Painting_West_3",
            "Painting_East_1", "Painting_East_2", "Painting_East_3",
        };

        [MenuItem("RuleGhost/Graybox/Add Painting Frames")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find("PaintingFrames");
            if (existing != null) Object.DestroyImmediate(existing);
            var root = new GameObject("PaintingFrames");

            var frameMat = GetOrCreateMaterial("M_Frame", new Color(0.12f, 0.08f, 0.05f), smoothness: 0.35f);

            int found = 0;
            foreach (var name in PaintingNames)
            {
                var painting = GameObject.Find(name);
                if (painting == null)
                {
                    Debug.LogWarning($"[AddPaintingFrames] Could not find {name}, skipping.");
                    continue;
                }
                BuildFrame(root.transform, painting.transform, frameMat, name);
                found++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[AddPaintingFrames] Framed {found}/{PaintingNames.Length} paintings. Scene saved.");
        }

        private static void BuildFrame(Transform root, Transform painting, Material mat, string label)
        {
            Vector3 pos = painting.position;
            Vector3 scale = painting.localScale;
            float height = scale.y;
            bool thicknessOnX = scale.x < scale.z;
            float width = thicknessOnX ? scale.z : scale.x;
            float thickness = thicknessOnX ? scale.x : scale.z;
            float frameDepth = thickness + FrameDepthAdd;

            var group = new GameObject($"Frame_{label}");
            group.transform.SetParent(root, false);

            if (thicknessOnX)
            {
                // West/East walls: width runs along Z, thickness along X.
                Block(group.transform, mat, pos + new Vector3(0f, height / 2f + FrameBorder / 2f, 0f),
                    new Vector3(frameDepth, FrameBorder, width + FrameBorder * 2f));
                Block(group.transform, mat, pos - new Vector3(0f, height / 2f + FrameBorder / 2f, 0f),
                    new Vector3(frameDepth, FrameBorder, width + FrameBorder * 2f));
                Block(group.transform, mat, pos + new Vector3(0f, 0f, width / 2f + FrameBorder / 2f),
                    new Vector3(frameDepth, height, FrameBorder));
                Block(group.transform, mat, pos - new Vector3(0f, 0f, width / 2f + FrameBorder / 2f),
                    new Vector3(frameDepth, height, FrameBorder));
            }
            else
            {
                // North wall: width runs along X, thickness along Z.
                Block(group.transform, mat, pos + new Vector3(0f, height / 2f + FrameBorder / 2f, 0f),
                    new Vector3(width + FrameBorder * 2f, FrameBorder, frameDepth));
                Block(group.transform, mat, pos - new Vector3(0f, height / 2f + FrameBorder / 2f, 0f),
                    new Vector3(width + FrameBorder * 2f, FrameBorder, frameDepth));
                Block(group.transform, mat, pos + new Vector3(width / 2f + FrameBorder / 2f, 0f, 0f),
                    new Vector3(FrameBorder, height, frameDepth));
                Block(group.transform, mat, pos - new Vector3(width / 2f + FrameBorder / 2f, 0f, 0f),
                    new Vector3(FrameBorder, height, frameDepth));
            }
        }

        private static void Block(Transform parent, Material mat, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "FrameBar";
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
