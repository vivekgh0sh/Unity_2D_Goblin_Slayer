// Minotaur.cs
using UnityEngine;
using System.Collections;

public class Minotaur : MonoBehaviour, IDamageable
{
    [Header("Core Stats")]
    public int maxHealth = 200;
    public int currentHealth;
    public float moveSpeed = 2.5f;
    public float detectionRange = 12f;
    public float loseSightRange = 17f;
    public float attackStoppingDistance = 2.5f; // How close to get before stopping to attack

    [Header("Attack (Heavy Swing)")]
    public int attackDamage = 25;
    public float attackRange = 3.0f;
    public float attackCooldown = 3.5f;
    public AudioClip attackSfx;

    [Header("References")]
    public Transform playerTransform;
    public LayerMask playerLayer;
    public Transform attackPoint;
    public float attackHitboxSize = 1.0f;

    [Header("UI References")]
    public UnityEngine.UI.Image bossHealthFillImage;
    public GameObject bossHealthBarObject;
    public TMPro.TextMeshProUGUI endGameTextTMP;

    [Header("General SFX")]
    public AudioClip takeHitSfx;
    public AudioClip deathSfx;
    public AudioClip[] walkSfx;

    [Header("Feedback & Polish")]
    public float hitStunDuration = 0.4f;
    public float deathCleanupDelay = 4f;

    // Components
    private Animator _animator;
    private Rigidbody2D _rb;
    private AudioSource _audioSource;

    // State
    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private bool _isTakingHit = false;
    private bool _isDead = false;
    private float _lastAttackTime = -Mathf.Infinity;

