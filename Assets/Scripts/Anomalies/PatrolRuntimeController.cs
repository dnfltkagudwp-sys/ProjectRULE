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
    // Anomaly application is not implemented yet: PatrolGenerator.Generate still runs (so the
    // Conflict Validator/generation path stays exercised), but the resulting anomalies are
    // logged and otherwise ignored this milestone. See the class-level comment in
    // AnomalyRuntimeApplier (not yet created) for where that hooks in.
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

        public static PatrolRuntimeController Instance { get; private set; }

        public State CurrentState { get; private set; } = State.Idle;
        public PatrolProfile CurrentProfile { get; private set; }
        public PatrolProgress CurrentProgress { get; private set; }
        public PatrolResult LastResult { get; private set; }

        private int currentIndex = -1;
        private Random rng;

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

            var generation = PatrolGenerator.Generate(CurrentProfile, combinationRuleSet, rng);
            Debug.Log($"[PatrolRuntimeController] Patrol {CurrentProfile.PatrolIndex} ({CurrentProfile.Slot}) started -- " +
                      $"{generation.Anomalies.Count} anomaly(ies) generated (not yet applied this milestone).");
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
        public void EditorConfigure(PatrolProfile[] profiles, CombinationRuleSet ruleSet)
        {
            patrolSequence = profiles;
            combinationRuleSet = ruleSet;
        }
#endif
    }
}
