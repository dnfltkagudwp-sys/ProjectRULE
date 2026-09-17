using System.IO;
using RuleGhost.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuleGhost.EditorTools
{
    // Builds the Project 1 (규칙 괴담) museum lobby graybox from the confirmed Notion spec:
    // 14x20x5m lobby, center statue sightline blocker, 9 wall paintings, opposite-end
    // thermo-hygrometer/inspection door, entrance-side guard room with a partial view only.
    // Primitives + temporary materials only, per the current work-scope limits.
    public static class LobbyGrayboxBuilder
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string MaterialFolder = "Assets/_Graybox/Materials";

        private const float HalfWidth = 7f;   // 14m
        private const float HalfDepth = 10f;  // 20m
        private const float Height = 5f;
        private const float WallThickness = 0.2f;

        [MenuItem("RuleGhost/Graybox/Build Lobby Graybox Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform root = CreateGroup("Lobby_Graybox");
            BuildLighting(root);

            Transform structure = CreateGroup("Structure", root);
            BuildStructure(structure);

            Transform statue = CreateGroup("CenterStatue", root);
            BuildCenterStatue(statue);

            Transform paintings = CreateGroup("Paintings", root);
            BuildPaintings(paintings);

            Transform checkpoints = CreateGroup("Checkpoints", root);
            BuildCheckpoints(checkpoints);

            Transform guardRoom = CreateGroup("GuardRoom", root);
            BuildGuardRoom(guardRoom);

            BuildPlayer(root);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LobbyGrayboxBuilder] Saved scene to {ScenePath}");
        }

        private static void BuildLighting(Transform root)
        {
            var lightGO = new GameObject("Directional Light");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
        }

        private static void BuildStructure(Transform parent)
        {
            var matFloor = GetOrCreateMaterial("Floor", new Color(0.55f, 0.55f, 0.55f));
            var matCeiling = GetOrCreateMaterial("Ceiling", new Color(0.85f, 0.85f, 0.85f));
            var matWall = GetOrCreateMaterial("Wall", new Color(0.75f, 0.75f, 0.75f));

            CreateBlock("Floor", parent, new Vector3(0, -WallThickness / 2f, 0),
                new Vector3(HalfWidth * 2f, WallThickness, HalfDepth * 2f), matFloor);
            CreateBlock("Ceiling", parent, new Vector3(0, Height + WallThickness / 2f, 0),
                new Vector3(HalfWidth * 2f, WallThickness, HalfDepth * 2f), matCeiling);

            // West wall is split to leave a 3m doorway (z -9.5..-6.5) into the guard room.
            CreateBlock("Wall_West_South", parent, new Vector3(-HalfWidth - WallThickness / 2f, Height / 2f, -9.75f),
                new Vector3(WallThickness, Height, 0.5f), matWall);
            CreateBlock("Wall_West_North", parent, new Vector3(-HalfWidth - WallThickness / 2f, Height / 2f, 1.75f),
                new Vector3(WallThickness, Height, 16.5f), matWall);

            CreateBlock("Wall_East", parent, new Vector3(HalfWidth + WallThickness / 2f, Height / 2f, 0),
                new Vector3(WallThickness, Height, HalfDepth * 2f), matWall);

            CreateBlock("Wall_North_Inner", parent, new Vector3(0, Height / 2f, HalfDepth + WallThickness / 2f),
                new Vector3(HalfWidth * 2f, Height, WallThickness), matWall);

            // South (entrance) wall keeps a 3m centered gap as the lobby entrance.
            CreateBlock("Wall_South_Left", parent, new Vector3(-4.25f, Height / 2f, -HalfDepth - WallThickness / 2f),
                new Vector3(5.5f, Height, WallThickness), matWall);
            CreateBlock("Wall_South_Right", parent, new Vector3(4.25f, Height / 2f, -HalfDepth - WallThickness / 2f),
                new Vector3(5.5f, Height, WallThickness), matWall);
        }

        private static void BuildCenterStatue(Transform parent)
        {
            var matPedestal = GetOrCreateMaterial("Pedestal", new Color(0.4f, 0.4f, 0.4f));
            var matStatue = GetOrCreateMaterial("Statue", new Color(0.3f, 0.3f, 0.3f));

            // Elongated along Z (entrance <-> north wall) instead of a round pillar, so it reads
            // as a long central spine and forces a real left/right corridor choice.
            CreateBlock("Pedestal", parent, new Vector3(0, 0.5f, 0), new Vector3(3f, 1f, 10f), matPedestal);
            CreateBlock("Statue", parent, new Vector3(0, 2.3f, 0), new Vector3(2.2f, 2.6f, 8.5f), matStatue,
                PrimitiveType.Cube);
        }

        private static void BuildPaintings(Transform parent)
        {
            var matPainting = GetOrCreateMaterial("Painting", new Color(0.55f, 0.35f, 0.2f));
            const float paintW = 1.2f, paintH = 1.6f, paintT = 0.05f, y = 2.5f;

            float[] northX = { -4f, 0f, 4f };
            for (int i = 0; i < northX.Length; i++)
            {
                CreateBlock($"Painting_North_{i + 1}", parent, new Vector3(northX[i], y, HalfDepth - 0.05f),
                    new Vector3(paintW, paintH, paintT), matPainting);
            }

            float[] sideZ = { -5f, 0f, 5f };
            for (int i = 0; i < sideZ.Length; i++)
            {
                CreateBlock($"Painting_West_{i + 1}", parent, new Vector3(-HalfWidth + 0.05f, y, sideZ[i]),
                    new Vector3(paintT, paintH, paintW), matPainting);
            }
            for (int i = 0; i < sideZ.Length; i++)
            {
                CreateBlock($"Painting_East_{i + 1}", parent, new Vector3(HalfWidth - 0.05f, y, sideZ[i]),
                    new Vector3(paintT, paintH, paintW), matPainting);
            }
        }

        private static void BuildCheckpoints(Transform parent)
        {
            var matThermo = GetOrCreateMaterial("Thermo", new Color(0.2f, 0.5f, 0.9f));
            var matDoor = GetOrCreateMaterial("InspectionDoor", new Color(0.7f, 0.15f, 0.15f));
            var matMarker = GetOrCreateMaterial("EntranceMarker", new Color(0.2f, 0.8f, 0.3f));

            // Thermo-hygrometer stays on the inner (north) wall, left side; pulled further left
            // and mounted higher so it clears Painting_North_1 instead of crowding it.
            CreateBlock("CheckPoint_ThermoHygrometer", parent, new Vector3(-6f, 2.2f, HalfDepth - 0.1f),
                new Vector3(0.5f, 0.5f, 0.15f), matThermo);

            // Inspection door moved to the far end of the right (east) wall, facing into the
            // lobby, clear of the East wall paintings (z -5/0/5) and enlarged slightly.
            CreateBlock("CheckPoint_InspectionDoor", parent, new Vector3(HalfWidth - 0.1f, 1.3f, 9f),
                new Vector3(0.15f, 2.6f, 1.4f), matDoor);

            var entranceMarker = new GameObject("CheckPoint_Entrance");
            entranceMarker.transform.SetParent(parent, false);
            entranceMarker.transform.localPosition = new Vector3(0, 0, -HalfDepth);

            CreateBlock("CheckPoint_Entrance_FloorMark", parent, new Vector3(0, 0.03f, -HalfDepth + 0.3f),
                new Vector3(3f, 0.04f, 0.6f), matMarker);
        }

        private static void BuildGuardRoom(Transform parent)
        {
            var matGuard = GetOrCreateMaterial("GuardRoom", new Color(0.65f, 0.6f, 0.4f));
            const float gx = -8.7f, gz = -8f, roomSize = 3.2f, roomHeight = 3f;

            // Encloses on 3 sides; the 4th (east) side is the doorway gap left in Wall_West.
            // The rest of Wall_West stays solid, so the lobby's full length is not visible from here.
            CreateBlock("GuardRoom_Floor", parent, new Vector3(gx, -0.1f, gz), new Vector3(roomSize, 0.2f, roomSize), matGuard);
            CreateBlock("GuardRoom_Ceiling", parent, new Vector3(gx, roomHeight + 0.1f, gz), new Vector3(roomSize, 0.2f, roomSize), matGuard);
            CreateBlock("GuardRoom_Wall_West", parent, new Vector3(gx - roomSize / 2f, roomHeight / 2f, gz),
                new Vector3(WallThickness, roomHeight, roomSize), matGuard);
            CreateBlock("GuardRoom_Wall_South", parent, new Vector3(gx, roomHeight / 2f, gz - roomSize / 2f),
                new Vector3(roomSize, roomHeight, WallThickness), matGuard);
            CreateBlock("GuardRoom_Wall_North", parent, new Vector3(gx, roomHeight / 2f, gz + roomSize / 2f),
                new Vector3(roomSize, roomHeight, WallThickness), matGuard);
        }

        private static void BuildPlayer(Transform root)
        {
            var player = new GameObject("Player_TestController");
            player.transform.SetParent(root, false);
            player.transform.position = new Vector3(0f, 0f, -HalfDepth + 1f);

            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1f, 0);
            cc.height = 2f;
            cc.radius = 0.4f;

            var camGO = new GameObject("Main Camera");
            camGO.transform.SetParent(player.transform, false);
            camGO.transform.localPosition = new Vector3(0, 1.6f, 0);
            camGO.tag = "MainCamera";
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();

            player.AddComponent<GrayboxTestController>();
        }

        private static Transform CreateGroup(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            return go.transform;
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector3 localPos, Vector3 size,
            Material material, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            return go;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/M_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
