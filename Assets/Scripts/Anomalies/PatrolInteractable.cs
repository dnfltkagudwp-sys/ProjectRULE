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

        private Transform player;

        public TargetRef Target => kind == TargetKind.SpecificPainting
            ? TargetRef.Painting(wall, index)
            : TargetRef.Simple(kind);

        private void Update()
        {
            if (player == null)
            {
                var controller = FindFirstObjectByType<CharacterController>();
                if (controller != null)
                {
                    player = controller.transform;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || player == null)
            {
                return;
            }

            float dist = Vector3.Distance(player.position, transform.position);
            if (dist <= interactRange && keyboard.eKey.wasPressedThisFrame)
            {
                PatrolRuntimeController.Instance?.RecordVisit(Target);
            }
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
#endif
    }
}
