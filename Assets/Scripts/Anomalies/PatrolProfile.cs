using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    // One entry per patrol in sequence (1..6 for the current 3-day x 2-patrol structure).
    // Adding/removing rounds is just adding/removing PatrolProfile assets — no code change.
    [CreateAssetMenu(menuName = "RuleGhost/Anomalies/Patrol Profile", fileName = "Patrol_")]
    public class PatrolProfile : ScriptableObject
    {
        [SerializeField] private int patrolIndex;
        [SerializeField] private TimeSlot slot;
        [SerializeField] private PatrolDuty duty;
        [SerializeField] private List<AnomalyDefinition> anomalyPool = new();
        [SerializeField] private DifficultyRule difficulty;

        public int PatrolIndex => patrolIndex;
        public TimeSlot Slot => slot;
        public PatrolDuty Duty => duty;
        public IReadOnlyList<AnomalyDefinition> AnomalyPool => anomalyPool;
        public DifficultyRule Difficulty => difficulty;

#if UNITY_EDITOR
        public void EditorInitialize(int index, TimeSlot newSlot, PatrolDuty newDuty,
            IEnumerable<AnomalyDefinition> pool, DifficultyRule rule)
        {
            patrolIndex = index;
            slot = newSlot;
            duty = newDuty;
            anomalyPool = new List<AnomalyDefinition>(pool);
            difficulty = rule;
        }
#endif
    }
}
