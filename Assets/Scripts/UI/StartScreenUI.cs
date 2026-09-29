using System.Collections;
using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RuleGhost.UI
{
    // Title screen. Direct Keyboard/Mouse polling (no EventSystem/Button), same convention as the
    // rest of the project. Any key or click starts the game, Esc quits. Every timing/text value is
    // a serialized field so it can be tuned in the Inspector without touching code.
    public class StartScreenUI : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Lobby_Graybox";
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private CanvasGroup fadeOutGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Text hintText;

        [SerializeField] private float titleFadeInDelay = 0.8f;
        [SerializeField] private float titleFadeInSeconds = 2.0f;
        [SerializeField] private float hintFadeInDelay = 2.8f;
        [SerializeField] private float hintFadeInSeconds = 1.0f;
        [SerializeField] private float hintPulseSpeed = 1.6f;
        [SerializeField, Range(0f, 1f)] private float hintPulseMinAlpha = 0.25f;
        [SerializeField] private float startFadeOutSeconds = 1.2f;

        private float elapsed;
        private bool starting;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            if (titleGroup != null) titleGroup.alpha = 0f;
            if (hintGroup != null) hintGroup.alpha = 0f;
            if (fadeOutGroup != null) fadeOutGroup.alpha = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (titleGroup != null)
            {
                titleGroup.alpha = Mathf.Clamp01((elapsed - titleFadeInDelay) / Mathf.Max(0.01f, titleFadeInSeconds));
            }

            if (hintGroup != null && !starting)
            {
                float appear = Mathf.Clamp01((elapsed - hintFadeInDelay) / Mathf.Max(0.01f, hintFadeInSeconds));
                float pulse = Mathf.Lerp(hintPulseMinAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(elapsed * hintPulseSpeed * Mathf.PI));
                hintGroup.alpha = appear * pulse;
            }

            if (starting)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Quit();
                return;
            }

            bool pressed = (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                           || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (pressed)
            {
                StartCoroutine(StartGame());
            }
        }

        private IEnumerator StartGame()
        {
            starting = true;
            if (hintGroup != null) hintGroup.alpha = 0f;
            SoundBank.Play2D(SoundBank.Instance?.TitleStart);

            if (fadeOutGroup != null)
            {
                float t = 0f;
                while (t < startFadeOutSeconds)
                {
                    t += Time.deltaTime;
                    fadeOutGroup.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, startFadeOutSeconds));
                    yield return null;
                }
                fadeOutGroup.alpha = 1f;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        public void EditorConfigure(CanvasGroup title, CanvasGroup hint, CanvasGroup fadeOut, Text titleLabel, Text hintLabel)
        {
            titleGroup = title;
            hintGroup = hint;
            fadeOutGroup = fadeOut;
            titleText = titleLabel;
            hintText = hintLabel;
        }
#endif
    }
}
