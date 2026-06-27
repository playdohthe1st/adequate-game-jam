using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AdequateEnough
{
    public class PauseMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private LoreManager loreManager;

        private PlayerController player;
        private bool isPaused;
        private void Awake()
        {
            player = FindAnyObjectByType<PlayerController>();
        }
        private void Update()
        {
        if (!player.isVideoPlaying && loreManager.activePanelIndex == -1)

            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
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
            EventSystem.current?.SetSelectedGameObject(null);
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
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void OnQuitPressed()
        {
            ScreenFader.Instance.FadeToScene("Splash", 1);
            settingsPanel.SetActive(false);
            pausePanel.SetActive(false);
        }
    }
}
