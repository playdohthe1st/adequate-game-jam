using UnityEngine;
using TMPro;
using System.Collections;

public class DialogueText : MonoBehaviour
{
    public float textSpeed;
    public TextMeshProUGUI dialogueText;
    public GameObject dialogueBox;
    public string[] lines;
    private int textIndex;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialogueText.text = string.Empty;
        StartDialogue();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (dialogueText.text == lines[textIndex])
            {
                NextLine();
            }
            else
            {
                StopAllCoroutines();
                dialogueText.text = lines[textIndex];
            }
        }
    }

    void StartDialogue()
    {
        textIndex = 0;
        StartCoroutine(TypeLine());
    }

    IEnumerator TypeLine()
    {
        foreach (char c in lines[textIndex].ToCharArray())
        {
            dialogueText.text += c;
            yield return new WaitForSecondsRealtime(textSpeed);
        }
    }

    void NextLine()
    {
        if ( textIndex < lines.Length - 1 )
        {
            textIndex++;
            dialogueText.text = string.Empty;
            StartCoroutine(TypeLine());
        }
    }
}