    // Animator Hashes for performance
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int TakeHitTriggerHash = Animator.StringToHash("TakeHit");
    private static readonly int DeathTriggerHash = Animator.StringToHash("Death");

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        currentHealth = maxHealth;
    }

    void Start()
    {
        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
            else
            {
                Debug.LogError($"Minotaur ({gameObject.name}): Player not found! Assign Player Transform or ensure Player is tagged 'Player'.");
                enabled = false; // Disable script if no player
                return;
            }
        }

        // Initialize cooldowns to allow immediate action
        _lastAttackTime = -attackCooldown;

        // Hide health bar initially
        if (bossHealthBarObject != null)
        {
            bossHealthBarObject.SetActive(false);
        }
    }

    void Update()
    {
        // Stop all logic if dead, taking a hit, or player is gone
        if (_isDead || _isTakingHit || playerTransform == null)
        {
            _animator.SetBool(IsWalkingHash, false);
            if (_rb != null && !_isDead) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        HandlePlayerDetection(distanceToPlayer);

        if (_isPlayerDetected)
        {
            FacePlayer();

            // If an action is in progress, wait for it to finish
            if (_isAttacking)
            {
                _animator.SetBool(IsWalkingHash, false);
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
                return;
            }

            // AI Decision Making
            DecideNextAction(distanceToPlayer);
        }
        else // Player not detected
        {
            _animator.SetBool(IsWalkingHash, false);
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        }
    }

    void HandlePlayerDetection(float distanceToPlayer)
    {
        if (!_isPlayerDetected && distanceToPlayer <= detectionRange)
        {
            // Use a raycast to ensure line of sight
            RaycastHit2D hit = Physics2D.Raycast(transform.position, (playerTransform.position - transform.position).normalized, detectionRange, ~LayerMask.GetMask("Enemy", "Ignore Raycast"));
            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                _isPlayerDetected = true;
                Debug.Log($"{gameObject.name} detected Player!");

                // Show and initialize the health bar
                if (bossHealthBarObject != null)
                {
                    bossHealthBarObject.SetActive(true);
                    UpdateHealthBar();
                }
            }
        }
        else if (_isPlayerDetected && distanceToPlayer > loseSightRange)
        {
            _isPlayerDetected = false;
            Debug.Log($"{gameObject.name} lost sight of Player.");
            _animator.SetBool(IsWalkingHash, false);
        }
    }

    void DecideNextAction(float distanceToPlayer)
    {
        // Priority 1: Attack if in range and cooldown is ready
        if (distanceToPlayer <= attackRange && Time.time >= _lastAttackTime + attackCooldown)
        {
            StartCoroutine(PerformAttackCoroutine());
        }
        // Priority 2: If in attack range (or very close to it), but on cooldown, stand still and wait.
        // This prevents the "idle -> walk -> idle" jitter while waiting for the next attack.
        else if (distanceToPlayer <= attackStoppingDistance)
        {
            _animator.SetBool(IsWalkingHash, false);
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        }
        // Priority 3: Otherwise, the player must be too far away, so move towards them.
        else
        {
            MoveTowardsPlayer();
            _animator.SetBool(IsWalkingHash, true);
        }
    }

    void FacePlayer()
    {
        if (playerTransform.position.x < transform.position.x)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void MoveTowardsPlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        _rb.linearVelocity = new Vector2(direction.x * moveSpeed, _rb.linearVelocity.y);
    }

    IEnumerator PerformAttackCoroutine()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        _animator.SetTrigger(AttackTriggerHash);
        PlaySound(attackSfx);
        _rb.linearVelocity = Vector2.zero; // Halt movement

        // This coroutine will be stopped by the animation event 'OnAttackFinished'
        // But we add a safety timeout just in case the event is not set up correctly
        float safetyTimeout = 5f;
        float elapsedTime = 0f;
        while (_isAttacking && !_isDead && !_isTakingHit && elapsedTime < safetyTimeout)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Failsafe in case animation event doesn't fire
        _isAttacking = false;
    }

    // --- Animation Event Methods ---
    // These methods MUST be called from events in your animation clips.

    /// <summary>
    /// Call this from an Animation Event at the frame where the attack should deal damage.
    /// </summary>
    public void OnAttackHitFrame()
    {
        if (_isAttacking && !_isDead && !_isTakingHit)
        {
            DealDamageToPlayer(attackDamage);
        }
    }

    /// <summary>
    /// Call this from an Animation Event at the very end of the attack animation.
    /// </summary>
    public void OnAttackFinished()
    {
        _isAttacking = false;
    }

    void DealDamageToPlayer(int damage)
    {
        if (attackPoint == null) return;

        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxSize, playerLayer);
        foreach (Collider2D playerCollider in hitPlayers)
        {
            Player player = playerCollider.GetComponent<Player>();
            if (player != null && !player._IsDeath)
            {
                Debug.Log($"{gameObject.name} hit Player for {damage} damage.");
                player.TakePlayerDamage(damage);

                // If you want the minotaur to react to player attacks, you would need
                // the player to call a public method on the boss, like SkeletonKnight's OnPlayerAttack().
                // For this simple boss, we don't need it.
                break;
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (_isDead || _isTakingHit) return;

        currentHealth -= damageAmount;
        PlaySound(takeHitSfx);
        Debug.Log($"{gameObject.name} took {damageAmount} damage. Health: {currentHealth}/{maxHealth}");

        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        else
        {
            StartCoroutine(TakeHitStunCoroutine());
        }
    }

    IEnumerator TakeHitStunCoroutine()
    {
        _isTakingHit = true;
        _isAttacking = false; // Interrupt attack
        _animator.SetTrigger(TakeHitTriggerHash);
        _rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(hitStunDuration);
        _isTakingHit = false;
    }

    /// <summary>
    /// Call this from an Animation Event at the end of the TakeHit animation.
    /// </summary>
    public void OnTakeHitFinished()
    {
        _isTakingHit = false;
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        if (endGameTextTMP != null)
        {
            endGameTextTMP.text = "THE MINOTAUR IS DEFEATED";
            endGameTextTMP.gameObject.SetActive(true);
        }

        if (bossHealthBarObject != null)
        {
            bossHealthBarObject.SetActive(false);
        }

        _animator.SetTrigger(DeathTriggerHash);
        PlaySound(deathSfx);

        // Disable physics and collider
        _rb.linearVelocity = Vector2.zero;
        _rb.isKinematic = true;
        GetComponent<Collider2D>().enabled = false;

        // Clean up the object after a delay
        Destroy(gameObject, deathCleanupDelay);
    }

    private void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// Call this from Animation Events on the walk animation frames to play footstep sounds.
    /// </summary>
    public void PlayWalkSound()
    {
        if (walkSfx != null && walkSfx.Length > 0)
        {
            PlaySound(walkSfx[Random.Range(0, walkSfx.Length)], 0.8f);
        }
    }

    void UpdateHealthBar()
    {
        if (bossHealthFillImage != null)
        {
            bossHealthFillImage.fillAmount = (float)currentHealth / maxHealth;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Detection Ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseSightRange);

        // AI Distances
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, attackStoppingDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Attack Hitbox
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackHitboxSize);
        }
    }
}