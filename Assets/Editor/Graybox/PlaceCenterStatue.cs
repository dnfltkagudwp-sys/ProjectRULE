using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Places a dense, packed crowd of the final VARGO/Blender statue figure
    // (Figure1_Standing.fbx) on the pedestal in the real Lobby_Graybox scene,
    // replacing the graybox placeholder cube. Reframed from a formal
    // "행렬" (procession, a neat line) to a "순례자 무리" (a gathered
    // crowd of pilgrims) -- scattered positions, varied sizes, and now a
    // much higher density read as natural for a crowd. Front-on sightline
    // blocking is no longer a hard requirement (the player has to walk past
    // this to reach the thermometer/inspection door regardless).
    // Compensates for the known Blender->FBX->Unity axis bug (height on
    // local Z instead of Y) with a -90 X root rotation, and a further
    // +180 Y so the mesh's well-textured front (its back is poorly known
    // from a single reference photo) faces south toward the entrance.
    public static class PlaceCenterStatue
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string FigurePath = "Assets/Art/Statue/Figure1_Standing.fbx";

        // Pulls the whole pedestal + pilgrim cluster south, toward the
        // entrance, per feedback. Moves the CenterStatue group itself, so
        // the pedestal, placeholder cube, and every pilgrim (all
        // local-positioned under it) shift together as one rigid unit.
        private const float PedestalSouthShift = 2.0f;

        // Extends the pedestal further north only (south edge stays put) so
        // there's more length for pilgrims -- pulling the whole cluster
        // south left the pedestal looking short relative to the room.
        private const float PedestalNorthExtension = 4.0f;

        // Dense jittered-grid crowd generation, fixed seed for reproducible
        // layouts between runs (tweak the seed or ranges and re-run to try
        // a different arrangement). The grid is centered on the pedestal's
        // own (possibly extended) local center, not on local Z=0.
        private const int RandomSeed = 20260921;
        private const int GridCols = 3;      // across the 3m-wide pedestal
        private const int GridRows = 15;      // along the extended usable length
        private const float ColSpacing = 0.9f;
        private const float RowSpacing = 0.95f;
        private const float XJitter = 0.22f;
        private const float ZJitter = 0.3f;
        private const float MinHeight = 1.2f;
        private const float MaxHeight = 3.4f;

        // Measured from the standing figure's own bounds (e.g. height=3.6 ->
        // width~1.13 -> half-width~0.565 -> ratio~0.157). Used to keep each
        // pilgrim's own footprint inside the pedestal instead of just its
        // center point -- the front-row overhang came from clamping (or not
        // clamping) the center only, ignoring how wide a tall figure actually is.
        private const float HalfWidthRatio = 0.157f;
        private const float PedestalHalfWidth = 1.5f;
        private const float PedestalSouthEdge = -5f;
        private const float EdgeMargin = 0.1f;

        private struct Pilgrim
        {
            public float Height;
            public float X;
            public float Z;
            public Pilgrim(float h, float x, float z) { Height = h; X = x; Z = z; }
        }

        [MenuItem("RuleGhost/Graybox/Place Center Statue")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var placeholder = GameObject.Find("CenterStatue/Statue");
            if (placeholder == null)
            {
                Debug.LogError("[PlaceCenterStatue] Could not find CenterStatue/Statue placeholder.");
                return;
            }
            var centerStatue = placeholder.transform.parent;

            // Set (not add) an absolute position so re-running this method
            // stays idempotent instead of shifting further south each time.
            centerStatue.position = new Vector3(0f, 0f, -PedestalSouthShift);

            float pedestalTopY = placeholder.transform.position.y - placeholder.transform.localScale.y / 2f;
            Debug.Log($"[PlaceCenterStatue] pedestalTopY={pedestalTopY}");

            var placeholderRenderer = placeholder.GetComponent<Renderer>();
            if (placeholderRenderer != null) placeholderRenderer.enabled = false;

            var pedestal = GameObject.Find("CenterStatue/Pedestal");
            if (pedestal == null)
            {
                Debug.LogError("[PlaceCenterStatue] Could not find CenterStatue/Pedestal.");
                return;
            }
            // Extend north only: keep the south edge fixed, push the north
            // edge out and re-center so the box still covers the same south
            // edge -- original pedestal was localPos.z=0, localScale.z=10
            // (edges at -5/+5), so the new center is +extension/2.
            float baseLength = 10f;
            float newLength = baseLength + PedestalNorthExtension;
            var pedScale = pedestal.transform.localScale;
            pedestal.transform.localScale = new Vector3(pedScale.x, pedScale.y, newLength);
            var pedPos = pedestal.transform.localPosition;
            pedestal.transform.localPosition = new Vector3(pedPos.x, pedPos.y, PedestalNorthExtension / 2f);
            float pedestalCenterZ = PedestalNorthExtension / 2f;
            Debug.Log($"[PlaceCenterStatue] Pedestal extended: length {baseLength}->{newLength}, centerZ={pedestalCenterZ}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FigurePath);
            if (prefab == null)
            {
                Debug.LogError($"[PlaceCenterStatue] Could not load {FigurePath}");
                return;
            }

            // Clean up any previous placement or connector test objects.
            for (int i = 0; i < centerStatue.childCount; i++)
            {
                var child = centerStatue.GetChild(i);
                if (child.name.StartsWith("Statue_Figure1") || child.name.StartsWith("Pilgrim_") || child.name.StartsWith("Connector_"))
                {
                    Object.DestroyImmediate(child.gameObject);
                    i--;
                }
            }

            var pilgrims = GeneratePilgrims(pedestalCenterZ);
            for (int i = 0; i < pilgrims.Count; i++)
            {
                PlaceOne(prefab, centerStatue, pedestalTopY, pilgrims[i], i);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PlaceCenterStatue] Placed {pilgrims.Count} pilgrims. Scene saved.");
        }

        private static List<Pilgrim> GeneratePilgrims(float centerZ)
        {
            var rand = new System.Random(RandomSeed);
            var list = new List<Pilgrim>();

            float xStart = -(GridCols - 1) * ColSpacing / 2f;
            float zStart = centerZ - (GridRows - 1) * RowSpacing / 2f;
            float northEdge = 5f + PedestalNorthExtension;

            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridCols; col++)
                {
                    float x = xStart + col * ColSpacing + Lerp(-XJitter, XJitter, rand);
                    float z = zStart + row * RowSpacing + Lerp(-ZJitter, ZJitter, rand);
                    float height = Lerp(MinHeight, MaxHeight, rand);

                    float halfWidth = height * HalfWidthRatio;
                    x = Mathf.Clamp(x, -PedestalHalfWidth + halfWidth + EdgeMargin, PedestalHalfWidth - halfWidth - EdgeMargin);
                    z = Mathf.Clamp(z, PedestalSouthEdge + halfWidth + EdgeMargin, northEdge - halfWidth - EdgeMargin);

                    list.Add(new Pilgrim(height, x, z));
                }
            }
            return list;
        }

        private static float Lerp(float min, float max, System.Random rand)
        {
            return min + (float)rand.NextDouble() * (max - min);
        }

        private static void PlaceOne(GameObject prefab, Transform parent, float pedestalTopY, Pilgrim p, int index)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = $"Pilgrim_{index}";
            instance.transform.localPosition = new Vector3(p.X, 0f, p.Z);
            instance.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError($"[PlaceCenterStatue] Figure {index}: no renderers.");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Statue/M_Figure1_Standing.mat");
            if (mat != null)
            {
                foreach (var r in renderers) r.sharedMaterial = mat;
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float rawHeight = bounds.size.y;
            float scaleFactor = p.Height / rawHeight;
            instance.transform.localScale = Vector3.one * scaleFactor;

            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float feetY = bounds.min.y;
            float deltaY = pedestalTopY - feetY;
            instance.transform.position += new Vector3(0f, deltaY, 0f);
        }
    }
}
