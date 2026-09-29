using RuleGhost.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off check for DoorTestInteraction.IsLookingAtDoor on the live guard room door: looking
    // straight at the leaf must pass; looking away, or at a wall/floor from the same spot, must not.
    public static class GuardDoorGazeTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Tests/Guard Door Gaze")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var hinge = GameObject.Find("GuardRoom_Door_Hinge");
            var leaf = GameObject.Find("GuardRoom_Door_Leaf");
            if (hinge == null || leaf == null)
            {
                Debug.LogError("[GuardDoorGazeTest] Door objects not found.");
                return;
            }
            var leafCol = leaf.GetComponent<Collider>();
            Debug.Log($"[GuardDoorGazeTest] leaf collider={(leafCol != null)} isTrigger={leafCol?.isTrigger} leafPos={leaf.transform.position} hingePos={hinge.transform.position}");

            Physics.SyncTransforms();
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();

            // Eye 1.5m east of the leaf (lobby side), looking straight at it.
            Vector3 leafPos = leaf.transform.position;
            camGO.transform.position = leafPos + new Vector3(1.5f, 0.35f, 0f);
            camGO.transform.LookAt(leafPos);
            Debug.Log($"[GuardDoorGazeTest] look at leaf   -> {DoorTestInteraction.IsLookingAtDoor(cam, leafCol, 3.5f)} (expect True)");

            // Same spot, turned away (west->east, opposite direction).
            camGO.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Debug.Log($"[GuardDoorGazeTest] look away      -> {DoorTestInteraction.IsLookingAtDoor(cam, leafCol, 3.5f)} (expect False)");

            // Same spot, looking at the floor.
            camGO.transform.rotation = Quaternion.LookRotation(Vector3.down);
            Debug.Log($"[GuardDoorGazeTest] look at floor  -> {DoorTestInteraction.IsLookingAtDoor(cam, leafCol, 3.5f)} (expect False)");

            // Same spot, looking well off to the side along the wall.
            camGO.transform.rotation = Quaternion.LookRotation(new Vector3(-0.2f, 0f, 1f));
            Debug.Log($"[GuardDoorGazeTest] look along wall-> {DoorTestInteraction.IsLookingAtDoor(cam, leafCol, 3.5f)} (expect False)");

            Object.DestroyImmediate(camGO);
        }
    }
}
