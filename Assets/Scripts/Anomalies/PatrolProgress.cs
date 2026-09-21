using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Plain record of what the player actually did this round -- deliberately knows nothing
    // about PatrolDuty or what was required. PatrolEvaluator is the only thing that compares
    // this against the duty.
    public class PatrolProgress
    {
        private readonly List<TargetRef> visitOrder = new();
        private readonly HashSet<TargetRef> visited = new();

        public IReadOnlyList<TargetRef> VisitOrder => visitOrder;

        public TargetRef? LastChecked => visitOrder.Count > 0 ? visitOrder[^1] : (TargetRef?)null;

        public void RecordVisit(TargetRef target)
        {
            visitOrder.Add(target);
            visited.Add(target);
        }

        public bool HasVisited(TargetRef target) => visited.Contains(target);
    }
}
