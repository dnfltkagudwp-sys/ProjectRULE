using System;

namespace RuleGhost.Anomalies
{
    // Rule 6 (FlippedPainting) fixed pairing: West1<->East1, West2<->East2, West3<->East3.
    // Deliberately a static lookup, not a geometry calculation — the pairing is a design
    // decision, not a spatial one.
    public static class MirrorPairTable
    {
        public static TargetRef GetMirror(TargetRef target)
        {
            if (target.Kind != TargetKind.SpecificPainting || target.Wall == PaintingWall.North)
            {
                throw new ArgumentException(
                    $"Mirror pairing only applies to West/East wall paintings, got {target}.");
            }

            var mirrorWall = target.Wall == PaintingWall.West ? PaintingWall.East : PaintingWall.West;
            return TargetRef.Painting(mirrorWall, target.Index);
        }
    }
}
