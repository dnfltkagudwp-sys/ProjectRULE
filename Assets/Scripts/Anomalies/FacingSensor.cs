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
    }
}
