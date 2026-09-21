using UnityEditor;
using UnityEditor.SceneManagement;
using RuleGhost.Debugging;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Adds the walk-up-and-press-E test toggle to the inspection door hinge
    // so the door's open/close feel can be tried directly in Play mode.
    public static class AttachDoorTestInteraction
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Graybox/Attach Door Test Interaction")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var hinge = GameObject.Find("CheckPoint_InspectionDoor_Hinge");
            if (hinge == null)
            {
                Debug.LogError("[AttachDoorTestInteraction] Could not find CheckPoint_InspectionDoor_Hinge -- run Make Door Hinged first.");
                return;
            }

            if (hinge.GetComponent<DoorTestInteraction>() == null)
            {
                hinge.AddComponent<DoorTestInteraction>();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AttachDoorTestInteraction] DoorTestInteraction attached to hinge. Scene saved. Walk within 2.5m of the door in Play mode and press E.");
        }
    }
}
