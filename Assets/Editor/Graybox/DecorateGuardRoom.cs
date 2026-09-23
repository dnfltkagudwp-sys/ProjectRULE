using RuleGhost.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Fills in the guard room, which has stood as bare floor/ceiling/3 walls since
    // LobbyGrayboxBuilder first raised it: a real hinged door across its open (east) side, a desk
    // against the back wall facing the doorway, a chair, and a placeholder document prop on the
    // desk (the future rule-sheet interactable -- UI/content comes later, this just gives it a
    // physical home). Same primitives-only graybox language as the rest of the level; the guard
    // room's warm point light (Point_GuardRoom, from ApplyDramaticLighting) already lights it.
    public static class DecorateGuardRoom
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        // The 3m-wide opening in Wall_West (z -9.5..-6.5, wall centered x=-7) that connects the
        // lobby to the guard room -- see LobbyGrayboxBuilder.BuildWalls.
        private const float WallX = -7f;
        private const float OpeningSouthZ = -9.5f;

        // Human-scale single interior door (was 1.3 x 2.6 x 0.08 -- read as a warehouse shutter,
        // not a door; 0.95 x 2.1 read right but caught on the player in practice -- the door
        // frame's casing (DoorFrameBorder, 0.08) eats into the opening on all sides, and against
        // the player's own CharacterController (radius 0.4 / height 2, see LobbyGrayboxBuilder)
        // that left only ~4cm of clearance on the sides and ~6cm overhead. 1.2 x 2.4 keeps the
        // same proportions but gives real clearance (~16cm sides, ~36cm overhead) after the frame.
        private const float DoorLeafWidth = 1.2f;
        private const float DoorLeafHeight = 2.4f;
        private const float DoorLeafThickness = 0.05f;
        // Built closed (0) -- DoorTestInteraction (added below) owns opening it at runtime, the
        // same walk-up-and-press-E pattern as the inspection door. A static "always ajar" angle
        // was also blocking the doorway for anyone trying to actually walk in.
        // Positive opens outward into the lobby -- the guard room is small and has furniture in
        // it, so swinging inward risks clipping the desk/chair. Sign convention matches
        // MakeDoorHinged's inspection door leaf, just the opposite choice here.
        private const float DoorRestAngle = 0f;

        // The opening is 3m wide (z -9.5..-6.5) but the door is only 0.95m -- the rest (the door's
        // closed edge to the opening's far edge) is filled with a fixed wall panel + window, like a
        // real security-booth service window, instead of a second door leaf.
        private const float FillWallSouthZ = OpeningSouthZ + DoorLeafWidth; // door's closed-edge position
        private const float FillWallNorthZ = OpeningSouthZ + 3f; // -6.5, the opening's far edge
        private const float FillWallTopY = 3f; // matches BuildGuardRoom's roomHeight / the lintel's start
        // A single header spans the FULL opening width right above the door's own height, instead
        // of the door and the side panel each having their own separate top treatment at different
        // heights -- that mismatch was what made them read as two unrelated blocks rather than one
        // doorway structure.
        private const float HeaderBottomY = DoorLeafHeight;
        private const float WindowSillY = 0.9f;
        private const float WindowHeadY = 1.9f;
        // Matches DoorFrameBorder so the window casing and the door casing read as the same kit of
        // parts (was 0.15, a noticeably different width that looked like leftover wall, not a frame).
        private const float WindowMargin = 0.08f;

        // Same proportions AddPaintingFrames.cs already established for framing an opening in this
        // game (border width / protrusion depth) -- reused directly instead of inventing new values,
        // so every framed opening in the level (paintings, this door) reads as one consistent part.
        private const float DoorFrameBorder = 0.08f;
        private const float DoorFrameDepthAdd = 0.03f;

        // Guard room interior: centered (-8.7, -8), 3.2m square, walls 0.2m thick -- see
        // LobbyGrayboxBuilder.BuildGuardRoom.
        private const float RoomCenterX = -8.7f;
        private const float RoomCenterZ = -8f;
        private const float RoomWallThickness = 0.2f;
        private const float InteriorWestX = RoomCenterX - 1.6f + RoomWallThickness; // west wall's inner face
        // The guard room's south/north inner wall faces happen to land exactly on the doorway
        // opening's own south/north edges (both derive from the same 3.2m room / 3m opening
        // geometry) -- reusing OpeningSouthZ/FillWallNorthZ directly instead of redefining them.
        private const float EastBoundaryX = WallX - RoomWallThickness / 2f; // guard-room-facing face of the lobby wall

        // Same values as the lobby's own baseboard (AddMuseumDetails.AddTrimRing) -- reused
        // rather than re-tuned so the two rooms' trim reads as one consistent building part.
        private const float BaseboardHeight = 0.13f;
        private const float BaseboardDepth = 0.05f;

        private const float DeskDepth = 0.55f;
        private const float DeskWidth = 1.3f;
        private const float DeskHeight = 0.75f;

        [MenuItem("RuleGhost/Graybox/Decorate Guard Room")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find("GuardRoomFurnishing");
            if (existing != null) Object.DestroyImmediate(existing);
            var root = new GameObject("GuardRoomFurnishing");

            var doorMat = GetOrCreateMaterial("M_GuardRoomDoor", new Color(0.14f, 0.1f, 0.08f), smoothness: 0.3f);
            var deskMat = GetOrCreateMaterial("M_Desk", new Color(0.25f, 0.16f, 0.1f), smoothness: 0.3f);
            var chairMat = GetOrCreateMaterial("M_Chair", new Color(0.18f, 0.12f, 0.08f), smoothness: 0.3f);
            var documentMat = GetOrCreateMaterial("M_RuleDocumentPlaceholder", new Color(0.82f, 0.79f, 0.7f), smoothness: 0.1f);
            // Matches whatever the guard room's own walls currently use -- UnifyGuardRoomAtmosphere
            // retints both the walls and this fill panel together, so they always stay in sync.
            var fillWallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Graybox/Materials/M_GuardRoom.mat");
            var glassMat = GetOrCreateMaterial("M_GuardRoomWindowGlass", new Color(0.75f, 0.82f, 0.85f), smoothness: 0.85f);
            // Same dark trim material already used for baseboards/painting frames elsewhere --
            // reused directly (not a duplicate with matching values) so it can never drift out of
            // sync with the rest of the level's trim color.
            var trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Architecture/M_Trim.mat");

            AddDoor(root.transform, doorMat);
            AddDoorFrame(root.transform, trimMat);
            float deskCenterX = AddDesk(root.transform, deskMat);
            AddChair(root.transform, chairMat, deskCenterX);
            AddDocument(root.transform, documentMat, deskCenterX);
            AddDoorFillWallWithWindow(root.transform, fillWallMat, glassMat, trimMat);
            AddSharedHeader(root.transform, fillWallMat);
            AddBaseboard(root.transform, trimMat);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[DecorateGuardRoom] Door, door frame, header, fill wall + window, baseboard, desk, chair and a placeholder rule document added. Scene saved.");
        }

        private static void AddDoor(Transform root, Material mat)
        {
            var existingHinge = GameObject.Find("GuardRoom_Door_Hinge");
            if (existingHinge != null)
            {
                Object.DestroyImmediate(existingHinge);
            }

            var hinge = new GameObject("GuardRoom_Door_Hinge");
            hinge.transform.SetParent(root, false);
            // Mounted flush with the lobby-facing wall surface (not the wall's centerline) so the
            // leaf's own thickness sits entirely on the lobby side it swings into, instead of
            // straddling the centerline and always poking slightly into the guard room regardless
            // of open angle.
            hinge.transform.position = new Vector3(WallX + RoomWallThickness / 2f, 0f, OpeningSouthZ);
            hinge.transform.localRotation = Quaternion.Euler(0f, DoorRestAngle, 0f);

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "GuardRoom_Door_Leaf";
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(0f, DoorLeafHeight / 2f, DoorLeafWidth / 2f);
            leaf.transform.localScale = new Vector3(DoorLeafThickness, DoorLeafHeight, DoorLeafWidth);
            leaf.GetComponent<Renderer>().sharedMaterial = mat;
            // Solid by default (closed) so the door actually blocks passage -- DoorTestInteraction
            // itself now toggles this to a trigger only while the leaf is actively swinging, so it
            // doesn't shove/snag the player who opened it from close range without losing the
            // "closed door blocks you" behavior at rest.

            // Same walk-up-and-press-E toggle as the inspection door (DoorTestInteraction) --
            // defaults (100 degree open angle, 2.5m range) already match what's wanted here: a
            // fully-open swing outward, no separate configuration needed.
            if (hinge.GetComponent<DoorTestInteraction>() == null)
            {
                hinge.AddComponent<DoorTestInteraction>();
            }
        }

        // Wraps the door's own opening (not the whole 3m gap) with a casing, the same technique
        // AddPaintingFrames.cs uses: bars centered ON each edge, sized edge-length + border, so
        // half the bar sits over the wall and half over the opening -- reads as a real jamb/head
        // instead of the door looking like a plank dropped into a hole.
        private static void AddDoorFrame(Transform root, Material mat)
        {
            var group = new GameObject("GuardRoom_DoorFrame");
            group.transform.SetParent(root, false);

            float southZ = OpeningSouthZ, northZ = FillWallSouthZ;
            float depth = RoomWallThickness + DoorFrameDepthAdd * 2f;

            Block(group.transform, "GuardRoom_DoorFrame_Left", mat,
                new Vector3(WallX, DoorLeafHeight / 2f, southZ), new Vector3(depth, DoorLeafHeight + DoorFrameBorder, DoorFrameBorder));
            Block(group.transform, "GuardRoom_DoorFrame_Right", mat,
                new Vector3(WallX, DoorLeafHeight / 2f, northZ), new Vector3(depth, DoorLeafHeight + DoorFrameBorder, DoorFrameBorder));
            Block(group.transform, "GuardRoom_DoorFrame_Top", mat,
                new Vector3(WallX, DoorLeafHeight, (southZ + northZ) / 2f), new Vector3(depth, DoorFrameBorder, northZ - southZ + DoorFrameBorder));
        }

        // A single band spanning the FULL opening width, right above the door's height -- ties the
        // door and the side window panel together as one structure instead of two blocks that
        // happen to be next to each other with different top lines.
        private static void AddSharedHeader(Transform root, Material wallMat)
        {
            Block(root, "GuardRoom_DoorHeader", wallMat,
                new Vector3(WallX, (HeaderBottomY + FillWallTopY) / 2f, (OpeningSouthZ + FillWallNorthZ) / 2f),
                new Vector3(RoomWallThickness, FillWallTopY - HeaderBottomY, FillWallNorthZ - OpeningSouthZ));
        }

        // Returns the desk's world X center, so the chair/document can be placed relative to it
        // without recomputing the same geometry.
        private static float AddDesk(Transform root, Material mat)
        {
            float deskCenterX = InteriorWestX + DeskDepth / 2f;
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "GuardRoom_Desk";
            desk.transform.SetParent(root, false);
            desk.transform.position = new Vector3(deskCenterX, DeskHeight / 2f, RoomCenterZ);
            desk.transform.localScale = new Vector3(DeskDepth, DeskHeight, DeskWidth);
            desk.GetComponent<Renderer>().sharedMaterial = mat;
            return deskCenterX;
        }

        private static void AddChair(Transform root, Material mat, float deskCenterX)
        {
            var group = new GameObject("GuardRoom_Chair");
            group.transform.SetParent(root, false);

            // Faces the desk (-X direction), sits east of it, between desk and doorway.
            float chairX = deskCenterX + 0.55f;
            Block(group.transform, "Seat", mat, new Vector3(chairX, 0.42f, RoomCenterZ), new Vector3(0.45f, 0.05f, 0.45f));
            Block(group.transform, "Backrest", mat, new Vector3(chairX + 0.2f, 0.65f, RoomCenterZ), new Vector3(0.05f, 0.5f, 0.45f));
        }

        // Closes off the rest of the 3m opening the door doesn't cover, with a window inset
        // (sill/head solid wall, margins a dark trim casing matching the door frame, the window
        // itself a glossy "glass" panel) instead of a second door leaf -- reads as a security-booth
        // service window. Stops at HeaderBottomY -- the shared header above takes over from there.
        private static void AddDoorFillWallWithWindow(Transform root, Material wallMat, Material glassMat, Material trimMat)
        {
            var group = new GameObject("GuardRoom_DoorFillWall");
            group.transform.SetParent(root, false);

            float southZ = FillWallSouthZ, northZ = FillWallNorthZ;
            float windowSouthZ = southZ + WindowMargin;
            float windowNorthZ = northZ - WindowMargin;

            // Flat, unique names (not nested short names) so UnifyGuardRoomAtmosphere can find
            // and retint each one individually via GameObject.Find.
            // Below the sill and above the head, full width of the fill panel.
            Block(group.transform, "GuardRoom_DoorFillWall_Bottom", wallMat,
                new Vector3(WallX, WindowSillY / 2f, (southZ + northZ) / 2f),
                new Vector3(RoomWallThickness, WindowSillY, northZ - southZ));
            Block(group.transform, "GuardRoom_DoorFillWall_Top", wallMat,
                new Vector3(WallX, (WindowHeadY + HeaderBottomY) / 2f, (southZ + northZ) / 2f),
                new Vector3(RoomWallThickness, HeaderBottomY - WindowHeadY, northZ - southZ));

            // Dark trim casing either side of the window (matches the door frame's width/material),
            // window-height only.
            float windowHeight = WindowHeadY - WindowSillY;
            float windowMidY = (WindowSillY + WindowHeadY) / 2f;
            Block(group.transform, "GuardRoom_DoorFillWall_LeftMargin", trimMat,
                new Vector3(WallX, windowMidY, (southZ + windowSouthZ) / 2f),
                new Vector3(RoomWallThickness, windowHeight, windowSouthZ - southZ));
            Block(group.transform, "GuardRoom_DoorFillWall_RightMargin", trimMat,
                new Vector3(WallX, windowMidY, (windowNorthZ + northZ) / 2f),
                new Vector3(RoomWallThickness, windowHeight, northZ - windowNorthZ));

            // The glass itself, thinner than the wall so it doesn't z-fight with the margins.
            Block(group.transform, "GuardRoom_Window_Glass", glassMat,
                new Vector3(WallX, windowMidY, (windowSouthZ + windowNorthZ) / 2f),
                new Vector3(RoomWallThickness * 0.5f, windowHeight, windowNorthZ - windowSouthZ));
        }

        // Wall-to-floor trim around the room -- west/south/north walls in full, plus the east
        // (door-opposite) side but only under the window/fill-wall section. Deliberately skips the
        // door's own opening so it doesn't obstruct the door's swing or the walkway through it.
        private static void AddBaseboard(Transform root, Material trimMat)
        {
            var group = new GameObject("GuardRoom_Baseboard");
            group.transform.SetParent(root, false);

            float southZ = OpeningSouthZ, northZ = FillWallNorthZ;
            float westEastCenterX = (InteriorWestX + EastBoundaryX) / 2f;
            float westEastSpan = EastBoundaryX - InteriorWestX;

            Block(group.transform, "GuardRoom_Baseboard_West", trimMat,
                new Vector3(InteriorWestX + BaseboardDepth / 2f, BaseboardHeight / 2f, (southZ + northZ) / 2f),
                new Vector3(BaseboardDepth, BaseboardHeight, northZ - southZ));
            Block(group.transform, "GuardRoom_Baseboard_South", trimMat,
                new Vector3(westEastCenterX, BaseboardHeight / 2f, southZ + BaseboardDepth / 2f),
                new Vector3(westEastSpan, BaseboardHeight, BaseboardDepth));
            Block(group.transform, "GuardRoom_Baseboard_North", trimMat,
                new Vector3(westEastCenterX, BaseboardHeight / 2f, northZ - BaseboardDepth / 2f),
                new Vector3(westEastSpan, BaseboardHeight, BaseboardDepth));
            Block(group.transform, "GuardRoom_Baseboard_East", trimMat,
                new Vector3(EastBoundaryX - BaseboardDepth / 2f, BaseboardHeight / 2f, (FillWallSouthZ + FillWallNorthZ) / 2f),
                new Vector3(BaseboardDepth, BaseboardHeight, FillWallNorthZ - FillWallSouthZ));
        }

        private static void AddDocument(Transform root, Material mat, float deskCenterX)
        {
            // Placeholder for the future rule-sheet interactable -- content/UI comes later, this
            // just claims a physical spot on the desk. No collider: a small flat prop like this
            // shouldn't obstruct the player the way the desk itself should.
            var doc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doc.name = "GuardRoom_RuleDocument";
            Object.DestroyImmediate(doc.GetComponent<Collider>());
            doc.transform.SetParent(root, false);
            doc.transform.position = new Vector3(deskCenterX, DeskHeight + 0.011f, RoomCenterZ);
            doc.transform.localScale = new Vector3(0.35f, 0.02f, 0.25f);
            doc.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static GameObject Block(Transform parent, string name, Material mat, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
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
