using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        public void RestartPresed()
        {
            Restart();
        }
        private void Restart()
        {
            ScreenFader.Instance.FadeToScene("MainLevel", 1);
            Destroy(FindFirstObjectByType<PauseMenuManager>());
        }
        public void OnSettingsPressed()
        {
            pausePanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        public void OnSettingsBackPressed()
        {
            settingsPanel.SetActive(false);
            
        }

        public void OnQuitPressed()
        {
            ScreenFader.Instance.FadeToScene("Splash", 1);
            settingsPanel.SetActive(false);
            pausePanel.SetActive(false);
        }
    }
}
