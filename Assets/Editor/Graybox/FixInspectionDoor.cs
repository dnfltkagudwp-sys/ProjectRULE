using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Cuts an actual rectangular gap in Wall_East for the inspection door
    // instead of overlapping a frame block on top of the solid wall. The
    // earlier frame-block approach put the frame at the exact same X depth
    // as the wall, so their coincident faces z-fought (flickered) wherever
    // the frame wasn't fully hidden behind the door -- splitting the wall
    // into three pieces around a real opening removes the overlap entirely.
    // Operates directly on the already-built scene rather than re-running
    // LobbyGrayboxBuilder, which would wipe out the pilgrim placement and
    // material work layered on top of it.
    public static class FixInspectionDoor
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float HalfWidth = 7f;
        private const float HalfDepth = 10f;
        private const float WallThickness = 0.2f;
        private const float Height = 5f;

        // Door opening: 1.4m wide (z 8.3..9.7), 2.6m tall from the floor (y 0..2.6).
        private const float OpenZMin = 8.3f;
        private const float OpenZMax = 9.7f;
        private const float OpenYMax = 2.6f;

        [MenuItem("RuleGhost/Graybox/Fix Inspection Door")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var wallEastGO = GameObject.Find("Wall_East");
            Transform parent;
            Material wallMat;
            float wallX;
            if (wallEastGO != null)
            {
                // First run: Wall_East is still one solid piece -- split it.
                parent = wallEastGO.transform.parent;
                wallMat = wallEastGO.GetComponent<Renderer>().sharedMaterial;
                wallX = wallEastGO.transform.position.x;
                Object.DestroyImmediate(wallEastGO);

                // Three pieces around the doorway instead of one solid wall, so
                // there's an actual hole rather than an overlapping frame block.
                MakeWallPiece("Wall_East_South", parent, wallMat, wallX,
                    yCenter: Height / 2f, yScale: Height,
                    zCenter: (-HalfDepth * 2f / 2f + OpenZMin) / 2f, zScale: (OpenZMin - (-HalfDepth)));
                MakeWallPiece("Wall_East_North", parent, wallMat, wallX,
                    yCenter: Height / 2f, yScale: Height,
                    zCenter: (OpenZMax + HalfDepth) / 2f, zScale: (HalfDepth - OpenZMax));
                MakeWallPiece("Wall_East_AboveDoor", parent, wallMat, wallX,
                    yCenter: (OpenYMax + Height) / 2f, yScale: (Height - OpenYMax),
                    zCenter: (OpenZMin + OpenZMax) / 2f, zScale: (OpenZMax - OpenZMin));
            }
            else
            {
                // Already split in a previous run -- reuse the existing pieces
                // and just re-derive wallMat/parent/wallX from one of them so
                // the frame/door touch-up below still runs.
                var southPiece = GameObject.Find("Wall_East_South");
                if (southPiece == null)
                {
                    Debug.LogError("[FixInspectionDoor] Could not find Wall_East or Wall_East_South.");
                    return;
                }
                parent = southPiece.transform.parent;
                wallMat = southPiece.GetComponent<Renderer>().sharedMaterial;
                wallX = southPiece.transform.position.x;
            }

            // Frame fills the cut opening exactly (no overlap with the
            // remaining wall pieces' faces).
            var frame = GameObject.Find("CheckPoint_InspectionDoor_Frame");
            if (frame == null)
            {
                frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "CheckPoint_InspectionDoor_Frame";
                if (parent != null) frame.transform.SetParent(parent, false);
            }
            float openZCenter = (OpenZMin + OpenZMax) / 2f;
            float openZLen = OpenZMax - OpenZMin;
            frame.transform.position = new Vector3(wallX, OpenYMax / 2f, openZCenter);
            frame.transform.localScale = new Vector3(WallThickness, OpenYMax, openZLen);
            // Reuse the wall's own material for the jamb instead of a
            // near-black flat color -- a frame this close in value to the
            // dark door mesh gave no visible contrast, so the recess read
            // as invisible and the door looked like a flat panel floating
            // in front of the wall rather than sitting inside a real hole.
            frame.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Door slab sits slightly forward (west, into the room) of the
            // frame's inner face, a little smaller than the opening so a
            // thin dark reveal shows around its edge.
            var door = GameObject.Find("CheckPoint_InspectionDoor");
            if (door == null)
            {
                Debug.LogError("[FixInspectionDoor] Could not find CheckPoint_InspectionDoor.");
                return;
            }
            float doorThickness = 0.08f;
            door.transform.position = new Vector3(HalfWidth - WallThickness / 2f - doorThickness / 2f, (OpenYMax - 0.1f) / 2f, openZCenter);
            door.transform.localScale = new Vector3(doorThickness, OpenYMax - 0.1f, openZLen - 0.1f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[FixInspectionDoor] Wall_East split around a real doorway opening. Scene saved.");
        }

        private static void MakeWallPiece(string name, Transform parent, Material mat, float x, float yCenter, float yScale, float zCenter, float zScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, yCenter, zCenter);
            go.transform.localScale = new Vector3(WallThickness, yScale, zScale);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
