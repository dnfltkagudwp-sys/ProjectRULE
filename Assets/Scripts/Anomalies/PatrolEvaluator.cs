using System;
using System.Collections.Generic;
using System.Linq;

namespace RuleGhost.Anomalies
{
    // Compares a PatrolDuty (the round's answer key) against a PatrolProgress (what the player
    // actually did), folding in the round's active Anomalies and Routine conditions. Kept
    // separate from PatrolRuntimeController so the runtime flow and the judging logic don't get
    // tangled together.
    //
    // Judging combines several independent signals, each keyed off the ActionTag rather than a
    // single "visited" bit, since the same E-key press can mean different things depending on the
    // tag involved:
    //  - a discrete "was this target visited" event (PatrolProgress.HasVisited, via
    //    PatrolInteractable's E key) for plain check/visit tags (CheckEntranceDoorClosed) or where
    //    NOT visiting is the violation, when that target isn't otherwise mandatory to visit
    //    (HighHumidity's AdjustThermostat).
    //  - a continuous "was the player looking at / facing this the right way" signal
    //    (ObservationReport, built by ObservationRuleMonitor from the player's camera every frame)
    //    for gaze/direction Anomaly tags: MakeEyeContact (EyesOpenPortrait); TurnAwayFromExhibit
    //    (required, PersonInLandscape) with FaceExhibit as its own forbidden counterpart ("kept
    //    looking instead" -- a longer dwell than the required side, see
    //    ObservationRuleMonitor.KeepFacingForbiddenDwellSeconds); and FaceExhibit (required)/
    //    ShowBackToExhibit (forbidden) for SoundFromExhibit.
    //  - a count-based signal (PatrolProgress.VisitCount) for RecheckExhibit (PersonInLandscape's
    //    other forbidden action): an E-key re-interaction check, distinct from the plain visit-based
    //    path because the target is already mandatory to visit once (every painting per
    //    Duty_AM1) -- "don't recheck it" can only mean "don't visit it a second time."
    //  - a loose gaze-scan signal (observedTargets, from LooseObservationTracker) for
    //    ObservePainting, InspectInspectionDoor and InspectThermometer: AM1's "확인" of the 9
    //    paintings, and the inspection door/thermometer's own baseline check (both slots), are a
    //    visual scan, not an E-key press -- E on either means a real action instead
    //    (CloseInspectionDoorFully / AdjustThermostat), so "just checking" must never use E at all.
    //  - a specific-action signal (PatrolProgress.HasPerformedAction) for Routine conditions
    //    (StraightenPainting, AdjustHumidity) -- these are never part of a static Duty/Anomaly
    //    asset; RoutinePatrolState rolls them fresh each round and Evaluate is told directly which
    //    ones are active this round via the `routine` parameter.
    //  - a terminal Anomaly (InspectionDoorWideOpen) replaces the whole round's judgment: the
    //    correct move is to abort straight to the guard room, not finish the checklist -- this
    //    overrides Routine conditions too (return-and-abort requires nothing else).
    //
    // Most forbidden actions above are ALSO checked live, the instant they happen, via
    // FindForbiddenAnomaly -- see PatrolRuntimeController. Evaluate() still repeats the same check
    // at round end as the authoritative source of truth (and the only path for the few forbidden
    // actions that stay visit-based here, like AdjustThermostat).
    //
    // FlippedPainting's ModifyOriginalPainting and KnockOnDoor's OperateEntranceDoor still can't be
    // judged: their target is otherwise mandatory to visit (every painting per Duty_AM1; the
    // entrance, for an AM5 round, to end it), so a mere visit can't mean "did the forbidden thing"
    // -- unaffected by any of the paths above.
    public static class PatrolEvaluator
    {
        private static readonly PaintingWall[] AllWalls = { PaintingWall.North, PaintingWall.West, PaintingWall.East };

        private static bool IsObservationBased(ActionTag action) => action is
            ActionTag.MakeEyeContact or ActionTag.TurnAwayFromExhibit or
            ActionTag.FaceExhibit or ActionTag.ShowBackToExhibit;

        // Baseline "확인" checks satisfied by LooseObservationTracker (a glance, no E-key) rather
        // than a visit -- ObservePainting for the 9 paintings, InspectInspectionDoor because the
        // door's E-key interaction has a real side effect (see class comment), and
        // InspectThermometer for the same reason as the door: its own E-key press means
        // AdjustThermostat (an actual, judged action), so the baseline "확인" can't share that key.
        private static bool IsLooseObservationBased(ActionTag action) => action is
            ActionTag.ObservePainting or ActionTag.InspectInspectionDoor or ActionTag.InspectThermometer;

