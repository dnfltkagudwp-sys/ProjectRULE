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
    //  3. A deadline: discovery starts a 7-second clock, and letting it run out is a violation on
    //     its own. Without it the rule was nearly impossible to actually break -- (2) only sees
    //     E-key checkpoint visits, but "계속 순찰한다" in practice means looking at paintings, and
    //     painting 확인 is gaze-based (LooseObservationTracker), so it never reached NotifyVisit.
    //     The clock also makes the rule legible without any HUD: dawdling kills you, so the only
    //     safe response is to leave immediately, which is exactly what the rule text asks for.
    //     Deliberately not surfaced on screen -- the player is meant to feel the pressure, not
    //     read a countdown.
    //
    // Nothing needs to stop the clock on a successful return: reaching the guard room ends the
    // round (PatrolRuntimeController.TryCompletePatrol), which leaves PatrolActive, and Tick is
    // only called while a patrol is active.
    public class TerminalAbortState
    {
        public const float DiscoveryDwellSeconds = 0.5f;
        public const float DiscoveryConeHalfAngleDegrees = 30f;
        public const float MaxDiscoveryDistance = 8f;
        public const float AbortDeadlineSeconds = 7f;

        private float discoveryDwell;
        private float sinceDiscovery;

        public bool Discovered { get; private set; }
        public bool Violated { get; private set; }

        public void ResetForRound()
        {
            discoveryDwell = 0f;
            sinceDiscovery = 0f;
            Discovered = false;
            Violated = false;
        }

        // Ticked every frame a terminal anomaly is active this round: before discovery it watches
        // for the 0.5s gaze that confirms it, and after discovery it runs the return deadline.
        public void Tick(float deltaTime, Camera camera, Transform playerRoot, PatrolSceneBindings bindings)
        {
            if (Violated)
            {
                return;
            }

            if (Discovered)
            {
                sinceDiscovery += deltaTime;
                if (sinceDiscovery >= AbortDeadlineSeconds)
                {
                    Violated = true;
                }
                return;
            }

            if (camera == null || bindings == null)
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
