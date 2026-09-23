using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Base-duty fields (success, what's missing, whether the entrance wasn't last) plus
    // ForbiddenAnomalyActions -- ids of active anomalies whose forbidden action was detected
    // (e.g. touching the thermostat during HighHumidity, continuing the patrol instead of
    // aborting during a terminal anomaly, a gaze/facing rule violation, or rechecking an exhibit
    // that should've been left alone) -- MissingObservations -- ids of active anomalies whose
    // gaze/facing-based required action (e.g. turning away from a landscape) was never satisfied
    // this round -- MissingRechecks -- ids of active anomalies whose required RecheckExhibit
    // (E-key re-interaction) never happened -- and MissingRoutineTasks -- ordinary (non-Anomaly)
    // Routine conditions this round (a crooked painting, an out-of-range humidity reading) that
    // were never resolved, identified by short fixed labels ("TiltedPainting"/"RoutineHumidity")
    // rather than an id, since a Routine condition has no AnomalyDefinition/id of its own.
    public class PatrolResult
    {
        public bool Success;
        public List<TargetRef> MissingTargets = new();
        public bool EntranceNotLast;
        public List<string> ForbiddenAnomalyActions = new();
        public List<string> MissingObservations = new();
        public List<string> MissingRechecks = new();
        public List<string> MissingRoutineTasks = new();
    }
}