        // A recheck is satisfied by a second (or later) E-key visit to the same target -- the
        // first visit is already spent satisfying the base duty's "inspect every painting."
        // Public so PatrolRuntimeController's live (immediate-fail) check uses the exact same
        // threshold instead of a copy that can drift out of sync.
        public const int RecheckVisitThreshold = 2;

        // AM5's own closing check (entrance must be visited, and must be last) is a structural
        // rule of that slot specifically -- AM1 closes on GuardRoomReturn instead, which needs no
        // such "last" ordering (see PatrolRuntimeController.RecordVisit / class comment there).
        // Keyed off PatrolDuty.Slot rather than a new field since every Duty already belongs to
        // exactly one of the two (fixed) time slots.
        private static bool RequiresEntranceLast(PatrolDuty duty) => duty.Slot == TimeSlot.AM5;

        public static PatrolResult Evaluate(PatrolDuty duty, PatrolProgress progress,
            IReadOnlyList<ResolvedAnomaly> anomalies = null, ObservationReport observation = null,
            IReadOnlyCollection<TargetRef> observedTargets = null, RoutinePatrolState routine = null,
            bool terminalAbortViolated = false)
        {
            anomalies ??= Array.Empty<ResolvedAnomaly>();
            observation ??= ObservationReport.Empty;
            observedTargets ??= Array.Empty<TargetRef>();

            var terminal = anomalies.FirstOrDefault(a => a.Source.IsTerminal);
            if (terminal != null)
            {
                return EvaluateTerminalAbort(terminal, terminalAbortViolated);
            }

            // `required`/`requiredPairs` both accumulate every required target -- the former is
            // target-only (what BuildRequiredTargets used to return) for the plain "was it
            // visited" pass; the latter also keeps the exact tag that required it, so the
            // forbidden-visit pass below can tell "already required to visit, so a forbidden
            // *visit* tag on the same target is meaningless" (the original reason this existed)
            // apart from "required via a DIFFERENT tag than the forbidden one" (e.g. required to
            // CheckEntranceDoorClosed but forbidden to OperateEntranceDoor -- both real, both
            // checkable).
            var required = new HashSet<TargetRef>();
            var requiredPairs = new HashSet<(TargetRef Target, ActionTag Action)>();
            var missing = new List<TargetRef>();

            foreach (var req in duty.RequiredActions)
            {
                if (IsLooseObservationBased(req.Action))
                {
                    if (!observedTargets.Contains(req.Target))
                    {
                        missing.Add(req.Target);
                    }
                    requiredPairs.Add((req.Target, req.Action));
                }
                else if (req.Target.Kind == TargetKind.WholePatrol && req.Action == ActionTag.InspectAllPaintings)
                {
                    foreach (var wall in AllWalls)
                    {
                        for (int i = 1; i <= 3; i++)
                        {
                            var painting = TargetRef.Painting(wall, i);
                            required.Add(painting);
                            requiredPairs.Add((painting, req.Action));
                        }
                    }
                }
                else if (req.Target.Kind == TargetKind.GuardRoomReturn)
                {
                    // Tautologically satisfied -- Evaluate for an AM1 round is only ever reached
                    // via GuardRoomReturnTrigger firing this exact visit in the first place. Kept
                    // in the data so Duty_AM1's required-action list stays a complete, honest
                    // checklist rather than silently omitting its own closing step.
                }
                else if (req.Target.Kind != TargetKind.WholePatrol)
                {
                    required.Add(req.Target);
                    requiredPairs.Add((req.Target, req.Action));
                }
            }

            if (RequiresEntranceLast(duty))
            {
                required.Add(TargetRef.Simple(TargetKind.EntranceDoor));
                requiredPairs.Add((TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.CheckEntranceDoorClosed));
            }

            var missingRoutine = new List<string>();
            if (routine?.TiltedPainting is TargetRef tiltedPainting &&
                !progress.HasPerformedAction(tiltedPainting, ActionTag.StraightenPainting))
            {
                missingRoutine.Add("TiltedPainting");
            }
            if (routine != null && routine.HumidityNeedsAdjustment &&
                !progress.HasPerformedAction(TargetRef.Simple(TargetKind.Thermometer), ActionTag.AdjustHumidity))
            {
                missingRoutine.Add("RoutineHumidity");
            }

            var missingObservations = new List<string>();
            var missingRechecks = new List<string>();
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
                    else if (req.Action == ActionTag.RecheckExhibit)
                    {
                        if (progress.VisitCount(req.Target) < RecheckVisitThreshold)
                        {
                            missingRechecks.Add(anomaly.Id);
                        }
                    }
                    else if (req.Target.Kind != TargetKind.WholePatrol)
                    {
                        required.Add(req.Target);
                        requiredPairs.Add((req.Target, req.Action));
                    }
                }
            }

