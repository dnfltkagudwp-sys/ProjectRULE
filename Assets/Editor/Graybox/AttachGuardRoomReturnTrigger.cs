using RuleGhost.Anomalies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Wires AM1's actual closing action (physically walking back into the guard room) into the
    // scene: a Trigger Collider covering most of the guard room's interior, with
    // GuardRoomReturnTrigger reporting TargetKind.GuardRoomReturn to PatrolRuntimeController on
    // entry, plus the PatrolSceneBindings link so PatrolEvaluator/RoutinePatrolState can resolve
    // that target. Operates on the live scene like AttachPatrolRuntime, and is safe to re-run --
    // it finds-or-creates its one GameObject and updates it in place rather than duplicating.
    public static class AttachGuardRoomReturnTrigger
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        // Guard room interior is roughly x in [-10.2,-7.1], z in [-9.5,-6.5] (3.2m square minus
        // 0.2m wall thickness, centered at (-8.7,-8) -- see DecorateGuardRoom/
        // GuardRoomFurnishingCheckTest). Sized/centered a bit inside those bounds so the trigger
        // needs an actual step into the room, not just standing at the doorway threshold (x=-7).
        private static readonly Vector3 TriggerCenter = new Vector3(-8.65f, 1f, -8f);
        private static readonly Vector3 TriggerSize = new Vector3(2.6f, 2f, 2.6f);

        [MenuItem("RuleGhost/Anomalies/Attach Guard Room Return Trigger")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var bindingsGO = GameObject.Find("PatrolSceneBindings");
            var bindings = bindingsGO != null ? bindingsGO.GetComponent<PatrolSceneBindings>() : null;
            if (bindings == null)
            {
                Debug.LogError("[AttachGuardRoomReturnTrigger] Could not find PatrolSceneBindings -- run Attach Patrol Runtime first.");
                return;
            }

            var triggerGO = GameObject.Find("GuardRoomReturnTrigger");
            if (triggerGO == null)
            {
                triggerGO = new GameObject("GuardRoomReturnTrigger");
            }
            triggerGO.transform.position = TriggerCenter;

            var collider = triggerGO.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = triggerGO.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;
            collider.size = TriggerSize;

            if (triggerGO.GetComponent<GuardRoomReturnTrigger>() == null)
            {
                triggerGO.AddComponent<GuardRoomReturnTrigger>();
            }

            bindings.ConfigureGuardRoomReturn(triggerGO.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AttachGuardRoomReturnTrigger] GuardRoomReturnTrigger placed and wired into PatrolSceneBindings. Scene saved.");
        }
    }
}
