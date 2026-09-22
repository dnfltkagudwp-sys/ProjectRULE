using RuleGhost.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Attaches ObservationDebugOverlay next to PatrolRuntimeController so gaze/facing anomaly
    // rules (MakeEyeContact, TurnAwayFromExhibit, FaceExhibit, ShowBackToExhibit) can be diagnosed
    // from the console during play -- see that script for what it logs and why.
    public static class AttachObservationDebugOverlay
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Anomalies/Attach Observation Debug Overlay")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var controllerGO = GameObject.Find("PatrolRuntimeController");
            if (controllerGO == null)
            {
                Debug.LogError("[AttachObservationDebugOverlay] Could not find PatrolRuntimeController -- " +
                                "run RuleGhost/Anomalies/Attach Patrol Runtime first.");
                return;
            }

            if (controllerGO.GetComponent<ObservationDebugOverlay>() == null)
            {
                controllerGO.AddComponent<ObservationDebugOverlay>();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AttachObservationDebugOverlay] ObservationDebugOverlay attached. Scene saved.");
        }
    }
}
