using AdequateEnough;
using UnityEngine;

public class EndGameDoor : MonoBehaviour
{

    private GameObject playerObject = null;
    private bool playerInZone = false;



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
        if (playerObject != null)
        {
            var playerScript = playerObject.GetComponent<PlayerController>();

            if (playerScript != null)
            {
                playerScript.Win();
            }
        }
    }
}