using UnityEngine;

namespace AdequateEnough
{
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;

            if (GetComponent<Rigidbody2D>() == null)
            {
                var rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            CheckpointManager.Instance?.SetCheckpoint(this, other.transform.position);
        }
    }
}
