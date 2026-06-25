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

        [Header("Navigation")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask obstacleLayer = ~0;
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private float wallCheckDistance = 0.4f;
        [SerializeField] private float jumpForceMin = 3f;
        [SerializeField] private float jumpForce = 10f;
        [SerializeField] private float jumpCooldown = 0.8f;
        [SerializeField] private float jumpProbeHeight = 3f;
        [SerializeField] private float jumpClearance = 0.2f;

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
        private float savedTimeScale = 1f;
        private bool isMoving;
        private bool isGrounded;
        private float lastJumpTime;
        private float stuckTimer;
        private float stairsAvoidTimer;
        private float defaultGravityScale;

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
            defaultGravityScale = rb.gravityScale;

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
                meleeDamage = attackDamage;

                if (data.isBoss)
                    transform.localScale *= data.ScaleMultiplier;
                else
                    ApplyCommonVariation();
            }

            currentHealth = maxHealth;
        }

        private void ApplyCommonVariation()
        {
            if (enemySpriterenderer != null)
            {
                float scaleFactor = Random.Range(0.85f, 1.15f);
                enemySpriterenderer.transform.localScale *= scaleFactor;

                float h = Random.Range(0f, 1f);
                float s = Random.Range(0.3f, 0.7f);
                float v = Random.Range(0.7f, 1f);
                enemySpriterenderer.color = Color.HSVToRGB(h, s, v);
            }
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

            int enemyLayerMask = 1 << gameObject.layer;
            foreach (PlatformEffector2D effector in FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None))
                effector.colliderMask &= ~enemyLayerMask;
        }

        private void Update()
        {
            HandleRegen();
            VisualUpdater();
        }

        private void FixedUpdate()
        {
            if (isDead) return;
            CheckGround();
            PushPlayerIfOverlapping();
            if (stairsAvoidTimer > 0f) stairsAvoidTimer -= Time.fixedDeltaTime;

            bool playerInRange = stairsAvoidTimer <= 0f && player != null && !player.IsDead && Vector2.Distance(transform.position, player.transform.position) <= detectionRadius;

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
            if (rb.linearVelocityX > 0.1f)
                enemySpriterenderer.flipX = false;
            else if (rb.linearVelocityX < -0.1f)
                enemySpriterenderer.flipX = true;
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

            ApplyGroundedMove(chaseDir, moveSpeed);
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
            ApplyGroundedMove(dir, wanderSpeed);
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
            if (Time.timeScale == 0f) yield break;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopDuration);
            Time.timeScale = savedTimeScale;
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
            Time.timeScale = savedTimeScale;
            if (weaponCollider != null) weaponCollider.enabled = false;
            // TODO: death animation, despawn logic
            Destroy(gameObject);
        }

        private void CheckGround()
        {
            if (col == null) { isGrounded = false; return; }
            Vector2 center = new Vector2(col.bounds.center.x, col.bounds.min.y - groundCheckDistance * 0.5f);
            Vector2 size   = new Vector2(col.bounds.size.x * 0.8f, groundCheckDistance);
            isGrounded = Physics2D.OverlapBox(center, size, 0f, groundLayer) != null;
        }

        private bool HasWallAhead(float dir)
        {
            if (col == null) return false;
            return Physics2D.Raycast(col.bounds.center, Vector2.right * dir, wallCheckDistance, obstacleLayer);
        }

        private bool HasLedgeAhead(float dir)
        {
            if (col == null) return false;
            Vector2 origin = new Vector2(col.bounds.center.x + dir * (col.bounds.extents.x + 0.1f), col.bounds.min.y);
            return !Physics2D.Raycast(origin, Vector2.down, groundCheckDistance + 0.2f, groundLayer);
        }

        private float CalculateJumpForce(float dirSign)
        {
            if (col == null) return jumpForce;
            RaycastHit2D wallHit = Physics2D.Raycast(col.bounds.center, Vector2.right * dirSign, wallCheckDistance, obstacleLayer);
            float probeX      = wallHit ? wallHit.point.x + dirSign * 0.05f
                                        : col.bounds.center.x + dirSign * (col.bounds.extents.x + 0.15f);
            float probeStartY = col.bounds.max.y + jumpProbeHeight;
            float probeLength = jumpProbeHeight + col.bounds.size.y + 1f;
            Vector2 origin    = new Vector2(probeX, probeStartY);
            Debug.DrawRay(origin, Vector2.down * probeLength, Color.yellow, 0.1f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, probeLength, obstacleLayer);
            if (!hit) return jumpForce;
            float minSurfaceY = wallHit ? wallHit.point.y : col.bounds.min.y;
            float surfaceY    = Mathf.Max(hit.point.y, minSurfaceY);
            float heightNeeded = surfaceY - col.bounds.min.y + jumpClearance;
            if (heightNeeded <= 0f) return jumpForceMin;
            float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
            float needed  = Mathf.Sqrt(2f * gravity * heightNeeded);
            return Mathf.Clamp(needed, jumpForceMin, jumpForce);
        }

        private void TryJump(float moveDir)
        {
            if (!isGrounded || Time.fixedTime < lastJumpTime + jumpCooldown) return;
            if (!HasWallAhead(moveDir)) return;
            float jf = CalculateJumpForce(moveDir);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jf);
            lastJumpTime = Time.fixedTime;
            stuckTimer = 0f;
        }

        private void ApplyGroundedMove(float dirSign, float speed)
        {
            if (isGrounded && HasLedgeAhead(dirSign))
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                stuckTimer = 0f;
                return;
            }

            if (isGrounded && Mathf.Abs(rb.linearVelocity.x) < 0.3f && rb.linearVelocity.y <= 0.1f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 0.25f && Time.fixedTime >= lastJumpTime + jumpCooldown)
                {
                    float jf = CalculateJumpForce(dirSign);
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jf);
                    lastJumpTime = Time.fixedTime;
                    stuckTimer = 0f;
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;
            }

            TryJump(dirSign);
            rb.linearVelocity = new Vector2(dirSign * speed, rb.linearVelocity.y);
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!other.gameObject.CompareTag("Stairs")) return;
            chaseDir = -chaseDir;
            lastDirChangeTime = Time.fixedTime;
            wasChasing = false;
            isLingering = false;
            stairsAvoidTimer = 2f;
            wanderPauseTimer = 0f;
            wanderTarget = new Vector2(transform.position.x + chaseDir * wanderRadius, transform.position.y);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
