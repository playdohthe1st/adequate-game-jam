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
        [SerializeField] private float inputSmoothing = 12f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 15f;
        [SerializeField] private float jumpCutMultiplier = 0.4f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Gravity")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [SerializeField] private float apexThreshold = 2f;
        [SerializeField] private float apexGravityMultiplier = 0.5f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        [Header("Slopes")]
        [SerializeField] private float slopeCheckDistance = 0.5f;
        [SerializeField] private float maxSlopeAngle = 45f;
        [SerializeField] private float maxSlopeSpeed = 6f;
        [SerializeField] private PhysicsMaterial2D noFriction;
        [SerializeField] private PhysicsMaterial2D fullFriction;

        [Header("Spin")]
        [SerializeField] private float spinForce = 20f;
        [SerializeField] private float spinMaxSpeed = 18f;
        [SerializeField] private float spinDecayRate = 4f;
        [SerializeField] private float spinCooldown = 1f;
        [SerializeField] private float upwardSpinGravityScale = 0.5f;

        private Rigidbody2D rb;
        private CapsuleCollider2D col;
        private InputManager input;

        private float defaultGravityScale;
        private float currentMaxSpeed;
        private float nextSpinTime;
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

        private bool CanJump => coyoteTimeCounter > 0f;
        private bool IsAtApex => !isGrounded && Mathf.Abs(rb.linearVelocity.y) < apexThreshold;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CapsuleCollider2D>();
            input = GetComponent<InputManager>();
            defaultGravityScale = rb.gravityScale;
            currentMaxSpeed = maxSpeed;
        }

        private void Update()
        {
            CheckGround();
            CheckSlope();
            UpdateState();
            UpdateTimers();
            SmoothInput();
            HandleVariableJump();
            HandleGravity();

            if (input.GetJumpPressed()) jumpBufferCounter = jumpBufferTime;
            if (input.GetSpinPressed()) spinQueued = true;
        }

        private void FixedUpdate()
        {
            HandleSpinDecay();
            Move();
            TryJump();
            TrySpin();
        }

        private void SmoothInput()
        {
            smoothedInput = Vector2.Lerp(smoothedInput, input.GetMove(), inputSmoothing * Time.deltaTime);
        }

        private void UpdateTimers()
        {
            coyoteTimeCounter = isGrounded ? coyoteTime : coyoteTimeCounter - Time.deltaTime;
            jumpBufferCounter -= Time.deltaTime;
        }

        private void HandleVariableJump()
        {
            bool jumpHeld = input.GetJumpHeld();

            // Only cut once on the frame jump is released while still rising
            if (wasJumpHeld && !jumpHeld && rb.linearVelocity.y > 0f && !isGrounded && !isSpinning)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

            wasJumpHeld = jumpHeld;
        }

        private void HandleGravity()
        {
            if (isSpinning) return;

            float targetGravity;

            if (IsAtApex)
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

            // Ignore hits that are far below — the player is standing on a surface above the slope
            if (hit && hit.distance <= groundCheckSize.y + 0.05f)
            {
                slopeNormal = hit.normal;
                float angle = Vector2.Angle(slopeNormal, Vector2.up);
                isOnSlope = angle > 1f && angle <= maxSlopeAngle;

                if (col != null)
                {
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
                if (Mathf.Abs(smoothedInput.x) < 0.01f)
                    rb.AddForce(-rb.linearVelocity.x * deceleration * Vector2.right, ForceMode2D.Force);
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

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        private void TrySpin()
        {
            if (!spinQueued) return;
            spinQueued = false;

            if (Time.time < nextSpinTime) return;

            nextSpinTime = Time.time + spinCooldown;
            isSpinning = true;

            Vector2 rawInput = input.GetMove();
            Vector2 spinDirection = rawInput.sqrMagnitude > 0.001f
                ? Get8WayDirection(rawInput)
                : new Vector2(lastFacingDirection, 0f);

            if (spinDirection.y > 0.1f)
                rb.gravityScale = upwardSpinGravityScale;

            rb.linearVelocity = spinDirection * spinForce;
            currentMaxSpeed = spinMaxSpeed;
        }

        private void HandleSpinDecay()
        {
            if (!isSpinning) return;

            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, maxSpeed, spinDecayRate * Time.fixedDeltaTime);

            if (Mathf.Approximately(currentMaxSpeed, maxSpeed))
            {
                currentMaxSpeed = maxSpeed;
                isSpinning = false;
                rb.gravityScale = defaultGravityScale;
            }
        }

        private Vector2 Get8WayDirection(Vector2 input)
        {
            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped)).normalized;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }
}
