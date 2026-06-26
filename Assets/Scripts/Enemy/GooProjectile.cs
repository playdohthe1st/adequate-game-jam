using System.Collections;
using UnityEngine;

namespace AdequateEnough
{
    public class GooProjectile : MonoBehaviour
    {
        [SerializeField] private float arcHeight = 2f;

        private SpriteRenderer sr;
        private Rigidbody2D rb;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
        }

        private float damage;

        public void Launch(Vector3 target, float travelTime, bool flipX, float scaleMultiplier = 1f, float damage = 0f)
        {
            this.damage = damage;
            if (sr != null) sr.flipX = flipX;
            float targetZ = flipX ? -60f : 60f;
            StartCoroutine(ArcRoutine(transform.position, target, travelTime, scaleMultiplier, targetZ));
        }

        private IEnumerator ArcRoutine(Vector3 start, Vector3 end, float duration, float scaleMultiplier, float targetZ)
        {
            Vector3 startScale = new Vector3(0.1f * scaleMultiplier, 0.1f * scaleMultiplier, 1f);
            Vector3 endScale   = new Vector3(0.3f * scaleMultiplier, 0.3f * scaleMultiplier, 1f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                Vector3 pos = Vector3.Lerp(start, end, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                if (rb != null) rb.MovePosition(pos);
                else transform.position = pos;
                transform.localScale = Vector3.Lerp(startScale, endScale, t);
                transform.eulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(0f, targetZ, t));
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (rb != null) rb.MovePosition(end);
            else transform.position = end;
            transform.localScale = endScale;
            transform.eulerAngles = new Vector3(0f, 0f, targetZ);
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log($"[GooProjectile] Hit: {other.gameObject.name} (layer={LayerMask.LayerToName(other.gameObject.layer)}, tag={other.gameObject.tag})");
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc == null) { Debug.Log("[GooProjectile] Not a player, ignoring."); return; }
            Debug.Log("[GooProjectile] Hit player — applying goo debuff.");
            if (damage > 0f) pc.TakeDamage(damage);
            pc.ApplyGooDebuff();
            Destroy(gameObject);
        }
    }
}
