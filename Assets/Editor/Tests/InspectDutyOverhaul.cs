using RuleGhost.Anomalies;
using RuleGhost.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Read-only structural check for the AM1/AM5 Duty overhaul -- confirms the rebuilt data
    // assets and scene wiring actually match what PatrolEvaluator/PatrolRuntimeController now
    // expect, without needing Play mode.
    public static class InspectDutyOverhaul
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Tests/Inspect Duty Overhaul")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var dutyAm1 = AssetDatabase.LoadAssetAtPath<PatrolDuty>("Assets/Data/Anomalies/Duty_AM1.asset");
            var dutyAm5 = AssetDatabase.LoadAssetAtPath<PatrolDuty>("Assets/Data/Anomalies/Duty_AM5.asset");

            Debug.Log($"[InspectDutyOverhaul] Duty_AM1: slot={dutyAm1.Slot}, requiredCount={dutyAm1.RequiredActions.Count} " +
                      $"(expect 12: 9 ObservePainting + Thermometer + InspectionDoor + GuardRoomReturn), " +
                      $"ruleText.Length={dutyAm1.RuleText?.Length ?? 0}");
            int observeCount = 0;
            foreach (var req in dutyAm1.RequiredActions)
            {
                if (req.Action == ActionTag.ObservePainting) observeCount++;
            }
            Debug.Log($"[InspectDutyOverhaul] Duty_AM1 ObservePainting entries: {observeCount} (expect 9)");

            Debug.Log($"[InspectDutyOverhaul] Duty_AM5: slot={dutyAm5.Slot}, requiredCount={dutyAm5.RequiredActions.Count} " +
                      $"(expect 3: Thermometer + InspectionDoor + EntranceDoor), ruleText.Length={dutyAm5.RuleText?.Length ?? 0}");

            var triggerGO = GameObject.Find("GuardRoomReturnTrigger");
            var collider = triggerGO != null ? triggerGO.GetComponent<BoxCollider>() : null;
            var triggerComp = triggerGO != null ? triggerGO.GetComponent<GuardRoomReturnTrigger>() : null;
            Debug.Log($"[InspectDutyOverhaul] GuardRoomReturnTrigger found: {triggerGO != null}, " +
                      $"BoxCollider.isTrigger: {(collider != null ? collider.isTrigger.ToString() : "N/A")}, " +
                      $"has GuardRoomReturnTrigger component: {triggerComp != null}");

            var bindingsGO = GameObject.Find("PatrolSceneBindings");
            var bindings = bindingsGO != null ? bindingsGO.GetComponent<PatrolSceneBindings>() : null;
            if (bindings != null)
            {
                var resolved = bindings.Resolve(TargetRef.Simple(TargetKind.GuardRoomReturn));
                Debug.Log($"[InspectDutyOverhaul] PatrolSceneBindings.Resolve(GuardRoomReturn) -> " +
                          $"{(resolved != null ? resolved.name : "NULL")}");
            }
            else
            {
                Debug.LogError("[InspectDutyOverhaul] PatrolSceneBindings not found.");
            }

            var controllerGO = GameObject.Find("PatrolRuntimeController");
            Debug.Log($"[InspectDutyOverhaul] PatrolRuntimeController found: {controllerGO != null}");

            var hinge = GameObject.Find("CheckPoint_InspectionDoor_Hinge");
            var leftoverToggle = hinge != null ? hinge.GetComponent<DoorTestInteraction>() : null;
            Debug.Log($"[InspectDutyOverhaul] Leftover DoorTestInteraction on inspection door hinge " +
                      $"(should be False): {leftoverToggle != null}");

            Debug.Log($"[InspectDutyOverhaul] Duty_AM1.RuleNumber={dutyAm1.RuleNumber} (expect 1), " +
                      $"Duty_AM5.RuleNumber={dutyAm5.RuleNumber} (expect 2)");

            var profiles = new[]
            {
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_01_AM1.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_02_AM5.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_03_AM1.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_04_AM5.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_05_AM1.asset"),
                AssetDatabase.LoadAssetAtPath<PatrolProfile>("Assets/Data/Anomalies/Patrol_06_AM5.asset")
            };
            string fullRulebook = RulebookTextBuilder.BuildAll(profiles);
            int ruleBlockCount = fullRulebook.Split(new[] { "\n\n" }, System.StringSplitOptions.None).Length;
            Debug.Log($"[InspectDutyOverhaul] RulebookTextBuilder.BuildAll produced {ruleBlockCount} rule blocks " +
                      $"(expect 9), total length={fullRulebook.Length}");
            Debug.Log($"[InspectDutyOverhaul] Full rulebook text:\n{fullRulebook}");
        }
    }
}
