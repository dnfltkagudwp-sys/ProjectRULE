using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuleGhost.Audio
{
    // Footstep sounds from the SoundBank's footsteps slot, one per stride walked. Measured from the
    // player's own position change rather than CharacterController.velocity -- velocity keeps its
    // last value while the controller is disabled (rounds freeze it), which would loop footsteps
    // on a frozen player. A sudden large jump (the round-start teleport) is ignored, not counted.
    public class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private float walkStrideMeters = 1.7f;
        [SerializeField] private float sprintStrideMeters = 2.3f;
        [SerializeField] private float pitchVariation = 0.06f;
        [SerializeField] private float teleportThresholdMeters = 1.5f;

        private AudioSource source;
        private Vector3 lastPosition;
        private float stride;
        private int lastIndex = -1;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private void Start()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPosition;
            lastPosition = pos;
            delta.y = 0f;

            float moved = delta.magnitude;
            if (moved > teleportThresholdMeters)
            {
                stride = 0f;
                return;
            }

            stride += moved;

            var keyboard = Keyboard.current;
            bool sprinting = keyboard != null && keyboard.leftShiftKey.isPressed;
            float needed = sprinting ? sprintStrideMeters : walkStrideMeters;
            if (stride < needed)
            {
                return;
            }

            stride = 0f;
            PlayStep();
        }

        private void PlayStep()
        {
            var bank = SoundBank.Instance;
            var clips = bank != null ? bank.Footsteps : null;
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            // Never the same clip twice in a row (when there's more than one to pick from).
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex)
            {
                index = (index + 1) % clips.Length;
            }
            lastIndex = index;

            var clip = clips[index];
            if (clip == null)
            {
                return;
            }

            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clip, bank.FootstepVolume);
        }
    }
}
