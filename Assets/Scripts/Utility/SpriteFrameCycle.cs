using System.Collections;
using UnityEngine;

public class SpriteFrameCycle : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] frames;
    [SerializeField] private float interval = 0.5f;

    private int current = 0;

    private void OnEnable()
    {
        ShowFrame(0);
        StartCoroutine(Cycle());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator Cycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            int next = (current + 1) % frames.Length;
            ShowFrame(next);
        }
    }

    private void ShowFrame(int index)
    {
        for (int i = 0; i < frames.Length; i++)
            frames[i].enabled = i == index;
        current = index;
    }
}
