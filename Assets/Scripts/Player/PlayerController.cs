using System;
using System.Collections;
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
        [SerializeField] private float inputSmoothing = 12f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 15f;
        [SerializeField] private float jumpCutMultiplier = 0.4f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Air Control")]
        [SerializeField][Range(0f, 1f)] private float airControlMultiplier = 0.3f;
        [SerializeField][Range(0f, 1f)] private float spinAirControlMultiplier = 0.05f;

        [Header("Gravity")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [SerializeField] private float apexThreshold = 2f;
        [SerializeField] private float apexGravityMultiplier = 0.5f;
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
        [SerializeField] private float maxSlopeSpeed = 6f;
        [SerializeField] private PhysicsMaterial2D noFriction;
        [SerializeField] private PhysicsMaterial2D fullFriction;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float regenRate = 5f;
        [SerializeField] private float regenDelay = 10f;

        [Header("Invincibility Frames (I-Frames)")]
        [Tooltip("How long the player is invincible after taking damage")]
        [SerializeField] private float invincibilityDuration = 0.8f;
        private float invincibilityTimer;

        [Header("Hurt and Death Visuals")]
        [SerializeField] private string hurtAnimTrigger = "Hurt";

        [Header("Spin")]
        [SerializeField] private float spinForce = 20f;
        [SerializeField] private float spinMaxSpeed = 18f;
        [SerializeField] private float spinDecayRate = 4f;
        [SerializeField] private float spinCooldown = 1.5f;
        [SerializeField] private float upwardSpinGravityScale = 0.5f;
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
        [SerializeField] private AudioClip doorSuccessSFX;
        [SerializeField] private AudioClip doorLockedSFX;
        [SerializeField] private AudioClip doorEnterSFX;
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
        [SerializeField] private GameObject landingSmokePrefab;
        [SerializeField] private ScreenFader screenFader;
        [SerializeField] private GameObject deathScreen;
        [SerializeField] private GameObject creditScreen;
        [SerializeField] private Image healthImage;
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

        public static event System.Action OnRespawnedAtOrigin;

        private bool CanJump => coyoteTimeCounter > 0f;
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

            if (input.GetJumpPressed()) jumpBufferCounter = jumpBufferTime;
            if (input.GetSpinPressed()) spinQueued = true;
            TryDropThrough();
            if (input.GetInteract()) GoToNextRoom();
            if (screenFader == null)
            {
                screenFader = FindAnyObjectByType<ScreenFader>();
            }
        }

        private void FixedUpdate()
        {
            if (isDead) return;
            HandleSpinDecay();
            Move();
            TryJump();
            TrySpin();
        }

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

            if (currentKeycard >= currentDoor.requiredLevel)
            {
                if (doorSuccessSFX != null) AudioManager.Instance?.PlaySFX(doorSuccessSFX);
                if (doorEnterSFX != null) AudioManager.Instance?.PlaySFX(doorEnterSFX);
                ScreenFader.Instance.StartCoroutine(ScreenFader.Instance.FadeOutToRoom(gameObject, currentDoor.destination));
            }
            else
            {
                if (doorLockedSFX != null) AudioManager.Instance?.PlaySFX(doorLockedSFX);
                Debug.Log("Locked! You need a " + currentDoor.requiredLevel + " card.");
            }
        }

        private void SmoothInput()
        {
            smoothedInput = Vector2.Lerp(smoothedInput, input.GetMove(), inputSmoothing * Time.deltaTime);
        }

        private void VisualUpdater()
        {
            playerAnimator.SetBool("IsSpinning", isSpinning);
            playerAnimator.SetBool("IsGrounded", isGrounded);
            if (input.GetMove().magnitude > 0)
            {
                playerAnimator.SetBool("IsMoving", true);
            }
            else
            {
                playerAnimator.SetBool("IsMoving", false);
            }

            if (input.GetMove().x < 0)
            {
                playerSpriterender.flipX = false;
            }
            if (input.GetMove().x > 0)
            {
                playerSpriterender.flipX = true;
            }

            // Flicker opacity while invincible to give the player visual feedback
            if (invincibilityTimer > 0f)
            {
                float alpha = Mathf.PingPong(Time.time * 15f, 0.7f) + 0.3f;
                if (playerSpriterender != null)
                {
                    Color c = playerSpriterender.color;
                    playerSpriterender.color = new Color(c.r, c.g, c.b, alpha);
                }
            }
            else
            {
                // Ensure opacity is reset to 100% when invincibility ends
                if (playerSpriterender != null && playerSpriterender.color.a != 1f)
                {
                    Color c = playerSpriterender.color;
                    playerSpriterender.color = new Color(c.r, c.g, c.b, 1f);
                }
            }

            healthImage.fillAmount = currentHealth / maxHealth;
        }

        private void UpdateTimers()
        {
            coyoteTimeCounter = isGrounded ? coyoteTime : coyoteTimeCounter - Time.deltaTime;
            jumpBufferCounter -= Time.deltaTime;

            // Tick down the invincibility frame timer
            if (invincibilityTimer > 0f)
            {
                invincibilityTimer -= Time.deltaTime;
            }
        }

        private void HandleVariableJump()
        {
            bool jumpHeld = input.GetJumpHeld();

            if (wasJumpHeld && !jumpHeld && rb.linearVelocity.y > 0f && !isGrounded && !isSpinning)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

            wasJumpHeld = jumpHeld;
        }

        private void HandleGravity()
        {
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

            Vector2 rawInput = input.GetMove();
            Vector2 spinDirection = rawInput.sqrMagnitude > 0.001f
                ? Get8WayDirection(rawInput)
                : new Vector2(lastFacingDirection, 0f);

            if (spinDirection.y > 0.1f)
                rb.gravityScale = upwardSpinGravityScale;

            rb.linearVelocity = spinDirection * spinForce;
            spinInitialYVelocity = rb.linearVelocity.y;
            currentMaxSpeed = spinMaxSpeed * speedMultiplier;
        }

        private void HandleSpinDecay()
        {
            if (!isSpinning) return;

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

                yield return new WaitForSeconds(0.15f);
                float safetyTimeout = Time.time + 2f;
                while (Physics2D.Distance(col, platform).distance < 0f && Time.time < safetyTimeout)
                    yield return null;

                Physics2D.IgnoreCollision(col, platform, false);
            }

            isDropping = false;
        }

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

            // IGNORE DAMAGE if the player is currently within their invincibility window
            if (invincibilityTimer > 0f) return;

            currentHealth = Mathf.Max(currentHealth - amount, 0f);
            timeSinceLastDamage = 0f;
            slopeDisableTimer = knockbackSlopeDisableDuration;

            // Trigger the invincibility frames
            invincibilityTimer = invincibilityDuration;

            // Trigger the player taking hit animation
            if (playerAnimator != null && !string.IsNullOrEmpty(hurtAnimTrigger))
                playerAnimator.SetTrigger(hurtAnimTrigger);

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

            yield return StartCoroutine(ScreenFader.Instance.Fade(0f, 1f, 1f));

            if (videoPlayer != null && videoPanel != null)
            {
                videoPanel.SetActive(true);
                videoPlayer.Prepare();
                while (!videoPlayer.isPrepared)
                    yield return null;

                videoPlayer.Play();
                yield return new WaitForEndOfFrame();
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

            // Reset invincibility timer so player isn't invincible on spawn
            invincibilityTimer = 0f;

            if (squashCoroutine != null) StopCoroutine(squashCoroutine);
            Transform squashTarget = spriteTransform != null ? spriteTransform : transform;
            squashTarget.localScale = defaultScale;
            skipLandingUntil = Time.time + 0.5f;
        }

        private void DetectLanding()
        {
            if (Time.time < skipLandingUntil)
            {
                wasGrounded = isGrounded;
                return;
            }

            if (!wasGrounded && isGrounded && !isSpinning && Time.time >= nextSquashTime)
            {
                nextSquashTime = Time.time + squashDuration + squashRecoverDuration + 0.1f;
                PlayRandomSFX(GetLandClips(GetSurfaceBelow()));

                if (landingSmokePrefab != null)
                {
                    Vector3 spawnPosition = groundCheck != null ? groundCheck.position : transform.position;
                    GameObject smoke = Instantiate(landingSmokePrefab, spawnPosition, Quaternion.identity);
                    Destroy(smoke, 2f);
                }

                if (squashCoroutine != null) StopCoroutine(squashCoroutine);
                squashCoroutine = StartCoroutine(LandingSquash());
            }

            wasGrounded = isGrounded;
        }

        private System.Collections.IEnumerator LandingSquash()
        {
            Transform squashTarget = spriteTransform != null ? spriteTransform : transform;
            Vector3 squashed = new Vector3(defaultScale.x * (1f + squashAmount), defaultScale.y * (1f - squashAmount), defaultScale.z);

            float elapsed = 0f;
            while (elapsed < squashDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(defaultScale, squashed, elapsed / squashDuration);
                yield return null;
            }

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
            bool isFalling = !isGrounded && rb.linearVelocity.y < -0.1f;

            if (isFalling)
            {
                if (squashCoroutine == null)
                {
                    Transform squashTarget = spriteTransform != null ? spriteTransform : transform;
                    float fallPercentage = Mathf.Clamp01(Mathf.Abs(rb.linearVelocity.y) / maxFallSpeed);
                    float currentStretch = fallPercentage * maxFallStretch;

                    Vector3 targetFallScale = new Vector3(
                        defaultScale.x * (1f - currentStretch),
                        defaultScale.y * (1f + currentStretch),
                        defaultScale.z
                    );

                    squashTarget.localScale = Vector3.MoveTowards(squashTarget.localScale, targetFallScale, Time.deltaTime * fallStretchLerpSpeed);
                }
            }
            else
            {
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

            float elapsed = 0f;
            float anticipationDuration = squashDuration * 0.3f;
            while (elapsed < anticipationDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(defaultScale, anticipationSquash, elapsed / anticipationDuration);
                yield return null;
            }

            elapsed = 0f;
            float launchDuration = squashDuration * 0.7f;
            while (elapsed < launchDuration)
            {
                elapsed += Time.deltaTime;
                squashTarget.localScale = Vector3.Lerp(anticipationSquash, jumpStretch, elapsed / launchDuration);
                yield return null;
            }

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
                "Wood" => SurfaceType.Wood,
                "Stairs" => SurfaceType.Wood,
                "Metal" => SurfaceType.Metal,
                "Mud" => SurfaceType.Mud,
                "Bed" => SurfaceType.Bed,
                _ => SurfaceType.Stone
            };
        }

        private AudioClip[] GetLandClips(SurfaceType surface) => surface switch
        {
            SurfaceType.Wood => landWoodSFX,
            SurfaceType.Metal => landMetalSFX,
            SurfaceType.Mud => landMudSFX,
            SurfaceType.Bed => landBedSFX,
            _ => landStoneSFX
        };

        private AudioClip[] GetFootstepClips(SurfaceType surface) => surface switch
        {
            SurfaceType.Wood => footWoodSFX,
            SurfaceType.Metal => footMetalSFX,
            SurfaceType.Mud => footMudSFX,
            SurfaceType.Bed => footBedSFX,
            _ => footStoneSFX
        };

        private void PlayRandomSFX(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip != null) AudioManager.Instance?.PlaySFX(clip);
        }

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
            if (isSpinning)
            {
                col.gameObject.GetComponent<Enemy>()?.TakeDamage(spinDamage);
            }

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
                currentDoor = null;
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