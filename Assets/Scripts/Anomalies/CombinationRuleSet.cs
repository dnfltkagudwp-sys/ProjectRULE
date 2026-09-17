using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Default-ALLOW + exceptions: pairs not listed here are ALLOW. DENY/CONDITIONAL must be
    // registered explicitly; an explicit ALLOW entry is also permitted purely as a design
    // record ("this pair was reviewed and is fine"), and behaves identically to the default.
    [CreateAssetMenu(menuName = "RuleGhost/Anomalies/Combination Rule Set", fileName = "CombinationRuleSet")]
    public class CombinationRuleSet : ScriptableObject
    {
        [SerializeField] private List<CombinationRule> rules = new();

        public IReadOnlyList<CombinationRule> Rules => rules;

        public CombinationState GetState(AnomalyDefinition a, AnomalyDefinition b)
        {
            foreach (var rule in rules)
            {
                if ((rule.AnomalyA == a && rule.AnomalyB == b) || (rule.AnomalyA == b && rule.AnomalyB == a))
                {
                    return rule.State;
                }
            }

            return CombinationState.Allow;
        }

#if UNITY_EDITOR
        public void EditorSetRules(IEnumerable<CombinationRule> newRules)
        {
            rules = new List<CombinationRule>(newRules);
        }
#endif
    }
}
