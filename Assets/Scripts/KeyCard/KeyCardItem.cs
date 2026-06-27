using UnityEngine;
using AdequateEnough;

public class KeycardItem : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private KeycardLevel cardTier = KeycardLevel.SectorA;
    [SerializeField] private float pickupDelay = 1.5f;

    [Header("Floating Animation")]
    [SerializeField] private float bounceSpeed = 4f;
    [SerializeField] private float bounceHeight = 0.2f;
    [SerializeField] private GameObject imageBeingActivated;

    private Vector3 startPosition;
    private float pickupEnabledTime;

    private void Start()
    {
        startPosition = transform.position;
        pickupEnabledTime = Time.time + pickupDelay;
    }

    private void Update()
    {
        float newY = startPosition.y + (Mathf.Sin(Time.time * bounceSpeed) * bounceHeight);
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (Time.time < pickupEnabledTime) return;

        PlayerController player = col.GetComponent<PlayerController>();
        if (player == null) return;

        player.SetKeycardLevel(cardTier);
        CheckpointManager.Instance?.SetCheckpoint(player.transform.position);
        if (imageBeingActivated != null) imageBeingActivated.SetActive(true);
        Destroy(gameObject);
    }
}
