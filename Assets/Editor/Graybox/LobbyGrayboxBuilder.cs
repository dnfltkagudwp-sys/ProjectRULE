using System.IO;
using RuleGhost.Anomalies;
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
            var (northPaintings, westPaintings, eastPaintings) = BuildPaintings(paintings);

            Transform checkpoints = CreateGroup("Checkpoints", root);
            var (thermometer, inspectionDoor, entranceMarker) = BuildCheckpoints(checkpoints);

            Transform guardRoom = CreateGroup("GuardRoom", root);
            BuildGuardRoom(guardRoom);

            BuildPlayer(root);
            BuildPatrolSystem(root, northPaintings, westPaintings, eastPaintings, thermometer, inspectionDoor, entranceMarker);

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

            // The doorway opening above spans the wall's full height (5m),
            // but the guard room itself is only 3m tall (BuildGuardRoom's
            // roomHeight) -- without this lintel, looking through the
            // doorway shows straight past the guard room's roofline into
            // open void above it, a real hole rather than just a dark wall.
            const float guardRoomHeight = 3f; // must match BuildGuardRoom's roomHeight
            CreateBlock("GuardRoom_Doorway_Lintel", parent,
                new Vector3(-HalfWidth - WallThickness / 2f, (guardRoomHeight + Height) / 2f, -8f),
                new Vector3(WallThickness, Height - guardRoomHeight, 3f), matWall);

            // East wall is split to leave a real doorway opening for the
            // inspection checkpoint door (1.4m wide, z 8.3..9.7; 2.6m tall
            // from the floor) instead of a solid wall with a frame block
            // layered on top -- the overlapping geometry from that approach
            // z-fought (flickered) since the frame and the wall shared the
            // exact same depth. See CheckPoint_InspectionDoor below.
            CreateBlock("Wall_East_South", parent, new Vector3(HalfWidth + WallThickness / 2f, Height / 2f, -0.85f),
                new Vector3(WallThickness, Height, 18.3f), matWall);
            CreateBlock("Wall_East_North", parent, new Vector3(HalfWidth + WallThickness / 2f, Height / 2f, 9.85f),
                new Vector3(WallThickness, Height, 0.3f), matWall);
            CreateBlock("Wall_East_AboveDoor", parent, new Vector3(HalfWidth + WallThickness / 2f, 3.8f, 9f),
                new Vector3(WallThickness, 2.4f, 1.4f), matWall);

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

        private static (Transform[] north, Transform[] west, Transform[] east) BuildPaintings(Transform parent)
        {
            var matPainting = GetOrCreateMaterial("Painting", new Color(0.55f, 0.35f, 0.2f));
            const float paintW = 1.2f, paintH = 1.6f, paintT = 0.05f, y = 2.5f;

            var north = new Transform[3];
            var west = new Transform[3];
            var east = new Transform[3];

            float[] northX = { -4f, 0f, 4f };
            for (int i = 0; i < northX.Length; i++)
            {
                var go = CreateBlock($"Painting_North_{i + 1}", parent, new Vector3(northX[i], y, HalfDepth - 0.05f),
                    new Vector3(paintW, paintH, paintT), matPainting);
                north[i] = go.transform;
            }

            float[] sideZ = { -5f, 0f, 5f };
            for (int i = 0; i < sideZ.Length; i++)
            {
                var go = CreateBlock($"Painting_West_{i + 1}", parent, new Vector3(-HalfWidth + 0.05f, y, sideZ[i]),
                    new Vector3(paintT, paintH, paintW), matPainting);
                west[i] = go.transform;
            }
            for (int i = 0; i < sideZ.Length; i++)
            {
                var go = CreateBlock($"Painting_East_{i + 1}", parent, new Vector3(HalfWidth - 0.05f, y, sideZ[i]),
                    new Vector3(paintT, paintH, paintW), matPainting);
                east[i] = go.transform;
            }

            return (north, west, east);
        }

        private static (Transform thermometer, Transform inspectionDoor, Transform entranceMarker) BuildCheckpoints(Transform parent)
        {
            var matThermo = GetOrCreateMaterial("Thermo", new Color(0.2f, 0.5f, 0.9f));
            var matDoor = GetOrCreateMaterial("InspectionDoor", new Color(0.7f, 0.15f, 0.15f));
            var matMarker = GetOrCreateMaterial("EntranceMarker", new Color(0.2f, 0.8f, 0.3f));
            var matWall = GetOrCreateMaterial("Wall", new Color(0.75f, 0.75f, 0.75f));

            // Thermo-hygrometer stays on the inner (north) wall, left side; pulled further left
            // and mounted higher so it clears Painting_North_1 instead of crowding it.
            var thermometerGO = CreateBlock("CheckPoint_ThermoHygrometer", parent, new Vector3(-6f, 2.2f, HalfDepth - 0.1f),
                new Vector3(0.5f, 0.5f, 0.15f), matThermo);

            // Inspection door moved to the far end of the right (east) wall, facing into the
            // lobby, clear of the East wall paintings (z -5/0/5) and enlarged slightly.
            // Sits in the real doorway opening cut into Wall_East above (z 8.3..9.7,
            // y 0..2.6) rather than a frame layered on top of a solid wall -- the frame
            // exactly fills that opening, and the door slab sits slightly forward
            // (west) of it with a small margin, so a thin reveal shows at its edge.
            // Reuses the wall's own material (not a separate near-black one) so the
            // reveal has visible contrast against the dark door -- a frame that close
            // in value to the door erased the sense of a recess, making the door read
            // as a flat panel floating in front of the wall instead of built into it.
            CreateBlock("CheckPoint_InspectionDoor_Frame", parent, new Vector3(HalfWidth + WallThickness / 2f, 1.3f, 9f),
                new Vector3(WallThickness, 2.6f, 1.4f), matWall);

            // Door is a hinge + leaf, not a flat slab centered in the opening --
            // a static centered slab read as "a door-shaped object placed in a
            // hole" rather than an actual door. The hinge sits on the door's
            // north edge (z=9.65; the handle reads on the south/-Z side of the
            // door face, opposite the hinge, per the earlier handle-mirror fix),
            // and the leaf is offset -0.65 in local Z so it swings into the room
            // like a real door -- also needed later for the patrol anomaly
            // system's ajar/wide-open door states.
            var doorHinge = new GameObject("CheckPoint_InspectionDoor_Hinge");
            doorHinge.transform.SetParent(parent, false);
            doorHinge.transform.localPosition = new Vector3(HalfWidth - WallThickness / 2f - 0.04f, 0f, 9.65f);
            var doorGO = CreateBlock("CheckPoint_InspectionDoor", doorHinge.transform, new Vector3(0f, 1.25f, -0.65f),
                new Vector3(0.08f, 2.5f, 1.3f), matDoor);
            // Graybox-only walk-up-and-press-E toggle so the hinge's feel can
            // be playtested directly; the later patrol/rule system will drive
            // the same hinge transform for the ajar/wide-open anomaly states.
            doorHinge.AddComponent<DoorTestInteraction>();

            var entranceMarker = new GameObject("CheckPoint_Entrance");
            entranceMarker.transform.SetParent(parent, false);
            entranceMarker.transform.localPosition = new Vector3(0, 0, -HalfDepth);

            CreateBlock("CheckPoint_Entrance_FloorMark", parent, new Vector3(0, 0.03f, -HalfDepth + 0.3f),
                new Vector3(3f, 0.04f, 0.6f), matMarker);

            return (thermometerGO.transform, doorGO.transform, entranceMarker.transform);
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
            var playerCamera = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();

            // Solid black instead of the default skybox -- the floor-to-
            // ceiling entrance gap and the guard room doorway both look
            // straight out at Unity's default sky otherwise, reading as a
            // light leak even though nothing there is actually lighting the
            // room (ambient mode is Flat, so the skybox isn't contributing
            // light -- it was purely a visible-background problem).
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = Color.black;

            // Player-carried flashlight: the room's base lighting is dark
            // enough that a fixed light rig alone isn't meant to make it
            // fully visible on its own -- the player has to actively light
            // their own way, with the gallery's accent spots discovered as
            // points of interest rather than doing that job for them.
            var flashGO = new GameObject("Flashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            var flashlight = flashGO.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.color = new Color(0.92f, 0.95f, 1f);
            flashlight.intensity = 13f;
            flashlight.range = 10f;
            flashlight.spotAngle = 40f;
            flashlight.innerSpotAngle = 20f;
            flashlight.shadows = LightShadows.Soft;

            player.AddComponent<GrayboxTestController>();
        }

        private static void BuildPatrolSystem(Transform root, Transform[] north, Transform[] west, Transform[] east,
            Transform thermometer, Transform inspectionDoor, Transform entranceMarker)
        {
            var bindingsGO = new GameObject("PatrolSceneBindings");
            bindingsGO.transform.SetParent(root, false);
            var bindings = bindingsGO.AddComponent<PatrolSceneBindings>();
            bindings.Configure(north, west, east, thermometer, inspectionDoor, entranceMarker);

            var ruleSet = AssetDatabase.LoadAssetAtPath<CombinationRuleSet>("Assets/Data/Anomalies/CombinationRuleSet.asset");
            var profiles = new[]
            {
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_01_AM1.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_02_AM5.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_03_AM1.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_04_AM5.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_05_AM1.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_06_AM5.asset")
            };

            if (ruleSet == null || System.Array.Exists(profiles, p => p == null))
            {
                Debug.LogWarning("[LobbyGrayboxBuilder] Anomaly data set not found under Assets/Data/Anomalies " +
                                 "-- run RuleGhost/Anomalies/Build Anomaly Data Set first. Skipping PatrolTestHarness wiring.");
                return;
            }

            var harnessGO = new GameObject("PatrolTestHarness (Debug)");
            harnessGO.transform.SetParent(root, false);
            var harness = harnessGO.AddComponent<PatrolTestHarness>();
            harness.EditorConfigure(bindings, ruleSet, profiles);
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
