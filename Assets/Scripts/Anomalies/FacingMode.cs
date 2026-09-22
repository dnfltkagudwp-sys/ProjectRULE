namespace RuleGhost.Anomalies
{
    // How a FacingSensor angle reading should be interpreted for a given anomaly rule.
    public enum FacingMode
    {
        Toward, // player is looking roughly at the target (small angle)
        Away, // player has turned mostly away from the target (large angle)
        KeepInFront // target just isn't directly behind the player (looser than Toward)
    }
}
