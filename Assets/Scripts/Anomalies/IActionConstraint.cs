using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Implemented by both PatrolDuty and (resolved) Anomalies so the Conflict Validator can
    // check a patrol's active Duty and active Anomalies through one shared code path.
    public interface IActionConstraint
    {
        string Id { get; }
        IReadOnlyList<ActionRequirement> RequiredActions { get; }
        IReadOnlyList<ActionRequirement> ForbiddenActions { get; }
    }
}
