namespace RuleGhost.Anomalies
{
    // North wall paintings are never a target or mirror partner for Rule 6 (FlippedPainting) —
    // that anomaly only ever occurs between the West and East walls.
    public enum PaintingWall
    {
        North,
        West,
        East
    }
}
