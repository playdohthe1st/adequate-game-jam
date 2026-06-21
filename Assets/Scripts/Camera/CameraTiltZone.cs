using UnityEngine;

namespace AdequateEnough
{
    [RequireComponent(typeof(Collider2D))]
    public class CameraTiltZone : MonoBehaviour
    {
        [Tooltip("The tilt angle to use while the player is inside this zone.")]
        [SerializeField] private float tiltAngle = 4f;
        [Tooltip("How far to pan the camera on the X axis while inside this zone.")]
        [SerializeField] private float panOffset = 1f;
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
            CameraTiltController.Instance?.EnterZone(this, tiltAngle, panOffset);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            CameraTiltController.Instance?.ExitZone(this);
        }
    }
}
