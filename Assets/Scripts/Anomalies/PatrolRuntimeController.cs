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

        private IReadOnlyList<ResolvedAnomaly> currentAnomalies = Array.Empty<ResolvedAnomaly>();
        private int currentIndex = -1;
        private Random rng;
        private readonly AnomalyRuntimeApplier anomalyApplier = new();
        private readonly ObservationRuleMonitor observationMonitor = new();
        private Camera playerCamera;
        private Transform playerRoot;
        private bool loggedMissingCameraWarning;

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

            anomalyApplier.ResetAll(sceneBindings);
            observationMonitor.ResetAll();
            var generation = PatrolGenerator.Generate(CurrentProfile, combinationRuleSet, rng);
            currentAnomalies = generation.Anomalies;
            if (sceneBindings != null)
            {
                anomalyApplier.Apply(sceneBindings, generation.Anomalies);
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

        // Called by PatrolInteractable when the player checks something. Ignored outside
        // PatrolActive so a stray interaction after a round ends can't corrupt the next one.
        public void RecordVisit(TargetRef target)
        {
            if (CurrentState != State.PatrolActive)
            {
                return;
            }

            CurrentProgress.RecordVisit(target);
            Debug.Log($"[PatrolRuntimeController] Checked: {target}");

            if (target.Kind == TargetKind.EntranceDoor)
            {
                FinishPatrol();
            }
        }

        private void FinishPatrol()
        {
            CurrentState = State.Result;
            var observation = observationMonitor.BuildReport();
            LastResult = PatrolEvaluator.Evaluate(CurrentProfile.Duty, CurrentProgress, currentAnomalies, observation);

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
                Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} FAILED. " +
                          $"Missing=[{missing}] EntranceNotLast={LastResult.EntranceNotLast} " +
                          $"ViolatedAnomalyRules=[{violated}] MissingObservations=[{missingObservations}]");
            }

            CurrentState = State.Complete;
            StartPatrolAt(currentIndex + 1);
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
