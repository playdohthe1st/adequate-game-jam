using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;


public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    public float textSpeed;
    public Image characterIcon;
    public TextMeshProUGUI characterName;
    public TextMeshProUGUI dialogueText;
    public GameObject dialogueBox;
    private Queue<DialogueLine> lines; 
    public bool dialogueActive = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        lines = new Queue<DialogueLine>();
    }

    public void StartDialogue(Dialogue dialogue) //starts dialogue when needed
    {
        dialogueActive = true;

        dialogueBox.gameObject.SetActive(true);



        lines.Clear();

        foreach(DialogueLine dialogueLine in dialogue.dialogueLines)
        {
            lines.Enqueue(dialogueLine);
        }

        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (lines.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine currentLine = lines.Dequeue();

        characterIcon.sprite = currentLine.character.icon;
        Debug.Log("icon");
        characterName.text = currentLine.character.name;
        Debug.Log("name");
        StopAllCoroutines();
        StartCoroutine(TypeLine(currentLine));
    }

    IEnumerator TypeLine(DialogueLine dialogueLines) //makes the text appear letter by letter
    {
        dialogueText.text = "";
        foreach (char c in dialogueLines.line.ToCharArray())
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(textSpeed);
        }
    }

    public void EndDialogue()
    {
        dialogueActive = false;
        dialogueBox.gameObject.SetActive(false);    
    }
}
