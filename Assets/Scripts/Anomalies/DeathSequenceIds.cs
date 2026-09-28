namespace RuleGhost.Anomalies
{
    // Keys passed through PatrolRuntimeController.PlayDeathSequence -- mostly just the existing
    // anomaly.Id strings used everywhere else (ResolvedAnomaly.Id, PatrolResult.ForbiddenAnomalyActions),
    // collected in one place so call sites don't retype/typo them. PatrolFailed is the one
    // synthetic id, for the shared "순찰 종료 판정형" death that isn't any single anomaly.
    public static class DeathSequenceIds
    {
        public const string EyesOpenPortrait = "EyesOpenPortrait";
        public const string PersonInLandscape = "PersonInLandscape";
        public const string SoundFromExhibit = "SoundFromExhibit";
        public const string HighHumidity = "HighHumidity";
        public const string InspectionDoorWideOpen = "InspectionDoorWideOpen";
        public const string PatrolFailed = "PatrolFailed";
    }
}
