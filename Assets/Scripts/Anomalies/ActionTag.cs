namespace RuleGhost.Anomalies
{
    // Plain actions only — whether an action is required or forbidden comes from which list
    // (ActionRequirement / ForbiddenActions) it appears in, not from the tag name itself.
    public enum ActionTag
    {
        InspectAllPaintings,
        // A painting bumped crooked as an ordinary Routine condition (not an Anomaly) -- fixed by
        // an E-key press, the only thing E does to a painting during AM1.
        StraightenPainting,
        MakeEyeContact,
        RecheckExhibit,
        TurnAwayFromExhibit,
        FlipPainting,
        ModifyOriginalPainting,
        // Renamed from TouchThermostat -- the sensor only ever knows an E-key press happened on
        // the thermometer, never "the player merely touched it"; whether that press is required
        // (Routine 56-69%, see AdjustHumidity) or forbidden (HighHumidity, 70%+) is decided by
        // which Duty/Anomaly is active, not by the tag name itself.
        AdjustThermostat,
        CloseInspectionDoorFully,
        ReturnToGuardRoom,
        ContinuePatrol,
        FaceExhibit,
        ShowBackToExhibit,
        OperateEntranceDoor,
        KeepDistanceAndWait,
        CheckEntranceDoorClosed,
        // AM1's "confirm every painting's state" requirement -- gaze-based (see
        // PaintingObservationTracker), distinct from an E-key visit. Not the same as
        // InspectAllPaintings, which stays the WholePatrol-placeholder expansion tag for the
        // (still-supported, visit-based) generic case.
        ObservePainting,
        // Plain "went and looked at it" for the thermometer/inspection door -- gaze-based (see
        // LooseObservationTracker), required every AM1 and AM5 round regardless of whether an
        // anomaly is active on that target -- distinct from AdjustThermostat/
        // CloseInspectionDoorFully, which are E-key actions that actually change something.
        InspectThermometer,
        InspectInspectionDoor,
        // Fixing an ordinary Routine humidity reading (56-69%) back to normal -- distinct from
        // AdjustThermostat, which is the same physical E-key press but judged against HighHumidity
        // (70%+) instead, where it's forbidden.
        AdjustHumidity
    }
}
