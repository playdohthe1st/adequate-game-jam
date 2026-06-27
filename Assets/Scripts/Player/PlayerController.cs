using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
public enum KeycardLevel
{
    None = 0,
    SectorA = 1,
    SectorB = 2,
    SectorC = 3
}
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

        [Header("Fall Stretch Settings")]
        [SerializeField] private float maxFallStretch = 0.15f;   
        [SerializeField] private float maxFallSpeed = 20f;         
        [SerializeField] private float fallStretchLerpSpeed = 10f; 

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        [Header("Slopes")]
        [SerializeField] private float slopeCheckDistance = 0.5f;
        [SerializeField] private float maxSlopeAngle = 45f;
        [SerializeField] private float knockbackSlopeDisableDuration = 0.6f;
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

        [Header("One-Way Platforms")]
        [SerializeField] private LayerMask oneWayPlatformLayer;

        [Header("SFX")]
        [SerializeField] private AudioClip jumpSFX;
        [SerializeField] private AudioClip spinIntroSFX;
        [SerializeField] private AudioClip rollLoopSFX;
        [SerializeField] private AudioClip playerHurtSFX;
        [SerializeField] private AudioClip keycardSFX;
        [Header("Landing SFX")]
        [SerializeField] private AudioClip[] landStoneSFX;
        [SerializeField] private AudioClip[] landWoodSFX;
        [SerializeField] private AudioClip[] landMetalSFX;
        [SerializeField] private AudioClip[] landMudSFX;
        [SerializeField] private AudioClip[] landBedSFX;
        [Header("Footstep SFX")]
        [SerializeField] private AudioClip[] footStoneSFX;
        [SerializeField] private AudioClip[] footWoodSFX;
        [SerializeField] private AudioClip[] footMetalSFX;
        [SerializeField] private AudioClip[] footMudSFX;
        [SerializeField] private AudioClip[] footBedSFX;

        [Header("Security Clearance")]
        [SerializeField] private KeycardLevel currentKeycard = KeycardLevel.None;
        public Door currentDoor = null;
        public static event Action<KeycardLevel> OnKeycardUpgraded;

        [Header("Visuals")]
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private SpriteRenderer playerSpriterender;
        [SerializeField] private Transform spriteTransform;
        //Particles
        [SerializeField] private GameObject landingSmokePrefab;
        [SerializeField] private ScreenFader screenFader;
        // ui
        [SerializeField] private GameObject deathScreen;
        [SerializeField] private GameObject creditScreen;
        [SerializeField] private Image healthImage;
        //end video     
        [SerializeField] private GameObject videoPanel; 
        [SerializeField] private VideoPlayer videoPlayer;
        private Coroutine videoRoutineInstance;
        public bool isVideoPlaying = false;

        private Rigidbody2D rb;
        private CapsuleCollider2D col;
        private InputManager input;
        private float speedMultiplier = 1f;
        private Coroutine gooDebuffCoroutine;
        private AudioSource rollLoopSource;
        private AudioSource footstepSource;

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
        private float slopeDisableTimer;
        private bool isDead;
        private bool isOnStairs;
        private Vector2 stairContactNormal = Vector2.up;
        private bool isDropping;
        private Collider2D currentOneWayPlatform;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public bool IsSpinning => isSpinning;

        // Fired when the player respawns at the original spawn point with no checkpoint active
        public static event System.Action OnRespawnedAtOrigin;

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
            currentMaxSpeed = maxSpeed * speedMultiplier;
            currentHealth = maxHealth;
            defaultScale = spriteTransform != null ? spriteTransform.localScale : transform.localScale;
            skipLandingUntil = Time.time + 0.5f;
            OnKeycardUpgraded?.Invoke(currentKeycard);

            rollLoopSource = gameObject.AddComponent<AudioSource>();
            rollLoopSource.clip = rollLoopSFX;
            rollLoopSource.loop = true;
            rollLoopSource.playOnAwake = false;
            rollLoopSource.spatialBlend = 0f;

            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.loop = false;
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 0f;
        }
        // Input reading and non-physics state changes go in Update so they run every rendered frame.
        // Physics forces go in FixedUpdate so they run at a fixed timestep, independent of frame rate.
        public void Start()
        {
            if (videoPlayer != null)
                videoPlayer.loopPointReached += OnVideoFinished;

            if (AudioManager.Instance != null)
            {
                var sfxGroup = AudioManager.Instance.SFXMixerGroup;
                if (rollLoopSource != null) rollLoopSource.outputAudioMixerGroup = sfxGroup;
                if (footstepSource != null) footstepSource.outputAudioMixerGroup = sfxGroup;
            }
        }
        private void Update()
        {
            HandleRegen();
            if (isDead) return;
            //StopVideo();
            CheckGround();
            DetectLanding();
            CheckSlope();
            UpdateState();
            UpdateTimers();
            SmoothInput();
            HandleVariableJump();
            HandleGravity();
            VisualUpdater();
            HandleFallStretch();
            // Buffer the input flags here so they're not missed if FixedUpdate runs late
            if (input.GetJumpPressed()) jumpBufferCounter = jumpBufferTime;
            if (input.GetSpinPressed()) spinQueued = true;
            TryDropThrough();
            if (input.GetInteract()) GoToNextRoom();
            if (screenFader == null)
            {
                screenFader = FindAnyObjectByType<ScreenFader>();
            }
            // for skip finalCutsene video 
         
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

        //private void StopVideo()
        //{
        //    if (isVideoPlaying)
         //   {
         //       if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
         //       {
         //           if (videoPlayer != null)
         //           {
         //               videoPlayer.loopPointReached -= OnVideoFinished;
         //               videoPlayer.Stop();
         //           }
         //
          //          if (videoRoutineInstance != null)
          //          {
          //              StopCoroutine(videoRoutineInstance);
         //               videoRoutineInstance = null;
         //           }
         //
         //           StartCoroutine(StartCredits());
         //       }
         //   }
        //}
        public void GiveNextKeycard()
        {
            if (currentKeycard < KeycardLevel.SectorC)
            {
                currentKeycard++;
                OnKeycardUpgraded?.Invoke(currentKeycard);
            }
        }
        public void SetKeycardLevel(KeycardLevel newLevel)
        {
            if (newLevel > currentKeycard)
            {
                currentKeycard = newLevel;
                OnKeycardUpgraded?.Invoke(currentKeycard);
                if (keycardSFX != null) AudioManager.Instance?.PlaySFX(keycardSFX);
            }
        }

        private void GoToNextRoom()
        {
            if (currentDoor == null) return;

            // Compare the player's card level against the door's required level
            if (currentKeycard >= currentDoor.requiredLevel)
            {
                ScreenFader.Instance.StartCoroutine(ScreenFader.Instance.FadeOutToRoom(gameObject, currentDoor.destination));
            }
            else
            {
                Debug.Log("Locked! You need a " + currentDoor.requiredLevel + " card.");
            }
        }
        private void SmoothInput()
        {
            smoothedInput = Vector2.Lerp(smoothedInput, input.GetMove(), inputSmoothing * Time.deltaTime);
        }

        private void VisualUpdater()
        {
            // animation
            playerAnimator.SetBool("IsSpinning", isSpinning); // this set bool is spining on animator controller to the is spining on the player controller
            playerAnimator.SetBool("IsGrounded", isGrounded);
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
                playerSpriterender.flipX = false;
            }
            if (input.GetMove().x > 0)
            {
                playerSpriterender.flipX = true;
            }
            healthImage.fillAmount = currentHealth / maxHealth; 

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
            // Kill gravity while standing still on a slope so the player can't slide down.
            // Velocity is also zeroed in Move(), but gravity reapplies every physics step —
            // both together are needed to fully prevent creep.
            if ((isOnSlope && Mathf.Abs(input.GetMove().x) < 0.01f) || isOnStairs)
            {
                rb.gravityScale = 0f;
                return;
            }

            float targetGravity;

            if (!isSpinning && IsAtApex)
                targetGravity = defaultGravityScale * apexGravityMultiplier;
            else if (rb.linearVelocity.y < 0f)
                targetGravity = defaultGravityScale * fallGravityMultiplier;
            else
                targetGravity = defaultGravityScale;

            rb.gravityScale = Mathf.Lerp(rb.gravityScale, targetGravity, 12f * Time.deltaTime);
        }

        private void CheckGround()
        {
            if (groundCheck == null) { isGrounded = false; return; }
            isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer | oneWayPlatformLayer);
        }

        private void CheckSlope()
        {
            if (!isGrounded || slopeDisableTimer > 0f)
            {
                isOnSlope = false;
                if (col != null) col.sharedMaterial = noFriction;
                return;
            }

            RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, slopeCheckDistance, groundLayer);

            if (hit && hit.distance <= groundCheckSize.y + 0.05f)
            {
                slopeNormal = hit.normal;
                float angle = Vector2.Angle(slopeNormal, Vector2.up);
                isOnSlope = angle > 10f && angle <= maxSlopeAngle;

                if (col != null)
                {
                    bool standing = Mathf.Abs(input.GetMove().x) < 0.01f;
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
                    float effectiveMaxSpeed = (IsAtApex ? maxSpeed * apexSpeedBoost : maxSpeed) * speedMultiplier;
                    float airDesiredSpeed = smoothedInput.x * effectiveMaxSpeed;
                    rb.AddForce((airDesiredSpeed - rb.linearVelocity.x) * acceleration * controlMult * Vector2.right, ForceMode2D.Force);
                }
                else
                {
                    rb.AddForce(-rb.linearVelocity.x * deceleration * controlMult * Vector2.right, ForceMode2D.Force);
                }
                return;
            }

            // Stairs: bypass AddForce entirely and set velocity directly along the slope surface.
            // Force-based movement produces unpredictable directions on angled surfaces.
            if (isOnStairs)
            {
                if (Mathf.Abs(smoothedInput.x) < 0.01f)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    Vector2 stairDir = new Vector2(stairContactNormal.y, -stairContactNormal.x) * Mathf.Sign(smoothedInput.x);
                    rb.linearVelocity = stairDir * Mathf.Abs(smoothedInput.x) * maxSpeed * speedMultiplier;
                }
                return;
            }

            float desiredSpeed = smoothedInput.x * currentMaxSpeed;
            float rate;

            if (Mathf.Abs(smoothedInput.x) > 0.01f)
            {
                bool changingDir = Mathf.Sign(smoothedInput.x) != Mathf.Sign(rb.linearVelocity.x)
                                   && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
                rate = changingDir ? deceleration * 1.5f : acceleration;
            }
            else
            {
                rate = deceleration;
            }

            if (isOnSlope && Mathf.Abs(input.GetMove().x) < 0.01f)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 moveDir = isOnSlope
                ? new Vector2(slopeNormal.y, -slopeNormal.x) * Mathf.Sign(smoothedInput.x)
                : Vector2.right;

            rb.AddForce((desiredSpeed - rb.linearVelocity.x) * rate * moveDir, ForceMode2D.Force);

            if (isOnSlope)
                rb.linearVelocity = new Vector2(Mathf.Clamp(rb.linearVelocity.x, -maxSlopeSpeed, maxSlopeSpeed), rb.linearVelocity.y);
        }

        private void TryJump()
        {
            if (jumpBufferCounter <= 0f) return;
            if (!CanJump) return;

            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;

            if (squashCoroutine != null) StopCoroutine(squashCoroutine);

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            if (jumpSFX != null) AudioManager.Instance?.PlaySFX(jumpSFX);

            // 2. Start the animation normally without a physics delay
            squashCoroutine = StartCoroutine(JumpingSquash());
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
            if (spinIntroSFX != null) AudioManager.Instance?.PlaySFX(spinIntroSFX);
            if (rollLoopSource != null && !rollLoopSource.isPlaying) rollLoopSource.Play();

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
            currentMaxSpeed = spinMaxSpeed * speedMultiplier;
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

            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, maxSpeed * speedMultiplier, spinDecayRate * Time.fixedDeltaTime);

            if (Mathf.Approximately(currentMaxSpeed, maxSpeed * speedMultiplier))
            {
                currentMaxSpeed = maxSpeed * speedMultiplier;
                isSpinning = false;
                rb.gravityScale = defaultGravityScale;
                rollLoopSource?.Stop();
            }
        }

        private void TryDropThrough()
        {
            if (!isGrounded || isDropping) return;

            bool dropPressed = input.GetMove().y < -0.5f
                || Keyboard.current.cKey.isPressed
                || Keyboard.current.leftCtrlKey.isPressed
                || Keyboard.current.rightCtrlKey.isPressed;

            if (dropPressed) StartCoroutine(DropThrough());
        }

        private IEnumerator DropThrough()
        {
            isDropping = true;

            Collider2D platform = currentOneWayPlatform;
            if (platform != null)
            {
                Physics2D.IgnoreCollision(col, platform, true);

                // Wait a minimum time, then keep waiting until the player is fully clear.
                // Re-enabling while still inside would snap the player back up.
                yield return new WaitForSeconds(0.15f);
                float safetyTimeout = Time.time + 2f;
                while (Physics2D.Distance(col, platform).distance < 0f && Time.time < safetyTimeout)
                    yield return null;

                Physics2D.IgnoreCollision(col, platform, false);
            }

            isDropping = false;
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
            slopeDisableTimer = Mathf.Max(slopeDisableTimer - Time.deltaTime, 0f);
            if (timeSinceLastDamage >= regenDelay)
                currentHealth = Mathf.Min(currentHealth + regenRate * Time.deltaTime, maxHealth);
        }

        public void ApplyGooDebuff()
        {
            InterruptSpin();
            if (gooDebuffCoroutine != null) StopCoroutine(gooDebuffCoroutine);
            gooDebuffCoroutine = StartCoroutine(GooDebuffRoutine());
        }

        private void InterruptSpin()
        {
            if (!isSpinning) return;
            isSpinning = false;
            rb.gravityScale = defaultGravityScale;
            currentMaxSpeed = maxSpeed * speedMultiplier;
            nextSpinTime = Time.time + spinCooldown;
            rollLoopSource?.Stop();
        }

        private IEnumerator GooDebuffRoutine()
        {
            speedMultiplier = 0.5f;
            if (!isSpinning) currentMaxSpeed = maxSpeed * speedMultiplier;
            if (playerSpriterender != null) playerSpriterender.color = Color.green;
            yield return new WaitForSeconds(3f);
            speedMultiplier = 1f;
            if (!isSpinning) currentMaxSpeed = maxSpeed * speedMultiplier;
            if (playerSpriterender != null) playerSpriterender.color = Color.white;
            gooDebuffCoroutine = null;
        }

        public void TakeDamage(float amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Max(currentHealth - amount, 0f);
            timeSinceLastDamage = 0f;
            slopeDisableTimer = knockbackSlopeDisableDuration;
            if (playerHurtSFX != null) AudioManager.Instance?.PlaySFX(playerHurtSFX);
            if (currentHealth <= 0f) Die();
        }

        [ContextMenu("Debug/Kill Player")]
        public void DebugKill() => Die();

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            rb.linearVelocity = Vector2.zero;
            if (ScreenFader.Instance != null)
                ScreenFader.Instance.RespawnFade(Respawn);
            else
                Respawn();
        }
        public void Win()
        {
    
                if (isVideoPlaying) return;
                TriggerVideoSequence();
 
        }
        public void TriggerVideoSequence()
        {
            if (!isVideoPlaying)
            {
                videoRoutineInstance = StartCoroutine(StartVideo());
            }
        }
        private IEnumerator StartVideo()
        {
            isVideoPlaying = true;

            // 1. Fade to Black
            yield return StartCoroutine(ScreenFader.Instance.Fade(0f, 1f, 1f));
            if (videoPlayer != null && videoPanel != null)
            {
                videoPanel.SetActive(true);
                videoPlayer.Play();
                yield return StartCoroutine(ScreenFader.Instance.Fade(1f, 0f, 0.3f));
            }
            else
            {
                StartCoroutine(StartCredits());
            }
        }
        private void OnVideoFinished(VideoPlayer vp)
        {
            StopAllCoroutines();
            StartCoroutine(StartCredits());
        }

        private IEnumerator StartCredits()
        {
            if (videoPanel != null) videoPanel.SetActive(false);
            isVideoPlaying = false;
            creditScreen.SetActive(true);
            var creditsScript = creditScreen.GetComponentInChildren<CreditsScroll>();
            if (creditsScript != null)
            {
                creditsScript.StartCredits();
            }
            yield return StartCoroutine(ScreenFader.Instance.Fade(1f, 0f, 1f));
        }
        private void Respawn()
        {
            if (deathScreen != null) deathScreen.SetActive(false);
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
            Transform squashTarget = spriteTransform != null ? spriteTransform : transform;
            squashTarget.localScale = defaultScale;
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
                PlayRandomSFX(GetLandClips(GetSurfaceBelow()));

                if (landingSmokePrefab != null)
                {
                    // Use groundCheckTransform if available, otherwise default to player position
                    Vector3 spawnPosition = groundCheck != null ? groundCheck.position : transform.position;

                    // Instantiate the particle system
                    GameObject smoke = Instantiate(landingSmokePrefab, spawnPosition, Quaternion.identity);

                    // Optional: Auto-destroy the particle object after 2 seconds so it doesn't clutter your hierarchy
                    Destroy(smoke, 2f);
                }

                if (squashCoroutine != null) StopCoroutine(squashCoroutine);
                squashCoroutine = StartCoroutine(LandingSquash());
            }

            wasGrounded = isGrounded;
        }

        // Classic squash-and-stretch landing feedback: quickly flatten the sprite wide on impact,
        // then spring it back to normal. This is a juice/feel technique common in platformers.
        private System.Collections.IEnumerator LandingSquash()
        {
            Transform squashTarget = spriteTransform != null ? spriteTransform : transform;

            // Widen and shorten the scale to simulate impact compression
            Vector3 squashed = new Vector3(defaultScale.x * (1f + squashAmount), defaultScale.y * (1f - squashAmount), defaultScale.z);

            float elapsed = 0f;
            while (elapsed < squashDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(defaultScale, squashed, elapsed / squashDuration);
                yield return null;
            }

            // Spring back to default scale
            elapsed = 0f;
            while (elapsed < squashRecoverDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(squashed, defaultScale, elapsed / squashRecoverDuration);
                yield return null;
            }

            squashTarget.localScale = defaultScale;
            squashCoroutine = null;
        }

        private void HandleFallStretch()
        {
            // Check if player is in the air and moving downward
            bool isFalling = !isGrounded && rb.linearVelocity.y < -0.1f;

            if (isFalling)
            {
                // 1. PLAYER MOVEMENT IS UNTOUCHED: 
                // We do not modify rb.linearVelocity.x here anymore, allowing full air control.

                // 2. APPLY STRETCH: Only if a high-priority jump/land coroutine isn't running
                if (squashCoroutine == null)
                {
                    Transform squashTarget = spriteTransform != null ? spriteTransform : transform;

                    // Calculate fall intensity based on velocity (0 = just started falling, 1 = max speed)
                    float fallPercentage = Mathf.Clamp01(Mathf.Abs(rb.linearVelocity.y) / maxFallSpeed);
                    float currentStretch = fallPercentage * maxFallStretch;

                    // Narrow the width (X), stretch the height (Y)
                    Vector3 targetFallScale = new Vector3(
                        defaultScale.x * (1f - currentStretch),
                        defaultScale.y * (1f + currentStretch),
                        defaultScale.z
                    );

                    // Smoothly ease into the stretched shape
                    squashTarget.localScale = Vector3.MoveTowards(squashTarget.localScale, targetFallScale, Time.deltaTime * fallStretchLerpSpeed);
                }
            }
            else
            {
                // If grounded or moving upward, and no coroutine is running, smoothly snap back to normal
                if (squashCoroutine == null)
                {
                    Transform squashTarget = spriteTransform != null ? spriteTransform : transform;
                    squashTarget.localScale = Vector3.MoveTowards(squashTarget.localScale, defaultScale, Time.deltaTime * fallStretchLerpSpeed);
                }
            }
        }
        private System.Collections.IEnumerator JumpingSquash()
        {
            Transform squashTarget = spriteTransform != null ? spriteTransform : transform;

            Vector3 anticipationSquash = new Vector3(
                defaultScale.x * (1f + squashAmount),
                defaultScale.y * (1f - squashAmount),
                defaultScale.z
            );

            Vector3 jumpStretch = new Vector3(
                defaultScale.x * (1f - squashAmount),
                defaultScale.y * (1f + squashAmount),
                defaultScale.z
            );

            // PHASE 1: Quick dip (Happens just as the player leaves the ground)
            float elapsed = 0f;
            float anticipationDuration = squashDuration * 0.3f;
            while (elapsed < anticipationDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(defaultScale, anticipationSquash, elapsed / anticipationDuration);
                yield return null;
            }

            // PHASE 2: Stretch upward mid-air
            elapsed = 0f;
            float launchDuration = squashDuration * 0.7f;
            while (elapsed < launchDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(anticipationSquash, jumpStretch, elapsed / launchDuration);
                yield return null;
            }

            // PHASE 3: Recover to normal scale
            elapsed = 0f;
            while (elapsed < squashRecoverDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(jumpStretch, defaultScale, elapsed / squashRecoverDuration);
                yield return null;
            }

            squashTarget.localScale = defaultScale;
            squashCoroutine = null;
        }

        private enum SurfaceType { Stone, Wood, Metal, Mud, Bed }

        private SurfaceType GetSurfaceBelow()
        {
            if (groundCheck == null) return SurfaceType.Stone;
            RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, slopeCheckDistance, groundLayer);
            if (!hit) return SurfaceType.Stone;
            return hit.collider.tag switch
            {
                "Wood"   => SurfaceType.Wood,
                "Stairs" => SurfaceType.Wood,
                "Metal"  => SurfaceType.Metal,
                "Mud"    => SurfaceType.Mud,
                "Bed"    => SurfaceType.Bed,
                _        => SurfaceType.Stone
            };
        }

        private AudioClip[] GetLandClips(SurfaceType surface) => surface switch
        {
            SurfaceType.Wood  => landWoodSFX,
            SurfaceType.Metal => landMetalSFX,
            SurfaceType.Mud   => landMudSFX,
            SurfaceType.Bed   => landBedSFX,
            _                 => landStoneSFX
        };

        private AudioClip[] GetFootstepClips(SurfaceType surface) => surface switch
        {
            SurfaceType.Wood  => footWoodSFX,
            SurfaceType.Metal => footMetalSFX,
            SurfaceType.Mud   => footMudSFX,
            SurfaceType.Bed   => footBedSFX,
            _                 => footStoneSFX
        };

        private void PlayRandomSFX(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip != null) AudioManager.Instance?.PlaySFX(clip);
        }

        // Called by walk/run animation events
        public void PlayFootstepSFX()
        {
            if (!isGrounded || isDead || footstepSource == null) return;
            AudioClip[] clips = GetFootstepClips(GetSurfaceBelow());
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null) return;
            footstepSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(clip);
        }

        private void OnCollisionEnter2D(Collision2D col)
        {
            if (col.gameObject.CompareTag("Stairs"))
            {
                foreach (ContactPoint2D contact in col.contacts)
                {
                    if (contact.normal.y > 0.1f) { isOnStairs = true; stairContactNormal = contact.normal; break; }
                }
            }
            if (!isSpinning) return;
            col.gameObject.GetComponent<Enemy>()?.TakeDamage(spinDamage);
        }

        private void OnCollisionStay2D(Collision2D col)
        {
            if (col.gameObject.CompareTag("Stairs"))
            {
                foreach (ContactPoint2D contact in col.contacts)
                {
                    if (contact.normal.y > 0.1f) { isOnStairs = true; stairContactNormal = contact.normal; return; }
                }
            }
            if (((1 << col.gameObject.layer) & oneWayPlatformLayer) != 0)
                currentOneWayPlatform = col.collider;
        }

        private void OnCollisionExit2D(Collision2D col)
        {
            if (col.gameObject.CompareTag("Stairs")) { isOnStairs = false; stairContactNormal = Vector2.up; }
            if (col.collider == currentOneWayPlatform) currentOneWayPlatform = null;
        }

        private void OnTriggerEnter2D(Collider2D col)
        {
            // Handle combat damage
            if (isSpinning)
            {
                col.gameObject.GetComponent<Enemy>()?.TakeDamage(spinDamage);
            }

            // Handle touching a door
            Door door = col.gameObject.GetComponent<Door>();
            if (door != null)
            {
                currentDoor = door;
            }
        }

        private void OnTriggerExit2D(Collider2D col) 
        {
            Door door = col.gameObject.GetComponent<Door>();
            if (door != null && currentDoor == door)
            {
                currentDoor = null; // No longer touching this door
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }
}
