using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 
/// </summary>
namespace AdequateEnough
{
    public class PlayerController : MonoBehaviour
    {
        #region States Enum
        public enum PlayerState
        {
            Grounded,
            Airborne,
            Spinning
        }
        #endregion
        public PlayerState CurrentState { get; private set; }

        [Header("References")]
        [SerializeField] private InputManager input;
        [SerializeField] private Rigidbody2D rb;

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

        
        private float currentMaxSpeed;
        private float nextSpinTime;
        private float lastFacingDirection = 1f;

        

        private bool isGrounded;
        private bool isSpinning;


      

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            currentMaxSpeed = maxSpeed;
        }

        private void Update()
        {
            CheckGround();
        }

        private void FixedUpdate()
        {
            Move();
            TryJump();
            TrySpin();
            
        }

        

        private void CheckGround()
        {
            if (groundCheck == null)
            {
                isGrounded = false;
                return;
            }

            isGrounded = Physics2D.OverlapBox(
                groundCheck.position,
                groundCheckSize,
                0f,
                groundLayer);
        }


        private void Move()
        {
            Vector2 moveInput = input.GetMove();

            if (Mathf.Abs(moveInput.x) > 0.01f)
                lastFacingDirection = Mathf.Sign(moveInput.x);

            if (CurrentState == PlayerState.Airborne)
            {
                if (Mathf.Abs(moveInput.x) < 0.01f)
                {
                    float velocityDifference = -rb.linearVelocity.x;

                    rb.AddForce(
                        velocityDifference * deceleration * Vector2.right,
                        ForceMode2D.Force);
                }

                return;
            }

            float desiredSpeed = moveInput.x * currentMaxSpeed;

            float movementRate;

            if (Mathf.Abs(moveInput.x) > 0.01f)
            {
                bool isChangingDirection =
                    Mathf.Sign(moveInput.x) != Mathf.Sign(rb.linearVelocity.x) &&
                    Mathf.Abs(rb.linearVelocity.x) > 0.1f;

                movementRate = isChangingDirection
                    ? deceleration * 1.5f
                    : acceleration;
            }
            else
            {
                movementRate = deceleration;
            }

            float speedDifference = desiredSpeed - rb.linearVelocity.x;
            float movementForce = speedDifference * movementRate;

            rb.AddForce(movementForce * Vector2.right, ForceMode2D.Force);
        }

        




        #region Actions

        private void TryJump()
        {
            if (!input.GetJumpPressed())
                return;

            if (CurrentState != PlayerState.Grounded)
                return;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        private void TrySpin()
        {
            if (!input.GetSpinPressed())
                return;

            if (Time.time < nextSpinTime)
                return;

            nextSpinTime = Time.time + spinCooldown;
            isSpinning = true;

            Vector2 moveInput = input.GetMove();

            Vector2 spinDirection;

            if (moveInput.sqrMagnitude > 0.001f)
                spinDirection = Get8WayDirection(moveInput);
            else
                spinDirection = new Vector2(lastFacingDirection, 0f);

            if (spinDirection.y > 0.1f)
                rb.gravityScale = upwardSpinGravityScale;

            rb.linearVelocity = spinDirection * spinForce;

            currentMaxSpeed = spinMaxSpeed;
        }

        #endregion

        

        

        private Vector2 Get8WayDirection(Vector2 input)
        {
            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            float radians = snappedAngle * Mathf.Deg2Rad;

            return new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians)
            ).normalized;
        }


        
    }
}
