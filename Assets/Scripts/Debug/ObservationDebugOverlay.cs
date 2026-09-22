using System.Collections.Generic;
using UnityEngine;
using RuleGhost.Anomalies;

namespace RuleGhost.Debugging
{
    // Graybox-only diagnostic for the gaze/facing anomaly rules (MakeEyeContact, TurnAwayFromExhibit,
    // FaceExhibit, ShowBackToExhibit). Logs only on state changes (entered/left the cone or angle
    // band, or a dwell threshold was reached) instead of every frame or on a timer -- a continuous
    // per-tick dump is unreadable while actually playing. Reading the log:
    //  - nothing at all -> no anomaly with a gaze/facing action is active this round (check the
    //    round-start log's anomaly id list), or the camera/CharacterController lookup failed (see
    //    PatrolRuntimeController's own warning for that case).
    //  - "entered"/"left" pairs with no "CONFIRMED" between them -> aim is reaching the zone but
    //    not holding it long enough (compare the held duration printed on "left" to the dwell
    //    threshold also printed).
    //  - never even "entered" -> aiming/geometry issue, not a dwell/timing one.
    public class ObservationDebugOverlay : MonoBehaviour
    {
        private class RuleState
        {
            public bool Active;
            public float EnteredAt;
            public bool Announced;
        }

        private readonly Dictionary<string, RuleState> states = new();

        private void Update()
        {
            var controller = PatrolRuntimeController.Instance;
            if (controller == null || controller.CurrentState != PatrolRuntimeController.State.PatrolActive)
            {
                return;
            }

            var bindings = controller.SceneBindings;
            var anomalies = controller.CurrentAnomalies;
            if (bindings == null || anomalies == null || anomalies.Count == 0)
            {
                return;
            }

            var characterController = FindFirstObjectByType<CharacterController>();
            var camera = characterController != null ? characterController.GetComponentInChildren<Camera>() : null;
            if (camera == null)
            {
                return;
            }

            var playerRoot = characterController.transform;
            foreach (var anomaly in anomalies)
            {
                CheckEyeContact(anomaly, bindings, camera, playerRoot);
                CheckRequiredFacing(anomaly, bindings, camera);
                CheckForbiddenFacing(anomaly, bindings, camera);
            }
        }

        private void CheckEyeContact(ResolvedAnomaly anomaly, PatrolSceneBindings bindings, Camera camera, Transform playerRoot)
        {
            foreach (var forbid in anomaly.ForbiddenActions)
            {
                if (forbid.Action != ActionTag.MakeEyeContact)
                {
                    continue;
                }

                var painting = bindings.Resolve(forbid.Target);
                var anchor = painting != null ? painting.Find("EyeAnchor") : null;
                if (anchor == null)
                {
                    LogOnce($"missing-anchor:{anomaly.Id}",
                        $"[ObservationDebugOverlay] {anomaly.Id}: target has no EyeAnchor child -- run RuleGhost/Anomalies/Add Eye Anchors.");
                    return;
                }

                bool inCone = GazeSensor.IsWithinGazeCone(camera, painting, anchor, playerRoot,
                    ObservationRuleMonitor.MaxGazeDistance, ObservationRuleMonitor.EyeContactConeHalfAngleDegrees);
                UpdateState($"gaze:{anomaly.Id}", inCone, ObservationRuleMonitor.EyeContactDwellSeconds,
                    $"{anomaly.Id}: eye contact", "CONFIRMED (violation)");
                return;
            }
        }

        private void CheckRequiredFacing(ResolvedAnomaly anomaly, PatrolSceneBindings bindings, Camera camera)
        {
            foreach (var req in anomaly.RequiredActions)
            {
                FacingMode? mode = req.Action switch
                {
                    ActionTag.TurnAwayFromExhibit => FacingMode.Away,
                    ActionTag.FaceExhibit => FacingMode.Toward,
                    _ => null
                };
                if (mode == null)
                {
                    continue;
                }

                var target = bindings.Resolve(req.Target);
                if (target == null)
                {
                    return;
                }

                float angle = FacingSensor.HorizontalAngleToTarget(camera, target);
                bool holds = mode == FacingMode.Toward
                    ? angle <= ObservationRuleMonitor.TowardAngleThreshold
                    : angle >= ObservationRuleMonitor.AwayAngleThreshold;

                UpdateState($"reqFacing:{anomaly.Id}", holds, ObservationRuleMonitor.FacingDwellSeconds,
                    $"{anomaly.Id}: {req.Action} (required)", "CONFIRMED (satisfied)");
                return;
            }
        }

        private void CheckForbiddenFacing(ResolvedAnomaly anomaly, PatrolSceneBindings bindings, Camera camera)
        {
            foreach (var forbid in anomaly.ForbiddenActions)
            {
                if (forbid.Action != ActionTag.ShowBackToExhibit)
                {
                    continue;
                }

                var target = bindings.Resolve(forbid.Target);
                if (target == null)
                {
                    return;
                }

                float angle = FacingSensor.HorizontalAngleToTarget(camera, target);
                bool violating = angle >= ObservationRuleMonitor.FrontHemisphereAngleThreshold;

                UpdateState($"forbidFacing:{anomaly.Id}", violating, ObservationRuleMonitor.FacingDwellSeconds,
                    $"{anomaly.Id}: ShowBackToExhibit (forbidden)", "CONFIRMED (violation)");
                return;
            }
        }

        // Logs only the moment a rule's condition starts holding, the moment it stops (with how
        // long it held vs. the dwell threshold), and the moment it's actually confirmed -- never a
        // per-frame value.
        private void UpdateState(string key, bool active, float dwellThreshold, string label, string confirmedLabel)
        {
            if (!states.TryGetValue(key, out var state))
            {
                state = new RuleState();
                states[key] = state;
            }

            if (active && !state.Active)
            {
                state.Active = true;
                state.Announced = false;
                state.EnteredAt = Time.time;
                Debug.Log($"[ObservationDebugOverlay] {label}: entered (needs {dwellThreshold:F2}s held).");
            }
            else if (!active && state.Active)
            {
                state.Active = false;
                float held = Time.time - state.EnteredAt;
                Debug.Log($"[ObservationDebugOverlay] {label}: left after {held:F2}s (needed {dwellThreshold:F2}s).");
            }
            else if (active && !state.Announced && Time.time - state.EnteredAt >= dwellThreshold)
            {
                state.Announced = true;
                Debug.Log($"[ObservationDebugOverlay] {label}: {confirmedLabel}.");
            }
        }

        private readonly HashSet<string> loggedOnce = new();

        private void LogOnce(string key, string message)
        {
            if (loggedOnce.Add(key))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
