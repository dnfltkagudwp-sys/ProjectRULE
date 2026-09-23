using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Owns the round's gaze/facing judgment: every frame during PatrolActive, PatrolRuntimeController
    // calls Tick() with the round's active anomalies, and this checks each one's observation-based
    // required/forbidden action (GazeSensor for MakeEyeContact, FacingSensor for TurnAwayFromExhibit/
    // FaceExhibit/ShowBackToExhibit) against the player's current camera. A rule only counts once it
    // holds continuously for its dwell threshold -- a single frame of clipping through the angle/cone
    // boundary (mouse jitter, a glance in passing) shouldn't flip a verdict. Once an anomaly's
    // required action is satisfied or its forbidden action is violated, that's locked in for the
    // round (mirrors how PatrolEvaluator treats a forbidden visit -- it doesn't get "undone").
    //
    // Visit-based judgment (PatrolProgress/PatrolEvaluator's existing required/forbidden targets)
    // stays completely separate from this -- this only covers the action tags that need continuous
    // camera observation instead of a discrete E-key event.
    public class ObservationRuleMonitor
    {
        // Between the flashlight's inner (10 half-angle) and outer (20 half-angle) cone -- without
        // a crosshair, holding dead-center on a specific point in the dark for a sustained dwell is
        // harder in practice than it looks on paper, so this leans toward the more forgiving side
        // rather than strictly matching the inner beam. Public so ObservationDebugOverlay's live
        // readings use the exact same thresholds as the real judgment instead of a copy that can
        // silently drift out of sync.
        public const float EyeContactConeHalfAngleDegrees = 15f;
        public const float MaxGazeDistance = 4.5f;
        public const float EyeContactDwellSeconds = 0.3f;

        public const float TowardAngleThreshold = 60f;
        public const float AwayAngleThreshold = 120f;
        public const float FrontHemisphereAngleThreshold = 90f;
        // Generous on purpose -- the lobby's own diagonal is ~24m, and a facing rule only needs
        // "reasonably in the room and not blocked by a wall/the center statue," not precise range.
        public const float MaxFacingDistance = 15f;
        // Confirmed via playtest to feel right at 2s -- a facing rule (TurnAwayFromExhibit,
        // FaceExhibit, ShowBackToExhibit) needs a real, sustained turn, not a quick glance.
        public const float FacingDwellSeconds = 2f;

        private readonly Dictionary<string, float> eyeContactDwell = new();
        private readonly Dictionary<string, float> requiredFacingDwell = new();
        private readonly Dictionary<string, float> forbiddenFacingDwell = new();
        private readonly HashSet<string> satisfiedRequired = new();
        private readonly HashSet<string> violatedForbidden = new();

        // Direct read-only view (no allocation) so PatrolRuntimeController can notice a violation
        // the same frame it happens, for the immediate-fail overlay -- BuildReport()'s copies are
        // for the end-of-round Evaluate() pass, not for cheap per-frame polling.
        public IReadOnlyCollection<string> ViolatedForbiddenIds => violatedForbidden;

        // Same idea, the "required just got satisfied" counterpart -- lets PatrolRuntimeController
        // trigger a required action's own visual payoff (e.g. PersonInLandscape's texture revert)
        // the instant it's earned, not just report it at round end.
        public IReadOnlyCollection<string> SatisfiedRequiredIds => satisfiedRequired;

        public void ResetAll()
        {
            eyeContactDwell.Clear();
            requiredFacingDwell.Clear();
            forbiddenFacingDwell.Clear();
            satisfiedRequired.Clear();
            violatedForbidden.Clear();
        }

        public void Tick(float deltaTime, Camera camera, Transform playerRoot, PatrolSceneBindings bindings,
            IReadOnlyList<ResolvedAnomaly> anomalies)
        {
            if (camera == null || bindings == null || anomalies == null)
            {
                return;
            }

            foreach (var anomaly in anomalies)
            {
                TickEyeContact(deltaTime, camera, playerRoot, bindings, anomaly);
                TickRequiredFacing(deltaTime, camera, playerRoot, bindings, anomaly);
                TickForbiddenFacing(deltaTime, camera, playerRoot, bindings, anomaly);
            }
        }

        private void TickEyeContact(float deltaTime, Camera camera, Transform playerRoot,
            PatrolSceneBindings bindings, ResolvedAnomaly anomaly)
        {
            if (violatedForbidden.Contains(anomaly.Id))
            {
                return;
            }

            foreach (var forbid in anomaly.ForbiddenActions)
            {
                if (forbid.Action != ActionTag.MakeEyeContact)
                {
                    continue;
                }

                var painting = bindings.Resolve(forbid.Target);
                var anchor = painting != null ? painting.Find("EyeAnchor") : null;
                bool inCone = anchor != null && GazeSensor.IsWithinGazeCone(
                    camera, painting, anchor, playerRoot, MaxGazeDistance, EyeContactConeHalfAngleDegrees);

                float dwell = eyeContactDwell.GetValueOrDefault(anomaly.Id, 0f);
                dwell = inCone ? dwell + deltaTime : 0f;
                eyeContactDwell[anomaly.Id] = dwell;

                if (dwell >= EyeContactDwellSeconds)
                {
                    violatedForbidden.Add(anomaly.Id);
                }

                return; // at most one MakeEyeContact entry per anomaly currently
            }
        }

        private void TickRequiredFacing(float deltaTime, Camera camera, Transform playerRoot,
            PatrolSceneBindings bindings, ResolvedAnomaly anomaly)
        {
            if (satisfiedRequired.Contains(anomaly.Id))
            {
                return;
            }

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
                bool angleHolds = mode == FacingMode.Toward ? angle <= TowardAngleThreshold : angle >= AwayAngleThreshold;
                // A wall or the center statue between the player and the target pauses the dwell --
                // it shouldn't be possible to satisfy (or violate) a facing rule through solid
                // geometry, the same way GazeSensor already guards against that for MakeEyeContact.
                bool holds = angleHolds && FacingSensor.HasClearLineOfSight(camera, target, playerRoot, MaxFacingDistance);

                float dwell = requiredFacingDwell.GetValueOrDefault(anomaly.Id, 0f);
                dwell = holds ? dwell + deltaTime : 0f;
                requiredFacingDwell[anomaly.Id] = dwell;

                if (dwell >= FacingDwellSeconds)
                {
                    satisfiedRequired.Add(anomaly.Id);
                }

                return; // at most one facing-mode required entry per anomaly currently
            }
        }

        private void TickForbiddenFacing(float deltaTime, Camera camera, Transform playerRoot,
            PatrolSceneBindings bindings, ResolvedAnomaly anomaly)
        {
            if (violatedForbidden.Contains(anomaly.Id))
            {
                return;
            }

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
                bool violating = angle >= FrontHemisphereAngleThreshold &&
                    FacingSensor.HasClearLineOfSight(camera, target, playerRoot, MaxFacingDistance);

                float dwell = forbiddenFacingDwell.GetValueOrDefault(anomaly.Id, 0f);
                dwell = violating ? dwell + deltaTime : 0f;
                forbiddenFacingDwell[anomaly.Id] = dwell;

                if (dwell >= FacingDwellSeconds)
                {
                    violatedForbidden.Add(anomaly.Id);
                }

                return; // at most one ShowBackToExhibit entry per anomaly currently
            }
        }

        public ObservationReport BuildReport() => new ObservationReport
        {
            SatisfiedRequiredAnomalyIds = new HashSet<string>(satisfiedRequired),
            ViolatedForbiddenAnomalyIds = new HashSet<string>(violatedForbidden)
        };
    }
}
