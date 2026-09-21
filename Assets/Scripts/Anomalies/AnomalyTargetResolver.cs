using System;
using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Substitutes an AnomalyDefinition's placeholder targets (TargetPlaceholder) for concrete
    // ones chosen at generation time, producing a ResolvedAnomaly ready for conflict checking.
    public static class AnomalyTargetResolver
    {
        private static readonly PaintingWall[] AllWalls = { PaintingWall.North, PaintingWall.West, PaintingWall.East };

        public static ResolvedAnomaly Resolve(AnomalyDefinition def, Random rng)
        {
            TargetRef any = default, discovered = default, mirror = default;

            if (def.UsesMirrorPairing)
            {
                var sideWall = rng.Next(2) == 0 ? PaintingWall.West : PaintingWall.East;
                int index = rng.Next(1, 4); // 1..3
                discovered = TargetRef.Painting(sideWall, index);
                mirror = MirrorPairTable.GetMirror(discovered);
            }
            else if (def.NeedsPaintingTarget)
            {
                // Restrict to the anomaly's allowed wall(s) if it has any (e.g. a portrait
                // anomaly only ever lands on the North wall); otherwise any wall is fair game.
                IReadOnlyList<PaintingWall> walls = def.PaintingWallOptions.Count > 0 ? def.PaintingWallOptions : AllWalls;
                var wall = walls[rng.Next(walls.Count)];
                int index = rng.Next(1, 4);
                any = TargetRef.Painting(wall, index);
            }

            var required = Substitute(def.RequiredActions, any, discovered, mirror);
            var forbidden = Substitute(def.ForbiddenActions, any, discovered, mirror);
            return new ResolvedAnomaly(def, required, forbidden);
        }

        private static List<ActionRequirement> Substitute(IReadOnlyList<ActionRequirement> source,
            TargetRef anyPaintingReal, TargetRef mirrorDiscoveredReal, TargetRef mirrorTargetReal)
        {
            var list = new List<ActionRequirement>(source.Count);
            foreach (var req in source)
            {
                var resolvedTarget = req.Target;
                if (req.Target.Equals(TargetPlaceholder.AnyPainting))
                {
                    resolvedTarget = anyPaintingReal;
                }
                else if (req.Target.Equals(TargetPlaceholder.MirrorDiscovered))
                {
                    resolvedTarget = mirrorDiscoveredReal;
                }
                else if (req.Target.Equals(TargetPlaceholder.MirrorTarget))
                {
                    resolvedTarget = mirrorTargetReal;
                }

                list.Add(new ActionRequirement(resolvedTarget, req.Action));
            }

            return list;
        }
    }
}
