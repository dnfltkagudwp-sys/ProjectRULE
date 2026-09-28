using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off resize of the already-diverged live scene: 14x20m -> 18x26m (HalfWidth 7->9,
    // HalfDepth 10->13), side-wall painting spacing 5m -> 6.5m apart. Deliberately NOT a re-run of
    // LobbyGrayboxBuilder.Build() -- that starts a brand new scene and would wipe everything added
    // since (imported desk/chair models, the entrance door, the death-sequence textures, dramatic
    // lighting, ...). Instead this repositions/rescales the specific objects LobbyGrayboxBuilder
    // originally created, by exact hand-derived coordinates, and leaves everything else alone.
    //
    // The room grows symmetrically about the origin on both axes (HalfWidth/HalfDepth both used as
    // +/- from center), so:
    //   deltaWidth  = +2  (west side objects shift -2, east side objects shift +2)
    //   deltaNorth  = +3  (objects anchored to the north/+Z wall shift +3)
    //   deltaSouth  = -3  (objects anchored to the south/-Z wall -- including the entrance and the
    //                      guard room, which sits just outside the west wall near the south end --
    //                      shift -3, so they keep the same distance from the (now farther away)
    //                      entrance wall instead of ending up inside the bigger room)
    //
    // Run once. Afterward, re-run (in order): AttachGuardRoomReturnTrigger, AddMuseumDetails,
    // ApplyDramaticLighting, ApplyReflectionProbe, AddPaintingFrames -- each of those already
    // destroys-and-rebuilds its own output from live transforms/updated constants, so they pick up
    // this resize automatically once their own hardcoded literals are updated to match.
    public static class ResizeLobby
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        private const float OldHalfWidth = 7f;
        private const float OldHalfDepth = 10f;
        private const float NewHalfWidth = 9f;
        private const float NewHalfDepth = 13f;
        private const float Height = 5f;
        private const float GuardRoomHeight = 3f;

        [MenuItem("RuleGhost/Graybox/Resize Lobby (14x20 -> 18x26)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ResizeStructure();
            ResizePaintings();
            ResizeCheckpoints();
            TranslateGroup("GuardRoom", new Vector3(-2f, 0f, -3f));
            TranslateGroup("GuardRoomFurnishing", new Vector3(-2f, 0f, -3f));
            TranslateGroup("EntranceDoor", new Vector3(0f, 0f, -3f));
            RepositionPlayerSpawn();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ResizeLobby] Core structure resized to 18x26m. Now re-run (in order): " +
                      "Attach Guard Room Return Trigger, Add Museum Details, Apply Dramatic Lighting, " +
                      "Apply Reflection Probe, Add Painting Frames.");
        }

        private static void ResizeStructure()
        {
            SetLocal("Floor", scale: new Vector3(NewHalfWidth * 2f, 0.2f, NewHalfDepth * 2f));
            SetLocal("Ceiling", scale: new Vector3(NewHalfWidth * 2f, 0.2f, NewHalfDepth * 2f));

            SetLocal("Wall_West_South", pos: new Vector3(-9.1f, 2.5f, -12.75f));
            SetLocal("Wall_West_North", pos: new Vector3(-9.1f, 2.5f, 1.75f), scale: new Vector3(0.2f, 5f, 22.5f));
            SetLocal("GuardRoom_Doorway_Lintel", pos: new Vector3(-9.1f, (GuardRoomHeight + Height) / 2f, -11f));

            SetLocal("Wall_East_South", pos: new Vector3(9.1f, 2.5f, -0.85f), scale: new Vector3(0.2f, 5f, 24.3f));
            SetLocal("Wall_East_North", pos: new Vector3(9.1f, 2.5f, 12.85f));
            SetLocal("Wall_East_AboveDoor", pos: new Vector3(9.1f, 3.8f, 12f));

            SetLocal("Wall_North_Inner", pos: new Vector3(0f, 2.5f, 13.1f), scale: new Vector3(NewHalfWidth * 2f, 5f, 0.2f));

            // Width was left at its old 5.5m (matching the old HalfWidth=7 corner-to-gap span) --
            // the wall's outer edge stayed at the old +/-7 corner while the west/east walls moved
            // out to +/-9, leaving a 2m gap on each side. Widened to span the new corner (+/-9) to
            // the same 3m entrance gap (+/-1.5), so it meets the new wall corners again.
            SetLocal("Wall_South_Left", pos: new Vector3(-5.25f, 2.5f, -13.1f), scale: new Vector3(7.5f, 5f, 0.2f));
            SetLocal("Wall_South_Right", pos: new Vector3(5.25f, 2.5f, -13.1f), scale: new Vector3(7.5f, 5f, 0.2f));
        }

        private static void ResizePaintings()
        {
            SetLocal("Painting_North_1", pos: new Vector3(-4f, 2.35f, NewHalfDepth - 0.05f));
            SetLocal("Painting_North_2", pos: new Vector3(0f, 2.35f, NewHalfDepth - 0.05f));
            SetLocal("Painting_North_3", pos: new Vector3(4f, 2.35f, NewHalfDepth - 0.05f));

            float[] sideZ = { -6.5f, 0f, 6.5f };
            for (int i = 0; i < sideZ.Length; i++)
            {
                SetLocal($"Painting_West_{i + 1}", pos: new Vector3(-NewHalfWidth + 0.05f, 2.35f, sideZ[i]));
                SetLocal($"Painting_East_{i + 1}", pos: new Vector3(NewHalfWidth - 0.05f, 2.35f, sideZ[i]));
            }
        }

        private static void ResizeCheckpoints()
        {
            SetLocal("CheckPoint_ThermoHygrometer", pos: new Vector3(-6f, 2.2f, NewHalfDepth - 0.1f));
            SetLocal("CheckPoint_InspectionDoor_Frame", pos: new Vector3(NewHalfWidth + 0.1f, 1.3f, 12f));
            SetLocal("CheckPoint_InspectionDoor_Hinge", pos: new Vector3(NewHalfWidth - 0.1f - 0.04f, 0f, 12.65f));
            SetLocal("CheckPoint_Entrance", pos: new Vector3(0f, 0f, -NewHalfDepth));
            SetLocal("CheckPoint_Entrance_FloorMark", pos: new Vector3(0f, 0.03f, -NewHalfDepth + 0.3f));
        }

        private static void RepositionPlayerSpawn()
        {
            var player = GameObject.Find("Player_TestController");
            if (player != null)
            {
                var pos = player.transform.position;
                player.transform.position = new Vector3(pos.x, pos.y, -NewHalfDepth + 1f);
            }
        }

        private static void TranslateGroup(string rootName, Vector3 delta)
        {
            var go = GameObject.Find(rootName);
            if (go == null)
            {
                Debug.LogWarning($"[ResizeLobby] Could not find '{rootName}' to translate -- skipping.");
                return;
            }
            go.transform.position += delta;
        }

        private static void SetLocal(string name, Vector3? pos = null, Vector3? scale = null)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[ResizeLobby] Could not find '{name}' -- skipping.");
                return;
            }
            if (pos.HasValue) go.transform.localPosition = pos.Value;
            if (scale.HasValue) go.transform.localScale = scale.Value;
        }
    }
}
