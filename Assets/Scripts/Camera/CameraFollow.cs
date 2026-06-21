using UnityEngine;

namespace AdequateEnough
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothSpeed = 6f;
        [SerializeField] private Vector2 offset = new(0f, 1f);

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }
    }
}
