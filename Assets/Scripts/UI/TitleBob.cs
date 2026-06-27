using UnityEngine;

public class TitleBob : MonoBehaviour
{
    [SerializeField] private float bobDistance = -30f;
    [SerializeField] private float bobDuration = 1.5f;

    private RectTransform rt;
    private float startY;
    private float elapsed;
    private bool goingDown = true;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        startY = rt.anchoredPosition.y;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / bobDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        float fromY = goingDown ? startY : startY + bobDistance;
        float toY   = goingDown ? startY + bobDistance : startY;

        Vector2 pos = rt.anchoredPosition;
        pos.y = Mathf.Lerp(fromY, toY, smoothT);
        rt.anchoredPosition = pos;

        if (t >= 1f)
        {
            elapsed = 0f;
            goingDown = !goingDown;
        }
    }
}
