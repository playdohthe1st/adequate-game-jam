using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AdequateEnough
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Particles")]
        [SerializeField] private GameObject dustParticles;

        [Header("Video Intro Settings")]
        [SerializeField] private GameObject videoPanel; // GameObject holding your RawImage and VideoPlayer
        [SerializeField] private VideoPlayer videoPlayer; // Drag the VideoPlayer component here
        [SerializeField] private AudioClip introAudio;
        private AudioSource audioSource;

        [Header("Credits")]
        [SerializeField] private GameObject creditsPanel;
        private CreditsScroll creditsScroll;

        private bool isVideoPlaying = false;
        private void Awake()
        {
            creditsPanel.SetActive(false);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
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
                mainPanel.SetActive(false);
                if (dustParticles != null) dustParticles.SetActive(false);
                StartCoroutine(PlayIntroVideo());
            }
            else
            {
                StartGameTransition();
            }
        }

        private IEnumerator PlayIntroVideo()
        {
            yield return StartCoroutine(ScreenFader.Instance.Fade(0f, 1f, 1f));

            videoPanel.SetActive(true);
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
                yield return null;

            videoPlayer.Play();

            if (introAudio != null)
            {
                audioSource.clip = introAudio;
                audioSource.Play();
            }

            yield return new WaitForEndOfFrame();
            yield return StartCoroutine(ScreenFader.Instance.Fade(1f, 0f, 0.3f));
        }
        public void OnCreditsPressed()
        {
            creditsPanel.SetActive(true);
            creditsScroll = creditsPanel.GetComponentInChildren<CreditsScroll>();
            creditsScroll.StartCredits();

        }

        // Automatically runs when the video finishes playing naturally
        private void OnVideoFinished(VideoPlayer vp)
        {
            StartGameTransition();
        }

        private void StartGameTransition()
        {
            isVideoPlaying = false;
            audioSource.Stop();

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