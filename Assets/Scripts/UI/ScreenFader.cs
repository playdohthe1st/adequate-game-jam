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

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(Fade(1f, 0f));
        }

        public void FadeToScene(string sceneName)
        {
            StartCoroutine(FadeOutThenLoad(sceneName));
        }

        private IEnumerator FadeOutThenLoad(string sceneName)
        {
            yield return StartCoroutine(Fade(0f, 1f));
            SceneManager.LoadScene(sceneName);
        }

        private IEnumerator Fade(float from, float to)
        {
            blackScreen.blocksRaycasts = true;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                blackScreen.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                yield return null;
            }

            blackScreen.alpha = to;
            blackScreen.blocksRaycasts = to > 0.5f;
        }
    }
}
