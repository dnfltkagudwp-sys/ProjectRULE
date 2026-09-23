using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Pure horizontal-facing math: how far off, in degrees, is the camera's forward direction
    // from the direction toward a target? Y is flattened on both vectors first so looking up/down
    // (pitch) never counts as turning toward or away from something -- only yaw matters, which
    // matches how a player would describe "I'm facing it" or "I turned my back on it".
    public static class FacingSensor
    {
        public static float HorizontalAngleToTarget(Camera camera, Transform target)
        {
            if (camera == null || target == null)
            {
                return 180f;
            }

            Vector3 toTarget = target.position - camera.transform.position;
            toTarget.y = 0f;

            Vector3 forward = camera.transform.forward;
            forward.y = 0f;

            if (toTarget.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                return 180f;
            }

            return Vector3.Angle(forward, toTarget);
        }

        // Angle alone can't tell "facing/not-facing" from "facing, but through a wall or the
        // center statue" -- a straight line between the player and the target has to be clear (or
        // only blocked by the target/player's own collider) and within a sane distance, or the
        // facing rule shouldn't be able to hold at all. Mirrors GazeSensor.IsWithinGazeCone's own
        // self/player exemption, just without a cone-angle component (facing direction is checked
        // separately, via HorizontalAngleToTarget).
        public static bool HasClearLineOfSight(Camera camera, Transform target, Transform playerRoot, float maxDistance)
        {
            if (camera == null || target == null)
            {
                return false;
            }

            Vector3 camPos = camera.transform.position;
            float distance = Vector3.Distance(camPos, target.position);
            if (distance < 0.001f || distance > maxDistance)
            {
                return false;
            }

            if (Physics.Linecast(camPos, target.position, out var hit))
            {
                bool hitIsTarget = hit.transform == target || hit.transform.IsChildOf(target);
                bool hitIsPlayer = playerRoot != null &&
                    (hit.transform == playerRoot || hit.transform.IsChildOf(playerRoot));
                if (!hitIsTarget && !hitIsPlayer)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
