using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Owns InspectionDoorWideOpen's own two-part behavior, kept separate from
    // ObservationRuleMonitor (that class is generic per-anomaly gaze/facing judgment) and from
    // PatrolEvaluator (that stays the end-of-round judge) so this one terminal anomaly's special
    // rule doesn't get folded into either general-purpose class:
    //
    //  1. Discovery: a 0.5s gaze at the inspection door confirms the player has actually noticed
    //     it's wide open -- before that moment, ordinary patrol actions (checking a painting, the
    //     thermometer, ...) are NOT a rule violation; the player hasn't been told anything is wrong
    //     yet. Reuses GazeSensor the same way LooseObservationTracker does for the door's own
    //     baseline "확인", just with its own (slightly stricter) dwell threshold, since this
    //     moment carries real weight -- it flips the whole round into Abort mode.
    //  2. Once discovered, any further checkpoint visited other than the inspection door itself
    //     (already discovered) or the guard room (the mandated destination) is "continuing the
    //     patrol instead of aborting" -- forbidden, per rule 7's own text. Deliberately NOT
    //     rebuilt from PatrolProgress.VisitOrder history at round end (the original approach) --
    //     that can't express "only violations AFTER discovery count," so this tracks it live
    //     instead and PatrolEvaluator.EvaluateTerminalAbort just reads the verdict back out.
    //
    // The 7-second "return within N seconds or fail" timer described in the original report is
    // deliberately NOT implemented here -- it was never part of the confirmed rule text ("가까이
    // 가지 말고 즉시 경비실로 복귀한다" has no time limit), only the 0.5s discovery dwell is.
    public class TerminalAbortState
    {
        public const float DiscoveryDwellSeconds = 0.5f;
        public const float DiscoveryConeHalfAngleDegrees = 30f;
        public const float MaxDiscoveryDistance = 8f;

        private float discoveryDwell;

        public bool Discovered { get; private set; }
        public bool Violated { get; private set; }

        public void ResetForRound()
        {
            discoveryDwell = 0f;
            Discovered = false;
            Violated = false;
        }

        // Ticked every frame a terminal anomaly is active this round -- a no-op once Discovered,
        // since there's nothing left to confirm.
        public void Tick(float deltaTime, Camera camera, Transform playerRoot, PatrolSceneBindings bindings)
        {
            if (Discovered || camera == null || bindings == null)
            {
                return;
            }

            var door = bindings.Resolve(TargetRef.Simple(TargetKind.InspectionDoor));
            bool inView = door != null && GazeSensor.IsWithinGazeCone(
                camera, door, door, playerRoot, MaxDiscoveryDistance, DiscoveryConeHalfAngleDegrees);

            discoveryDwell = inView ? discoveryDwell + deltaTime : 0f;
            if (discoveryDwell >= DiscoveryDwellSeconds)
            {
                Discovered = true;
            }
        }

        // Called by PatrolRuntimeController.RecordVisit for every checkpoint visit. Only matters
        // once Discovered -- before that, a plain patrol visit is still just a plain patrol visit.
        public void NotifyVisit(TargetRef target)
        {
            if (!Discovered || Violated)
            {
                return;
            }

            if (target.Kind != TargetKind.InspectionDoor && target.Kind != TargetKind.GuardRoomReturn)
            {
                Violated = true;
            }
        }
    }
}
