// MeleeEnemy.cs
using UnityEngine;
using System.Collections; // For coroutines like attack cooldown

public class ganja : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 5;
    public int currentHealth;
    public float moveSpeed = 2f;
    public int attackDamage = 1;
    public float attackRange = 1.5f;      // How close to attack
    public float attackCooldown = 2f;   // Time between attacks
    public float detectionRange = 8f;   // How far to detect player
    public float loseSightRange = 12f;  // How far until player is "lost" if previously detected

    [Header("References")]
    public Transform playerTransform;     // Assign the Player's transform
    public LayerMask playerLayer;         // Set this to the layer your Player is on
    public Transform attackPoint;         // Empty child GameObject where attack originates (for hitbox)
    public float attackHitboxSize = 0.5f; // Radius/size of the attack hitbox

    [Header("SFX")]
    public AudioClip attackSfx;
    public AudioClip takeHitSfx;
    public AudioClip deathSfx;
    // public AudioClip[] footstepSfx; // Optional for running sound

    private Animator _animator;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private AudioSource _audioSource;

    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private float _lastAttackTime = -Mathf.Infinity; // Initialize to allow first attack immediately
    private bool _isTakingHit = false;
    private bool _isDead = false;

    // Animator Hashes (Create corresponding parameters in your Animator Controller)
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int TakeHitTriggerHash = Animator.StringToHash("TakeHit");
    private static readonly int DeathTriggerHash = Animator.StringToHash("Death");

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        currentHealth = maxHealth;
    }

    void Start()
    {
        // Try to find player if not assigned (useful if enemy is spawned)
        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
            else
            {
                Debug.LogError($"MeleeEnemy ({gameObject.name}): Player not found! Assign Player Transform or ensure Player is tagged 'Player'.");
                enabled = false; // Disable script if no player
                return;
            }
        }
    }

    void Update()
    {
        if (_isDead || _isTakingHit || playerTransform == null)
        {
            _animator.SetBool(IsRunningHash, false); // Stop running animation if dead, hit, or no player
            if (_rb != null && !_isDead) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop horizontal movement unless dead
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // Player Detection Logic
        if (!_isPlayerDetected && distanceToPlayer <= detectionRange)
        {
            // Check if there's a clear line of sight (optional, but good)
            RaycastHit2D hit = Physics2D.Raycast(transform.position, (playerTransform.position - transform.position).normalized, detectionRange, ~LayerMask.GetMask("Enemy", "Ignore Raycast")); // Ignore self and other enemies
            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                _isPlayerDetected = true;
                Debug.Log($"{gameObject.name} detected Player!");
            }
        }
        else if (_isPlayerDetected && distanceToPlayer > loseSightRange)
        {
            _isPlayerDetected = false;
            Debug.Log($"{gameObject.name} lost sight of Player.");
            _animator.SetBool(IsRunningHash, false);
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop when losing sight
            return; // Exit update early if player is lost
        }


        if (_isPlayerDetected)
        {
            FacePlayer();

            // Attack Logic
            if (distanceToPlayer <= attackRange && Time.time >= _lastAttackTime + attackCooldown && !_isAttacking)
            {
                StartCoroutine(Attack());
            }
            // Movement Logic (Chase)
            else if (distanceToPlayer > attackRange && !_isAttacking) // Only move if not in attack range and not currently attacking
            {
                MoveTowardsPlayer();
                _animator.SetBool(IsRunningHash, true);
            }
            else if (_isAttacking) // If attacking, don't move or run animation
            {
                _animator.SetBool(IsRunningHash, false);
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop horizontal movement while attacking
            }
            else // In attack range but on cooldown
            {
                _animator.SetBool(IsRunningHash, false);
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop if in range but on cooldown
            }
        }
        else // Not detected
        {
            _animator.SetBool(IsRunningHash, false);
            // Optional: Implement patrol behavior here
            // For now, just stop:
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        }
    }

    void FacePlayer()
    {
        if (playerTransform.position.x < transform.position.x)
        {
            // Player is to the left, flip enemy to face left
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else
        {
            // Player is to the right, face enemy right
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void MoveTowardsPlayer()
    {
        if (playerTransform == null) return;

        Vector2 direction = (playerTransform.position - transform.position).normalized;
        _rb.linearVelocity = new Vector2(direction.x * moveSpeed, _rb.linearVelocity.y); // Keep existing Y velocity for gravity
    }

    IEnumerator Attack()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time; // Set cooldown timer start
        _animator.SetTrigger(AttackTriggerHash); // Trigger attack animation
        PlaySound(attackSfx);

        // Wait for a specific part of the animation to apply damage
        // This duration should match your attack animation's "hit frame"
        // For example, if the hit frame is 0.3 seconds into the animation:
        yield return new WaitForSeconds(0.3f); // Adjust this delay!

        // Perform damage check only if still attacking (not interrupted by hit, etc.) and not dead
        if (_isAttacking && !_isDead && !_isTakingHit)
        {
            DealDamageToPlayer();
        }

        // Wait for the rest of the animation or a set duration
        // This duration should be a bit less than the total attack animation length to allow blending
        yield return new WaitForSeconds(0.4f); // Adjust this delay to match attack animation wind-down!

        _isAttacking = false;
    }

    void DealDamageToPlayer()
    {
        // Use OverlapCircle or BoxCast from attackPoint to find player
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxSize, playerLayer);

        foreach (Collider2D playerCollider in hitPlayers)
        {
            Player player = playerCollider.GetComponent<Player>();
            if (player != null && !player._IsDeath) // Check if player is not already dead
            {
                Debug.Log($"{gameObject.name} hit Player for {attackDamage} damage.");
                player.TakePlayerDamage(attackDamage);
                // Add knockback or other effects here if desired
                break; // Usually only hit one player per attack
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (_isDead || _isTakingHit) return; // Don't take damage if already dead or in hit stun

        currentHealth -= damage;
        PlaySound(takeHitSfx);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Play take hit animation (hit stun)
            StartCoroutine(TakeHitStun());
        }
    }

    IEnumerator TakeHitStun()
    {
        _isTakingHit = true;
        _isAttacking = false; // Cancel current attack if any
        StopCoroutine(Attack()); // Stop attack coroutine if it was running
        _animator.SetTrigger(TakeHitTriggerHash);
        _rb.linearVelocity = Vector2.zero; // Briefly stop movement

        // Duration of hit stun (should roughly match your "TakeHit" animation length)
        yield return new WaitForSeconds(0.5f); // Adjust this!

        _isTakingHit = false;
    }

    void Die()
    {
        _isDead = true;
        _animator.SetTrigger(DeathTriggerHash);
        PlaySound(deathSfx);
        _rb.linearVelocity = Vector2.zero;
        _rb.isKinematic = true; // Stop physics interactions
        GetComponent<Collider2D>().enabled = false; // Disable collider

        // Destroy GameObject after death animation (adjust delay)
        Destroy(gameObject, 2f); // Destroy after 2 seconds
    }

    // Helper for SFX
    private void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip, volume);
        }
    }

    // Optional: For footstep sounds during run animation (called by Animation Event)
    public void PlayFootstepSound()
    {
        // if (footstepSfx != null && footstepSfx.Length > 0)
        // {
        //     PlayRandomSound(footstepSfx, 0.5f);
        // }
    }

    // Gizmos for visualizing ranges in Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseSightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackHitboxSize);
        }
    }
}