using System;

namespace RuleGhost.Anomalies
{
    [Serializable]
    public struct TargetRef : IEquatable<TargetRef>
    {
        public TargetKind Kind;
        public PaintingWall Wall;
        public int Index; // 1-based painting slot; negative values are resolver placeholders (see TargetPlaceholder)

        public static TargetRef Painting(PaintingWall wall, int index) =>
            new TargetRef { Kind = TargetKind.SpecificPainting, Wall = wall, Index = index };

        public static TargetRef Simple(TargetKind kind) => new TargetRef { Kind = kind };

        public bool Equals(TargetRef other) =>
            Kind == other.Kind && Wall == other.Wall && Index == other.Index;

        public override bool Equals(object obj) => obj is TargetRef other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ (int)Wall;
                hash = hash * 397 ^ Index;
                return hash;
            }
        }

        public override string ToString() =>
            Kind == TargetKind.SpecificPainting ? $"Painting_{Wall}_{Index}" : Kind.ToString();
    }
}
