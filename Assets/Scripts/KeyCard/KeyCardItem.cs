using UnityEngine;
using AdequateEnough;
public class KeycardItem : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private KeycardLevel cardTier = KeycardLevel.SectorA;

    [Header("Floating Animation")]
    [SerializeField] private float bounceSpeed = 4f;    // How fast it moves up and down
    [SerializeField] private float bounceHeight = 0.2f; // How far up and down it travels
    [SerializeField] private GameObject imageBeingActivated;
    private Vector3 startPosition;

    private void Start()
    {
        // Remember the exact spot where we placed the item in the scene
        startPosition = transform.position;
    }

    private void Update()
    {
        // Calculate the new Y position using a Sine wave based on time
        float newY = startPosition.y + (Mathf.Sin(Time.time * bounceSpeed) * bounceHeight);

        // Apply the new position while keeping the original X and Z coordinates
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        PlayerController player = col.GetComponent<PlayerController>();

        if (player != null)
        {
            player.SetKeycardLevel(cardTier);
            imageBeingActivated.SetActive(true);
            Destroy(gameObject);
        }
    }
}