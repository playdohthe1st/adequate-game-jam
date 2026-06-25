using UnityEngine;
using UnityEngine.SceneManagement;
using AdequateEnough;
using System.Collections;

public class CreditsScroll : MonoBehaviour
{
    public static CreditsScroll Instance { get; private set; }
    public enum StartPosition { BottomOfScreen, TopOfScreen }

    [Header("Scroll Setup")]
    [SerializeField] private StartPosition startingPosition = StartPosition.BottomOfScreen;
    [SerializeField] private float scrollSpeed = 50f;
    [SerializeField] private float startDelay = 1f;

    [Header("Exit Setup")]
    [SerializeField] private float exitThreshold = 1500f;

    private RectTransform rectTransform;
    private RectTransform parentRectTransform;
    private float timer;
    private bool isScrollingDown;
    private bool hasStarted = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (transform.parent != null)
        {
            parentRectTransform = transform.parent.GetComponent<RectTransform>();
        }

        timer = startDelay;
    }

    public void StartCredits()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (parentRectTransform == null && transform.parent != null) parentRectTransform = transform.parent.GetComponent<RectTransform>();

        timer = startDelay;

        SetupStartingPosition();
        hasStarted = true;
    }
    private void SetupStartingPosition()
    {
        Canvas.ForceUpdateCanvases();

        float viewportHeight = parentRectTransform != null ? parentRectTransform.rect.height : Screen.height;

        if (startingPosition == StartPosition.TopOfScreen)
        {
            rectTransform.anchoredPosition = new Vector2(0, 0);
            isScrollingDown = true;
        }
        else
        {
            rectTransform.anchoredPosition = new Vector2(0, -viewportHeight);
            isScrollingDown = false;
        }
    }

    private void Update()
    {
        if (!hasStarted) return;

        if (timer > 0)
        {
            timer -= Time.deltaTime;
            return;
        }

        MoveCredits();
        CheckIfFinished();
    }

    private void MoveCredits()
    {
        if (isScrollingDown)
        {
            rectTransform.anchoredPosition -= new Vector2(0, scrollSpeed * Time.deltaTime);
        }
        else
        {
            rectTransform.anchoredPosition += new Vector2(0, scrollSpeed * Time.deltaTime);
        }
    }

    private void CheckIfFinished()
    {
        bool finishedScrollingUp = !isScrollingDown && rectTransform.anchoredPosition.y > exitThreshold;
        bool finishedScrollingDown = isScrollingDown && rectTransform.anchoredPosition.y < -exitThreshold;
        bool playerSkipped = Input.GetKeyDown(KeyCode.Escape);

        if (finishedScrollingUp || finishedScrollingDown || playerSkipped)
        {
            ReturnToMenu();
        }
    }

    public void ReturnToMenu()
    {
        hasStarted = false;

        SetupStartingPosition();

        ScreenFader.Instance.FadeToScene("Splash", 1);
    }
}
