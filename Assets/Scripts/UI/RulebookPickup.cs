using RuleGhost.Anomalies;
using RuleGhost.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuleGhost.UI
{
    // The rulebook prop on the guard room desk. Looking straight at it (camera center ray, same
    // "only what you're looking at" rule as DoorTestInteraction) shows an "E : 규칙서 획득" prompt;
    // E picks it up, hides the prop and unlocks Tab in RulebookUI for the rest of the run.
    public class RulebookPickup : MonoBehaviour
    {
        [SerializeField] private float pickupRange = 2.5f;

        private Camera playerCamera;
        private Collider pickupCollider;

        private void Awake()
        {
            pickupCollider = GetComponent<Collider>();
        }

        private void OnDisable()
        {
            RulebookUI.Instance?.SetPickupPrompt(false);
        }

        private void Update()
        {
            var ui = RulebookUI.Instance;
            if (ui == null || ui.Acquired)
            {
                return;
            }

            if (playerCamera == null)
            {
                var controller = FindFirstObjectByType<GrayboxTestController>();
                if (controller != null)
                {
                    playerCamera = controller.GetComponentInChildren<Camera>();
                }
            }

            bool looking = IsLookingAt(playerCamera, pickupCollider, pickupRange);
            ui.SetPickupPrompt(looking);

            var keyboard = Keyboard.current;
            if (looking && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                ui.Acquire();
                SoundBank.Play2D(SoundBank.Instance?.RulebookPickup);
                gameObject.SetActive(false);
            }
        }

        public static bool IsLookingAt(Camera camera, Collider target, float maxDistance)
        {
            if (camera == null || target == null)
            {
                return false;
            }

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            return hit.collider == target;
        }
    }
}
