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

        private int currentIndex = -1;
        private Random rng;
        private readonly AnomalyRuntimeApplier anomalyApplier = new();

        private void Awake()
        {
            Instance = this;
            rng = new Random();
        }

        private void Start()
        {
            StartPatrolAt(0);
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
            var generation = PatrolGenerator.Generate(CurrentProfile, combinationRuleSet, rng);
            if (sceneBindings != null)
            {
                anomalyApplier.Apply(sceneBindings, generation.Anomalies);
            }
            else if (generation.Anomalies.Count > 0)
            {
                Debug.LogWarning("[PatrolRuntimeController] PatrolSceneBindings not assigned -- anomalies generated but not applied.");
            }

            Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} ({CurrentProfile.Slot}) started -- " +
                      $"{generation.Anomalies.Count} anomaly(ies) applied.");
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
            LastResult = PatrolEvaluator.Evaluate(CurrentProfile.Duty, CurrentProgress);

            if (LastResult.Success)
            {
                Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} SUCCESS.");
            }
            else
            {
                string missing = LastResult.MissingTargets.Count > 0
                    ? string.Join(", ", LastResult.MissingTargets)
                    : "(none)";
                Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} FAILED. " +
                          $"Missing=[{missing}] EntranceNotLast={LastResult.EntranceNotLast}");
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
