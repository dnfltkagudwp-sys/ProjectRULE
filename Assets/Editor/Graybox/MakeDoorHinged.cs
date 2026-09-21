using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Restructures the inspection door from a flat slab centered in its
    // opening into a real hinge + leaf: an empty pivot at the door's edge,
    // with the door mesh as its child offset by half the door's width. A
    // static centered slab reads as "a door-shaped object placed in a hole"
    // rather than an actual door -- hinging it lets it swing open/closed
    // like the real thing, which the patrol anomaly system also needs
    // later (Anomaly_InspectionDoorAjar / Anomaly_InspectionDoorWideOpen).
    //
    // Uses SetParent(hinge, worldPositionStays: true) to reparent the
    // existing door GameObject, so its fileID (and therefore any existing
    // PatrolSceneBindings serialized reference to it) stays intact --
    // only its place in the hierarchy and local transform change.
    public static class MakeDoorHinged
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        // Door leaf spans z 8.35..9.65 (openZCenter 9 +/- halfWidth 0.65).
        // Handle reads on the south (-Z) side of the door face in renders,
        // so the hinge sits on the opposite (north, +Z) edge at z=9.65.
        private const float HingeZ = 9.65f;
        private const float HalfWidth = 7f;
        private const float WallThickness = 0.2f;
        private const float DoorThickness = 0.08f;

        [MenuItem("RuleGhost/Graybox/Make Door Hinged")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var door = GameObject.Find("CheckPoint_InspectionDoor");
            if (door == null)
            {
                Debug.LogError("[MakeDoorHinged] Could not find CheckPoint_InspectionDoor.");
                return;
            }

            var existingHinge = GameObject.Find("CheckPoint_InspectionDoor_Hinge");
            if (existingHinge != null)
            {
                Debug.Log("[MakeDoorHinged] Hinge already exists, re-using it.");
            }

            var originalParent = door.transform.parent;
            float hingeX = door.transform.position.x;

            GameObject hinge = existingHinge != null ? existingHinge : new GameObject("CheckPoint_InspectionDoor_Hinge");
            hinge.transform.SetParent(originalParent, false);
            hinge.transform.localPosition = new Vector3(hingeX, 0f, HingeZ);
            hinge.transform.localRotation = Quaternion.identity;

            // worldPositionStays=true keeps the door exactly where it
            // currently sits (closed) while re-expressing its transform
            // relative to the new hinge pivot -- no manual offset math.
            door.transform.SetParent(hinge.transform, true);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MakeDoorHinged] Door leaf hinged at z={HingeZ}. Closed-position local transform: pos={door.transform.localPosition}, rot={door.transform.localEulerAngles}. Scene saved.");
        }

        // Sets the hinge's open angle for visual testing (0 = closed).
        // Positive/negative sign was determined empirically by rendering
        // both and checking which direction swings into the room instead
        // of clipping into the wall.
        [MenuItem("RuleGhost/Graybox/Test - Open Door 90")]
        public static void TestOpen90() => SetAngle(90f);

        [MenuItem("RuleGhost/Graybox/Test - Open Door Ajar30")]
        public static void TestAjar30() => SetAngle(30f);

        [MenuItem("RuleGhost/Graybox/Test - Close Door")]
        public static void TestClose() => SetAngle(0f);

        private static void SetAngle(float angle)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var hinge = GameObject.Find("CheckPoint_InspectionDoor_Hinge");
            if (hinge == null)
            {
                Debug.LogError("[MakeDoorHinged] No hinge found -- run Make Door Hinged first.");
                return;
            }
            hinge.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MakeDoorHinged] Hinge angle set to {angle}. Scene saved.");
        }
    }
}
