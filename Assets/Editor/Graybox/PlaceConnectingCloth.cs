using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Quick rough test: fills the sightline gaps between the procession
    // figures with simple box "cloth" blockers reaching up to about eye
    // height, so the row reads as one joined mass instead of separate
    // statues with visible gaps between them. Placeholder geometry only --
    // just to check whether the idea feels right before modeling a real
    // draped connector.
    public static class PlaceConnectingCloth
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float ConnectorHeight = 1.8f;
        private const float ConnectorTopY = 2.8f; // pedestalTopY(1.0) + ConnectorHeight

        [MenuItem("RuleGhost/Graybox/Place Connecting Cloth")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var centerStatueGO = GameObject.Find("CenterStatue");
            if (centerStatueGO == null)
            {
                Debug.LogError("[PlaceConnectingCloth] Could not find CenterStatue.");
                return;
            }
            var centerStatue = centerStatueGO.transform;

            // Clean up any previous run.
            for (int i = 0; i < centerStatue.childCount; i++)
            {
                var child = centerStatue.GetChild(i);
                if (child.name.StartsWith("Connector_"))
                {
                    Object.DestroyImmediate(child.gameObject);
                    i--;
                }
            }

            // Gather the placed figures in Z order.
            var figures = new System.Collections.Generic.List<(Transform t, Bounds b)>();
            for (int i = 0; i < centerStatue.childCount; i++)
            {
                var child = centerStatue.GetChild(i);
                if (!child.name.StartsWith("Statue_Figure1_")) continue;
                var renderers = child.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                figures.Add((child, bounds));
            }
            figures.Sort((a, b) => a.b.center.z.CompareTo(b.b.center.z));

            if (figures.Count < 2)
            {
                Debug.LogError("[PlaceConnectingCloth] Need at least 2 placed figures.");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Statue/M_Figure1_Standing.mat");

            for (int i = 0; i < figures.Count - 1; i++)
            {
                var (tA, bA) = figures[i];
                var (tB, bB) = figures[i + 1];

                float zA = bA.center.z;
                float zB = bB.center.z;
                float centerZ = (zA + zB) / 2f;
                float spacing = zB - zA;
                float depth = spacing + 0.15f; // slight overlap into each neighbor

                float width = Mathf.Max(bA.size.x, bB.size.x) * 1.15f;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Connector_{i}";
                go.transform.SetParent(centerStatue, false);
                go.transform.position = new Vector3(0f, (1.0f + ConnectorTopY) / 2f, centerZ);
                go.transform.localScale = new Vector3(width, ConnectorHeight, depth);

                var renderer = go.GetComponent<Renderer>();
                if (mat != null) renderer.sharedMaterial = mat;

                Debug.Log($"[PlaceConnectingCloth] Connector {i}: z={centerZ} width={width} depth={depth}");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[PlaceConnectingCloth] Scene saved.");
        }
    }
}
