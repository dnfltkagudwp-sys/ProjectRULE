using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Fires when the player physically walks into the guard room, ending an AM1 patrol -- rule 1
    // ends on "경비실로 복귀하면 순찰 종료", not on an E-key check the way AM5's entrance marker
    // works. Trigger-based rather than PatrolInteractable's walk-up-and-press-E pattern, since
    // returning to a room is a traversal action, not something the player deliberately interacts
    // with. Requires a Collider (isTrigger = true) set up by the attaching editor script.
    public class GuardRoomReturnTrigger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() == null)
            {
                return;
            }

            PatrolRuntimeController.Instance?.RecordVisit(TargetRef.Simple(TargetKind.GuardRoomReturn));
        }
    }
}
