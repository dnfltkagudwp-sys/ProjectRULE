using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RuleGhost.UI
{
    // Player-facing "규칙서" panel: Tab toggles it, showing the FULL 근무수칙 1-9 every time --
    // not just this round's active ones. This is a real work manual the guard was handed, not a
    // hint system that narrows itself to the current situation; the player has to recognize which
    // rule applies themselves, same as reading an actual employee handbook.
    //
    // All 9 rules concatenated run taller than the panel, so the body sits in a masked viewport
    // and this class scrolls it directly (contentRect.anchoredPosition) rather than using a Unity
    // ScrollRect -- this project has no EventSystem anywhere, since every interaction already
    // polls Keyboard.current/Mouse.current directly instead of going through Unity's UI event
    // system, and a ScrollRect's wheel/drag handling needs one.
    public class RulebookUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text bodyText;
        [SerializeField] private RectTransform contentRect;
        [SerializeField] private RectTransform viewportRect;

        // Exposed (not const) because the right feel depends on how the new Input System reports
        // wheel deltas on your actual hardware, which isn't something to guess blindly from code --
        // tune these in the Inspector during Play Mode, then bake the value back in here.
        // Wheel sign follows the common "scroll down reveals what's below" convention -- flip the
        // sign in TickScroll if it feels backwards once you've actually tried it.
        [SerializeField] private float wheelScrollScale = 20f;
        [SerializeField] private float keyScrollSpeed = 900f; // px/sec

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && panelRoot != null)
            {
                bool willOpen = !panelRoot.activeSelf;
                panelRoot.SetActive(willOpen);
                if (willOpen)
                {
                    Refresh();
                }
            }

            if (panelRoot != null && panelRoot.activeSelf)
            {
                TickScroll();
            }
        }

        private void Refresh()
        {
            if (bodyText == null)
            {
                return;
            }

            var controller = PatrolRuntimeController.Instance;
            if (controller == null)
            {
                bodyText.text = "규칙 정보를 불러올 수 없습니다.";
            }
            else
            {
                string text = RulebookTextBuilder.BuildAll(controller.AllProfiles);
                bodyText.text = string.IsNullOrEmpty(text) ? "등록된 규칙이 없습니다." : text;
            }

            if (contentRect != null)
            {
                // Force the ContentSizeFitter to resolve the new text's height NOW, not on the
                // next layout pass -- TickScroll's clamp below needs contentRect.rect.height to
                // already be correct the same frame the panel opens.
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, 0f);
            }
        }

        private void TickScroll()
        {
            if (contentRect == null || viewportRect == null)
            {
                return;
            }

            float maxScroll = Mathf.Max(0f, contentRect.rect.height - viewportRect.rect.height);
            if (maxScroll <= 0f)
            {
                return;
            }

            float delta = 0f;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                delta -= mouse.scroll.ReadValue().y * wheelScrollScale;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.downArrowKey.isPressed) delta += keyScrollSpeed * Time.deltaTime;
                if (keyboard.upArrowKey.isPressed) delta -= keyScrollSpeed * Time.deltaTime;
            }

            if (delta == 0f)
            {
                return;
            }

            float newY = Mathf.Clamp(contentRect.anchoredPosition.y + delta, 0f, maxScroll);
            contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, newY);
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject panel, Text body, RectTransform content, RectTransform viewport)
        {
            panelRoot = panel;
            bodyText = body;
            contentRect = content;
            viewportRect = viewport;
        }
#endif
    }
}