            missing.AddRange(required.Where(t => !progress.HasVisited(t)));

            bool entranceNotLast = RequiresEntranceLast(duty) &&
                (progress.LastChecked == null || progress.LastChecked.Value.Kind != TargetKind.EntranceDoor);

            // A visit-based forbidden target only counts as a real "don't visit this" rule when it
            // isn't already required via that SAME tag -- otherwise the player has no way to
            // satisfy both at once. A different required tag on the same target (e.g. required to
            // InspectThermometer, forbidden to AdjustThermostat) is not that conflict; both get
            // checked normally.
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
                    else if (forbid.Action == ActionTag.RecheckExhibit)
                    {
                        if (progress.VisitCount(forbid.Target) >= RecheckVisitThreshold)
                        {
                            forbiddenTaken.Add(anomaly.Id);
                        }
                    }
                    else if (!requiredPairs.Contains((forbid.Target, forbid.Action)) && progress.HasVisited(forbid.Target))
                    {
                        forbiddenTaken.Add(anomaly.Id);
                    }
                }
            }

            return new PatrolResult
            {
                MissingTargets = missing,
                MissingObservations = missingObservations,
                MissingRechecks = missingRechecks,
                MissingRoutineTasks = missingRoutine,
                EntranceNotLast = entranceNotLast,
                ForbiddenAnomalyActions = forbiddenTaken,
                Success = missing.Count == 0 && missingObservations.Count == 0 && missingRechecks.Count == 0
                          && missingRoutine.Count == 0 && !entranceNotLast && forbiddenTaken.Count == 0
            };
        }

        // A terminal anomaly (currently only InspectionDoorWideOpen) overrides the whole round:
        // RequiredActions says ReturnToGuardRoom, ForbiddenActions says ContinuePatrol -- the
        // correct response is to abort straight to the guard room, not finish the normal checklist
        // (Routine conditions included -- an abort requires nothing else). This is only ever
        // reached from PatrolRuntimeController.TryCompletePatrol, i.e. only once the player has
        // already walked back into the guard room -- so arrival itself needs no separate check
        // here, the same way AM1's own GuardRoomReturn requirement is already tautological above.
        //
        // Whether the player "continued the patrol instead" is decided live by TerminalAbortState,
        // not re-derived from PatrolProgress.VisitOrder here -- the rule only kicks in once the
        // player has actually discovered the door is wide open (a 0.5s gaze), and a flat visit-
        // history scan can't express "only violations after that moment count."
        private static PatrolResult EvaluateTerminalAbort(ResolvedAnomaly terminal, bool violated)
        {
            return new PatrolResult
            {
                ForbiddenAnomalyActions = violated ? new List<string> { terminal.Id } : new List<string>(),
                Success = !violated
            };
        }

        // Live counterpart to the forbidden-action pass inside Evaluate() above -- called by
        // PatrolRuntimeController the instant a plain (non-observation, non-recheck) action occurs,
        // so a forbidden action fails the round immediately instead of only being caught once the
        // player eventually returns to the guard room. Only checks active anomalies -- no Duty
        // today actually populates ForbiddenActions for this kind of check (Duty encodes its own
        // closing/ordering rules structurally instead), so there's nothing to gain by consulting it
        // here and real risk in resurrecting stale/unused duty data as a live check.
        public static string FindForbiddenAnomaly(TargetRef target, ActionTag action, IReadOnlyList<ResolvedAnomaly> anomalies)
        {
            if (anomalies == null)
            {
                return null;
            }

            foreach (var anomaly in anomalies)
            {
                foreach (var forbid in anomaly.ForbiddenActions)
                {
                    if (forbid.Target.Equals(target) && forbid.Action == action)
                    {
                        return anomaly.Id;
                    }
                }
            }

            return null;
        }
    }
}
