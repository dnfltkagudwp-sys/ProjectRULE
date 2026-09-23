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
    public class RulebookUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text bodyText;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame || panelRoot == null)
            {
                return;
            }

            bool willOpen = !panelRoot.activeSelf;
            panelRoot.SetActive(willOpen);
            if (willOpen)
            {
                Refresh();
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
                return;
            }

            string text = RulebookTextBuilder.BuildAll(controller.AllProfiles);
            bodyText.text = string.IsNullOrEmpty(text) ? "등록된 규칙이 없습니다." : text;
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject panel, Text body)
        {
            panelRoot = panel;
            bodyText = body;
        }
#endif
    }
}
