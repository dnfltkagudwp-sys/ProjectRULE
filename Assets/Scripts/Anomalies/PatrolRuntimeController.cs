using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace RuleGhost.Anomalies
{
    // Owns the runtime flow of a patrol round: start it, collect what the player checks via
    // PatrolInteractable.RecordVisit, and hand off to PatrolEvaluator once the entrance is
    // checked. Deliberately does not judge anything itself (that's PatrolEvaluator) or record
    // what the player did itself (that's PatrolProgress) -- this only sequences the three states
    // and moves to the next PatrolProfile in order when one round ends.
    //
    // Each round's generated anomalies are handed to AnomalyRuntimeApplier so they're actually
    // visible/tangible in the scene (a swapped painting texture, an open door, ...) -- see that
    // class for what it does and does not cover yet.
    public class PatrolRuntimeController : MonoBehaviour
    {
        public enum State
        {
            Idle,
            PatrolActive,
            Result,
            Complete
        }

        [SerializeField] private PatrolProfile[] patrolSequence = new PatrolProfile[6];
        [SerializeField] private CombinationRuleSet combinationRuleSet;
        [SerializeField] private PatrolSceneBindings sceneBindings;

        public static PatrolRuntimeController Instance { get; private set; }

        public State CurrentState { get; private set; } = State.Idle;
        public PatrolProfile CurrentProfile { get; private set; }
        public PatrolProgress CurrentProgress { get; private set; }
        public PatrolResult LastResult { get; private set; }

        // Exposed for ObservationDebugOverlay -- it needs to see the same round data this
        // controller is feeding to ObservationRuleMonitor to report why a gaze/facing rule isn't
        // (or is) triggering.
        public IReadOnlyList<ResolvedAnomaly> CurrentAnomalies => currentAnomalies;
        public PatrolSceneBindings SceneBindings => sceneBindings;

        // Exposed for the 규칙서 UI, which shows the full rulebook (every Duty/Anomaly rule text
        // across the whole sequence) rather than only this round's active ones -- a real work
        // manual isn't filtered down to "today's relevant pages."
        public IReadOnlyList<PatrolProfile> AllProfiles => patrolSequence;

        private IReadOnlyList<ResolvedAnomaly> currentAnomalies = Array.Empty<ResolvedAnomaly>();
        private int currentIndex = -1;
        private Random rng;
        private readonly AnomalyRuntimeApplier anomalyApplier = new();
        private readonly ObservationRuleMonitor observationMonitor = new();
        private readonly LooseObservationTracker looseObservationTracker = new();
        private readonly RoutinePatrolState routineState = new();
        private readonly TerminalAbortState terminalAbortState = new();
        private Camera playerCamera;
        private Transform playerRoot;
        private bool loggedMissingCameraWarning;

        // Captured at the end of the previous round for the minimal result overlay in OnGUI --
        // deliberately not CurrentProfile/LastResult alone, since StartPatrolAt overwrites
        // CurrentProfile with the *next* round before this frame ever renders.
        private PatrolProfile lastCompletedProfile;
        private string lastResultSummary;

        // Set the instant a forbidden action is caught live (this round only), so OnGUI can show
        // the failure immediately instead of waiting for the player to walk back to the guard
        // room -- TryCompletePatrol/Evaluate() still make the actual pass/fail call when that
        // happens, this is purely the early notice.
        private bool roundFailed;
        private string activeNotice;
        private readonly HashSet<string> notifiedViolations = new();
        // Same idea as notifiedViolations, for the "required action just satisfied" side (e.g.
        // PersonInLandscape's texture revert) -- see HandleAnomalyRequiredSatisfied.
        private readonly HashSet<string> notifiedSatisfactions = new();

        private void Awake()
        {
            Instance = this;
            rng = new Random();
        }

        private void Start()
        {
            StartPatrolAt(0);
        }

        // Gaze/facing rules need a continuous per-frame check against the player's camera --
        // everything else here reacts to discrete events (RecordVisit, StartPatrolAt), so this is
        // the one place that needs an Update loop at all.
        private void Update()
        {
            if (CurrentState != State.PatrolActive)
            {
                return;
            }

            if (playerCamera == null)
            {
                var controller = FindFirstObjectByType<CharacterController>();
                if (controller != null)
                {
                    playerRoot = controller.transform;
                    playerCamera = controller.GetComponentInChildren<Camera>();
                }
            }

            if (playerCamera != null)
            {
                observationMonitor.Tick(Time.deltaTime, playerCamera, playerRoot, sceneBindings, currentAnomalies);
                looseObservationTracker.Tick(Time.deltaTime, playerCamera, playerRoot, sceneBindings);
                if (FindTerminalAnomaly() != null)
                {
                    terminalAbortState.Tick(Time.deltaTime, playerCamera, playerRoot, sceneBindings);
                }

                // Gaze/facing forbidden actions (MakeEyeContact, ShowBackToExhibit) are judged
                // continuously by ObservationRuleMonitor rather than at a discrete E-key moment --
                // this is what gives THEM the same "fail immediately" treatment RecordVisit's
                // plain-action checks get, by noticing the instant one flips into violatedForbidden.
                foreach (var id in observationMonitor.ViolatedForbiddenIds)
                {
                    if (notifiedViolations.Add(id))
                    {
                        FailPatrolImmediately(id);
                    }
                }

                // A required gaze/facing action's own visual payoff (e.g. PersonInLandscape's
                // texture reverting once the player has turned away long enough) has to happen the
                // instant it's earned, same reasoning as the violation loop above.
                foreach (var id in observationMonitor.SatisfiedRequiredIds)
                {
                    if (notifiedSatisfactions.Add(id))
                    {
                        HandleAnomalyRequiredSatisfied(id);
                    }
                }
            }
            else if (!loggedMissingCameraWarning)
            {
                loggedMissingCameraWarning = true;
                Debug.LogWarning("[PatrolRuntimeController] Could not find a CharacterController/Camera in the scene -- " +
                                  "gaze/facing anomaly rules will never trigger this session.");
            }
        }

        public void StartPatrolAt(int index)
        {
            if (patrolSequence == null || index < 0 || index >= patrolSequence.Length || patrolSequence[index] == null)
            {
                CurrentState = State.Idle;
                Debug.Log("[PatrolRuntimeController] No more patrols in the sequence -- all complete.");
                return;
            }

            if (combinationRuleSet == null)
            {
                Debug.LogError("[PatrolRuntimeController] CombinationRuleSet not assigned.");
                return;
            }

            currentIndex = index;
            CurrentProfile = patrolSequence[index];
            CurrentProgress = new PatrolProgress();
            LastResult = null;
            CurrentState = State.PatrolActive;
            roundFailed = false;
            activeNotice = null;
            notifiedViolations.Clear();
            notifiedSatisfactions.Clear();
            terminalAbortState.ResetForRound();

            anomalyApplier.ResetAll(sceneBindings);
            observationMonitor.ResetAll();
            looseObservationTracker.ResetAll();
            routineState.ResetVisuals(sceneBindings);
            var generation = PatrolGenerator.Generate(CurrentProfile, combinationRuleSet, rng);
            currentAnomalies = generation.Anomalies;
            if (sceneBindings != null)
            {
                TeleportPlayerToGuardRoom(sceneBindings);
                anomalyApplier.Apply(sceneBindings, generation.Anomalies);
                routineState.RollForRound(CurrentProfile, generation.Anomalies, rng, sceneBindings);
            }
            else if (generation.Anomalies.Count > 0)
            {
                Debug.LogWarning("[PatrolRuntimeController] PatrolSceneBindings not assigned -- anomalies generated but not applied.");
            }

            string anomalyIds = generation.Anomalies.Count > 0
                ? string.Join(", ", System.Linq.Enumerable.Select(generation.Anomalies, a => a.Id))
                : "(none)";
            Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} ({CurrentProfile.Slot}) started -- " +
                      $"{generation.Anomalies.Count} anomaly(ies) applied: [{anomalyIds}]");
        }

        // Called by PatrolInteractable (E-key visits) and GuardRoomReturnTrigger (walking into the
        // guard room) when the player checks/reaches something. Ignored outside PatrolActive so a
        // stray interaction after a round ends can't corrupt the next one.
        //
        // GuardRoomReturn is never recorded into CurrentProgress -- it's purely the "attempt to
        // finish" signal (see TryCompletePatrol), not a duty checkpoint. Recording it would make it
        // PatrolProgress.LastChecked from that moment on, which would break Duty_AM5's own
        // "entrance must be the last thing checked" rule (PatrolEvaluator.RequiresEntranceLast):
        // every AM5 round would then always look like the entrance wasn't last, since the guard
        // room arrival itself would always be. Both AM1's tautological GuardRoomReturn requirement
        // and the terminal-abort path already treat "we're evaluating at all" as proof the player
        // arrived, so nothing downstream needs it recorded.
        public void RecordVisit(TargetRef target)
        {
            if (CurrentState != State.PatrolActive)
            {
                return;
            }

            if (target.Kind == TargetKind.GuardRoomReturn)
            {
                TryCompletePatrol();
                return;
            }

            CurrentProgress.RecordVisit(target);
            Debug.Log($"[PatrolRuntimeController] Checked: {target}");

            HandleRoutineAction(target);
            HandleAnomalyAction(target);
            CheckLiveRecheckViolation(target);

            bool wasAlreadyViolated = terminalAbortState.Violated;
            terminalAbortState.NotifyVisit(target);
            if (terminalAbortState.Violated && !wasAlreadyViolated)
            {
                var terminal = FindTerminalAnomaly();
                if (terminal != null)
                {
                    FailPatrolImmediately(terminal.Id);
                }
            }
        }

        private ResolvedAnomaly FindTerminalAnomaly()
        {
            foreach (var anomaly in currentAnomalies)
            {
                if (anomaly.Source.IsTerminal)
                {
                    return anomaly;
                }
            }
            return null;
        }

        // A plain E-key visit is ambiguous between "just checking" and "fixing something" -- this
        // is what tells them apart for the two Routine conditions RoutinePatrolState can roll:
        // pressing E on the currently-tilted painting straightens it, and pressing E on the
        // thermometer while its Routine humidity needs adjusting fixes that. Both are no-ops when
        // their condition isn't active, so this never affects a plain "InspectX" visit.
        private void HandleRoutineAction(TargetRef target)
        {
            if (target.Kind == TargetKind.SpecificPainting && routineState.TiltedPainting.HasValue &&
                routineState.TiltedPainting.Value.Equals(target))
            {
                routineState.Straighten(sceneBindings);
                CurrentProgress.RecordAction(target, ActionTag.StraightenPainting);
            }
            else if (target.Kind == TargetKind.Thermometer && routineState.HumidityNeedsAdjustment)
            {
                routineState.AdjustHumidity(sceneBindings);
                CurrentProgress.RecordAction(target, ActionTag.AdjustHumidity);
            }
        }

        // A plain E-key visit to the inspection door or thermometer never means "just checking" --
        // their own baseline "확인" is a gaze check instead (see LooseObservationTracker), so E on
        // either always means a real action: closing the door, or adjusting the thermostat. A
        // SpecificPainting visit can similarly mean "flip this one" when it's FlippedPainting's own
        // required MirrorTarget, in addition to HandleRoutineAction's straighten-if-tilted check.
        private void HandleAnomalyAction(TargetRef target)
        {
            if (target.Kind == TargetKind.InspectionDoor)
            {
                foreach (var anomaly in currentAnomalies)
                {
                    if (anomaly.Id == "InspectionDoorAjar")
                    {
                        anomalyApplier.CloseInspectionDoor();
                        break;
                    }
                }
            }
            else if (target.Kind == TargetKind.Thermometer)
            {
                // AdjustThermostat itself isn't recorded into PatrolProgress -- nothing reads it
                // back (the Routine humidity fix below uses its own AdjustHumidity tag); it only
                // needs to exist long enough to check against HighHumidity's forbidden list.
                string violator = PatrolEvaluator.FindForbiddenAnomaly(target, ActionTag.AdjustThermostat, currentAnomalies);
                if (violator != null)
                {
                    FailPatrolImmediately(violator);
                }
            }
            else if (target.Kind == TargetKind.SpecificPainting)
            {
                HandleMirrorFlip(target);
            }
        }

        // FlippedPainting's payoff for actually doing the required action: flip the mirror
        // counterpart the player finds their way to, so the fix is visible instead of a silent
        // checkbox. Guarded by HasPerformedAction so a repeat E-press on the same painting doesn't
        // flip it a second time (which would just rotate it back to normal -- see
        // AnomalyRuntimeApplier.FlipPainting).
        private void HandleMirrorFlip(TargetRef target)
        {
            if (CurrentProgress.HasPerformedAction(target, ActionTag.FlipPainting))
            {
                return;
            }

            foreach (var anomaly in currentAnomalies)
            {
                if (anomaly.Id != "FlippedPainting")
                {
                    continue;
                }

                foreach (var req in anomaly.RequiredActions)
                {
                    if (req.Action == ActionTag.FlipPainting && req.Target.Equals(target))
                    {
                        anomalyApplier.FlipPainting(sceneBindings, target);
                        CurrentProgress.RecordAction(target, ActionTag.FlipPainting);
                        return;
                    }
                }
            }
        }

        // A required gaze/facing action's own visual payoff -- currently only PersonInLandscape:
        // once the player has turned away for the full dwell, the person in the painting is gone,
        // confirming the fix without needing to look at it again (which would violate RecheckExhibit
        // anyway). Kept as its own narrowly-scoped method rather than a generic
        // "on any anomaly satisfied, do X" table -- there's exactly one case today.
        private void HandleAnomalyRequiredSatisfied(string anomalyId)
        {
            if (anomalyId != "PersonInLandscape")
            {
                return;
            }

            foreach (var anomaly in currentAnomalies)
            {
                if (anomaly.Id != "PersonInLandscape")
                {
                    continue;
                }

                foreach (var req in anomaly.RequiredActions)
                {
                    if (req.Action == ActionTag.TurnAwayFromExhibit)
                    {
                        anomalyApplier.RevertTexture(sceneBindings, req.Target);
                        return;
                    }
                }
            }
        }

        // RecheckExhibit (PersonInLandscape's forbidden action) is a second-or-later E-key visit
        // to the same target, not a distinct action tag -- checked generically here, for every
        // visit, rather than only for a specific target kind. Gated to fire exactly once (at the
        // exact visit that crosses the threshold) so it doesn't re-fire on a third, fourth, ... visit.
        private void CheckLiveRecheckViolation(TargetRef target)
        {
            if (CurrentProgress.VisitCount(target) != PatrolEvaluator.RecheckVisitThreshold)
            {
                return;
            }

            string violator = PatrolEvaluator.FindForbiddenAnomaly(target, ActionTag.RecheckExhibit, currentAnomalies);
            if (violator != null)
            {
                FailPatrolImmediately(violator);
            }
        }

        // The live counterpart to Evaluate()'s own forbidden-action pass -- fires the instant a
        // forbidden action happens instead of waiting for the player to walk back to the guard
        // room. Deliberately doesn't end the round itself: the player still has to return to the
        // guard room to close it out (TryCompletePatrol), which independently arrives at the same
        // Success=false via the normal Evaluate() call -- this is purely the early UI notice.
        private void FailPatrolImmediately(string violatorId)
        {
            if (roundFailed)
            {
                return;
            }

            roundFailed = true;
            activeNotice = $"규칙 위반: {violatorId}";
            Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} -- forbidden action detected live ({violatorId}).");
        }

        // Meters, +X (away from the guard room, which sits at the lobby's negative-X corner --
        // see DecorateGuardRoom/AttachGuardRoomReturnTrigger) applied to the GuardRoomReturn
        // binding's own position, so the round-start spawn lands just outside its return-trigger
        // volume instead of inside it. Spawning literally on top of that trigger would fire
        // GuardRoomReturn/TryCompletePatrol the instant the round starts (evaluating a completely
        // empty PatrolProgress) -- harmless (nothing's missing except everything, so the patrol
        // just shows the "incomplete" notice and keeps going), but confusing to see, and fragile
        // wrt exactly when Unity re-fires OnTriggerEnter for a CharacterController toggled back on
        // while already overlapping a trigger.
        private const float GuardRoomSpawnLobbyOffset = 4f;

        // Every round starts "경비실을 나와" per the design doc -- without this the player just
        // stays wherever the previous round ended (or, on the very first round, wherever
        // LobbyGrayboxBuilder's one-time BuildPlayer placement happened to be), neither of which is
        // the guard room. Reuses the already-bound GuardRoomReturn target instead of a new spawn
        // marker, matching PatrolSceneBindings' existing TargetRef -> Transform convention.
        private static void TeleportPlayerToGuardRoom(PatrolSceneBindings bindings)
        {
            var spawn = bindings.Resolve(TargetRef.Simple(TargetKind.GuardRoomReturn));
            if (spawn == null)
            {
                return;
            }

            var controller = FindFirstObjectByType<CharacterController>();
            if (controller == null)
            {
                return;
            }

            controller.enabled = false;
            controller.transform.position =
                new Vector3(spawn.position.x + GuardRoomSpawnLobbyOffset, 0f, spawn.position.z);
            controller.enabled = true;
        }

        // Called whenever the player physically reaches the guard room (both slots now -- see
        // RecordVisit). Evaluates once; a shortfall that's ONLY missing/incomplete required items
        // (no forbidden violation) keeps the patrol going instead of ending it, per the confirmed
        // design: "필요한 걸 다 했으면 복귀 시 종료, 안 했으면 경비 유지." Any forbidden violation --
        // whether just caught here for the first time, or already flagged live by
        // FailPatrolImmediately -- ends the round as a fail; Evaluate() agrees with the live flag
        // either way since both read the same underlying progress/anomaly state.
        private void TryCompletePatrol()
        {
            var observation = observationMonitor.BuildReport();
            var result = PatrolEvaluator.Evaluate(CurrentProfile.Duty, CurrentProgress, currentAnomalies, observation,
                looseObservationTracker.Observed, routineState, terminalAbortState.Violated);

            bool onlyMissingRequirements = !result.Success && result.ForbiddenAnomalyActions.Count == 0;
            if (onlyMissingRequirements)
            {
                // Testing-only detail -- names exactly which targets/rules are still outstanding,
                // the same way FinishPatrol's own FAIL log does, instead of a bare "something's
                // missing" that gives no lead on what to go check next.
                var parts = new List<string>();
                if (result.MissingTargets.Count > 0) parts.Add(string.Join(", ", result.MissingTargets));
                if (result.MissingObservations.Count > 0) parts.Add(string.Join(", ", result.MissingObservations));
                if (result.MissingRechecks.Count > 0) parts.Add(string.Join(", ", result.MissingRechecks));
                if (result.MissingRoutineTasks.Count > 0) parts.Add(string.Join(", ", result.MissingRoutineTasks));
                if (result.EntranceNotLast) parts.Add("출입문이 마지막이 아님");
                string detail = string.Join(" / ", parts);

                Debug.Log($"[PatrolRuntimeController] Reached guard room but required checks are incomplete -- patrol continues. [{detail}]");
                activeNotice = $"점검하지 않은 항목: {detail}";
                return;
            }

            FinishPatrol(result);
        }

        private void FinishPatrol(PatrolResult result)
        {
            CurrentState = State.Result;
            LastResult = result;
            activeNotice = null;

            // Stashed for OnGUI's result overlay -- CurrentProfile itself gets overwritten by
            // StartPatrolAt below before this frame ever renders.
            lastCompletedProfile = CurrentProfile;
            lastResultSummary = LastResult.Success
                ? "PASS"
                : $"FAIL (missing:{LastResult.MissingTargets.Count} entranceLast:{!LastResult.EntranceNotLast} " +
                  $"forbidden:{LastResult.ForbiddenAnomalyActions.Count} obs:{LastResult.MissingObservations.Count} " +
                  $"recheck:{LastResult.MissingRechecks.Count} routine:{LastResult.MissingRoutineTasks.Count})";

            if (LastResult.Success)
            {
                Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} SUCCESS.");
            }
            else
            {
                string missing = LastResult.MissingTargets.Count > 0
                    ? string.Join(", ", LastResult.MissingTargets)
                    : "(none)";
                string violated = LastResult.ForbiddenAnomalyActions.Count > 0
                    ? string.Join(", ", LastResult.ForbiddenAnomalyActions)
                    : "(none)";
                string missingObservations = LastResult.MissingObservations.Count > 0
                    ? string.Join(", ", LastResult.MissingObservations)
                    : "(none)";
                string missingRechecks = LastResult.MissingRechecks.Count > 0
                    ? string.Join(", ", LastResult.MissingRechecks)
                    : "(none)";
                string missingRoutine = LastResult.MissingRoutineTasks.Count > 0
                    ? string.Join(", ", LastResult.MissingRoutineTasks)
                    : "(none)";
                Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} FAILED. " +
                          $"Missing=[{missing}] EntranceNotLast={LastResult.EntranceNotLast} " +
                          $"ViolatedAnomalyRules=[{violated}] MissingObservations=[{missingObservations}] " +
                          $"MissingRechecks=[{missingRechecks}] MissingRoutineTasks=[{missingRoutine}]");
            }

            CurrentState = State.Complete;
            StartPatrolAt(currentIndex + 1);
        }

        // Placeholder-grade result feedback for testing only -- deliberately just corner labels,
        // no Canvas/animation/sound; the real pass/fail presentation is a separate, later task.
        // activeNotice covers the CURRENT round (an immediate-fail notice, or "go finish the
        // checklist"); the box below it shows the most recently COMPLETED round and persists into
        // the next one (which starts right away once the player does return -- see FinishPatrol).
        private void OnGUI()
        {
            int y = 10;
            if (!string.IsNullOrEmpty(activeNotice))
            {
                // Wide + word-wrapped -- the incomplete-checklist notice can list several missing
                // targets by name, which a fixed 380x30 box would just clip.
                var style = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
                float height = style.CalcHeight(new GUIContent(activeNotice), 600) + 10;
                GUI.color = roundFailed ? Color.red : Color.yellow;
                GUI.Box(new Rect(10, y, 600, height), activeNotice, style);
                GUI.color = Color.white;
                y += (int)height + 4;
            }

            if (lastCompletedProfile != null)
            {
                GUI.Box(new Rect(10, y, 380, 30),
                    $"Patrol {lastCompletedProfile.PatrolIndex} ({lastCompletedProfile.Slot}): {lastResultSummary}");
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(PatrolProfile[] profiles, CombinationRuleSet ruleSet, PatrolSceneBindings bindings)
        {
            patrolSequence = profiles;
            combinationRuleSet = ruleSet;
            sceneBindings = bindings;
        }
#endif
    }
}
