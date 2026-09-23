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
        // Player-facing rule sentence, verbatim from the design doc (e.g. "오전 1시가 되면...") --
        // for the 규칙서 UI. Distinct from Id, which is only ever a lookup key.
        [SerializeField, TextArea] private string ruleText;
        // The design doc's own rule number (1 for AM1, 2 for AM5) -- lets the 규칙서 UI show every
        // rule in the original 1-9 order instead of Id/asset order.
        [SerializeField] private int ruleNumber;

        public string Id => id;
        public TimeSlot Slot => slot;
        public string RuleText => ruleText;
        public int RuleNumber => ruleNumber;
        public IReadOnlyList<ActionRequirement> RequiredActions => requiredActions;
        public IReadOnlyList<ActionRequirement> ForbiddenActions => forbiddenActions;

#if UNITY_EDITOR
        public void EditorInitialize(string newId, TimeSlot newSlot,
            IEnumerable<ActionRequirement> required, IEnumerable<ActionRequirement> forbidden,
            string newRuleText = "", int newRuleNumber = 0)
        {
            id = newId;
            slot = newSlot;
            requiredActions = new List<ActionRequirement>(required);
            forbiddenActions = new List<ActionRequirement>(forbidden);
            ruleText = newRuleText;
            ruleNumber = newRuleNumber;
        }
#endif
    }
}
