namespace RuleGhost.Anomalies
{
    // AnomalyDefinition data is authored against these placeholders when the concrete painting
    // isn't known until generation time. AnomalyTargetResolver substitutes them for real targets.
    public static class TargetPlaceholder
    {
        public static readonly TargetRef AnyPainting =
            new TargetRef { Kind = TargetKind.SpecificPainting, Wall = PaintingWall.North, Index = -1 };

        public static readonly TargetRef MirrorDiscovered =
            new TargetRef { Kind = TargetKind.SpecificPainting, Wall = PaintingWall.North, Index = -2 };

        public static readonly TargetRef MirrorTarget =
            new TargetRef { Kind = TargetKind.SpecificPainting, Wall = PaintingWall.North, Index = -3 };
    }
}
