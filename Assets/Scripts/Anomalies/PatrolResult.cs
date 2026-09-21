using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Deliberately just the three fields the base-duty rule cares about right now
    // (success, what's missing, whether the entrance wasn't last) -- extend this
    // when anomaly-specific pass/fail reasons are needed, not before.
    public class PatrolResult
    {
        public bool Success;
        public List<TargetRef> MissingTargets = new();
        public bool EntranceNotLast;
    }
}
