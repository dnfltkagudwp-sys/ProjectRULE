namespace RuleGhost.Anomalies
{
    public enum DifficultyRule
    {
        None,                           // 정상 순찰 (1일차 오전1시)
        SingleZeroOrOne,                // 단일 이상현상 0~1개
        SingleOne,                      // 단일 이상현상 1개
        CompoundOnePlusOptionalSingle   // 복합 이상현상 1개 + 필요시 독립 단일 0~1개
    }
}
