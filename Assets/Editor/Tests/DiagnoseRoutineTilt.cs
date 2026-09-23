using RuleGhost.Anomalies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // One-off diagnostic: actually enters Play mode (batch-mode EditMode tests never run
    // MonoBehaviour Awake/Start/Update at all, so RoutinePatrolState.RollForRound being called
    // from PatrolRuntimeController.Start has never actually been exercised end-to-end until this),
    // lets a few frames tick so Patrol 1 starts for real, logs every painting's rotation, then
    // exits. Survives the domain reload that entering Play mode triggers via SessionState +
    // [InitializeOnLoad] re-arming the update callback.
    [InitializeOnLoad]
    public static class DiagnoseRoutineTilt
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string ArmedKey = "DiagnoseRoutineTilt_Armed";
        private const string FrameKey = "DiagnoseRoutineTilt_Frame";

        static DiagnoseRoutineTilt()
        {
            if (SessionState.GetBool(ArmedKey, false))
            {
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("RuleGhost/Tests/Diagnose Routine Tilt (Play Mode)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetInt(FrameKey, 0);
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                return;
            }

            int frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);

            if (frame < 15)
            {
                return;
            }

            EditorApplication.update -= Tick;
            SessionState.SetBool(ArmedKey, false);

            LogState();

            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        private static void LogState()
        {
            foreach (var wall in new[] { "North", "West", "East" })
            {
                for (int i = 1; i <= 3; i++)
                {
                    var go = GameObject.Find($"Painting_{wall}_{i}");
                    if (go == null)
                    {
                        Debug.LogError($"[DiagnoseRoutineTilt] Missing Painting_{wall}_{i}");
                        continue;
                    }
                    var e = go.transform.localRotation.eulerAngles;
                    Debug.Log($"[DiagnoseRoutineTilt] Painting_{wall}_{i} localEuler=({e.x:F1},{e.y:F1},{e.z:F1})");
                }
            }

            var controllerGO = GameObject.Find("PatrolRuntimeController");
            var controller = controllerGO != null ? controllerGO.GetComponent<PatrolRuntimeController>() : null;
            if (controller != null)
            {
                Debug.Log($"[DiagnoseRoutineTilt] CurrentState={controller.CurrentState}, " +
                          $"CurrentProfile={(controller.CurrentProfile != null ? controller.CurrentProfile.name : "NULL")}");
            }
            else
            {
                Debug.LogError("[DiagnoseRoutineTilt] PatrolRuntimeController not found at runtime.");
            }
        }
    }
}
