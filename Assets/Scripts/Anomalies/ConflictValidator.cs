using System.Collections.Generic;
using System.Linq;

namespace RuleGhost.Anomalies
{
    public static class ConflictValidator
    {
        // Can `candidate` be added alongside everything already active this round
        // (the PatrolDuty plus any Anomalies already selected)?
        public static bool CanAdd(ResolvedAnomaly candidate, IReadOnlyList<IActionConstraint> active,
            CombinationRuleSet ruleSet, out string reason)
        {
            var activeAnomalyDefs = active.OfType<ResolvedAnomaly>().Select(r => r.Source).ToList();

            // 1. Combination matrix — only meaningful between two Anomalies.
            foreach (var existingDef in activeAnomalyDefs)
            {
                if (ruleSet.GetState(candidate.Source, existingDef) == CombinationState.Deny)
                {
                    reason = $"combination matrix DENY: {candidate.Id} x {existingDef.Id}";
                    return false;
                }
            }

            // 2. Terminal exclusion — a Terminal anomaly can't share a round with any Anomaly
            //    that still requires an extra action from the player.
            foreach (var existingDef in activeAnomalyDefs)
            {
                bool candidateBlocksExisting = candidate.Source.IsTerminal && existingDef.RequiredActions.Count > 0;
                bool existingBlocksCandidate = existingDef.IsTerminal && candidate.Source.RequiredActions.Count > 0;
                if (candidateBlocksExisting || existingBlocksCandidate)
                {
                    reason = $"terminal exclusion: {candidate.Id} x {existingDef.Id}";
                    return false;
                }
            }

            // 3. Actual action-set compatibility — a shared target is fine as long as every
            //    Required/Forbidden action across the active set can be satisfied at once.
            //    A Terminal candidate overrides the standing PatrolDuty (the work rules
            //    themselves say an explicit override instruction always wins), so Duty is
            //    excluded from this comparison when candidate is Terminal.
            bool skipDuty = candidate.Source.IsTerminal;
            var required = new List<ActionRequirement>(candidate.RequiredActions);
            var forbidden = new List<ActionRequirement>(candidate.ForbiddenActions);
            foreach (var existing in active)
            {
                if (skipDuty && existing is PatrolDuty)
                {
                    continue;
                }

                required.AddRange(existing.RequiredActions);
                forbidden.AddRange(existing.ForbiddenActions);
            }

            foreach (var req in required)
            {
                foreach (var forb in forbidden)
                {
                    if (req.Target.Equals(forb.Target) && req.Action == forb.Action)
                    {
                        reason = $"same target ({req.Target}) both requires and forbids {req.Action}";
                        return false;
                    }
                }
            }

            reason = null;
            return true;
        }
    }
}
