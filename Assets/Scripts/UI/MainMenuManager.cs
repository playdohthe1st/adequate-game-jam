using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video; // Required for the VideoPlayer component

namespace AdequateEnough
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Video Intro Settings")]
        [SerializeField] private GameObject videoPanel; // GameObject holding your RawImage and VideoPlayer
        [SerializeField] private VideoPlayer videoPlayer; // Drag the VideoPlayer component here

        private bool isVideoPlaying = false;

        private void Start()
        {
            ShowMain();

            // Set up the listener for when the video reaches its loop point (the end)
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached += OnVideoFinished;
            }
        }

        public void OnStartPressed()
        {
            if (isVideoPlaying) return;

            if (videoPlayer != null && videoPanel != null)
            {
                isVideoPlaying = true;

                // Hide the main menu UI buttons
                mainPanel.SetActive(false);

                // Turn on the video panel and play
                videoPanel.SetActive(true);
                videoPlayer.Play();
            }
            else
            {
                // Fallback: If no video is assigned, skip straight to the game
                StartGameTransition();
            }
        }

        private void Update()
        {
            // Allow the player to skip the video using standard skip keys
            if (isVideoPlaying)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                {
                    StartGameTransition();
                }
            }
        }

        // Automatically runs when the video finishes playing naturally
        private void OnVideoFinished(VideoPlayer vp)
        {
            StartGameTransition();
        }

        private void StartGameTransition()
        {
            isVideoPlaying = false;

            // Clean up the event listener to prevent any errors/leaks
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
            }

            // Call fade logic on other script
            ScreenFader.Instance.FadeToScene("MainLevel",0.1f);
        }

        public void OnSettingsPressed()
        {
            mainPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        public void OnSettingsBackPressed()
        {
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void ShowMain()
        {
            mainPanel.SetActive(true);
            settingsPanel.SetActive(false);

            // Ensure the video panel is safely hidden when the menu opens
            if (videoPanel != null) videoPanel.SetActive(false);
        }
    }
}