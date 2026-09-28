using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuleGhost.Debugging
{
    // Editor-only preview keys for the five death sequences, since each one is normally gated
    // behind a randomly rolled anomaly (checking one specific sting would otherwise mean replaying
    // rounds until that anomaly comes up). Number keys 1-6 while a patrol is active:
    //   1 초상화 눈맞춤  2 풍경화 응시  3 소리 그림  4 고습도 온도계  5 점검문 완전개방  6 순찰 실패(공통)
    public class DeathSequenceTestKeys : MonoBehaviour
    {
#if UNITY_EDITOR
        private static readonly string[] DeathIdsByKey =
        {
            DeathSequenceIds.EyesOpenPortrait,
            DeathSequenceIds.PersonInLandscape,
            DeathSequenceIds.SoundFromExhibit,
            DeathSequenceIds.HighHumidity,
            DeathSequenceIds.InspectionDoorWideOpen,
            DeathSequenceIds.PatrolFailed,
        };

        private void Update()
        {
            var keyboard = Keyboard.current;
            var controller = PatrolRuntimeController.Instance;
            if (keyboard == null || controller == null)
            {
                return;
            }

            for (int i = 0; i < DeathIdsByKey.Length; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    controller.DebugTriggerDeath(DeathIdsByKey[i]);
                    return;
                }
            }
        }
#endif
    }
}
