using System;
using System.Collections;
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
            Complete,
            // Any forbidden action that ends the round outright (live-detected or judged at
            // guard-room return) routes through TriggerDeath and sits in this state for the
            // duration of the death visual -- Update()'s PatrolActive-only guard means nothing
            // else (observation ticks, RecordVisit) can run again until it's over.
            DeathSequence
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
        private readonly KnockOnDoorState knockState = new();
        private Camera playerCamera;
        private Transform playerRoot;
        private bool loggedMissingCameraWarning;

        // RuleGhost.Anomalies is its own assembly (RuleGhost.Anomalies.asmdef) and deliberately
        // can't reference the UI/Debug-folder scripts that compile into the default assembly
        // (that's the direction RulebookUI.cs already reaches the other way, via
        // PatrolRuntimeController.Instance) -- so the round-intro screen and player-freeze hooks
        // are exposed as static delegate slots instead of concrete types. RoundIntroUI and
        // GrayboxTestController register themselves into these in Awake/OnDestroy.
        public static Func<string, IEnumerator> RoundIntroShow;
        public static Func<IEnumerator> RoundIntroHide;
        public static Action<bool> SetPlayerControlsEnabled;

        // Same hook pattern, for the death sequence -- keyed by a DeathSequenceIds string so one
        // delegate covers all five death visuals instead of one field per anomaly.
        public static Func<string, IEnumerator> PlayDeathSequence;

        // The guard room door (DoorTestInteraction, a plain player-toggled hinge -- not part of
        // the anomaly system, unlike the inspection door which AnomalyRuntimeApplier already
        // resets itself) has no round-start reset of its own; left open, it stayed open into the
        // next round with nothing to close it. Same static-hook pattern as the two above.
        public static Action ResetGuardRoomDoor;

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

        // Time.time when the current round started -- guards against the round-start teleport
        // itself immediately re-firing GuardRoomReturn (see RecordVisit), since the player now
        // spawns literally inside that trigger's volume.
        private float roundStartTime;
        // The spawn teleport can fire the guard-room trigger's enter/exit within this window; a
        // genuine walk out or back in takes far longer, so anything sooner is ignored.
        private const float SpawnGraceSeconds = 1f;

        // Guards TriggerDeath against a second violation (in the same or a later frame) starting
        // a second death sequence -- StartCoroutine runs synchronously up to the first yield, so
        // this is already true before any other same-frame call site's own StartCoroutine call
        // returns. Checked alongside CurrentState == DeathSequence as a cheap second guard.
        private bool deathSequenceRunning;

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
                knockState.StopCue();
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
                var terminal = FindTerminalAnomaly();
                if (terminal != null)
                {
                    // The abort deadline expiring is a violation nothing else would notice --
                    // RecordVisit only hears about it when the player actually checks something.
                    bool wasViolated = terminalAbortState.Violated;
                    terminalAbortState.Tick(Time.deltaTime, playerCamera, playerRoot, sceneBindings);
                    if (terminalAbortState.Violated && !wasViolated)
                    {
                        StartCoroutine(TriggerDeath(terminal.Id));
                    }
                }

                // The knock starts on proximity and stops on a timer -- neither is an E-key event.
                if (IsAnomalyActive(DeathSequenceIds.KnockOnDoor))
                {
                    knockState.Tick(Time.deltaTime, playerRoot, sceneBindings);
                }

                // Gaze/facing forbidden actions (MakeEyeContact, ShowBackToExhibit) are judged
                // continuously by ObservationRuleMonitor rather than at a discrete E-key moment --
                // this is what gives THEM the same "fail immediately" treatment RecordVisit's
                // plain-action checks get, by noticing the instant one flips into violatedForbidden.
                foreach (var id in observationMonitor.ViolatedForbiddenIds)
                {
                    if (notifiedViolations.Add(id))
                    {
                        StartCoroutine(TriggerDeath(id));
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
            StartCoroutine(StartPatrolRoutine(index));
        }

        // Coroutine so the round-start intro screen (day/time black screen) can play, and the
        // player's controls can be frozen for it, before the round below actually begins -- the
        // guard clauses stay synchronous (no intro for "sequence exhausted"/misconfiguration).
        private IEnumerator StartPatrolRoutine(int index)
        {
            if (patrolSequence == null || index < 0 || index >= patrolSequence.Length || patrolSequence[index] == null)
            {
                CurrentState = State.Idle;
                Debug.Log("[PatrolRuntimeController] No more patrols in the sequence -- all complete.");
                yield break;
            }

            if (combinationRuleSet == null)
            {
                Debug.LogError("[PatrolRuntimeController] CombinationRuleSet not assigned.");
                yield break;
            }

            // Only the "show and hold" half plays here -- the teleport/anomaly-apply block below
            // runs while the screen is still fully black, and RoundIntroHide (the fade back to
            // the scene) doesn't run until after that's done. Otherwise the fade-out would reveal
            // the player still standing wherever they were before snapping to the guard room.
            bool showingIntro = RoundIntroShow != null;
            if (showingIntro)
            {
                SetPlayerControlsEnabled?.Invoke(false);
                yield return RoundIntroShow(BuildRoundLabel(patrolSequence[index]));
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
            knockState.ResetForRound();
            roundStartTime = Time.time;

            anomalyApplier.ResetAll(sceneBindings);
            ResetGuardRoomDoor?.Invoke();
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

            if (showingIntro)
            {
                if (RoundIntroHide != null)
                {
                    yield return RoundIntroHide();
                }
                SetPlayerControlsEnabled?.Invoke(true);
            }
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
        // Called by GuardRoomReturnTrigger when the player walks out of the guard room. Only
        // SoundFromExhibit's cue cares -- it waits for this instead of playing from round start.
        public void NotifyLeftGuardRoom()
        {
            if (CurrentState != State.PatrolActive || Time.time - roundStartTime < SpawnGraceSeconds)
            {
                return;
            }

            anomalyApplier.StartPendingSoundCue();
        }

        public void RecordVisit(TargetRef target)
        {
            if (CurrentState != State.PatrolActive)
            {
                return;
            }

            if (target.Kind == TargetKind.GuardRoomReturn)
            {
                // The player now spawns literally inside GuardRoomReturnTrigger's own volume (see
                // TeleportPlayerToGuardRoom), so re-enabling the CharacterController there re-fires
                // OnTriggerEnter the same frame the round starts -- a genuine walk back out and in
                // takes far longer than this window, so anything this soon after StartPatrolAt is
                // that spawn artifact, not a real return.
                if (Time.time - roundStartTime < SpawnGraceSeconds)
                {
                    return;
                }

                TryCompletePatrol();
                return;
            }

            CurrentProgress.RecordVisit(target);
            Debug.Log($"[PatrolRuntimeController] Checked: {target}");

            HandleRoutineAction(target);
            HandleKnockVisit(target);
            HandleAnomalyAction(target);
            CheckLiveRecheckViolation(target);

            bool wasAlreadyViolated = terminalAbortState.Violated;
            terminalAbortState.NotifyVisit(target);
            if (terminalAbortState.Violated && !wasAlreadyViolated)
            {
                var terminal = FindTerminalAnomaly();
                if (terminal != null)
                {
                    StartCoroutine(TriggerDeath(terminal.Id));
                }
            }
        }

        // KnockOnDoor's E-key side (see KnockOnDoorState): E on the entrance while it's still
        // knocking is the forbidden "touching the door" and fails on the spot; E after it has
        // stopped is the required check, recorded for PatrolEvaluator to read at round end.
        private void HandleKnockVisit(TargetRef target)
        {
            if (target.Kind != TargetKind.EntranceDoor || !IsAnomalyActive(DeathSequenceIds.KnockOnDoor))
            {
                return;
            }

            switch (knockState.NotifyEntranceVisit(sceneBindings))
            {
                case KnockOnDoorState.VisitOutcome.Violated:
                    CurrentProgress.RecordAction(target, ActionTag.OperateEntranceDoor);
                    StartCoroutine(TriggerDeath(DeathSequenceIds.KnockOnDoor));
                    break;

                case KnockOnDoorState.VisitOutcome.Checked:
                    CurrentProgress.RecordAction(target, ActionTag.KeepDistanceAndWait);
                    break;
            }
        }

        private bool IsAnomalyActive(string id)
        {
            foreach (var anomaly in currentAnomalies)
            {
                if (anomaly.Id == id)
                {
                    return true;
                }
            }
            return false;
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
                // Same squeak as the player's own flip -- both are a hand turning a frame.
                anomalyApplier.PlayPaintingFlipSound(sceneBindings, target);
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
                    StartCoroutine(TriggerDeath(violator));
                }
                else
                {
                    // Button beep for a routine adjustment or a no-op press alike; HighHumidity's
                    // press goes straight into its death sting instead.
                    var thermometer = sceneBindings?.Resolve(target);
                    if (thermometer != null)
                    {
                        SoundBank.PlayAt(SoundBank.Instance?.HumidityButton, thermometer.position);
                    }
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
                        anomalyApplier.PlayPaintingFlipSound(sceneBindings, target);
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
        //
        // Deliberately doesn't call TriggerDeath -- unlike the gaze/facing/thermostat/terminal
        // violations, this is a "handling/order" rule rather than an in-the-moment observation
        // reaction, so it stays in the 순찰 종료 판정형 bucket: silently noted here, and
        // Evaluate() independently catches it (via the same FindForbiddenAnomaly check, at guard-
        // room-return time) into result.ForbiddenAnomalyActions for FinishPatrol's shared death.
        private void CheckLiveRecheckViolation(TargetRef target)
        {
            if (CurrentProgress.VisitCount(target) != PatrolEvaluator.RecheckVisitThreshold)
            {
                return;
            }

            string violator = PatrolEvaluator.FindForbiddenAnomaly(target, ActionTag.RecheckExhibit, currentAnomalies);
            if (violator != null)
            {
                Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} -- {violator} re-check violated " +
                          "(deferred: judged at guard-room return, not an immediate death).");
            }
        }

        // Single entry point for every "this ends the round right now" path -- the terminal abort
        // and the three live-detected forbidden actions (eye contact, landscape stare, sound-cue
        // turn-back, forbidden thermostat touch) all funnel through here instead of each one
        // separately freezing the player/changing state, so there's exactly one place that can go
        // wrong instead of five. The bool+state guard blocks a second violation (detected in the
        // same or a later frame) from starting a second death while one is already playing.
        private IEnumerator TriggerDeath(string deathId)
        {
            if (deathSequenceRunning || CurrentState == State.DeathSequence)
            {
                yield break;
            }

            deathSequenceRunning = true;
            roundFailed = true;
            activeNotice = $"규칙 위반: {deathId}";
            CurrentState = State.DeathSequence;
            SetPlayerControlsEnabled?.Invoke(false);
            Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} -- death sequence triggered ({deathId}).");

            if (PlayDeathSequence != null)
            {
                yield return PlayDeathSequence(deathId);
            }
            else
            {
                Debug.LogError($"[PatrolRuntimeController] PlayDeathSequence hook is not registered (deathId={deathId}) -- " +
                                "resetting to Day 1 with no death visual. Is DeathSequenceUI missing from the scene?");
            }

            deathSequenceRunning = false;
            StartPatrolAt(0);
        }

        // Every round starts "경비실을 나와" per the design doc -- without this the player just
        // stays wherever the previous round ended (or, on the very first round, wherever
        // LobbyGrayboxBuilder's one-time BuildPlayer placement happened to be), neither of which is
        // the guard room. Reuses the already-bound GuardRoomReturn target instead of a new spawn
        // marker, matching PatrolSceneBindings' existing TargetRef -> Transform convention.
        //
        // PatrolProfile has no stored display label -- day number is implicit in PatrolIndex (two
        // patrols per day, AM1 then AM5; see AnomalyDataBuilder's CreateProfile calls), so this
        // derives "N일차 새벽 M시" the same way each time rather than storing a redundant string.
        private static string BuildRoundLabel(PatrolProfile profile)
        {
            int day = (profile.PatrolIndex - 1) / 2 + 1;
            string timeText = profile.Slot == TimeSlot.AM1 ? "새벽 1시" : "새벽 5시";
            return $"{day}일차 {timeText}";
        }

        // This lands the player literally inside GuardRoomReturnTrigger's own volume (it covers
        // most of the small guard room) -- see RecordVisit's SpawnGraceSeconds guard for why that
        // doesn't immediately re-end the round it just started.
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
            controller.transform.position = new Vector3(spawn.position.x, 0f, spawn.position.z);
            controller.enabled = true;
        }

        // Called whenever the player physically reaches the guard room (both slots now -- see
        // RecordVisit). Walking back in is a commitment, not a progress check: this evaluates once
        // and whatever it finds is final. An incomplete checklist used to just bounce the player
        // back out with a "go finish it" notice, but a half-done patrol is now as fatal as a
        // forbidden action -- both end here, in 순찰 종료 판정형's shared death (see FinishPatrol).
        private void TryCompletePatrol()
        {
            var observation = observationMonitor.BuildReport();
            var result = PatrolEvaluator.Evaluate(CurrentProfile.Duty, CurrentProgress, currentAnomalies, observation,
                looseObservationTracker.Observed, routineState, terminalAbortState.Violated);

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

            if (LastResult.Success)
            {
                CurrentState = State.Complete;
                StartPatrolAt(currentIndex + 1);
            }
            else
            {
                // 순찰 종료 판정형: this round's forbidden violation was only ever judged here (at
                // guard-room return), not caught live -- e.g. RecheckExhibit (see
                // CheckLiveRecheckViolation). Same shared death as every other failure, just a
                // later trigger point; TriggerDeath resets to Day 1 the same way regardless of id.
                StartCoroutine(TriggerDeath(DeathSequenceIds.PatrolFailed));
            }
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

        // Editor-only preview entry point (see DeathSequenceTestKeys) -- every real death is gated
        // behind a randomly rolled anomaly, so checking one specific sting is otherwise a matter of
        // replaying rounds until the right one comes up. Restricted to PatrolActive so it can't
        // interleave with StartPatrolRoutine's own intro coroutine.
        public void DebugTriggerDeath(string deathId)
        {
            if (CurrentState != State.PatrolActive)
            {
                Debug.LogWarning($"[PatrolRuntimeController] Ignoring debug death '{deathId}' -- no patrol is active.");
                return;
            }

            StartCoroutine(TriggerDeath(deathId));
        }
#endif
    }
}
