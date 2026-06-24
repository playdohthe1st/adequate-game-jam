using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueCharacter
{
    public string name;
    public Sprite icon;
}

[System.Serializable]
public class DialogueLines
{
    public DialogueCharacter character;
    [TextArea(3, 10)]
    public string lines;
}

[System.Serializable]
public class Dialogue
{
    public List<DialogueLines> dialogueLines = new List<DialogueLines>();
}

public class DialogueTrigger : MonoBehaviour
{
    public Dialogue dialogue;

    public void TriggerDialogue()
    {
        DialogueManager.instance.StartDialogue(dialogue);
    }
}
