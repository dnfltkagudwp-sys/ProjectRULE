using System;
using System.Collections.Generic;
using System.Linq;

namespace RuleGhost.Anomalies
{
    // Implements the generation order from the design doc: figure out how many anomalies this
    // round needs, pick candidates from the pool, and re-pick on any DENY/action conflict
    // instead of ever producing an unplayable combination.
    public static class PatrolGenerator
    {
        public static PatrolGenerationResult Generate(PatrolProfile profile, CombinationRuleSet ruleSet, Random rng)
        {
            var result = new PatrolGenerationResult();
            var active = new List<IActionConstraint> { profile.Duty };

            switch (profile.Difficulty)
            {
                case DifficultyRule.None:
                    break;

                case DifficultyRule.SingleZeroOrOne:
                    if (rng.Next(2) == 1)
                    {
                        AddSingle(profile, ruleSet, rng, result, active);
                    }
                    break;

                case DifficultyRule.SingleOne:
                    AddSingle(profile, ruleSet, rng, result, active);
                    break;

                case DifficultyRule.CompoundOnePlusOptionalSingle:
                    AddCompoundPair(profile, ruleSet, rng, result, active);
                    if (rng.Next(2) == 1)
                    {
                        AddSingle(profile, ruleSet, rng, result, active);
                    }
                    break;
            }

            return result;
        }

        private static void AddSingle(PatrolProfile profile, CombinationRuleSet ruleSet, Random rng,
            PatrolGenerationResult result, List<IActionConstraint> active)
        {
            foreach (var def in ShuffledPool(profile, rng))
            {
                if (result.Anomalies.Any(r => r.Source == def))
                {
                    continue;
                }

                var resolved = AnomalyTargetResolver.Resolve(def, rng);
                if (ConflictValidator.CanAdd(resolved, active, ruleSet, out _))
                {
                    result.Anomalies.Add(resolved);
                    active.Add(resolved);
                    return;
                }
            }
        }

        private static void AddCompoundPair(PatrolProfile profile, CombinationRuleSet ruleSet, Random rng,
            PatrolGenerationResult result, List<IActionConstraint> active)
        {
            var pool = ShuffledPool(profile, rng);

            for (int i = 0; i < pool.Count; i++)
            {
                var firstResolved = AnomalyTargetResolver.Resolve(pool[i], rng);
                if (!ConflictValidator.CanAdd(firstResolved, active, ruleSet, out _))
                {
                    continue;
                }

                var withFirst = new List<IActionConstraint>(active) { firstResolved };

                for (int j = 0; j < pool.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var secondResolved = AnomalyTargetResolver.Resolve(pool[j], rng);
                    if (ConflictValidator.CanAdd(secondResolved, withFirst, ruleSet, out _))
                    {
                        result.Anomalies.Add(firstResolved);
                        result.Anomalies.Add(secondResolved);
                        active.Add(firstResolved);
                        active.Add(secondResolved);
                        return;
                    }
                }
            }
        }

        private static List<AnomalyDefinition> ShuffledPool(PatrolProfile profile, Random rng)
        {
            var pool = new List<AnomalyDefinition>(profile.AnomalyPool);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return pool;
        }
    }
}
