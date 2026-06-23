using UnityEngine;

namespace AdequateEnough
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private EnemyData data;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float detectionRadius = 8f;

        [Header("Combat")]
        [SerializeField] private float regenRate = 5f;
        // Seconds after last hit before health starts regenerating
        [SerializeField] private float regenDelay = 20f;

        [Header("Wander")]
        [SerializeField] private float wanderRadius = 4f;
        [SerializeField] private float wanderSpeed = 1.5f;
        [SerializeField] private float wanderPauseMin = 1f;
        [SerializeField] private float wanderPauseMax = 3f;

        private Rigidbody2D rb;
        private PlayerController player;

        private float currentHealth;
        private float maxHealth;
        private float attackDamage;
        private float timeSinceLastDamage;
        private bool isDead;

        private Vector2 homePosition;
        private bool hasHome;
        private Vector2 wanderTarget;
        private float wanderPauseTimer;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            if (data != null)
            {
                maxHealth = data.ResolvedHealth;
                attackDamage = data.ResolvedAttackDamage;

                if (data.isBoss)
                    transform.localScale *= data.ScaleMultiplier;
            }

            currentHealth = maxHealth;
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        private void Update()
        {
            HandleRegen();
        }

        private void FixedUpdate()
        {
            if (isDead) return;

            if (player != null && Vector2.Distance(transform.position, player.transform.position) <= detectionRadius)
            {
                wanderPauseTimer = 0f;
                ChasePlayer();
            }
            else if (hasHome)
            {
                Wander();
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        private void ChasePlayer()
        {
            float dir = Mathf.Sign(player.transform.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
        }

        private void Wander()
        {
            if (wanderPauseTimer > 0f)
            {
                wanderPauseTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (wanderPauseTimer <= 0f) PickWanderTarget();
                return;
            }

            float dist = Mathf.Abs(transform.position.x - wanderTarget.x);
            if (dist < 0.2f)
            {
                wanderPauseTimer = Random.Range(wanderPauseMin, wanderPauseMax);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }

            float dir = Mathf.Sign(wanderTarget.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * wanderSpeed, rb.linearVelocity.y);
        }

        private void PickWanderTarget()
        {
            float offset = Random.Range(-wanderRadius, wanderRadius);
            wanderTarget = new Vector2(homePosition.x + offset, transform.position.y);
        }

        public void SetHome(Vector2 position)
        {
            homePosition = position;
            hasHome = true;
            PickWanderTarget();
        }

        private void HandleRegen()
        {
            if (isDead || currentHealth >= maxHealth) return;
            timeSinceLastDamage += Time.deltaTime;
            if (timeSinceLastDamage >= regenDelay)
                currentHealth = Mathf.Min(currentHealth + regenRate * Time.deltaTime, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Max(currentHealth - amount, 0f);
            timeSinceLastDamage = 0f;
            if (currentHealth <= 0f) Die();
        }

        private void Attack()
        {
            // TODO: implement attack
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            if (data != null && data.isBoss && player != null)
                player.GiveNextKeycard();

            // TODO: death animation, despawn logic
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
