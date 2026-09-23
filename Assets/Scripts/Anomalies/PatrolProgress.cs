using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Plain record of what the player actually did this round -- deliberately knows nothing
    // about PatrolDuty or what was required. PatrolEvaluator is the only thing that compares
    // this against the duty.
    public class PatrolProgress
    {
        private readonly List<TargetRef> visitOrder = new();
        private readonly Dictionary<TargetRef, int> visitCounts = new();
        // Which specific (target, action) pairs actually happened -- e.g. "straightened THIS
        // painting" or "adjusted the humidity" -- as opposed to merely visiting a target. A plain
        // E-key visit alone can mean several different things depending on what was going on at
        // that target (just checking it vs. fixing something on it); this is what lets
        // PatrolEvaluator tell those apart instead of relying on HasVisited for everything.
        private readonly HashSet<(TargetRef Target, ActionTag Action)> actionsTaken = new();

        public IReadOnlyList<TargetRef> VisitOrder => visitOrder;

        public TargetRef? LastChecked => visitOrder.Count > 0 ? visitOrder[^1] : (TargetRef?)null;

        public void RecordVisit(TargetRef target)
        {
            visitOrder.Add(target);
            visitCounts[target] = visitCounts.TryGetValue(target, out var count) ? count + 1 : 1;
        }

        public bool HasVisited(TargetRef target) => visitCounts.ContainsKey(target);

        // How many times the player has pressed E on this target this round -- RecheckExhibit
        // (e.g. PersonInLandscape) needs to tell "inspected once, as required by the base duty"
        // apart from "went back and rechecked it," which HasVisited alone can't distinguish.
        public int VisitCount(TargetRef target) => visitCounts.TryGetValue(target, out var count) ? count : 0;

        // Records that a specific action -- not just a visit -- happened at a target (e.g.
        // straightening a painting, adjusting the humidity). Does not also call RecordVisit;
        // callers that want both call each explicitly.
        public void RecordAction(TargetRef target, ActionTag action) => actionsTaken.Add((target, action));

        public bool HasPerformedAction(TargetRef target, ActionTag action) => actionsTaken.Contains((target, action));
    }
}
