using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Pure "is the player's flashlight beam centered on this exact point" check. Deliberately
    // works off a small anchor Transform (EyeAnchor, a child of the painting placed roughly at
    // the portrait's eyes) rather than a collider raycast -- paintings sit close together on the
    // same wall, and a collider hit only tells you which painting's box you struck, not whether
    // the player is precisely centered on this one's eyes. The flashlight (AddPlayerFlashlight.cs)
    // is reused as the game's only "reticle": its narrow inner cone lighting up a portrait's eyes
    // is the player's visual feedback for this check, so no crosshair UI is needed.
    public static class GazeSensor
    {
        public static bool IsWithinGazeCone(Camera camera, Transform paintingTransform, Transform eyeAnchor,
            Transform playerRoot, float maxDistance, float coneHalfAngleDegrees)
        {
            if (camera == null || eyeAnchor == null)
            {
                return false;
            }

            Vector3 camPos = camera.transform.position;
            Vector3 toAnchor = eyeAnchor.position - camPos;
            float distance = toAnchor.magnitude;
            if (distance < 0.001f || distance > maxDistance)
            {
                return false;
            }

            float angle = Vector3.Angle(camera.transform.forward, toAnchor);
            if (angle > coneHalfAngleDegrees)
            {
                return false;
            }

            // A real obstruction (a wall, a pilaster, the statue) between the anchor and the
            // camera blocks the beam. A hit on the painting's own collider (the anchor sits right
            // at/just past its front face) or on the player's own body isn't a real obstruction.
            if (Physics.Linecast(eyeAnchor.position, camPos, out var hit))
            {
                bool hitIsSelf = paintingTransform != null &&
                    (hit.transform == paintingTransform || hit.transform.IsChildOf(paintingTransform));
                bool hitIsPlayer = playerRoot != null &&
                    (hit.transform == playerRoot || hit.transform.IsChildOf(playerRoot));
                if (!hitIsSelf && !hitIsPlayer)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
