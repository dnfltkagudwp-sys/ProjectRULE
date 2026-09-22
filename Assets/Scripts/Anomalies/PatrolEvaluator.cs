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
    // Anomaly correction judging combines two independent signals:
    //  - a discrete "was this target visited" event (PatrolProgress, via PatrolInteractable's E
    //    key) for targets where visiting IS the correction (InspectionDoorAjar's
    //    CloseInspectionDoorFully) or where NOT visiting is the violation, when that target isn't
    //    otherwise mandatory to visit (HighHumidity's TouchThermostat).
    //  - a continuous "was the player looking at / facing this the right way" signal
    //    (ObservationReport, built by ObservationRuleMonitor from the player's camera every frame)
    //    for action tags that are inherently about gaze/direction rather than a button press:
    //    MakeEyeContact (EyesOpenPortrait), TurnAwayFromExhibit/FaceExhibit (required) and
    //    ShowBackToExhibit (forbidden) for PersonInLandscape/SoundFromExhibit.
    // RecheckExhibit (PersonInLandscape's forbidden action) is deliberately left alone -- it's
    // meant to become an E-key re-interaction check later, not a gaze one, and isn't handled by
    // either path yet. FlippedPainting's ModifyOriginalPainting and KnockOnDoor's
    // OperateEntranceDoor still can't be judged: their target is otherwise mandatory to visit
    // (every painting per Duty_AM1; the entrance, structurally, to end the round), so a mere visit
    // can't mean "did the forbidden thing" -- unaffected by adding the observation path.
    //
    //  - a terminal anomaly (InspectionDoorWideOpen) replaces the whole round's judgment: the
    //    correct move is to go straight to the entrance and abort, not finish the checklist.
    public static class PatrolEvaluator
    {
        private static readonly PaintingWall[] AllWalls = { PaintingWall.North, PaintingWall.West, PaintingWall.East };

        private static bool IsObservationBased(ActionTag action) => action is
            ActionTag.MakeEyeContact or ActionTag.TurnAwayFromExhibit or
            ActionTag.FaceExhibit or ActionTag.ShowBackToExhibit;

        public static PatrolResult Evaluate(PatrolDuty duty, PatrolProgress progress,
            IReadOnlyList<ResolvedAnomaly> anomalies = null, ObservationReport observation = null)
        {
            anomalies ??= Array.Empty<ResolvedAnomaly>();
            observation ??= ObservationReport.Empty;

            var terminal = anomalies.FirstOrDefault(a => a.Source.IsTerminal);
            if (terminal != null)
            {
                return EvaluateTerminalAbort(terminal, progress);
            }

            var required = new HashSet<TargetRef>(BuildRequiredTargets(duty));
            var missingObservations = new List<string>();
            foreach (var anomaly in anomalies)
            {
                foreach (var req in anomaly.RequiredActions)
                {
                    if (IsObservationBased(req.Action))
                    {
                        if (!observation.SatisfiedRequiredAnomalyIds.Contains(anomaly.Id))
                        {
                            missingObservations.Add(anomaly.Id);
                        }
                    }
                    else if (req.Target.Kind != TargetKind.WholePatrol)
                    {
                        required.Add(req.Target);
                    }
                }
            }

            var missing = required.Where(t => !progress.HasVisited(t)).ToList();
            bool entranceNotLast = progress.LastChecked == null
                                    || progress.LastChecked.Value.Kind != TargetKind.EntranceDoor;

            // A visit-based forbidden target only counts as a real "don't visit this" rule when it
            // isn't already required to be visited for some other reason -- otherwise the player
            // has no way to satisfy both rules at once (e.g. every painting must be inspected per
            // Duty_AM1, so a painting-targeted forbidden action can never mean "don't visit it").
            var forbiddenTaken = new List<string>();
            foreach (var anomaly in anomalies)
            {
                foreach (var forbid in anomaly.ForbiddenActions)
                {
                    if (IsObservationBased(forbid.Action))
                    {
                        if (observation.ViolatedForbiddenAnomalyIds.Contains(anomaly.Id))
                        {
                            forbiddenTaken.Add(anomaly.Id);
                        }
                    }
                    else if (!required.Contains(forbid.Target) && progress.HasVisited(forbid.Target))
                    {
                        forbiddenTaken.Add(anomaly.Id);
                    }
                }
            }

            return new PatrolResult
            {
                MissingTargets = missing,
                MissingObservations = missingObservations,
                EntranceNotLast = entranceNotLast,
                ForbiddenAnomalyActions = forbiddenTaken,
                Success = missing.Count == 0 && missingObservations.Count == 0
                          && !entranceNotLast && forbiddenTaken.Count == 0
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
