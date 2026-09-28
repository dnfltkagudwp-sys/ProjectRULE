using System.Collections;
using UnityEngine;

namespace RuleGhost.UI
{
    // Shared full-screen black CanvasGroup used by both RoundIntroUI and DeathSequenceUI. Sharing
    // one instance (rather than each owning its own overlay) means a death sequence ending on full
    // black and the next round's intro starting on full black never hand off between two separate
    // canvases -- it's the same alpha value the whole time, so there's no seam/flash risk between
    // "death overlay hides" and "intro overlay shows" (the intro's own fade-in from an already-1
    // alpha is just a harmless no-op hold).
    public class ScreenFadeController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        public float Alpha => canvasGroup != null ? canvasGroup.alpha : 0f;

        public IEnumerator FadeTo(float target, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            canvasGroup.blocksRaycasts = target > 0f;
            float from = canvasGroup.alpha;
            if (duration <= 0f)
            {
                canvasGroup.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = target;
        }

#if UNITY_EDITOR
        public void EditorConfigure(CanvasGroup group)
        {
            canvasGroup = group;
        }
#endif
    }
}
