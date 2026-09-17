namespace RuleGhost.Anomalies
{
    // Plain actions only — whether an action is required or forbidden comes from which list
    // (ActionRequirement / ForbiddenActions) it appears in, not from the tag name itself.
    public enum ActionTag
    {
        InspectAllPaintings,
        StraightenPainting,
        MakeEyeContact,
        RecheckExhibit,
        TurnAwayFromExhibit,
        FlipPainting,
        ModifyOriginalPainting,
        TouchThermostat,
        CloseInspectionDoorFully,
        ReturnToGuardRoom,
        ContinuePatrol,
        FaceExhibit,
        ShowBackToExhibit,
        OperateEntranceDoor,
        KeepDistanceAndWait,
        CheckEntranceDoorClosed
    }
}
