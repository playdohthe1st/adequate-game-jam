using UnityEngine;

namespace AdequateEnough
{
    public class PlayerController : MonoBehaviour
    {
        public enum PlayerState { Grounded, Airborne, Spinning }
        public PlayerState CurrentState { get; private set; }

        [Header("Movement")]
        [SerializeField] private float maxSpeed = 8f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 12f;
        // How quickly smoothedInput catches up to raw input - lower values feel floatier
        [SerializeField] private float inputSmoothing = 12f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 15f;
        // When the player releases jump early, multiply upward velocity by this to cut the jump short
        [SerializeField] private float jumpCutMultiplier = 0.4f;
        // Coyote time lets the player jump for a brief window after walking off a ledge.
        // Without it, jumping right at the edge of a platform feels unfair.
        [SerializeField] private float coyoteTime = 0.12f;
        // Jump buffering lets the player press jump slightly before landing and still get a jump.
        // Without it, pressing jump a frame too early feels like the input was ignored.
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Air Control")]
        // Fraction of ground acceleration applied in the air. Keeping this low makes
        // the player feel committed to their jump direction without being completely locked in.
        [SerializeField][Range(0f, 1f)] private float airControlMultiplier = 0.3f;
        // Much lower air control during a spin — the player is committed to their launch direction
        [SerializeField][Range(0f, 1f)] private float spinAirControlMultiplier = 0.05f;

        [Header("Gravity")]
        // Multiplied against the default gravity when the player is falling.
        // A value above 1 makes falls feel snappier and less floaty.
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        // When vertical speed drops below this threshold near the top of a jump, we're at the apex
        [SerializeField] private float apexThreshold = 2f;
        // Reduce gravity at the apex so the player hangs in the air briefly at the peak
        [SerializeField] private float apexGravityMultiplier = 0.5f;
        // Slight speed boost at the apex lets the player cover more horizontal distance at the top of a jump
        [SerializeField] private float apexSpeedBoost = 1.3f;

        [Header("Landing Squash")]
        [SerializeField] private float squashAmount = 0.15f;
        [SerializeField] private float squashDuration = 0.08f;
        [SerializeField] private float squashRecoverDuration = 0.12f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        [Header("Slopes")]
        [SerializeField] private float slopeCheckDistance = 0.5f;
        [SerializeField] private float maxSlopeAngle = 45f;
        // Cap speed on slopes so the player doesn't accelerate infinitely downhill
        [SerializeField] private float maxSlopeSpeed = 6f;
        // No friction while moving so the player slides smoothly; full friction while standing to prevent sliding
        [SerializeField] private PhysicsMaterial2D noFriction;
        [SerializeField] private PhysicsMaterial2D fullFriction;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float regenRate = 5f;
        // How many seconds after taking damage before regen kicks in
        [SerializeField] private float regenDelay = 10f;

        [Header("Spin")]
        [SerializeField] private float spinForce = 20f;
        [SerializeField] private float spinMaxSpeed = 18f;
        // How fast spinMaxSpeed decays back down to maxSpeed each fixed frame
        [SerializeField] private float spinDecayRate = 4f;
        [SerializeField] private float spinCooldown = 1.5f;
        // Reduced gravity when spinning upward so the spin feels like it has lift
        [SerializeField] private float upwardSpinGravityScale = 0.5f;
        // How many units above the spin's origin the player can travel before upward velocity is cut
        [SerializeField] private float spinMaxRise = 6f;
        [SerializeField] private float spinDamage = 20f;

        [Header("Visuals")]
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private SpriteRenderer playerSpriterender;

        private Rigidbody2D rb;
        private CapsuleCollider2D col;
        private InputManager input;

        private float defaultGravityScale;
        private float currentMaxSpeed;
        private float nextSpinTime;
        private float spinStartY;
        private float spinInitialYVelocity;
        private float lastFacingDirection = 1f;

        private bool isGrounded;
        private bool isOnSlope;
        private bool isSpinning;
        private Vector2 slopeNormal;
        private Vector2 smoothedInput;

        private float coyoteTimeCounter;
        private float jumpBufferCounter;
        private bool spinQueued;
        private bool wasJumpHeld;
        private bool wasGrounded;
        private Coroutine squashCoroutine;
        // Prevents landing squash from triggering immediately on spawn or respawn
        private float skipLandingUntil;
        private float nextSquashTime;
        private Vector3 defaultScale;

