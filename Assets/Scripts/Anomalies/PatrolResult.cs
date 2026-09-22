using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Base-duty fields (success, what's missing, whether the entrance wasn't last) plus
    // ForbiddenAnomalyActions -- ids of active anomalies whose forbidden action was detected
    // (e.g. touching the thermostat during HighHumidity, continuing the patrol instead of
    // aborting during a terminal anomaly, or a gaze/facing rule violation) -- and
    // MissingObservations -- ids of active anomalies whose gaze/facing-based required action
    // (e.g. turning away from a landscape) was never satisfied this round.
    public class PatrolResult
    {
        public bool Success;
        public List<TargetRef> MissingTargets = new();
        public bool EntranceNotLast;
        public List<string> ForbiddenAnomalyActions = new();
        public List<string> MissingObservations = new();
    }
}
