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
            var activeAnomalies = active.OfType<ResolvedAnomaly>().ToList();
            var activeAnomalyDefs = activeAnomalies.Select(r => r.Source).ToList();

            // 1. Combination matrix — only meaningful between two Anomalies.
            foreach (var existingDef in activeAnomalyDefs)
            {
                if (ruleSet.GetState(candidate.Source, existingDef) == CombinationState.Deny)
                {
                    reason = $"combination matrix DENY: {candidate.Id} x {existingDef.Id}";
                    return false;
                }
            }

            // 1b. Combination matrix — CONDITIONAL pairs are fine on separate exhibits but not
            //     worth stacking on the exact same one (even when their individual actions don't
            //     literally clash, which check 3 below would already catch on its own). Checked
            //     against each pair's actual resolved targets, not the placeholder definitions.
            foreach (var existing in activeAnomalies)
            {
                if (ruleSet.GetState(candidate.Source, existing.Source) != CombinationState.Conditional)
                {
                    continue;
                }

                var existingTargets = existing.RequiredActions.Concat(existing.ForbiddenActions).Select(a => a.Target);
                bool sameTarget = candidate.RequiredActions.Concat(candidate.ForbiddenActions)
                    .Any(a => existingTargets.Contains(a.Target));
                if (sameTarget)
                {
                    reason = $"combination matrix CONDITIONAL denied (same target): {candidate.Id} x {existing.Id}";
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
