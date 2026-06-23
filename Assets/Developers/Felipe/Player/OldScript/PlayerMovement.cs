using UnityEngine;
using UnityEngine.InputSystem;

namespace AdequateEnough
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Horizontal Movement")]
        [SerializeField] private float maxSpeed = 8f;
        [Range(0f, 100f)]
        [SerializeField] private float acceleration = 10f;
        [Range(0f, 100f)]
        [SerializeField] private float deceleration = 12f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        [Header("Impulse / Dash")]
        [SerializeField] private float impulseForce = 20f;       // Força do estouro inicial
        [SerializeField] private float impulseMaxSpeed = 18f;    // Teto de velocidade durante o impulso
        [SerializeField] private float impulseDecayRate = 4f;    // Quão lento você volta ao normal
        [SerializeField] private float impulseCooldown = 1f;

        private Rigidbody2D rb;
        private Vector2 inputDirection;

        private float lastFacingDirection = 1f;
        private float nextImpulseTime;
        private bool isGrounded;

        private float currentMaxSpeed;
        private bool isImpulsing;
        private Vector2 impulseDirection; // Guarda a direção do impulso atual

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            currentMaxSpeed = maxSpeed;
        }

        void Update()
        {
            CheckGround();
            HandleImpulseDecay();
        }

        void FixedUpdate()
        {
            HandleRealisticMovement();
        }

        public void OnMove(InputValue moveValue)
        {
            inputDirection = moveValue.Get<Vector2>();

            if (Mathf.Abs(inputDirection.x) > 0.01f)
            {
                lastFacingDirection = Mathf.Sign(inputDirection.x);
            }
        }

        public void OnJump()
        {
            if (isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            }
        }

        // MODIFICADO: Agora calcula 8 direções baseadas no seu input atual
        public void OnTriggerImpulse()
        {
            if (Time.time < nextImpulseTime) return;

            nextImpulseTime = Time.time + impulseCooldown;
            isImpulsing = true;

            // 1. Detectar direção (Se não houver input, impulsiona para o lado que está olhando)
            if (inputDirection.sqrMagnitude > 0.001f)
            {
                // O Get8WayDirection normaliza o vetor para uma das 8 diagonais/eixos puros
                impulseDirection = Get8WayDirection(inputDirection);
            }
            else
            {
                impulseDirection = new Vector2(lastFacingDirection, 0f);
            }

            // 2. Se o impulso for para cima, desliga a gravidade temporariamente para o impulso não ser "puxado" para baixo imediatamente
            if (impulseDirection.y > 0.1f)
            {
                rb.gravityScale = 0.5f; // Reduz a gravidade temporariamente (opcional, melhora o feeling)
            }

            // 3. Aplica a velocidade instantânea na direção escolhida
            rb.linearVelocity = impulseDirection * impulseForce;

            currentMaxSpeed = impulseMaxSpeed;
        }

        private void HandleImpulseDecay()
        {
            if (!isImpulsing) return;

            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, maxSpeed, impulseDecayRate * Time.deltaTime);

            if (Mathf.Approximately(currentMaxSpeed, maxSpeed))
            {
                currentMaxSpeed = maxSpeed;
                isImpulsing = false;
                rb.gravityScale = 3f; // Retorna a gravidade padrão do seu projeto (ajuste se a sua for diferente!)
            }
        }

        private void HandleRealisticMovement()
        {
            // NOVO: Se o jogador estiver no ar (e não estiver sob efeito de impulso), bloqueia o controle de aceleração horizontal
            if (!isGrounded && !isImpulsing)
            {
                // O jogador não consegue acelerar no ar, mas mantemos o arrasto (deceleration) caso ele solte os botões
                if (Mathf.Abs(inputDirection.x) < 0.01f)
                {
                    float speedDifDecel = 0f - rb.linearVelocity.x;
                    rb.AddForce(speedDifDecel * deceleration * Vector2.right, ForceMode2D.Force);
                }
                return;
            }

            // Movimento normal no chão ou comportamento durante o impulso
            float targetSpeed = inputDirection.x * currentMaxSpeed;
            float rate;

            if (Mathf.Abs(inputDirection.x) > 0.01f)
            {
                if (Mathf.Sign(inputDirection.x) != Mathf.Sign(rb.linearVelocity.x) && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
                {
                    rate = deceleration * 1.5f;
                }
                else
                {
                    rate = acceleration;
                }
            }
            else
            {
                rate = deceleration;
            }

            float speedDif = targetSpeed - rb.linearVelocity.x;
            float movement = speedDif * rate;

            rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
        }

        // Função auxiliar para travar o vetor de input estritamente em 8 direções limpas
        private Vector2 Get8WayDirection(Vector2 input)
        {
            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;

            // Arredonda o ângulo para o múltiplo de 45 graus mais próximo
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;

            // Converte de volta para um vetor direcional limpo
            float rad = snappedAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }

        private void CheckGround()
        {
            if (groundCheck == null) return;
            isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
            }
        }
    } 
}