        private float currentHealth;
        private float timeSinceLastDamage;
        private bool isDead;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        public bool HasKeycard1 { get; private set; }
        public bool HasKeycard2 { get; private set; }
        public bool HasKeycard3 { get; private set; }

        // Fired when the player respawns at the original spawn point with no checkpoint active
        public static event System.Action OnRespawnedAtOrigin;

        // Grants the lowest-numbered keycard not yet held. Returns false if all three are already owned.
        public bool GiveNextKeycard()
        {
            if (!HasKeycard1) { HasKeycard1 = true; return true; }
            if (!HasKeycard2) { HasKeycard2 = true; return true; }
            if (!HasKeycard3) { HasKeycard3 = true; return true; }
            return false;
        }

        // True while the coyote time window is open, meaning the player can still jump
        private bool CanJump => coyoteTimeCounter > 0f;
        // True near the top of a jump where vertical speed has almost stalled
        private bool IsAtApex => !isGrounded && Mathf.Abs(rb.linearVelocity.y) < apexThreshold;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CapsuleCollider2D>();
            input = GetComponent<InputManager>();
            defaultGravityScale = rb.gravityScale;
            currentMaxSpeed = maxSpeed;
            currentHealth = maxHealth;
            defaultScale = transform.localScale;
            // Give the physics a moment to settle on spawn before we check for landings
            skipLandingUntil = Time.time + 0.5f;
        }

        // Input reading and non-physics state changes go in Update so they run every rendered frame.
        // Physics forces go in FixedUpdate so they run at a fixed timestep, independent of frame rate.
        private void Update()
        {
            HandleRegen();
            if (isDead) return;

            CheckGround();
            DetectLanding();
            CheckSlope();
            UpdateState();
            UpdateTimers();
            SmoothInput();
            VisualUpdater();
            HandleVariableJump();
            HandleGravity();

            // Buffer the input flags here so they're not missed if FixedUpdate runs late
            if (input.GetJumpPressed()) jumpBufferCounter = jumpBufferTime;
            if (input.GetSpinPressed()) spinQueued = true;
        }

        private void FixedUpdate()
        {
            if (isDead) return;

            HandleSpinDecay();
            Move();
            TryJump();
            TrySpin();
        }

        // Lerp toward raw input each frame instead of using it directly.
        // This gives the movement a slight ramp-up/ramp-down feel without needing a state machine.
        private void SmoothInput()
        {
            smoothedInput = Vector2.Lerp(smoothedInput, input.GetMove(), inputSmoothing * Time.deltaTime);
        }

        private void VisualUpdater()
        {
            // animation
            playerAnimator.SetBool("IsSpinning", isSpinning); // this set bool is spining on animator controller to the is spining on the player controller
            if (input.GetMove().magnitude > 0)
            {
                playerAnimator.SetBool("IsMoving", true);
            }
            else
            {
                playerAnimator.SetBool("IsMoving", false);
            }
            // flipSprite
            if (input.GetMove().x < 0)
            {
                playerSpriterender.flipX = true;
            }
            if (input.GetMove().x > 0)
            {
                playerSpriterender.flipX = false;
            }
              

        }
        private void UpdateTimers()
        {
            // Reset coyote time while grounded; count it down once in the air
            coyoteTimeCounter = isGrounded ? coyoteTime : coyoteTimeCounter - Time.deltaTime;
            jumpBufferCounter -= Time.deltaTime;
        }

        private void HandleVariableJump()
        {
            bool jumpHeld = input.GetJumpHeld();

            // Only cut the jump on the exact frame the player releases the button while still rising.
            // This gives shorter hops for taps and full jumps for holds.
            if (wasJumpHeld && !jumpHeld && rb.linearVelocity.y > 0f && !isGrounded && !isSpinning)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

            wasJumpHeld = jumpHeld;
        }

        private void HandleGravity()
        {
            float targetGravity;

            if (!isSpinning && IsAtApex)
                targetGravity = defaultGravityScale * apexGravityMultiplier; // hang at the top (normal jump only)
            else if (rb.linearVelocity.y < 0f)
                targetGravity = defaultGravityScale * fallGravityMultiplier; // fall faster than we rose
            else
                targetGravity = defaultGravityScale;

            // Lerp to the target gravity scale so transitions aren't jarring
            rb.gravityScale = Mathf.Lerp(rb.gravityScale, targetGravity, 12f * Time.deltaTime);
        }

        private void CheckGround()
        {
            if (groundCheck == null) { isGrounded = false; return; }
            isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
        }

