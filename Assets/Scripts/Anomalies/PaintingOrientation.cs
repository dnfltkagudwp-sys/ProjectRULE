using UnityEngine;

namespace RuleGhost.Anomalies
{
    // The local axis a wall-mounted painting must rotate around to tilt/flip within its own wall
    // plane instead of swinging out of it. LobbyGrayboxBuilder.BuildPaintings sizes North
    // paintings as (width, height, depth) -- local Z is depth -- but sizes West/East paintings as
    // (depth, height, width) to fit their wall's orientation, putting depth on local X instead.
    // Every painting is otherwise unrotated (identity), so these are also the correct world axes.
    public static class PaintingOrientation
    {
        public static Vector3 DepthAxis(PaintingWall wall) => wall == PaintingWall.North ? Vector3.forward : Vector3.right;
    }
}
