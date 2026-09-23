namespace RuleGhost.Anomalies
{
    // Allow: no matrix restriction (ConflictValidator still separately blocks a genuine
    // required/forbidden action clash on a shared target).
    // Conditional: fine as long as the pair doesn't land on the exact same resolved target this
    // round; ConflictValidator denies it only when it does.
    // Deny: never allowed together, regardless of what they'd resolve to.
    public enum CombinationState
    {
        Allow,
        Conditional,
        Deny
    }
}
