using AdequateEnough;
using UnityEngine;

public class EndGameDoor : MonoBehaviour
{
    private CutsceneWalker cutsceneWalker;
    private GameObject playerObject = null;
    private bool playerInZone = false;
    private bool triggered = false;

    private void Start()
    {
        cutsceneWalker = FindFirstObjectByType<CutsceneWalker>();
    }

    void Update()
    {
        if (playerInZone)
        {
            TriggerPlayerEndGame();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            playerObject = other.gameObject;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
            playerObject = null;
        }
    }

    void TriggerPlayerEndGame()
    {
        if (triggered || playerObject == null) return;

        var playerScript = playerObject.GetComponent<PlayerController>();
        if (playerScript != null)
        {
            triggered = true;
            cutsceneWalker?.StopWalk();
            cutsceneWalker?.RainSource.Stop();
            playerScript.Win();
        }
    }
}