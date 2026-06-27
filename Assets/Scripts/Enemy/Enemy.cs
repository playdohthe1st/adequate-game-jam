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
        [SerializeField] private LayerMask obstacleLayer;
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
        [SerializeField] private float contactDamageCooldown = 1f;
        [SerializeField] private float contactDamageRange = 1.5f;

        [Header("Wander")]
        [SerializeField] private float wanderRadius = 4f;
        [SerializeField] private float wanderSpeed = 1.5f;
        [SerializeField] private float wanderPauseMin = 1f;
        [SerializeField] private float wanderPauseMax = 3f;

        [Header("Linger")]
        [SerializeField] private float airborneStuckTimeout = 1.2f;
        [SerializeField] private float lingerMin = 1f;
        [SerializeField] private float lingerMax = 3f;
        [SerializeField] private float dirChangeCooldown = 0.15f;

        [Header("Player Interaction")]
        [SerializeField] private float pushForce = 8f;
        [SerializeField] private float chaseStopDistance = 1.5f;

        [Header("Ranged Attack")]
        [SerializeField] private float shootCooldown = 5f;
        [SerializeField] private float shootMinDistance = 3f;
        [SerializeField] private float shootAnimDuration = 1f;
        [SerializeField] private int shootFireFrame = 0;
        [SerializeField] private float shootAnimFPS = 14f;
        [SerializeField] private float projectileTravelTime = 1f;
        [SerializeField] private GameObject gooProjectilePrefab;
        [SerializeField] private Transform shootLeftTransform;
        [SerializeField] private Transform shootRightTransform;

        [Header("Combat Behavior")]
        [SerializeField] private float chaseIntentWeight  = 1f;
        [SerializeField] private float shootIntentWeight  = 1f;
        [SerializeField] private float retreatIntentWeight = 1f;
        [SerializeField] private float retreatTargetDistance = 7f;
        [SerializeField] private float retreatMoveSpeed = 5f;

        [Header("Melee Attack")]
        [SerializeField] private Transform weaponTransform;
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private float meleeDamage = 10f;
        [SerializeField] private float meleeCooldown = 2f;
        [SerializeField] private float meleeKnockbackForce = 14f;
        [SerializeField] private float meleeShakeStrength = 0.1f;
        [Header("Melee Positions (X mirrored by facing direction)")]
        [SerializeField] private Vector2 meleeRestPos      = new Vector2(0.06f,  1.57f);
        [SerializeField] private float   meleeRestRot      = 32f;
        [SerializeField] private Vector2 meleeWindupPos    = new Vector2(0.06f,  2.268f);
        [SerializeField] private float   meleeWindupRot    = 57f;
        [SerializeField] private Vector2 meleePreStrikePos = new Vector2(0.06f,  2.268f);
        [SerializeField] private float   meleePreStrikeRot = 57f;
        [SerializeField] private Vector2 meleeStrikePos    = new Vector2(1.116f, 2.178f);
        [SerializeField] private float   meleeStrikeRot    = 27f;
        [Header("Melee Timings")]
        [SerializeField] private string meleeAnimTrigger   = "";
        [SerializeField] private float meleeWindupDuration = 0.2f;
        // Time to hold at windup before snapping to pre-strike; 0 skips the hold
        [SerializeField] private float meleeHoldDuration   = 0f;
        [SerializeField] private float meleeStrikeDuration        = 0.07f;
        // How long the collider lingers at the strike position after the sweep
        [SerializeField] private float meleeColliderLingerDuration = 0f;
        [SerializeField] private float meleeReturnDuration        = 0.3f;

        [Header("Boss Override")]
        [SerializeField] private bool isBoss = false;

        [Header("VisualUpdater")]
        [SerializeField] private Animator enemyAnimator;
        [SerializeField] private SpriteRenderer enemySpriterenderer;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private bool invertFlipX = false;

        [Header("Contact Retreat")]
        [SerializeField] private bool retreatAfterContact = false;
        [SerializeField] private float contactRetreatDuration = 1.5f;
        [SerializeField] private float wallBlockDuration = 1.5f;
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
        private float airborneStuckTimer;
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
        private float nextShootTime;
        private float nextContactDamageTime;
        private bool isRetreatingFromContact;
        private float contactRetreatTimer;
        private float contactRetreatDir;
        private bool isWallBlocked;
        private float wallBlockTimer;
        private float wallBlockDir;
        private bool isShooting;
        private Coroutine shootCoroutine;

        private enum CombatIntent { Shoot, Chase, Retreat }
        private CombatIntent currentIntent;
        private bool playerWasInRange;
        private float retreatTargetX;
        private bool retreatReached;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public int AssignedSortingOrder { get; private set; }

        public void SetSortingOrder(int order)
        {
            AssignedSortingOrder = order;
            if (enemySpriterenderer != null)
                enemySpriterenderer.sortingOrder = order;
        }

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

                bool isBossEnemy = isBoss || data.isBoss;
                if (isBossEnemy)
                    transform.localScale *= data.isBoss ? data.ScaleMultiplier : 2.5f;
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
            bool nearPlayer = player != null && Mathf.Abs(player.transform.position.x - transform.position.x) <= chaseStopDistance;
            if (!isAttacking && !isShooting && !isLingering && !nearPlayer && Mathf.Abs(rb.linearVelocity.x) < 0.3f)
            {
                airborneStuckTimer += Time.fixedDeltaTime;
                if (airborneStuckTimer >= airborneStuckTimeout)
                {
                    chaseDir = -chaseDir;
                    airborneStuckTimer = 0f;
                }
            }
            else
            {
                airborneStuckTimer = 0f;
            }
            PushPlayerIfOverlapping();
            if (stairsAvoidTimer > 0f) stairsAvoidTimer -= Time.fixedDeltaTime;

            if (isRetreatingFromContact)
            {
                contactRetreatTimer -= Time.fixedDeltaTime;
                bool blocked = isGrounded && (HasWallAhead(contactRetreatDir) || HasLedgeAhead(contactRetreatDir));
                if (contactRetreatTimer <= 0f || blocked)
                    isRetreatingFromContact = false;
                else
                {
                    rb.linearVelocity = new Vector2(contactRetreatDir * retreatMoveSpeed, rb.linearVelocity.y);
                    isMoving = true;
                    return;
                }
            }

            if (isWallBlocked)
            {
                wallBlockTimer -= Time.fixedDeltaTime;
                if (wallBlockTimer <= 0f || HasWallAhead(wallBlockDir))
                {
                    isWallBlocked = false;
                    PickWanderTarget();
                }
                else
                {
                    rb.linearVelocity = new Vector2(wallBlockDir * moveSpeed, rb.linearVelocity.y);
                    isMoving = true;
                    return;
                }
            }

            bool playerInRange = stairsAvoidTimer <= 0f && player != null && !player.IsDead && Vector2.Distance(transform.position, player.transform.position) <= detectionRadius;

            if (playerInRange)
            {
                isLingering = false;
                wanderPauseTimer = 0f;
                wasChasing = true;

                if (!playerWasInRange)
                {
                    playerWasInRange = true;
                    RollCombatIntent();
                    if (currentIntent == CombatIntent.Retreat)
                        SetupRetreatTarget();
                }

                float playerDist = Mathf.Abs(player.transform.position.x - transform.position.x);
                if ((data == null || data.canMelee) && !isAttacking && !isShooting && !player.IsSpinning && playerDist <= meleeRange && Time.fixedTime >= nextMeleeTime)
                {
                    nextMeleeTime = Time.fixedTime + meleeCooldown;
                    StartCoroutine(MeleeAttackRoutine());
                }

                HandleContactDamage();

                if (!isAttacking && !isShooting)
                    ExecuteCombatIntent(playerDist);
            }
            else if (wasChasing && !isLingering)
            {
                playerWasInRange = false;
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
            if (enemyAnimator != null) enemyAnimator.SetBool("IsMoving", isMoving);
            if (rb.linearVelocityX > 0.1f)
                SetFacing(invertFlipX ? 1f : -1f);
            else if (rb.linearVelocityX < -0.1f)
                SetFacing(invertFlipX ? -1f : 1f);
        }

        private void SetFacing(float dirSign)
        {
            if (enemySpriterenderer == null) return;
            Transform t = enemySpriterenderer.transform.parent != null
                ? enemySpriterenderer.transform.parent
                : enemySpriterenderer.transform;
            t.localEulerAngles = new Vector3(0f, dirSign > 0f ? 0f : 180f, 0f);
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
            if (isShooting && shootCoroutine != null)
            {
                StopCoroutine(shootCoroutine);
                shootCoroutine = null;
                isShooting = false;
                if (enemyAnimator != null) enemyAnimator.ResetTrigger("Shoot");
                if (enemyAnimator != null) enemyAnimator.CrossFade(idleStateName, 0.05f);
            }
            if (retreatAfterContact) TriggerContactRetreat();
            if (data?.hurtSFX != null) AudioManager.Instance?.PlaySFX(data.hurtSFX);
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

        private IEnumerator ShootRoutine()
        {
            isShooting = true;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            isMoving = false;
            if (player != null)
                chaseDir = Mathf.Sign(player.transform.position.x - transform.position.x);

            enemySpriterenderer.flipX = chaseDir > 0f;
            if (enemyAnimator != null) enemyAnimator.SetTrigger("Shoot");

            float fireTime = shootFireFrame / Mathf.Max(shootAnimFPS, 1f);
            float remaining = shootAnimDuration - fireTime;
            if (fireTime > 0f) yield return new WaitForSeconds(fireTime);

            if (gooProjectilePrefab != null && player != null)
            {
                Transform spawnPoint = chaseDir > 0f ? shootRightTransform : shootLeftTransform;
                if (spawnPoint == null) spawnPoint = transform;

                Collider2D playerCol = player.GetComponent<Collider2D>();
                Vector3 target = playerCol != null ? playerCol.bounds.min : player.transform.position;

                GameObject go = Instantiate(gooProjectilePrefab, spawnPoint.position, Quaternion.identity);
                GooProjectile goo = go.GetComponent<GooProjectile>();
                bool projectileFlip = chaseDir > 0f;
                float scaleMultiplier = enemySpriterenderer != null
                    ? enemySpriterenderer.transform.localScale.x / 0.43f
                    : 1f;
                goo?.Launch(target, projectileTravelTime, projectileFlip, scaleMultiplier, attackDamage);
                if (data?.attackSFX != null) AudioManager.Instance?.PlaySFX(data.attackSFX);
            }

            if (remaining > 0f) yield return new WaitForSeconds(remaining);
            isShooting = false;
        }

        private IEnumerator MeleeAttackRoutine()
        {
            isAttacking = true;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (!string.IsNullOrEmpty(meleeAnimTrigger) && enemyAnimator != null)
                enemyAnimator.SetTrigger(meleeAnimTrigger);

            float f = chaseDir;
            Vector2 restPos      = new Vector2(meleeRestPos.x      * f, meleeRestPos.y);
            Vector2 windupPos    = new Vector2(meleeWindupPos.x    * f, meleeWindupPos.y);
            Vector2 preStrikePos = new Vector2(meleePreStrikePos.x * f, meleePreStrikePos.y);
            Vector2 strikePos    = new Vector2(meleeStrikePos.x    * f, meleeStrikePos.y);
            float restRot      = meleeRestRot      * f;
            float windupRot    = meleeWindupRot    * f;
            float preStrikeRot = meleePreStrikeRot * f;
            float strikeRot    = meleeStrikeRot    * f;

            // Windup (duration 0 = instant snap)
            yield return LerpWeapon(restPos, windupPos, restRot, windupRot, meleeWindupDuration);

            // Optional hold, then snap to pre-strike position
            if (meleeHoldDuration > 0f)
            {
                yield return new WaitForSeconds(meleeHoldDuration);
                if (weaponTransform != null)
                {
                    weaponTransform.localPosition = preStrikePos;
                    weaponTransform.localEulerAngles = new Vector3(0f, 0f, preStrikeRot);
                }
                yield return null;
            }

            // Strike sweep — collider active, starting from pre-strike (or windup if no hold)
            Vector2 strikeFromPos = meleeHoldDuration > 0f ? preStrikePos : windupPos;
            float   strikeFromRot = meleeHoldDuration > 0f ? preStrikeRot : windupRot;
            if (data?.attackSFX != null) AudioManager.Instance?.PlaySFX(data.attackSFX);
            if (weaponCollider != null) weaponCollider.enabled = true;
            yield return LerpWeapon(strikeFromPos, strikePos, strikeFromRot, strikeRot, meleeStrikeDuration);
            if (meleeColliderLingerDuration > 0f)
                yield return new WaitForSeconds(meleeColliderLingerDuration);
            if (weaponCollider != null) weaponCollider.enabled = false;

            // Return to rest (duration 0 = instant snap)
            yield return LerpWeapon(strikePos, restPos, strikeRot, restRot, meleeReturnDuration);

            isAttacking = false;
        }

        private IEnumerator LerpWeapon(Vector2 fromPos, Vector2 toPos, float fromRot, float toRot, float duration)
        {
            if (weaponTransform == null) yield break;
            if (duration <= 0f)
            {
                weaponTransform.localPosition = toPos;
                weaponTransform.localEulerAngles = new Vector3(0f, 0f, toRot);
                yield break;
            }
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
            if (pc.IsSpinning) return;
            pc.TakeDamage(meleeDamage);
            if (playerRb != null)
            {
                Vector2 knockbackDir = new Vector2(chaseDir, 0.2f).normalized;
                playerRb.linearVelocity = Vector2.zero;
                playerRb.AddForce(knockbackDir * meleeKnockbackForce, ForceMode2D.Impulse);
            }
            impulseSource?.GenerateImpulse(meleeShakeStrength);
        }

        private void HandleContactDamage()
        {
            if (Time.fixedTime < nextContactDamageTime) return;
            if (player == null || player.IsSpinning) return;
            if (Vector2.Distance(transform.position, player.transform.position) > contactDamageRange) return;

            player.TakeDamage(attackDamage);
            nextContactDamageTime = Time.fixedTime + contactDamageCooldown;
            if (retreatAfterContact) TriggerContactRetreat();
        }

        private void TriggerWallBlock(float awayDir)
        {
            wallBlockDir = awayDir;
            wallBlockTimer = wallBlockDuration;
            isWallBlocked = true;
        }

        private void TriggerContactRetreat()
        {
            contactRetreatDir = player != null
                ? -Mathf.Sign(player.transform.position.x - transform.position.x)
                : -chaseDir;
            if (contactRetreatDir == 0f) contactRetreatDir = -chaseDir;
            isRetreatingFromContact = true;
            contactRetreatTimer = contactRetreatDuration;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            if (data?.deathSFX != null) AudioManager.Instance?.PlaySFX(data.deathSFX);
            bool isBossEnemy = isBoss || (data != null && data.isBoss);
            Debug.Log($"[Enemy.Die] isBoss={isBoss} data={data?.name} data.isBoss={data?.isBoss} isBossEnemy={isBossEnemy} keycardDropPrefab={data?.keycardDropPrefab?.name ?? "NULL"}");
            if (isBossEnemy && data?.keycardDropPrefab != null)
            {
                Debug.Log($"[Enemy.Die] Spawning keycard at {transform.position}");
                Vector3 dropPos = transform.position + Vector3.up * 1f;
                Instantiate(data.keycardDropPrefab, dropPos, Quaternion.identity);
            }
            Time.timeScale = savedTimeScale;
            if (weaponCollider != null) weaponCollider.enabled = false;
            // TODO: death animation, despawn logic
            Destroy(gameObject);
        }

        private void CheckGround()
        {
            if (col == null) { isGrounded = false; return; }
            float halfW = col.bounds.extents.x * 0.85f;
            Vector2 bottom = new Vector2(col.bounds.center.x, col.bounds.min.y);
            isGrounded = Physics2D.Raycast(bottom,                          Vector2.down, groundCheckDistance, groundLayer)
                      || Physics2D.Raycast(bottom + Vector2.right * halfW,  Vector2.down, groundCheckDistance, groundLayer)
                      || Physics2D.Raycast(bottom + Vector2.left  * halfW,  Vector2.down, groundCheckDistance, groundLayer);
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

        // Returns -1f if the wall ahead is too tall to jump — caller should reverse direction instead.
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
            if (needed > jumpForce) return -1f;
            return Mathf.Clamp(needed, jumpForceMin, jumpForce);
        }

        private bool IsWallToppedAhead(float moveDir)
        {
            if (col == null) return true;
            Vector2 topOrigin = new Vector2(col.bounds.center.x, col.bounds.max.y);
            return Physics2D.Raycast(topOrigin, Vector2.right * moveDir, wallCheckDistance, obstacleLayer);
        }

        private void TryJump(float moveDir)
        {
            if (!isGrounded || Time.fixedTime < lastJumpTime + jumpCooldown) return;
            if (!HasWallAhead(moveDir)) return;

            if (!IsWallToppedAhead(moveDir))
            {
                // Low obstacle — top of collider clears it, jump freely
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                lastJumpTime = Time.fixedTime;
                stuckTimer = 0f;
                return;
            }

            float jf = CalculateJumpForce(moveDir);
            if (jf < 0f) { TriggerWallBlock(-moveDir); stuckTimer = 0f; return; }
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jf);
            lastJumpTime = Time.fixedTime;
            stuckTimer = 0f;
        }

        private void ApplyGroundedMove(float dirSign, float speed)
        {
            if (isGrounded && Mathf.Abs(rb.linearVelocity.x) < 0.3f && rb.linearVelocity.y <= 0.1f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 0.25f && Time.fixedTime >= lastJumpTime + jumpCooldown)
                {
                    if (!IsWallToppedAhead(dirSign))
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                        lastJumpTime = Time.fixedTime;
                        stuckTimer = 0f;
                        return;
                    }
                    float jf = CalculateJumpForce(dirSign);
                    if (jf < 0f) { TriggerWallBlock(-dirSign); stuckTimer = 0f; return; }
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

        private void RollCombatIntent()
        {
            bool canShoot = data == null || data.canShoot;
            bool canMelee = data == null || data.canMelee;
            // Shoot-only enemies have no reason to chase (nothing to do at melee range)
            float effectiveChase   = (canShoot && !canMelee) ? 0f : chaseIntentWeight;
            float effectiveShoot   = canShoot ? shootIntentWeight   : 0f;
            float effectiveRetreat = canShoot ? retreatIntentWeight : 0f;
            float total = effectiveChase + effectiveShoot + effectiveRetreat;
            float roll = Random.Range(0f, total);
            if (roll < effectiveChase)
                currentIntent = CombatIntent.Chase;
            else if (roll < effectiveChase + effectiveShoot)
                currentIntent = CombatIntent.Shoot;
            else
                currentIntent = CombatIntent.Retreat;
        }

        private void SetupRetreatTarget()
        {
            float awayDir = -Mathf.Sign(player.transform.position.x - transform.position.x);
            retreatTargetX = transform.position.x + awayDir * retreatTargetDistance;
            retreatReached = false;
        }

        private void ExecuteCombatIntent(float playerDist)
        {
            switch (currentIntent)
            {
                case CombatIntent.Shoot:
                    if (playerDist > shootMinDistance && Time.fixedTime >= nextShootTime)
                    {
                        nextShootTime = Time.fixedTime + shootCooldown;
                        shootCoroutine = StartCoroutine(ShootRoutine());
                    }
                    else if (data != null && !data.canMelee)
                    {
                        // Shoot-only: back away to preferred distance during cooldown
                        if (playerDist < retreatTargetDistance)
                        {
                            float awayDir = -Mathf.Sign(player.transform.position.x - transform.position.x);
                            if (awayDir == 0f) awayDir = -chaseDir;
                            ApplyGroundedMove(awayDir, retreatMoveSpeed);
                            isMoving = true;
                        }
                        else
                        {
                            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                            isMoving = false;
                        }
                    }
                    else if (playerDist > chaseStopDistance)
                    {
                        ChasePlayer();
                        isMoving = true;
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        isMoving = false;
                    }
                    break;

                case CombatIntent.Chase:
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
                    break;

                case CombatIntent.Retreat:
                    if (!retreatReached)
                    {
                        float distToTarget = Mathf.Abs(transform.position.x - retreatTargetX);
                        if (distToTarget > 0.3f)
                        {
                            float dir = Mathf.Sign(retreatTargetX - transform.position.x);
                            ApplyGroundedMove(dir, retreatMoveSpeed);
                            isMoving = true;
                        }
                        else
                        {
                            retreatReached = true;
                            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                            isMoving = false;
                        }
                    }
                    else
                    {
                        if (playerDist > shootMinDistance && Time.fixedTime >= nextShootTime)
                        {
                            nextShootTime = Time.fixedTime + shootCooldown;
                            shootCoroutine = StartCoroutine(ShootRoutine());
                        }
                        else
                        {
                            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                            isMoving = false;
                        }
                    }
                    break;
            }
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
