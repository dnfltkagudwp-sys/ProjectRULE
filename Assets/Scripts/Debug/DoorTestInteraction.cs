using UnityEngine;
using UnityEngine.InputSystem;

namespace RuleGhost.Debugging
{
    // Temporary graybox-only "walk up and press E" toggle for the inspection
    // door hinge, so the door's open/close feel can be playtested directly
    // instead of only through editor menu items. Not part of the patrol/rule
    // system -- that will drive the same hinge transform later for the
    // ajar/wide-open anomaly states, but through its own logic, not this key.
    public class DoorTestInteraction : MonoBehaviour
    {
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float interactRange = 2.5f;
        [SerializeField] private float rotateSpeed = 120f;

        private bool isOpen;
        private float targetAngle;
        private Transform player;

        private void Update()
        {
            if (player == null)
            {
                var controller = FindFirstObjectByType<GrayboxTestController>();
                if (controller != null) player = controller.transform;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && player != null)
            {
                float dist = Vector3.Distance(player.position, transform.position);
                if (dist <= interactRange && keyboard.eKey.wasPressedThisFrame)
                {
                    isOpen = !isOpen;
                    targetAngle = isOpen ? openAngle : 0f;
                }
            }

            var current = transform.localRotation.eulerAngles.y;
            float next = Mathf.MoveTowardsAngle(current, targetAngle, rotateSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, next, 0f);
        }
    }
}
