using System.Collections;
using Cinemachine;
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

        [Header("Linger")]
        [SerializeField] private float lingerMin = 1f;
        [SerializeField] private float lingerMax = 3f;
        [SerializeField] private float dirChangeCooldown = 0.15f;

        [Header("Player Interaction")]
        [SerializeField] private float pushForce = 8f;
        [SerializeField] private float chaseStopDistance = 1.5f;

        [Header("Melee Attack")]
        [SerializeField] private Transform weaponTransform;
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private float meleeDamage = 10f;
        [SerializeField] private float meleeCooldown = 2f;
        [SerializeField] private float meleeWindupDuration = 0.2f;
        [SerializeField] private float meleeStrikeDuration = 0.07f;
        [SerializeField] private float meleeReturnDuration = 0.3f;
        [SerializeField] private float meleeKnockbackForce = 14f;
        [SerializeField] private float meleeShakeStrength = 0.1f;

        [Header("VisualUpdater")]
        [SerializeField] private Animator enemyAnimator;
        [SerializeField] private SpriteRenderer enemySpriterenderer;
        [Header("Hit Effects")]
        [SerializeField] private Material flashMaterial; 
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private float hitStopDuration = 0.1f; 

        private Material originalMaterial;

        private Rigidbody2D rb;
        [SerializeField] private Collider2D col;
        private PlayerController player;
        private Rigidbody2D playerRb;

        private float currentHealth;
        private float maxHealth;
        private float attackDamage;
        private float timeSinceLastDamage;
        private bool isDead;
        private bool isMoving;

        private Vector2 homePosition;
        private bool hasHome;
        private Vector2 wanderTarget;
        private float wanderPauseTimer;
        private float lingerTimer;
        private bool isLingering;
        private bool wasChasing;
        private float chaseDir = 1f;
        private float lastDirChangeTime;
        private Collider2D weaponCollider;
        private CinemachineImpulseSource impulseSource;
        private float nextMeleeTime;
        private bool isAttacking;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            if (weaponTransform != null)
            {
                weaponCollider = weaponTransform.GetComponent<Collider2D>();
                if (weaponCollider != null) weaponCollider.enabled = false;
            }

            impulseSource = GetComponent<CinemachineImpulseSource>();

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
            if (player != null)
            {
                playerRb = player.GetComponent<Rigidbody2D>();
                Collider2D playerCol = player.GetComponent<Collider2D>();
                if (col != null && playerCol != null)
                    Physics2D.IgnoreCollision(col, playerCol, true);
            }
        }

        private void Update()
        {
            HandleRegen();
            VisualUpdater();
        }

        private void FixedUpdate()
        {
            if (isDead) return;
            PushPlayerIfOverlapping();

            bool playerInRange = player != null && !player.IsDead && Vector2.Distance(transform.position, player.transform.position) <= detectionRadius;

            if (playerInRange)
            {
                isLingering = false;
                wanderPauseTimer = 0f;
                wasChasing = true;

                float playerDist = Mathf.Abs(player.transform.position.x - transform.position.x);
                if (!isAttacking && playerDist <= meleeRange && Time.fixedTime >= nextMeleeTime)
                {
                    nextMeleeTime = Time.fixedTime + meleeCooldown;
                    StartCoroutine(MeleeAttackRoutine());
                }

                if (!isAttacking)
                {
                    if (playerDist > chaseStopDistance)
                    {
                        ChasePlayer();
                        isMoving = true;
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        isMoving = false;
                    }
                }
            }
            else if (wasChasing && !isLingering)
            {
                wasChasing = false;
                isLingering = true;
                lingerTimer = Random.Range(lingerMin, lingerMax);
            }
            else if (isLingering)
            {
                lingerTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                isMoving = false;
                if (lingerTimer <= 0f)
                {
                    isLingering = false;
                    PickWanderTarget();
                }
            }
            else if (hasHome)
            {
                Wander();
                isMoving = true;
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
        // The method called by the boss when it dies
     

        private void VisualUpdater()
        {
            if (rb.linearVelocityX == 0)
            {
                isMoving = false;
            }
            enemyAnimator.SetBool("IsMoving", isMoving);
            if (rb.linearVelocityX > 0)
            {
                enemySpriterenderer.flipX = false;
            }
            if (rb.linearVelocityX < 0)
            {
                enemySpriterenderer.flipX = true;
            }
        }
        private void ChasePlayer()
        {
            float dx = player.transform.position.x - transform.position.x;
            float desiredDir = Mathf.Sign(dx);
            if (desiredDir != 0f && desiredDir != chaseDir && Time.fixedTime >= lastDirChangeTime + dirChangeCooldown)
            {
                chaseDir = desiredDir;
                lastDirChangeTime = Time.fixedTime;
            }

            if (Mathf.Abs(dx) <= chaseStopDistance)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }

            rb.linearVelocity = new Vector2(chaseDir * moveSpeed, rb.linearVelocity.y);
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
            StartCoroutine(FlashWhiteRoutine());
            StartCoroutine(HitStopRoutine());
            if (currentHealth <= 0f) Die();

        }

        private void PushPlayerIfOverlapping()
        {
            if (playerRb == null || col == null) return;
            Collider2D playerCol = playerRb.GetComponent<Collider2D>();
            if (playerCol == null) return;

            ColliderDistance2D dist = col.Distance(playerCol);
            if (!dist.isOverlapped) return;

            float dir = Mathf.Sign(player.transform.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;
            playerRb.AddForce(Vector2.right * dir * pushForce, ForceMode2D.Force);
        }

        private IEnumerator MeleeAttackRoutine()
        {
            isAttacking = true;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // Mirror positions to match facing direction
            float f = chaseDir;
            Vector2 restPos   = new Vector2(0.0441f * f, 1.1019f);
            Vector2 windupPos = new Vector2(0.0441f * f, 1.8f);
            Vector2 strikePos = new Vector2(1.1f * f,    1.71f);
            float restRot   = 32f * f;
            float windupRot = 57f * f;
            float strikeRot = 27f * f;

            // Windup: swing up and back
            yield return LerpWeapon(restPos, windupPos, restRot, windupRot, meleeWindupDuration);

            // Strike: sweep forward — collider active during this phase
            if (weaponCollider != null) weaponCollider.enabled = true;
            yield return LerpWeapon(windupPos, strikePos, windupRot, strikeRot, meleeStrikeDuration);
            if (weaponCollider != null) weaponCollider.enabled = false;

            // Return to rest
            yield return LerpWeapon(strikePos, restPos, strikeRot, restRot, meleeReturnDuration);

            isAttacking = false;
        }

        private IEnumerator LerpWeapon(Vector2 fromPos, Vector2 toPos, float fromRot, float toRot, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                weaponTransform.localPosition = Vector2.Lerp(fromPos, toPos, t);
                weaponTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(fromRot, toRot, t));
                yield return null;
            }
            weaponTransform.localPosition = toPos;
            weaponTransform.localEulerAngles = new Vector3(0f, 0f, toRot);
        }

        private IEnumerator FlashWhiteRoutine()
        {
            if (originalMaterial == null) originalMaterial = enemySpriterenderer.material;
            enemySpriterenderer.material = flashMaterial;
            yield return new WaitForSecondsRealtime(flashDuration);
            enemySpriterenderer.material = originalMaterial;
        }

        private IEnumerator HitStopRoutine()
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopDuration);
            Time.timeScale = 1f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isAttacking) return;
            var pc = other.GetComponent<PlayerController>();
            if (pc == null) return;
            pc.TakeDamage(meleeDamage);
            if (playerRb != null)
            {
                Vector2 knockbackDir = new Vector2(chaseDir, 0.2f).normalized;
                playerRb.linearVelocity = Vector2.zero;
                playerRb.AddForce(knockbackDir * meleeKnockbackForce, ForceMode2D.Impulse);
            }
            impulseSource?.GenerateImpulse(meleeShakeStrength);
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            if (data != null && data.isBoss && player != null)
                player.GiveNextKeycard();
            Time.timeScale = 1f;
            if (weaponCollider != null) weaponCollider.enabled = false;
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
