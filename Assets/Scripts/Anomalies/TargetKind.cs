namespace RuleGhost.Anomalies
{
    public enum TargetKind
    {
        SpecificPainting,
        Thermometer,
        InspectionDoor,
        EntranceDoor,
        WholePatrol,
        // AM1's actual closing action ("경비실로 복귀하면 순찰 종료") -- distinct from EntranceDoor,
        // which is AM5's own closing check. Resolved to a Trigger Collider inside the guard room,
        // not an E-key PatrolInteractable, since walking back in is a traversal action.
        GuardRoomReturn
    }
}
