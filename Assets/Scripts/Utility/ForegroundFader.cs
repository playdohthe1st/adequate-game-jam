using UnityEngine;

namespace AdequateEnough
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ForegroundFader : MonoBehaviour
    {
        [SerializeField] private float fadedAlpha = 0.3f;
        [SerializeField] private float fadeSpeed = 5f;

        private SpriteRenderer sr;
        private int playerCount;
        private float targetAlpha = 1f;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            Color c = sr.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, fadeSpeed * Time.deltaTime);
            sr.color = c;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            playerCount++;
            targetAlpha = fadedAlpha;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            playerCount = Mathf.Max(playerCount - 1, 0);
            if (playerCount == 0) targetAlpha = 1f;
        }
    }
}
