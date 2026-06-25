using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdequateEnough
{
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private CanvasGroup blackScreen;
        [SerializeField] private float fadeDuration = 1f;
        [SerializeField] private float respawnFadeDuration = 1f;
        [SerializeField] private float changeRoomFadeDuration = 1f;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(transform.root.gameObject);
                return;
            }

            Instance = this;

            var rootCanvas = transform.root.GetComponent<Canvas>();
            if (rootCanvas != null)
                rootCanvas.sortingOrder = 999;

            DontDestroyOnLoad(transform.root.gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(Fade(1f, 0f, fadeDuration));
        }

        public void FadeToScene(string sceneName, float fadeDurationValue)
        {
            fadeDuration = fadeDurationValue;
            StartCoroutine(FadeOutThenLoad(sceneName));
        }

        public void RespawnFade(System.Action onBlack)
        {
            StartCoroutine(RespawnFadeRoutine(onBlack));
        }

        private IEnumerator RespawnFadeRoutine(System.Action onBlack)
        {
            yield return StartCoroutine(Fade(0f, 1f, respawnFadeDuration));
            onBlack?.Invoke();
            yield return StartCoroutine(Fade(1f, 0f, respawnFadeDuration));
        }

        private IEnumerator FadeOutThenLoad(string sceneName)
        {
            yield return StartCoroutine(Fade(0f, 1f, fadeDuration));
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
        public IEnumerator FadeOutToRoom(GameObject player, Transform roomTransform)
        {
            yield return StartCoroutine(Fade(0f, 1f, fadeDuration));
            Time.timeScale = 1f;
            player.transform.position = roomTransform.position;
            yield return StartCoroutine(Fade(1f, 0f, fadeDuration));
        }

        public IEnumerator Fade(float from, float to, float duration)
        {
            if (blackScreen == null)
            {
                Debug.LogError("ScreenFader: blackScreen CanvasGroup is missing. Make sure it is a child of the ScreenFader GameObject so it persists across scenes.");
                yield break;
            }

            blackScreen.blocksRaycasts = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                blackScreen.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            blackScreen.alpha = to;
            blackScreen.blocksRaycasts = to > 0.5f;
        }
    }
}
