using System.Collections.Generic;
using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RuleGhost.UI
{
    // The guard's 근무수칙 as a physical booklet. It sits on the guard room desk (RulebookPickup);
    // until the player picks it up, Tab does nothing. Once acquired it stays acquired for the rest
    // of the run (rounds restart inside the same scene, so this instance persists across them) --
    // Tab then opens/closes it, and Left/Right (or the mouse wheel) turn its pages. Arrow keys
    // rather than A/D for page turns, since A/D still walk the player while the book is open.
    // The full 근무수칙 1-9 are always shown -- a real work manual, not a hint system that narrows
    // itself to the current round; the player has to recognize which rule applies themselves.
    //
    // Direct Keyboard/Mouse polling, no EventSystem/ScrollRect -- same convention as the rest of
    // the project.
    public class RulebookUI : MonoBehaviour
    {
        public static RulebookUI Instance { get; private set; }

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text pageIndicatorText;
        [SerializeField] private GameObject hintObject;
        [SerializeField] private GameObject pickupPromptObject;
        [SerializeField] private int rulesPerPage = RulebookTextBuilder.DefaultRulesPerPage;

        private readonly List<string> pages = new();
        private int pageIndex;

        public bool Acquired { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (hintObject != null) hintObject.SetActive(false);
            if (pickupPromptObject != null) pickupPromptObject.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Acquire()
        {
            Acquired = true;
            if (hintObject != null) hintObject.SetActive(true);
            SetPickupPrompt(false);
        }

        public void SetPickupPrompt(bool visible)
        {
            if (pickupPromptObject != null && pickupPromptObject.activeSelf != visible)
            {
                pickupPromptObject.SetActive(visible);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Acquired || panelRoot == null)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                bool willOpen = !panelRoot.activeSelf;
                panelRoot.SetActive(willOpen);
                var bank = SoundBank.Instance;
                SoundBank.Play2D(willOpen ? bank?.RulebookOpen : bank?.RulebookClose);
                if (willOpen)
                {
                    Refresh();
                }
            }

            if (panelRoot.activeSelf)
            {
                TickPageTurn(keyboard);
            }
        }

        private void Refresh()
        {
            pages.Clear();

            var controller = PatrolRuntimeController.Instance;
            if (controller == null)
            {
                pages.Add("규칙 정보를 불러올 수 없습니다.");
            }
            else
            {
                pages.AddRange(RulebookTextBuilder.BuildPages(controller.AllProfiles, rulesPerPage));
            }

            pageIndex = 0;
            ShowPage();
        }

        private void TickPageTurn(Keyboard keyboard)
        {
            int step = 0;
            if (keyboard.rightArrowKey.wasPressedThisFrame) step++;
            if (keyboard.leftArrowKey.wasPressedThisFrame) step--;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel < -0.01f) step++;
                else if (wheel > 0.01f) step--;
            }

            if (step == 0)
            {
                return;
            }

            int next = Mathf.Clamp(pageIndex + step, 0, pages.Count - 1);
            if (next != pageIndex)
            {
                pageIndex = next;
                ShowPage();
                SoundBank.Play2D(SoundBank.Instance?.RulebookPageTurn);
            }
        }

        private void ShowPage()
        {
            if (bodyText != null && pageIndex < pages.Count)
            {
                bodyText.text = pages[pageIndex];
            }

            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"- {pageIndex + 1} / {pages.Count} -";
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject panel, Text body, Text pageIndicator, GameObject hint, GameObject pickupPrompt)
        {
            panelRoot = panel;
            bodyText = body;
            pageIndicatorText = pageIndicator;
            hintObject = hint;
            pickupPromptObject = pickupPrompt;
        }
#endif
    }
}
