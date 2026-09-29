using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuleGhost.Debugging
{
    // Temporary graybox-only "walk up and press E" toggle for the inspection
    // door hinge, so the door's open/close feel can be playtested directly
    // instead of only through editor menu items. Not part of the patrol/rule
    // system -- that will drive the same hinge transform later for the
    // ajar/wide-open anomaly states, but through its own logic, not this key.
    //
    // In practice this only lives on GuardRoom_Door_Hinge (the inspection door is fully owned by
    // AnomalyRuntimeApplier.SetDoorAngle instead) -- so ResetGuardRoomDoor closing this on every
    // round start doesn't fight with the anomaly system's own inspection-door state.
    public class DoorTestInteraction : MonoBehaviour
    {
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float interactRange = 2.5f;
        [SerializeField] private float rotateSpeed = 120f;

        private bool isOpen;
        private float targetAngle;
        private Transform player;
        private Camera playerCamera;
        private Collider leafCollider;

        private void Awake()
        {
            // The leaf (a child of this hinge) carries the actual Collider -- cached once so Update
            // doesn't need GetComponentInChildren every frame.
            leafCollider = GetComponentInChildren<Collider>();
            PatrolRuntimeController.ResetGuardRoomDoor += CloseImmediately;
        }

        private void OnDestroy()
        {
            PatrolRuntimeController.ResetGuardRoomDoor -= CloseImmediately;
        }

        // Crosshair-style check: the camera's center ray must actually hit this door's leaf first,
        // so E only works on what the player is looking at (a wall, another object, or the door
        // behind them all block/miss it). Distance alone let E open the door from anywhere within
        // range regardless of where the player was facing.
        public static bool IsLookingAtDoor(Camera camera, Collider leaf, float maxDistance)
        {
            if (camera == null || leaf == null)
            {
                return false;
            }

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            return hit.collider == leaf;
        }

        private void CloseImmediately()
        {
            isOpen = false;
            targetAngle = 0f;
            transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (player == null)
            {
                var controller = FindFirstObjectByType<GrayboxTestController>();
                if (controller != null)
                {
                    player = controller.transform;
                    playerCamera = controller.GetComponentInChildren<Camera>();
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && player != null && keyboard.eKey.wasPressedThisFrame)
            {
                float dist = Vector3.Distance(player.position, transform.position);
                if (dist <= interactRange && IsLookingAtDoor(playerCamera, leafCollider, interactRange + 1f))
                {
                    isOpen = !isOpen;
                    targetAngle = isOpen ? openAngle : 0f;

                    var bank = SoundBank.Instance;
                    if (bank != null)
                    {
                        SoundBank.PlayAt(isOpen ? bank.GuardDoorOpen : bank.GuardDoorClose, transform.position);
                    }
                }
            }

            var current = transform.localRotation.eulerAngles.y;
            float next = Mathf.MoveTowardsAngle(current, targetAngle, rotateSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, next, 0f);

            if (leafCollider != null)
            {
                // Solid at rest (closed OR fully open) so the door still actually blocks passage
                // and reads as a real object; a trigger only while actively swinging, since a
                // Transform-driven collider (no Rigidbody) doesn't push the CharacterController out
                // of its way when it sweeps into the player standing close enough to have opened
                // it -- it just shoves/snags them instead.
                leafCollider.isTrigger = !Mathf.Approximately(next, targetAngle);
            }
        }
    }
}
