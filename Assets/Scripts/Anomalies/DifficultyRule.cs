namespace RuleGhost.Anomalies
{
    public enum DifficultyRule
    {
        None,             // 정상 순찰 (1일차 오전1시)
        SingleZeroOrOne,  // 단일 이상현상 0~1개
        SingleOne,        // 단일 이상현상 1개
        CompoundOnly      // 복합 이상현상 1개만 (최대 2개, 추가 단일 없음 — 3개 동시 발동으로 인한 피로도 우려로 2026-09-18 변경)
    }
}
