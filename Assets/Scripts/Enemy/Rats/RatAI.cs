using UnityEngine;
using System.Collections;

public class RatAI : MonoBehaviour
{
    [Header("Movement & AI")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float detectionRadius = 4f;
    public float minWanderTime = 1f;
    public float maxWanderTime = 3f;

    [Header("Effects on Explode")]
    public GameObject explosionParticles;
    public AudioClip explosionSound;

    private Transform playerTransform;
    private Rigidbody2D rb2d; // Added physics component reference
    private float wanderDirection = 1f; // 1 = right, -1 = left
    private bool isRunningAway = false;
    private bool isDead = false;

    void Start()
    {
        // Get the Rigidbody2D component attached to this rat
        rb2d = GetComponent<Rigidbody2D>();

        // Find the player automatically using the Player tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        // Start the random patrol routine
        StartCoroutine(PatrolRoutine());
    }

    void Update()
    {
        if (isDead) return;

        CheckForPlayer();
    }

    // FixedUpdate handles all physics movements safely
    void FixedUpdate()
    {
        if (isDead)
        {
            rb2d.linearVelocity = Vector2.zero;
            return;
        }

        if (isRunningAway && playerTransform != null)
        {
            RunFromPlayer();
        }
        else
        {
            PatrolMovement();
        }
    }

    void CheckForPlayer()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // If player is close, switch to running away
        if (distanceToPlayer <= detectionRadius)
        {
            isRunningAway = true;
        }
        else
        {
            isRunningAway = false;
        }
    }

    void PatrolMovement()
    {
        // Set physics velocity instead of moving via transform
        rb2d.linearVelocity = new Vector2(wanderDirection * walkSpeed, rb2d.linearVelocity.y);

        // Flip sprite visual direction to match movement
        if (wanderDirection != 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * wanderDirection, transform.localScale.y, transform.localScale.z);
        }
    }

    void RunFromPlayer()
    {
        // Calculate direction away from player (1D left/right vector)
        float directionAway = transform.position.x > playerTransform.position.x ? 1f : -1f;

        // Set physics velocity at running speed
        rb2d.linearVelocity = new Vector2(directionAway * runSpeed, rb2d.linearVelocity.y);

        // Flip sprite to look where it's running
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * directionAway, transform.localScale.y, transform.localScale.z);
    }

    IEnumerator PatrolRoutine()
    {
        while (!isDead)
        {
            if (!isRunningAway)
            {
                // Choose a random direction: -1 (Left), 0 (Idle/Pause), or 1 (Right)
                int rand = Random.Range(-1, 2);
                wanderDirection = (float)rand;

                // Wait for a random amount of time before changing directions again
                float waitTime = Random.Range(minWanderTime, maxWanderTime);
                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                // If running away, pause the routine loop momentarily
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    // Support for 2D colliders if your game is strict 2D physics
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isDead)
        {
            Explode();
        }
    }

    void Explode()
    {
        isDead = true;

        // Stop movement immediately on explosion
        rb2d.linearVelocity = Vector2.zero;

        // 1. Spawn your droplet explosion particle system
        if (explosionParticles != null)
        {
            Instantiate(explosionParticles, transform.position, Quaternion.identity);
        }

        // 2. Play explosion audio clip at the rat's current position
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }

        // 3. Destroy the rat object
        Destroy(gameObject);
    }

    // Visualise the detection radius ring in the Scene View
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
