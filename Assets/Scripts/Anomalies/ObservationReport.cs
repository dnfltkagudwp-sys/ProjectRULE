using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Snapshot of what ObservationRuleMonitor accumulated over a round -- which anomalies' gaze/
    // facing-based required action was satisfied at some point, and which had their gaze/facing-
    // based forbidden action triggered. Deliberately just anomaly ids (not targets or timestamps);
    // PatrolEvaluator only needs to know pass/fail per anomaly for this.
    public class ObservationReport
    {
        public static readonly ObservationReport Empty = new();

        public HashSet<string> SatisfiedRequiredAnomalyIds { get; set; } = new();
        public HashSet<string> ViolatedForbiddenAnomalyIds { get; set; } = new();
    }
}
