using UnityEngine;

namespace AdequateEnough
{
    public class PlayerController : MonoBehaviour
    {
        public enum PlayerState { Grounded, Airborne, Spinning }
        public PlayerState CurrentState { get; private set; }

        [Header("References")]
        [SerializeField] private InputManager input;

        [Header("Movement")]
        [SerializeField] private float maxSpeed = 8f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 12f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 12f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        [Header("Spin")]
        [SerializeField] private float spinForce = 20f;
        [SerializeField] private float spinMaxSpeed = 18f;
        [SerializeField] private float spinDecayRate = 4f;
        [SerializeField] private float spinCooldown = 1f;
        [SerializeField] private float upwardSpinGravityScale = 0.5f;

        private Rigidbody2D rb;
        private float defaultGravityScale;
        private float currentMaxSpeed;
        private float nextSpinTime;
        private float lastFacingDirection = 1f;

        private bool isGrounded;
        private bool isSpinning;

        // Buffered one-shot inputs read in Update, consumed in FixedUpdate
        private bool jumpQueued;
        private bool spinQueued;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            defaultGravityScale = rb.gravityScale;
            currentMaxSpeed = maxSpeed;
        }

        private void Update()
        {
            CheckGround();
            UpdateState();

            if (input.GetJumpPressed()) jumpQueued = true;
            if (input.GetSpinPressed()) spinQueued = true;
        }

        private void FixedUpdate()
        {
            HandleSpinDecay();
            Move();
            TryJump();
            TrySpin();
        }

        private void CheckGround()
        {
            if (groundCheck == null) { isGrounded = false; return; }
            isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
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
            Vector2 moveInput = input.GetMove();

            if (Mathf.Abs(moveInput.x) > 0.01f)
                lastFacingDirection = Mathf.Sign(moveInput.x);

            // Air control: only apply drag, no acceleration
            if (CurrentState == PlayerState.Airborne)
            {
                if (Mathf.Abs(moveInput.x) < 0.01f)
                    rb.AddForce(-rb.linearVelocity.x * deceleration * Vector2.right, ForceMode2D.Force);
                return;
            }

            float desiredSpeed = moveInput.x * currentMaxSpeed;
            float rate;

            if (Mathf.Abs(moveInput.x) > 0.01f)
            {
                bool changingDir = Mathf.Sign(moveInput.x) != Mathf.Sign(rb.linearVelocity.x)
                                   && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
                rate = changingDir ? deceleration * 1.5f : acceleration;
            }
            else
            {
                rate = deceleration;
            }

            rb.AddForce((desiredSpeed - rb.linearVelocity.x) * rate * Vector2.right, ForceMode2D.Force);
        }

        private void TryJump()
        {
            if (!jumpQueued) return;
            jumpQueued = false;

            if (CurrentState != PlayerState.Grounded) return;

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

            Vector2 moveInput = input.GetMove();
            Vector2 spinDirection = moveInput.sqrMagnitude > 0.001f
                ? Get8WayDirection(moveInput)
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
