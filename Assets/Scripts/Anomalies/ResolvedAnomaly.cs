using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // An AnomalyDefinition with its placeholder targets substituted for concrete ones
    // (see AnomalyTargetResolver). This is what the Conflict Validator and the generator
    // actually compare against each other and against the active PatrolDuty.
    public class ResolvedAnomaly : IActionConstraint
    {
        public AnomalyDefinition Source { get; }
        public IReadOnlyList<ActionRequirement> RequiredActions { get; }
        public IReadOnlyList<ActionRequirement> ForbiddenActions { get; }

        public string Id => Source.Id;

        public ResolvedAnomaly(AnomalyDefinition source, IReadOnlyList<ActionRequirement> requiredActions,
            IReadOnlyList<ActionRequirement> forbiddenActions)
        {
            Source = source;
            RequiredActions = requiredActions;
            ForbiddenActions = forbiddenActions;
        }
    }
}
