using UnityEngine;
using UnityEngine.InputSystem;

namespace RuleGhost.Debugging
{
    // Temporary graybox-only walk/look controller for playtesting space, sightlines and
    // movement time. Not part of the patrol/gameplay system.
    [RequireComponent(typeof(CharacterController))]
    public class GrayboxTestController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6.0f;
        [SerializeField] private float mouseSensitivity = 0.25f;
        [SerializeField] private float gravity = -9.81f;

        private CharacterController controller;
        private Camera cam;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            cam = GetComponentInChildren<Camera>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null)
            {
                return;
            }

            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(Vector3.up, delta.x);
                pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
                if (cam != null)
                {
                    cam.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
                }
            }

            float h = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float v = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;

            Vector3 move = (transform.right * h + transform.forward * v);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            verticalVelocity = controller.isGrounded ? -0.5f : verticalVelocity + gravity * Time.deltaTime;
            move = move * speed + Vector3.up * verticalVelocity;
            controller.Move(move * Time.deltaTime);

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
