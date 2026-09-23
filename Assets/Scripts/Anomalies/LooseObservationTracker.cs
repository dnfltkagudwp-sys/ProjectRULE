using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Baseline "확인" (check) actions that shouldn't need an E-key press at all: the 9 paintings
    // (ObservePainting) and the inspection door (InspectInspectionDoor) both just need the player
    // to glance their way. The inspection door specifically must NOT be E-key based for this --
    // its hinge still carries a graybox-only DoorTestInteraction toggle (see
    // AttachPatrolRuntime.RemoveInspectionDoorTestToggle), so an E-key "just checking" action
    // would physically pop a closed door open as a side effect.
    //
    // Deliberately much looser than GazeSensor's existing MakeEyeContact use (a wide cone, no
    // dedicated anchor point, generous distance/dwell) -- these rules only need "the player looked
    // this way," not precise aim.
    public class LooseObservationTracker
    {
        public const float ObserveConeHalfAngleDegrees = 30f;
        public const float MaxObserveDistance = 8f;
        public const float ObserveDwellSeconds = 0.3f;

        private static readonly PaintingWall[] AllWalls = { PaintingWall.North, PaintingWall.West, PaintingWall.East };

        private readonly Dictionary<TargetRef, float> dwell = new();
        private readonly HashSet<TargetRef> observed = new();

        public IReadOnlyCollection<TargetRef> Observed => observed;

        public void ResetAll()
        {
            dwell.Clear();
            observed.Clear();
        }

        public void Tick(float deltaTime, Camera camera, Transform playerRoot, PatrolSceneBindings bindings)
        {
            if (camera == null || bindings == null)
            {
                return;
            }

            foreach (var wall in AllWalls)
            {
                for (int i = 1; i <= 3; i++)
                {
                    TickTarget(TargetRef.Painting(wall, i), deltaTime, camera, playerRoot, bindings);
                }
            }

            TickTarget(TargetRef.Simple(TargetKind.InspectionDoor), deltaTime, camera, playerRoot, bindings);
            // Thermometer's own baseline "확인" (InspectThermometer) is gaze-based for the same
            // reason as the door -- its E-key press means AdjustThermostat instead (see
            // PatrolRuntimeController), so a plain glance has to be enough to satisfy the checklist.
            TickTarget(TargetRef.Simple(TargetKind.Thermometer), deltaTime, camera, playerRoot, bindings);
        }

        private void TickTarget(TargetRef target, float deltaTime, Camera camera, Transform playerRoot,
            PatrolSceneBindings bindings)
        {
            if (observed.Contains(target))
            {
                return;
            }

            // The target's own transform doubles as both the "self" exemption and the aim point --
            // a loose scan has no need for a dedicated anchor like the portraits' eye-contact check.
            var resolved = bindings.Resolve(target);
            bool inView = resolved != null && GazeSensor.IsWithinGazeCone(
                camera, resolved, resolved, playerRoot, MaxObserveDistance, ObserveConeHalfAngleDegrees);

            float d = dwell.TryGetValue(target, out var existing) ? existing : 0f;
            d = inView ? d + deltaTime : 0f;
            dwell[target] = d;

            if (d >= ObserveDwellSeconds)
            {
                observed.Add(target);
            }
        }

        public bool IsObserved(TargetRef target) => observed.Contains(target);
    }
}
