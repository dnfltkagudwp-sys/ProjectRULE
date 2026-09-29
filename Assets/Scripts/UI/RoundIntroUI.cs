using System;
using System.Collections;
using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.UI;

namespace RuleGhost.UI
{
    // Round-start black screen showing "N일차 새벽 M시" -- PatrolRuntimeController yields on
    // ShowAndHold()/FadeOut() before actually starting the round, so the player is looking at this
    // instead of the guard room while the round's anomalies get applied behind it.
    //
    // Registers itself into PatrolRuntimeController's static hooks rather than being looked up by
    // that controller directly -- RuleGhost.Anomalies is its own assembly and can't reference this
    // UI-folder script, only the other way around (see those fields' comments).
    public class RoundIntroUI : MonoBehaviour
    {
        [SerializeField] private ScreenFadeController fade;
        [SerializeField] private Text label;
        [SerializeField] private CanvasGroup labelGroup;

        private const float FadeInSeconds = 0.4f;
        private const float HoldSeconds = 1.6f;
        private const float FadeOutSeconds = 0.5f;
        // The label gets its own fade on top of the shared blackout. Without it the text simply
        // appears: a round that starts after a death is already on a fully black screen, so the
        // blackout's own fade-in has nothing left to do and the words just pop in.
        private const float LabelDelaySeconds = 0.35f;
        private const float LabelFadeSeconds = 0.5f;

        private void Awake()
        {
            // The scene starts fully black. The first round's intro used to fade IN to black from a
            // clear screen, which showed the museum for a moment (right after the title screen's
            // fade-out) before going dark again. Every later round already begins black or fades to
            // black on purpose, so only the very first one needed this.
            if (fade != null)
            {
                fade.SetAlpha(1f);
            }
        }

        private void OnEnable()
        {
            PatrolRuntimeController.RoundIntroShow = ShowAndHold;
            PatrolRuntimeController.RoundIntroHide = FadeOut;
        }

        private void OnDisable()
        {
            // Only clear a hook if it's still ours -- guards against this OnDisable clobbering a
            // different (newer) instance's registration, e.g. if a scene reload briefly overlapped
            // an old and new RoundIntroUI.
            if (PatrolRuntimeController.RoundIntroShow == (Func<string, IEnumerator>)ShowAndHold)
            {
                PatrolRuntimeController.RoundIntroShow = null;
            }
            if (PatrolRuntimeController.RoundIntroHide == (Func<IEnumerator>)FadeOut)
            {
                PatrolRuntimeController.RoundIntroHide = null;
            }
        }

        // Split in two (rather than one Play() that fades out on its own) so
        // PatrolRuntimeController can do the actual teleport/anomaly-apply work in between --
        // while the screen is still fully black -- instead of revealing wherever the player was
        // standing before snapping them to the guard room.
        public IEnumerator ShowAndHold(string text)
        {
            if (fade == null || label == null)
            {
                yield break;
            }

            label.text = text;
            if (labelGroup != null)
            {
                labelGroup.alpha = 0f;
            }

            yield return fade.FadeTo(1f, FadeInSeconds);
            yield return new WaitForSeconds(LabelDelaySeconds);
            SoundBank.Play2D(SoundBank.Instance?.RoundIntro);
            yield return FadeLabel(1f, LabelFadeSeconds);
            yield return new WaitForSeconds(HoldSeconds);
        }

        public IEnumerator FadeOut()
        {
            if (fade == null)
            {
                yield break;
            }

            yield return fade.FadeTo(0f, FadeOutSeconds);

            // Cleared rather than left in place: the label lives inside the shared blackout group,
            // so leftover text would become visible again the moment a death sequence blacks the
            // screen out.
            if (labelGroup != null)
            {
                labelGroup.alpha = 0f;
            }
            label.text = string.Empty;
        }

        private IEnumerator FadeLabel(float target, float duration)
        {
            if (labelGroup == null)
            {
                yield break;
            }

            float from = labelGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                labelGroup.alpha = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }
            labelGroup.alpha = target;
        }

#if UNITY_EDITOR
        public void EditorConfigure(ScreenFadeController fadeController, Text labelText, CanvasGroup labelCanvasGroup)
        {
            fade = fadeController;
            label = labelText;
            labelGroup = labelCanvasGroup;
        }
#endif
    }
}
