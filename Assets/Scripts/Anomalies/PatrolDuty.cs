using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Rule 1 (AM1) / Rule 2 (AM5) are baseline patrol duties, not randomly-selected anomalies —
    // they are always active for their time slot, so their targets never need resolving.
    [CreateAssetMenu(menuName = "RuleGhost/Anomalies/Patrol Duty", fileName = "Duty_")]
    public class PatrolDuty : ScriptableObject, IActionConstraint
    {
        [SerializeField] private string id;
        [SerializeField] private TimeSlot slot;
        [SerializeField] private List<ActionRequirement> requiredActions = new();
        [SerializeField] private List<ActionRequirement> forbiddenActions = new();

        public string Id => id;
        public TimeSlot Slot => slot;
        public IReadOnlyList<ActionRequirement> RequiredActions => requiredActions;
        public IReadOnlyList<ActionRequirement> ForbiddenActions => forbiddenActions;

#if UNITY_EDITOR
        public void EditorInitialize(string newId, TimeSlot newSlot,
            IEnumerable<ActionRequirement> required, IEnumerable<ActionRequirement> forbidden)
        {
            id = newId;
            slot = newSlot;
            requiredActions = new List<ActionRequirement>(required);
            forbiddenActions = new List<ActionRequirement>(forbidden);
        }
#endif
    }
}
