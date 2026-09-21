using System.Collections.Generic;
using System.Linq;

namespace RuleGhost.Anomalies
{
    // Compares a PatrolDuty (the round's answer key) against a PatrolProgress (what the player
    // actually did). Anomaly-specific pass/fail rules are not implemented yet -- this only
    // covers the base-duty rule: every required target checked at least once, entrance checked
    // last. Kept separate from PatrolRuntimeController so the runtime flow and the judging logic
    // don't get tangled together.
    public static class PatrolEvaluator
    {
        private static readonly PaintingWall[] AllWalls = { PaintingWall.North, PaintingWall.West, PaintingWall.East };

        public static PatrolResult Evaluate(PatrolDuty duty, PatrolProgress progress)
        {
            var required = BuildRequiredTargets(duty);
            var missing = required.Where(t => !progress.HasVisited(t)).ToList();
            bool entranceNotLast = progress.LastChecked == null
                                    || progress.LastChecked.Value.Kind != TargetKind.EntranceDoor;

            return new PatrolResult
            {
                MissingTargets = missing,
                EntranceNotLast = entranceNotLast,
                Success = missing.Count == 0 && !entranceNotLast
            };
        }

        // Duty.RequiredActions encodes "check every painting" as a single abstract
        // WholePatrol/InspectAllPaintings entry (see Duty_AM1.asset), not nine separate
        // requirements -- that's the one placeholder this expands. Every other required
        // target is used as-is. The entrance is always required and always last, regardless
        // of what any individual Duty asset lists, since that's a structural rule of the
        // patrol loop itself, not something specific to one time slot.
        private static List<TargetRef> BuildRequiredTargets(PatrolDuty duty)
        {
            var set = new HashSet<TargetRef>();
            foreach (var req in duty.RequiredActions)
            {
                if (req.Target.Kind == TargetKind.WholePatrol && req.Action == ActionTag.InspectAllPaintings)
                {
                    foreach (var wall in AllWalls)
                    {
                        for (int i = 1; i <= 3; i++)
                        {
                            set.Add(TargetRef.Painting(wall, i));
                        }
                    }
                }
                else if (req.Target.Kind != TargetKind.WholePatrol)
                {
                    set.Add(req.Target);
                }
            }

            set.Add(TargetRef.Simple(TargetKind.EntranceDoor));
            return set.ToList();
        }
    }
}
