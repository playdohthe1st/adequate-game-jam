using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Collections.Generic;


public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    public float textSpeed;
    public Image characterIcon;
    public TextMeshProUGUI characterName;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI dialogueArea;
    public GameObject dialogueBox;
    private Queue<DialogueLines> lines; 
    public bool dialogueActive = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (instance == null)
        {
            instance = this;
        }

    }

    public void StartDialogue(Dialogue dialogue) //starts dialogue when needed
    {
        dialogueActive = true;

        lines.Clear();

        foreach(DialogueLines dialogueLine in dialogue.dialogueLines)
        {
            lines.Enqueue(dialogueLine);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            DisplayNextLine();
        }
    }

    public void DisplayNextLine()
    {
        if (lines.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLines currentLine = lines.Dequeue();

        characterIcon.sprite = currentLine.character.icon;
        characterName.text = currentLine.character.name;

        StopAllCoroutines();
        StartCoroutine(TypeLine(currentLine));
    }

    IEnumerator TypeLine(DialogueLines dialogueLines) //makes the text appear letter by letter
    {
        dialogueArea.text = "";
        foreach (char c in dialogueLines.lines.ToCharArray())
        {
            dialogueArea.text += c;
            yield return new WaitForSecondsRealtime(textSpeed);
        }
    }

    public void EndDialogue()
    {
        dialogueActive = false;
        dialogueBox.gameObject.SetActive(false);    
    }
}
