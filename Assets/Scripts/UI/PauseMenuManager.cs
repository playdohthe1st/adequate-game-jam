using UnityEngine;
using UnityEngine.InputSystem;

namespace AdequateEnough
{
    public class PauseMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;

        private bool isPaused;

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                Toggle();
        }

        public void Toggle()
        {
            if (settingsPanel.activeSelf)
            {
                OnSettingsBackPressed();
                return;
            }

            if (isPaused) Resume();
            else Pause();
        }

        private void Pause()
        {
            isPaused = true;
            Time.timeScale = 0f;
            pausePanel.SetActive(true);
        }

        public void OnResumePressed()
        {
            Resume();
        }

        private void Resume()
        {
            isPaused = false;
            Time.timeScale = 1f;
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        public void OnSettingsPressed()
        {
            pausePanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        public void OnSettingsBackPressed()
        {
            settingsPanel.SetActive(false);
            pausePanel.SetActive(true);
        }

        public void OnQuitPressed()
        {
            ScreenFader.Instance.FadeToScene("Splash", 1);
        }
    }
}
