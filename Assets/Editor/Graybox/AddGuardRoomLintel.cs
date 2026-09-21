using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // The guard room doorway is a real hole, not just a dark-background
    // illusion: the guard room is only 3m tall, but the opening left in the
    // main west wall for its doorway spans the wall's full 5m height, so
    // above the guard room's own roof there's genuinely nothing between it
    // and open space -- looking through the doorway from the lobby, you can
    // see straight past the guard room's roofline into that void. Fills it
    // with a lintel block spanning the doorway width at the guard room's
    // ceiling height, closing it the way a real doorway lintel would.
    public static class AddGuardRoomLintel
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float HalfWidth = 7f;
        private const float WallThickness = 0.2f;
        private const float Height = 5f;
        private const float GuardRoomHeight = 3f;

        // Doorway gap in Wall_West: z -9.5..-6.5 (see LobbyGrayboxBuilder.BuildWalls).
        private const float DoorZMin = -9.5f;
        private const float DoorZMax = -6.5f;

        [MenuItem("RuleGhost/Graybox/Add Guard Room Lintel")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var wallSouth = GameObject.Find("Wall_West_South");
            if (wallSouth == null)
            {
                Debug.LogError("[AddGuardRoomLintel] Could not find Wall_West_South for parent/material reference.");
                return;
            }
            var parent = wallSouth.transform.parent;
            var wallMat = wallSouth.GetComponent<Renderer>().sharedMaterial;

            var existing = GameObject.Find("GuardRoom_Doorway_Lintel");
            var lintel = existing != null ? existing : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (existing == null)
            {
                lintel.name = "GuardRoom_Doorway_Lintel";
                Object.DestroyImmediate(lintel.GetComponent<Collider>());
            }
            lintel.transform.SetParent(parent, false);
            lintel.transform.position = new Vector3(-HalfWidth - WallThickness / 2f, (GuardRoomHeight + Height) / 2f, (DoorZMin + DoorZMax) / 2f);
            lintel.transform.localScale = new Vector3(WallThickness, Height - GuardRoomHeight, DoorZMax - DoorZMin);
            lintel.GetComponent<Renderer>().sharedMaterial = wallMat;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AddGuardRoomLintel] Lintel added above guard room doorway. Scene saved.");
        }
    }
}
