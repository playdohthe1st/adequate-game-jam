using System.Collections;
using UnityEngine;

namespace AdequateEnough
{
    public class CutsceneWalker : MonoBehaviour
    {
        [SerializeField] private Transform targetPoint;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private AudioClip rainSound;
        [SerializeField] private string camFollowName = "CamFollow";
        [SerializeField] private float camFollowY = 2.4f;
        [SerializeField] private GameObject hud;

        public AudioSource RainSource { get; private set; }

        private Animator cachedAnim;
        private Rigidbody2D cachedRb;

        private void Awake()
        {
            RainSource = gameObject.AddComponent<AudioSource>();
            RainSource.loop = true;
            RainSource.playOnAwake = false;
            RainSource.spatialBlend = 0f;
            RainSource.clip = rainSound;
            if (AudioManager.Instance != null)
                RainSource.outputAudioMixerGroup = AudioManager.Instance.SFXMixerGroup;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            var player = other.GetComponent<PlayerController>();
            var input  = other.GetComponent<InputManager>();
            var rb     = other.GetComponent<Rigidbody2D>();
            var anim   = other.GetComponentInChildren<Animator>();

            if (player == null || input == null || rb == null) return;

            cachedAnim = anim;
            cachedRb = rb;
            GetComponent<Collider2D>().enabled = false;
            input.enabled = false;

            Transform camFollow = other.transform.Find(camFollowName);
            if (camFollow != null)
            {
                Vector3 p = camFollow.localPosition;
                p.y = camFollowY;
                camFollow.localPosition = p;
            }

            hud?.SetActive(false);
            if (rainSound != null) RainSource.Play();
            StartCoroutine(Walk(rb, anim));
        }

        private IEnumerator Walk(Rigidbody2D rb, Animator anim)
        {
            float dir = Mathf.Sign(targetPoint.position.x - rb.position.x);

            if (anim != null)
            {
                anim.SetBool("IsMoving", true);
                anim.SetBool("IsGrounded", true);
            }

            if (anim != null) anim.speed = 0.5f;

            while (Mathf.Abs(targetPoint.position.x - rb.position.x) > 0.05f)
            {
                rb.linearVelocity = new Vector2(dir * walkSpeed, rb.linearVelocity.y);
                if (anim != null) anim.SetBool("IsMoving", true);
                yield return null;
            }

            if (anim != null) anim.speed = 1f;

            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (anim != null)
                anim.SetBool("IsMoving", false);
        }

        public void StopWalk()
        {
            StopAllCoroutines();
            if (cachedRb != null)
                cachedRb.linearVelocity = new Vector2(0f, cachedRb.linearVelocity.y);
            if (cachedAnim != null)
            {
                cachedAnim.SetBool("IsMoving", false);
                cachedAnim.speed = 1f;
            }
        }
    }
}
