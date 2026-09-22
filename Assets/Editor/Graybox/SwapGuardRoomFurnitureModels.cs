using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Replaces DecorateGuardRoom's placeholder cube desk/chair with the real Varco-generated
    // models (Assets/Art/Furniture/GuardRoom_Desk.fbx, GuardRoom_Chair.fbx). Kept as a separate
    // pass rather than folded into DecorateGuardRoom so that script still works standalone (cube
    // placeholders) if these model files are ever missing/regenerated.
    //
    // Orientation and per-axis size come from FurnitureOrientationCheckTest's isolated renders,
    // not guesswork -- the first pass assumed the model's raw X axis ("width", the long side) was
    // the wall-perpendicular depth, which put the desk's full 1m width sticking out of the wall
    // (clipping through it) and left the chair's approach direction and spacing wrong:
    //   Desk: local X = width (1.0 raw), Y = height (0.62 raw), Z = depth (0.58 raw).
    //   Chair: local X = width (0.55 raw), Y = height (1.00 raw), Z = depth/facing (0.58 raw,
    //          local +Z is the direction an occupant would face).
    public static class SwapGuardRoomFurnitureModels
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string DeskModelPath = "Assets/Art/Furniture/GuardRoom_Desk.fbx";
        private const string ChairModelPath = "Assets/Art/Furniture/GuardRoom_Chair.fbx";

        private const float DeskRawWidth = 1.00f;
        private const float DeskRawHeight = 0.62f;
        private const float DeskRawDepth = 0.58f;
        private const float ChairRawWidth = 0.55f;
        private const float ChairRawHeight = 1.00f;
        private const float ChairRawDepth = 0.58f;

        private const float DeskTargetHeight = 0.75f; // standard desk height
        private const float ChairTargetHeight = 0.85f; // seat + backrest, standard desk chair
        private const float DeskScale = DeskTargetHeight / DeskRawHeight;
        private const float ChairScale = ChairTargetHeight / ChairRawHeight;

        // Rotates the model's local width axis onto world Z (along the wall) and its local depth
        // axis onto world X (perpendicular to the wall, sticking into the room) -- see class
        // comment. The chair's extra rotation on top of that points its local +Z (occupant-facing)
        // toward -X, i.e. toward the desk.
        private const float DeskRotationY = 90f;
        private const float ChairRotationY = -90f;

        private const float RoomCenterZ = -8f;
        private const float InteriorWestX = -10.2f; // guard room's west wall inner face
        private const float ChairApproachGap = 0.15f; // clearance between chair front and desk edge

        [MenuItem("RuleGhost/Graybox/Swap Guard Room Furniture Models")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskModelPath);
            var chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChairModelPath);
            if (deskPrefab == null || chairPrefab == null)
            {
                Debug.LogError("[SwapGuardRoomFurnitureModels] Could not load desk/chair FBX models.");
                return;
            }

            var furnishingRoot = GameObject.Find("GuardRoomFurnishing");
            if (furnishingRoot == null)
            {
                Debug.LogError("[SwapGuardRoomFurnitureModels] Could not find GuardRoomFurnishing -- run Decorate Guard Room first.");
                return;
            }

            // The generated FBX models came in with a flat neutral-gray Lit material (no embedded
            // texture actually made it through Unity's import) -- reuse the same dark materials
            // DecorateGuardRoom already made for the cube placeholders instead of chasing FBX
            // texture extraction, so the color matches the intended dark-wood/dark-gray spec.
            var deskMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Architecture/M_Desk.mat");
            var chairMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Architecture/M_Chair.mat");

            // Desk sits against the west wall: its scaled depth (now on world X after rotation)
            // sticks out from the wall's inner face.
            float deskDepthWorld = DeskRawDepth * DeskScale;
            float deskCenterX = InteriorWestX + deskDepthWorld / 2f;
            float deskNearEdgeX = deskCenterX + deskDepthWorld / 2f; // the room-facing edge of the desk

            // Chair sits east of the desk, facing back toward it (-X), with a small approach gap
            // instead of the flat/wrong offset the first pass used.
            float chairDepthWorld = ChairRawDepth * ChairScale;
            float chairCenterX = deskNearEdgeX + ChairApproachGap + chairDepthWorld / 2f;

            SwapOne("GuardRoom_Desk", deskPrefab, furnishingRoot.transform,
                new Vector3(deskCenterX, 0f, RoomCenterZ), DeskScale, DeskTargetHeight, DeskRotationY, deskMat);
            SwapOne("GuardRoom_Chair", chairPrefab, furnishingRoot.transform,
                new Vector3(chairCenterX, 0f, RoomCenterZ), ChairScale, ChairTargetHeight, ChairRotationY, chairMat);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SwapGuardRoomFurnitureModels] Desk and chair replaced with generated models. Scene saved.");
        }

        private static void SwapOne(string placeholderName, GameObject prefab, Transform parent,
            Vector3 footprintXZ, float scale, float targetHeight, float rotationY, Material overrideMat)
        {
            var placeholder = GameObject.Find(placeholderName);
            if (placeholder != null)
            {
                Object.DestroyImmediate(placeholder);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = placeholderName;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            // The model's pivot sits at its bounding-box center, not its base -- lift it by half
            // its (scaled) height so the base rests on the floor instead of clipping through it.
            instance.transform.position = new Vector3(footprintXZ.x, targetHeight / 2f, footprintXZ.z);

            if (overrideMat != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = overrideMat;
                    }
                    renderer.sharedMaterials = mats;
                }
            }
        }
    }
}
