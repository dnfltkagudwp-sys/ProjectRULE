using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // The lobby's south wall has stood as a bare 3m gap (LobbyGrayboxBuilder.BuildStructure) since
    // the level was first built -- no door, and no floor beyond it either, so walking through it
    // drops the player into the void. Fills it with a real double glass door + flanking sidelights,
    // using the same hinge/frame technique DecorateGuardRoom already established for the guard
    // room's own door (same DoorLeafWidth/Height, same frame border/depth, same trim material) so
    // the two doors in the level read as one consistent kit of parts. This door is built CLOSED and
    // stays that way -- nothing today drives it open (unlike the inspection door's ajar/wide-open
    // anomaly states), so no DoorTestInteraction is attached; only PatrolInteractable's existing
    // CheckPoint_Entrance marker handles the "확인" interaction.
    public static class AddEntranceDoor
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        // Matches LobbyGrayboxBuilder's own private constants -- HalfWidth=7/HalfDepth=10/Height=5/
        // WallThickness=0.2 -- and the resulting south-wall gap (Wall_South_Left ends at x=-1.5,
        // Wall_South_Right starts at x=1.5; see BuildStructure).
        private const float OpeningHalfWidth = 1.5f;
        private const float WallZ = -10f - 0.2f / 2f; // -HalfDepth - WallThickness/2
        private const float WallThickness = 0.2f;
        private const float CeilingHeight = 5f;

        // Same leaf proportions DecorateGuardRoom already tuned against the player's own
        // CharacterController (radius 0.4 / height 2) for real clearance -- reused directly rather
        // than re-deriving the same numbers.
        private const float DoorLeafWidth = 1.2f;
        private const float DoorLeafHeight = 2.4f;
        private const float DoorLeafThickness = 0.05f;

        // Two leaves meet at x=0 (2.4m total) with a 0.3m fixed glass sidelight flanking each side,
        // filling the remaining width of the 3m opening symmetrically -- a double door with
        // sidelights instead of DecorateGuardRoom's single-door-plus-window, since this is the
        // building's main entrance rather than a service doorway.
        private const float SidelightWidth = OpeningHalfWidth - DoorLeafWidth; // 0.3

        private const float DoorFrameBorder = 0.08f; // AddPaintingFrames.cs's own established width
        private const float DoorFrameDepthAdd = 0.03f;

        [MenuItem("RuleGhost/Graybox/Add Entrance Door")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find("EntranceDoor");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
            var root = new GameObject("EntranceDoor");

            var glassMat = GetOrCreateMaterial("M_EntranceGlass", new Color(0.75f, 0.82f, 0.85f), smoothness: 0.85f);
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Graybox/Materials/M_Wall.mat");
            var trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Architecture/M_Trim.mat");

            AddDoorLeaf(root.transform, glassMat, hingeX: -DoorLeafWidth, swingSign: 1f, name: "Left");
            AddDoorLeaf(root.transform, glassMat, hingeX: DoorLeafWidth, swingSign: -1f, name: "Right");
            AddSidelights(root.transform, glassMat);
            AddHeader(root.transform, wallMat);
            AddDoorFrame(root.transform, trimMat);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AddEntranceDoor] Double glass door + sidelights + header + frame added across the lobby entrance. Scene saved.");
        }

        // hingeX: the door's own outer edge (where its hinge sits); swingSign: +1 for a leaf that
        // extends toward +X from its hinge (the left leaf, hinge at the opening's west edge),
        // -1 for one extending toward -X (the right leaf, hinge at the east edge) -- mirrors
        // DecorateGuardRoom.AddDoor's hinge+leaf pattern, with the swing axis rotated 90 degrees
        // since this wall runs along X instead of Z.
        private static void AddDoorLeaf(Transform root, Material mat, float hingeX, float swingSign, string name)
        {
            var hinge = new GameObject($"EntranceDoor_{name}_Hinge");
            hinge.transform.SetParent(root, false);
            hinge.transform.position = new Vector3(hingeX, 0f, WallZ);
            hinge.transform.localRotation = Quaternion.identity; // built closed; nothing drives this open today

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = $"EntranceDoor_{name}_Leaf";
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(swingSign * DoorLeafWidth / 2f, DoorLeafHeight / 2f, 0f);
            leaf.transform.localScale = new Vector3(DoorLeafWidth, DoorLeafHeight, DoorLeafThickness);
            leaf.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // Fixed glass panels flanking the double door, filling the rest of the 3m opening.
        private static void AddSidelights(Transform root, Material mat)
        {
            float centerX = OpeningHalfWidth - SidelightWidth / 2f;
            Block(root, "EntranceDoor_Sidelight_Left", mat,
                new Vector3(-centerX, DoorLeafHeight / 2f, WallZ), new Vector3(SidelightWidth, DoorLeafHeight, DoorLeafThickness));
            Block(root, "EntranceDoor_Sidelight_Right", mat,
                new Vector3(centerX, DoorLeafHeight / 2f, WallZ), new Vector3(SidelightWidth, DoorLeafHeight, DoorLeafThickness));
        }

        // Solid transom above the door/sidelights up to the ceiling, spanning the full opening --
        // otherwise the gap's original full-height (5m) span would leave open air above the glass.
        private static void AddHeader(Transform root, Material mat)
        {
            float height = CeilingHeight - DoorLeafHeight;
            Block(root, "EntranceDoor_Header", mat,
                new Vector3(0f, DoorLeafHeight + height / 2f, WallZ),
                new Vector3(OpeningHalfWidth * 2f, height, WallThickness));
        }

        // Frames just the double-door pair (not the sidelights) -- same technique and proportions
        // as DecorateGuardRoom.AddDoorFrame, axes swapped for a wall that runs along X.
        private static void AddDoorFrame(Transform root, Material mat)
        {
            float depth = WallThickness + DoorFrameDepthAdd * 2f;
            Block(root, "EntranceDoor_Frame_Left", mat,
                new Vector3(-DoorLeafWidth, DoorLeafHeight / 2f, WallZ),
                new Vector3(DoorFrameBorder, DoorLeafHeight + DoorFrameBorder, depth));
            Block(root, "EntranceDoor_Frame_Right", mat,
                new Vector3(DoorLeafWidth, DoorLeafHeight / 2f, WallZ),
                new Vector3(DoorFrameBorder, DoorLeafHeight + DoorFrameBorder, depth));
            Block(root, "EntranceDoor_Frame_Top", mat,
                new Vector3(0f, DoorLeafHeight, WallZ),
                new Vector3(DoorLeafWidth * 2f + DoorFrameBorder, DoorFrameBorder, depth));
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
