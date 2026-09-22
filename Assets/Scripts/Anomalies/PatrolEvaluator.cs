using System;
using System.Collections.Generic;
using System.Linq;

namespace RuleGhost.Anomalies
{
    // Compares a PatrolDuty (the round's answer key) against a PatrolProgress (what the player
    // actually did), now also folding in the round's active anomalies. Kept separate from
    // PatrolRuntimeController so the runtime flow and the judging logic don't get tangled
    // together.
    //
    // Anomaly correction judging is limited to what PatrolInteractable can actually observe --
    // a plain "was this target visited" signal, nothing about gaze direction, held duration, or
    // which of several actions was performed at a target. That rules out judging anomalies whose
    // required/forbidden action is a facing/gaze thing (EyesOpenPortrait's MakeEyeContact,
    // PersonInLandscape's TurnAwayFromExhibit/RecheckExhibit, SoundFromExhibit's
    // FaceExhibit/ShowBackToExhibit) or one where the forbidden action's target is otherwise
    // mandatory to visit anyway, so "visited" can't mean "did the forbidden thing" (FlippedPainting's
    // ModifyOriginalPainting on the discovered painting -- it's one of the 9 paintings Duty_AM1
    // requires inspecting regardless; KnockOnDoor's OperateEntranceDoor -- the entrance is always
    // required, structurally, to end the round). Those are left for a future extension once the
    // interaction model can distinguish more than "visited or not". What IS judgeable today:
    //  - a required action on a target that's otherwise optional (InspectionDoorAjar's
    //    CloseInspectionDoorFully) becomes a real required-to-visit target for the round.
    //  - a forbidden action on a target that's otherwise optional (HighHumidity's
    //    TouchThermostat -- the thermometer is never part of any base duty) becomes a real
    //    "don't visit this" rule.
    //  - a terminal anomaly (InspectionDoorWideOpen) replaces the whole round's judgment: the
    //    correct move is to go straight to the entrance and abort, not finish the checklist.
    public static class PatrolEvaluator
    {
        private static readonly PaintingWall[] AllWalls = { PaintingWall.North, PaintingWall.West, PaintingWall.East };

        public static PatrolResult Evaluate(PatrolDuty duty, PatrolProgress progress,
            IReadOnlyList<ResolvedAnomaly> anomalies = null)
        {
            anomalies ??= Array.Empty<ResolvedAnomaly>();

            var terminal = anomalies.FirstOrDefault(a => a.Source.IsTerminal);
            if (terminal != null)
            {
                return EvaluateTerminalAbort(terminal, progress);
            }

            var required = new HashSet<TargetRef>(BuildRequiredTargets(duty));
            foreach (var anomaly in anomalies)
            {
                foreach (var req in anomaly.RequiredActions)
                {
                    if (req.Target.Kind != TargetKind.WholePatrol)
                    {
                        required.Add(req.Target);
                    }
                }
            }

            var missing = required.Where(t => !progress.HasVisited(t)).ToList();
            bool entranceNotLast = progress.LastChecked == null
                                    || progress.LastChecked.Value.Kind != TargetKind.EntranceDoor;

            // A forbidden target only counts as a real "don't visit this" rule when it isn't
            // already required to be visited for some other reason -- otherwise the player has
            // no way to satisfy both rules at once (e.g. every painting must be inspected per
            // Duty_AM1, so a painting-targeted forbidden action can never mean "don't visit it").
            var forbiddenTaken = new List<string>();
            foreach (var anomaly in anomalies)
            {
                foreach (var forbid in anomaly.ForbiddenActions)
                {
                    if (!required.Contains(forbid.Target) && progress.HasVisited(forbid.Target))
                    {
                        forbiddenTaken.Add(anomaly.Id);
                    }
                }
            }

            return new PatrolResult
            {
                MissingTargets = missing,
                EntranceNotLast = entranceNotLast,
                ForbiddenAnomalyActions = forbiddenTaken,
                Success = missing.Count == 0 && !entranceNotLast && forbiddenTaken.Count == 0
            };
        }

        // A terminal anomaly (currently only InspectionDoorWideOpen) overrides the whole round:
        // RequiredActions says ReturnToGuardRoom, ForbiddenActions says ContinuePatrol -- the
        // correct response is to walk straight to the entrance and abort, not finish the normal
        // checklist. Success is "nothing but the entrance (and optionally the inspection door
        // itself, since checking the thing that triggered the abort isn't 'continuing the
        // patrol') was visited this round."
        private static PatrolResult EvaluateTerminalAbort(ResolvedAnomaly terminal, PatrolProgress progress)
        {
            bool visitedEntrance = progress.HasVisited(TargetRef.Simple(TargetKind.EntranceDoor));
            bool continuedPatrol = progress.VisitOrder.Any(t =>
                t.Kind != TargetKind.EntranceDoor && t.Kind != TargetKind.InspectionDoor);

            return new PatrolResult
            {
                EntranceNotLast = !visitedEntrance,
                ForbiddenAnomalyActions = continuedPatrol ? new List<string> { terminal.Id } : new List<string>(),
                Success = visitedEntrance && !continuedPatrol
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
