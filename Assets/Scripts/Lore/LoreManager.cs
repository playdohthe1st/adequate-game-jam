using UnityEngine;
using TMPro;
using System.Collections;

public class LoreManager : MonoBehaviour
{
    public GameObject[] lorePanels = new GameObject[3];
    public TextMeshProUGUI[] loreTexts = new TextMeshProUGUI[3];

    [Header("Typing Settings")]
    public float typeSpeed = 0.03f;

    [Header("Lore Content Strings")]
    [TextArea(3, 10)] public string lorePaper1;
    [TextArea(3, 10)] public string lorePaper2;
    [TextArea(3, 10)] public string lorePaper3;
    private bool hasPaper1 = false;
    private bool hasPaper2 = false;
    private bool hasPaper3 = false;
    private Coroutine typingCoroutine;
    public int activePanelIndex = -1; // -1 means no panel is open use this on player to do if active panel = -1 return;

    void Start()
    {
        foreach (GameObject panel in lorePanels)
        {
            if (panel != null) panel.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && hasPaper1) TogglePanel(0, lorePaper1);
        if (Input.GetKeyDown(KeyCode.Alpha2) && hasPaper2) TogglePanel(1, lorePaper2);
        if (Input.GetKeyDown(KeyCode.Alpha3) && hasPaper3) TogglePanel(2, lorePaper3);
        if (Input.GetKeyDown(KeyCode.Escape) && activePanelIndex != -1)
        {
            CloseActivePanel();
        }
    }

    void TogglePanel(int index, string content)
    {
        if (activePanelIndex == index)
        {
            CloseActivePanel();
        }
        else
        {
            OpenPanel(index, content);
        }
    }

    void OpenPanel(int index, string content)
    {
        if (activePanelIndex != -1)
        {
            CloseActivePanel();
        }
        if (index < 0 || index >= lorePanels.Length || lorePanels[index] == null) return;

        activePanelIndex = index;
        lorePanels[index].SetActive(true);
        if (loreTexts[index] != null)
        {
            typingCoroutine = StartCoroutine(AnimateText(loreTexts[index], content));
        }
    }

    void CloseActivePanel()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        if (activePanelIndex != -1 && lorePanels[activePanelIndex] != null)
        {
            lorePanels[activePanelIndex].SetActive(false);
        }

        activePanelIndex = -1;
    }

    IEnumerator AnimateText(TextMeshProUGUI textComponent, string fullText)
    {
        textComponent.text = "";
        foreach (char letter in fullText.ToCharArray())
        {
            textComponent.text += letter;
            yield return new WaitForSeconds(typeSpeed);
        }
    }
    public void CollectPaper(int paperID)
    {
        if (paperID == 1) { hasPaper1 = true; OpenPanel(0, lorePaper1); }
        if (paperID == 2) { hasPaper2 = true; OpenPanel(1, lorePaper2); }
        if (paperID == 3) { hasPaper3 = true; OpenPanel(2, lorePaper3); }
    }
}
