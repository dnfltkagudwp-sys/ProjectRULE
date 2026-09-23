using RuleGhost.Anomalies;
using RuleGhost.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Wires the patrol runtime loop into the already-built Lobby_Graybox scene: a
    // PatrolInteractable on each of the 9 paintings + thermometer + inspection door + entrance
    // marker, and a PatrolRuntimeController configured with the same 6 PatrolProfile assets and
    // CombinationRuleSet that PatrolTestHarness already uses. Operates on the live scene rather
    // than going through LobbyGrayboxBuilder, matching this project's established pattern of not
    // re-running the from-scratch builder once other work is layered on top of the scene.
    public static class AttachPatrolRuntime
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float InteractRange = 3.5f;

        [MenuItem("RuleGhost/Anomalies/Attach Patrol Runtime")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            AttachPainting("Painting_North_1", PaintingWall.North, 1);
            AttachPainting("Painting_North_2", PaintingWall.North, 2);
            AttachPainting("Painting_North_3", PaintingWall.North, 3);
            AttachPainting("Painting_West_1", PaintingWall.West, 1);
            AttachPainting("Painting_West_2", PaintingWall.West, 2);
            AttachPainting("Painting_West_3", PaintingWall.West, 3);
            AttachPainting("Painting_East_1", PaintingWall.East, 1);
            AttachPainting("Painting_East_2", PaintingWall.East, 2);
            AttachPainting("Painting_East_3", PaintingWall.East, 3);
            AttachSimple("CheckPoint_ThermoHygrometer", TargetKind.Thermometer);
            AttachSimple("CheckPoint_InspectionDoor", TargetKind.InspectionDoor);
            AttachSimple("CheckPoint_Entrance", TargetKind.EntranceDoor);
            RemoveInspectionDoorTestToggle();

            var bindingsGO = GameObject.Find("PatrolSceneBindings");
            var parent = bindingsGO != null ? bindingsGO.transform.parent : null;
            var sceneBindings = bindingsGO != null ? bindingsGO.GetComponent<PatrolSceneBindings>() : null;
            if (sceneBindings == null)
            {
                Debug.LogError("[AttachPatrolRuntime] Could not find PatrolSceneBindings in the scene.");
                return;
            }

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
                Debug.LogError("[AttachPatrolRuntime] Anomaly data set not found under Assets/Data/Anomalies.");
                return;
            }

            var controllerGO = GameObject.Find("PatrolRuntimeController");
            if (controllerGO == null)
            {
                controllerGO = new GameObject("PatrolRuntimeController");
                controllerGO.transform.SetParent(parent, false);
            }
            var controller = controllerGO.GetComponent<PatrolRuntimeController>();
            if (controller == null)
            {
                controller = controllerGO.AddComponent<PatrolRuntimeController>();
            }
            controller.EditorConfigure(profiles, ruleSet, sceneBindings);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AttachPatrolRuntime] PatrolInteractable attached to 12 targets, PatrolRuntimeController configured. Scene saved.");
        }

        private static void AttachPainting(string name, PaintingWall wall, int index)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogError($"[AttachPatrolRuntime] Could not find {name}.");
                return;
            }
            var interactable = go.GetComponent<PatrolInteractable>();
            if (interactable == null) interactable = go.AddComponent<PatrolInteractable>();
            interactable.EditorConfigure(TargetKind.SpecificPainting, wall, index);
            interactable.EditorSetInteractRange(InteractRange);
        }

        // The inspection door's baseline "확인" (InspectInspectionDoor) is now a gaze-only check
        // (see LooseObservationTracker) specifically because this leftover graybox toggle
        // (AttachDoorTestInteraction) makes an E-key press pop a closed door open as a side
        // effect. Real gameplay only ever needs E on this door for CloseInspectionDoorFully (when
        // InspectionDoorAjar is actually active), which PatrolInteractable already covers without
        // this toggle -- remove it so the two don't fight over the same key press.
        private static void RemoveInspectionDoorTestToggle()
        {
            var hinge = GameObject.Find("CheckPoint_InspectionDoor_Hinge");
            var toggle = hinge != null ? hinge.GetComponent<DoorTestInteraction>() : null;
            if (toggle != null)
            {
                Object.DestroyImmediate(toggle);
                Debug.Log("[AttachPatrolRuntime] Removed leftover DoorTestInteraction from CheckPoint_InspectionDoor_Hinge.");
            }
        }

        private static void AttachSimple(string name, TargetKind kind)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogError($"[AttachPatrolRuntime] Could not find {name}.");
                return;
            }
            var interactable = go.GetComponent<PatrolInteractable>();
            if (interactable == null) interactable = go.AddComponent<PatrolInteractable>();
            interactable.EditorConfigure(kind);
            interactable.EditorSetInteractRange(InteractRange);
        }
    }
}