        private void CheckSlope()
        {
            if (!isGrounded)
            {
                isOnSlope = false;
                if (col != null) col.sharedMaterial = noFriction;
                return;
            }

            RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, slopeCheckDistance, groundLayer);

            // Ignore hits that are far below - the player is standing on a surface above the slope
            if (hit && hit.distance <= groundCheckSize.y + 0.05f)
            {
                slopeNormal = hit.normal;
                float angle = Vector2.Angle(slopeNormal, Vector2.up);
                isOnSlope = angle > 1f && angle <= maxSlopeAngle;

                if (col != null)
                {
                    // Switch to full friction when standing still on a slope, otherwise the player slides
                    bool standing = Mathf.Abs(smoothedInput.x) < 0.01f;
                    col.sharedMaterial = (isOnSlope && standing) ? fullFriction : noFriction;
                }
            }
            else
            {
                isOnSlope = false;
            }
        }

        private void UpdateState()
        {
            if (isSpinning)
                CurrentState = PlayerState.Spinning;
            else if (isGrounded)
                CurrentState = PlayerState.Grounded;
            else
                CurrentState = PlayerState.Airborne;
        }

        private void Move()
        {
            if (Mathf.Abs(smoothedInput.x) > 0.01f)
                lastFacingDirection = Mathf.Sign(smoothedInput.x);

            if (CurrentState == PlayerState.Airborne)
            {
                float controlMult = isSpinning ? spinAirControlMultiplier : airControlMultiplier;

                // In the air we apply reduced force and can't exceed maxSpeed (with apex boost applied)
                if (Mathf.Abs(smoothedInput.x) > 0.01f)
                {
                    float effectiveMaxSpeed = IsAtApex ? maxSpeed * apexSpeedBoost : maxSpeed;
                    float airDesiredSpeed = smoothedInput.x * effectiveMaxSpeed;
                    rb.AddForce((airDesiredSpeed - rb.linearVelocity.x) * acceleration * controlMult * Vector2.right, ForceMode2D.Force);
                }
                else
                {
                    rb.AddForce(-rb.linearVelocity.x * deceleration * controlMult * Vector2.right, ForceMode2D.Force);
                }
                return;
            }

            float desiredSpeed = smoothedInput.x * currentMaxSpeed;
            float rate;

            if (Mathf.Abs(smoothedInput.x) > 0.01f)
            {
                // Use a faster rate when reversing direction so the player can pivot quickly
                bool changingDir = Mathf.Sign(smoothedInput.x) != Mathf.Sign(rb.linearVelocity.x)
                                   && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
                rate = changingDir ? deceleration * 1.5f : acceleration;
            }
            else
            {
                rate = deceleration;
            }

            // On a slope, push along the slope surface rather than horizontally so the player
            // stays flush with the ground instead of fighting against the slope's normal
            Vector2 moveDir = isOnSlope
                ? new Vector2(slopeNormal.y, -slopeNormal.x) * Mathf.Sign(smoothedInput.x)
                : Vector2.right;

            rb.AddForce((desiredSpeed - rb.linearVelocity.x) * rate * moveDir, ForceMode2D.Force);

            // Cap horizontal speed on slopes to prevent accelerating downhill, leave Y untouched
            if (isOnSlope)
                rb.linearVelocity = new Vector2(Mathf.Clamp(rb.linearVelocity.x, -maxSlopeSpeed, maxSlopeSpeed), rb.linearVelocity.y);
        }

        private void TryJump()
        {
            if (jumpBufferCounter <= 0f) return;
            if (!CanJump) return;

            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;

            // Zero out Y velocity before applying the jump impulse so the jump height is consistent
            // regardless of whether the player was moving up or down a slope
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        private void TrySpin()
        {
            if (!spinQueued) return;
            spinQueued = false;

            if (isSpinning) return;
            if (Time.time < nextSpinTime) return;

            nextSpinTime = Time.time + spinCooldown;
            isSpinning = true;
            spinStartY = transform.position.y;

            // If the player is holding a direction, snap it to the nearest 45 degree angle.
            // Otherwise default to the last direction the player was facing.
            Vector2 rawInput = input.GetMove();
            Vector2 spinDirection = rawInput.sqrMagnitude > 0.001f
                ? Get8WayDirection(rawInput)
                : new Vector2(lastFacingDirection, 0f);

            // Reduce gravity for upward spins so they feel more like a leap than a straight shot
            if (spinDirection.y > 0.1f)
                rb.gravityScale = upwardSpinGravityScale;

            rb.linearVelocity = spinDirection * spinForce;
            spinInitialYVelocity = rb.linearVelocity.y;
            currentMaxSpeed = spinMaxSpeed;
        }

        // Each FixedUpdate, bleed currentMaxSpeed back toward the normal maxSpeed.
        // When they're equal the spin is over and we restore gravity.
        private void HandleSpinDecay()
        {
            if (!isSpinning) return;

            // Smoothly scale down upward velocity as the player rises toward the cap.
            // At the origin t=0 so the cap is the full initial velocity; at the cap t=1 so it's clamped to 0.
            if (rb.linearVelocity.y > 0f && spinInitialYVelocity > 0f)
            {
                float t = Mathf.Clamp01((transform.position.y - spinStartY) / spinMaxRise);
                float maxY = spinInitialYVelocity * (1f - t);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, maxY));
            }

            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, maxSpeed, spinDecayRate * Time.fixedDeltaTime);

            if (Mathf.Approximately(currentMaxSpeed, maxSpeed))
            {
                currentMaxSpeed = maxSpeed;
                isSpinning = false;
                rb.gravityScale = defaultGravityScale;
            }
        }

        // Snaps an arbitrary input direction to the 8 cardinal/diagonal directions (every 45 degrees)
        private Vector2 Get8WayDirection(Vector2 input)
        {
            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped)).normalized;
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

        [ContextMenu("Debug/Kill Player")]
        public void DebugKill() => Die();

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            rb.linearVelocity = Vector2.zero;
            // TODO: Show death UI here before fading (score screen, respawn prompt, etc.)
            ScreenFader.Instance?.RespawnFade(Respawn);
        }

        private void Respawn()
        {
            bool noCheckpoint = CheckpointManager.Instance == null || !CheckpointManager.Instance.HasActiveCheckpoint;
            if (noCheckpoint) OnRespawnedAtOrigin?.Invoke();

            Vector2 spawnPos = CheckpointManager.Instance != null
                ? CheckpointManager.Instance.GetRespawnPosition()
                : (Vector2)transform.position;

            transform.position = spawnPos;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = defaultGravityScale;
            currentHealth = maxHealth;
            timeSinceLastDamage = 0f;
            isSpinning = false;
            isDead = false;

            // Clean up any mid-animation squash and reset to normal scale
            if (squashCoroutine != null) StopCoroutine(squashCoroutine);
            transform.localScale = defaultScale;
            // Prevent a false landing detection immediately after being placed at the spawn point
            skipLandingUntil = Time.time + 0.5f;
        }

        private void DetectLanding()
        {
            if (Time.time < skipLandingUntil)
            {
                wasGrounded = isGrounded;
                return;
            }

            // Landing = was airborne last frame and is grounded this frame
            if (!wasGrounded && isGrounded && !isSpinning && Time.time >= nextSquashTime)
            {
                nextSquashTime = Time.time + squashDuration + squashRecoverDuration + 0.1f;
                if (squashCoroutine != null) StopCoroutine(squashCoroutine);
                squashCoroutine = StartCoroutine(LandingSquash());
            }
            wasGrounded = isGrounded;
        }

        // Classic squash-and-stretch landing feedback: quickly flatten the sprite wide on impact,
        // then spring it back to normal. This is a juice/feel technique common in platformers.
        private System.Collections.IEnumerator LandingSquash()
        {
            // Widen and shorten the scale to simulate impact compression
            Vector3 squashed = new Vector3(defaultScale.x * (1f + squashAmount), defaultScale.y * (1f - squashAmount), defaultScale.z);

            float elapsed = 0f;
            while (elapsed < squashDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(defaultScale, squashed, elapsed / squashDuration);
                yield return null;
            }

            // Spring back to default scale
            elapsed = 0f;
            while (elapsed < squashRecoverDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(squashed, defaultScale, elapsed / squashRecoverDuration);
                yield return null;
            }

            transform.localScale = defaultScale;
            squashCoroutine = null;
        }

        private void OnCollisionEnter2D(Collision2D col)
        {
            if (!isSpinning) return;
            col.gameObject.GetComponent<Enemy>()?.TakeDamage(spinDamage);
        }

        private void OnTriggerEnter2D(Collider2D col)
        {
            if (!isSpinning) return;
            col.gameObject.GetComponent<Enemy>()?.TakeDamage(spinDamage);
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }
}
