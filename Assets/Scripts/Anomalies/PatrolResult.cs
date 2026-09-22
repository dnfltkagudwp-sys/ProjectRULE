using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Base-duty fields (success, what's missing, whether the entrance wasn't last) plus
    // ForbiddenAnomalyActions -- ids of active anomalies whose forbidden action was detected
    // (e.g. touching the thermostat during HighHumidity, or continuing the patrol instead of
    // aborting during a terminal anomaly).
    public class PatrolResult
    {
        public bool Success;
        public List<TargetRef> MissingTargets = new();
        public bool EntranceNotLast;
        public List<string> ForbiddenAnomalyActions = new();
    }
}
