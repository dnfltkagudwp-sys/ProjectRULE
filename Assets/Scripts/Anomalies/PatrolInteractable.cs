using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuleGhost.Anomalies
{
    // Attach to any scene object a patrol target resolves to (a painting, the thermometer, the
    // inspection door, the entrance marker). Walking within range and pressing E reports this
    // object's own TargetRef to PatrolRuntimeController.RecordVisit -- the same walk-up-and-
    // press-E pattern already used by DoorTestInteraction, reused here instead of building a
    // second input scheme, since the project has no general IInteractable system yet. Finds the
    // player via CharacterController rather than GrayboxTestController directly, since this
    // assembly (RuleGhost.Anomalies) deliberately has no reference to Assembly-CSharp/
    // RuleGhost.Debugging.
    public class PatrolInteractable : MonoBehaviour
    {
        [SerializeField] private TargetKind kind;
        [SerializeField] private PaintingWall wall;
        [SerializeField] private int index = 1; // 1-based, only meaningful for SpecificPainting
        // Paintings sit almost flush against the wall, but the walkable aisle can be several
        // meters away from it -- 2.5m meant standing right up against the frame to interact.
        [SerializeField] private float interactRange = 3.5f;

        // One E press reaches exactly one target. Ranges overlap all over the lobby (neighbouring
        // paintings, North_1/2 with the thermometer, North_3/East_3 with the inspection door), and
        // when every in-range interactable answered the same press, straightening a portrait also
        // pressed the thermometer (fatal under HighHumidity), touched the door during a terminal
        // abort, or counted as a recheck of the landscape next door. Whichever instance updates
        // first in a frame resolves the press for all of them: the in-range target closest to the
        // centre of the player's view wins.
        private static readonly List<PatrolInteractable> Active = new();
        private static int lastResolvedFrame = -1;

        private Transform player;
        private Transform playerCamera;

        public TargetRef Target => kind == TargetKind.SpecificPainting
            ? TargetRef.Painting(wall, index)
            : TargetRef.Simple(kind);

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            if (player == null)
            {
                var controller = FindFirstObjectByType<CharacterController>();
                if (controller != null)
                {
                    player = controller.transform;
                    var camera = controller.GetComponentInChildren<Camera>();
                    playerCamera = camera != null ? camera.transform : null;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || player == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (lastResolvedFrame == Time.frameCount)
            {
                return;
            }
            lastResolvedFrame = Time.frameCount;

            var chosen = PickTarget(Active, player, playerCamera);
            if (chosen != null)
            {
                PatrolRuntimeController.Instance?.RecordVisit(chosen.Target);
            }
        }

        // Horizontal facing only, like FacingSensor -- paintings hang well above eye level, so a
        // pitch component would favour whatever happens to be lower. Falls back to nearest when
        // there's no camera to face with.
        private static PatrolInteractable PickTarget(IEnumerable<PatrolInteractable> candidates, Transform player, Transform camera)
        {
            PatrolInteractable best = null;
            float bestScore = float.NegativeInfinity;
            Vector3 forward = camera != null ? Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized : Vector3.zero;

            foreach (var candidate in candidates)
            {
                float dist = Vector3.Distance(player.position, candidate.transform.position);
                if (dist > candidate.interactRange)
                {
                    continue;
                }

                float score;
                if (camera != null)
                {
                    Vector3 toTarget = Vector3.ProjectOnPlane(candidate.transform.position - camera.position, Vector3.up);
                    score = toTarget.sqrMagnitude > 0.0001f ? Vector3.Dot(forward, toTarget.normalized) : 1f;
                }
                else
                {
                    score = -dist;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

#if UNITY_EDITOR
        public void EditorConfigure(TargetKind targetKind, PaintingWall paintingWall = default, int paintingIndex = 1)
        {
            kind = targetKind;
            wall = paintingWall;
            index = paintingIndex;
        }

        public void EditorSetInteractRange(float range)
        {
            interactRange = range;
        }

        public float EditorInteractRange => interactRange;

        // Lets editor checks run the exact same selection against scene objects without Play Mode
        // (Active is only filled by OnEnable at runtime).
        public static PatrolInteractable EditorPickTarget(IEnumerable<PatrolInteractable> candidates,
            Transform player, Transform camera) => PickTarget(candidates, player, camera);
#endif
    }
}
