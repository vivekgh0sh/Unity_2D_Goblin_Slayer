// SkeletonKnight.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for List

public class SkeletonKnight : MonoBehaviour
{
    [Header("Core Stats")]
    public int maxHealth = 150;
    public int currentHealth;
    public float moveSpeed = 1.5f;
    public float detectionRange = 10f;  // How far to detect player
    public float loseSightRange = 15f; // How far until player is "lost"
    public float preferredStoppingDistance = 2f; // Distance to stop when near player but attacks might be on cooldown

    [Header("References")]
    public Transform playerTransform;   // Assign the Player's transform
    public LayerMask playerLayer;       // Set this to the layer your Player is on
    public Transform attackPoint;       // Empty child GameObject where attacks originate
    public float attackHitboxSize = 0.7f; // General radius/size of the attack hitbox (can be overridden per attack logic if complex)

    [Header("Attack 1 (Basic Swing)")]
    public int attack1Damage = 10;
    public float attack1Range = 2.0f;
    public float attack1Cooldown = 2.5f;
    public float attack1HitFrameDelay = 0.5f; // Time from animation start to damage frame
    public float attack1RecoveryTime = 0.7f; // Time for animation to finish after hit frame
    public AudioClip attack1Sfx;

    [Header("Attack 2 (Heavy Swing)")]
    public int attack2Damage = 20;
    public float attack2Range = 2.5f;
    public float attack2Cooldown = 4.0f;
    public float attack2HitFrameDelay = 0.8f;
    public float attack2RecoveryTime = 1.0f;
    public AudioClip attack2Sfx;

    [Header("Attack 3 (Shield Push)")]
    public int attack3Damage = 5; // Might do less damage but have knockback
    public float attack3Range = 1.5f;
    public float attack3Cooldown = 5.0f;
    public float attack3HitFrameDelay = 0.4f;
    public float attack3RecoveryTime = 0.6f;
    public AudioClip attack3Sfx;
    // public float attack3KnockbackForce = 10f; // Optional for shield push

    [Header("Shield Block")]
    public float shieldBlockChance = 0.3f; // Chance to block when eligible
    public float shieldBlockDuration = 2.0f;
    public float shieldBlockCooldown = 6.0f;
    [Tooltip("How much damage is reduced when blocking (0 = no damage, 1 = full damage)")]
    public float blockDamageMultiplier = 0.1f; // Takes 10% damage when blocking
    public AudioClip shieldBlockActivateSfx;
    public AudioClip shieldBlockImpactSfx;

    [Header("General SFX")]
    public AudioClip takeHitSfx;
    public AudioClip deathSfx;
    public AudioClip[] walkSfx; // Optional for walking sound

    // Components
    private Animator _animator;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private AudioSource _audioSource;

    // State
    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private bool _isBlocking = false;
    private bool _isTakingHit = false;
    private bool _isDead = false;

    private float _lastAttack1Time = -Mathf.Infinity;
    private float _lastAttack2Time = -Mathf.Infinity;
    private float _lastAttack3Time = -Mathf.Infinity;
    private float _lastBlockTime = -Mathf.Infinity;

    // Animator Hashes
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int Attack1TriggerHash = Animator.StringToHash("Attack1");
    private static readonly int Attack2TriggerHash = Animator.StringToHash("Attack2");
    private static readonly int Attack3TriggerHash = Animator.StringToHash("Attack3");
    private static readonly int ShieldBlockTriggerHash = Animator.StringToHash("ShieldBlock"); // Or "IsBlocking" (bool)
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
        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
            else
            {
                Debug.LogError($"SkeletonKnight ({gameObject.name}): Player not found! Assign Player Transform or ensure Player is tagged 'Player'.");
                enabled = false;
                return;
            }
        }
        // Initialize cooldowns to allow immediate action if desired
        _lastAttack1Time = -attack1Cooldown;
        _lastAttack2Time = -attack2Cooldown;
        _lastAttack3Time = -attack3Cooldown;
        _lastBlockTime = -shieldBlockCooldown;
    }

    void Update()
    {
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

            if (_isAttacking || _isBlocking) // If currently performing an action, let it finish
            {
                _animator.SetBool(IsWalkingHash, false);
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop movement during attack/block
                return;
            }

            // AI Decision Making
            DecideNextAction(distanceToPlayer);
        }
        else // Not detected
        {
            _animator.SetBool(IsWalkingHash, false);
            // Optional: Implement patrol behavior here
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop
        }
    }

    void HandlePlayerDetection(float distanceToPlayer)
    {
        if (!_isPlayerDetected && distanceToPlayer <= detectionRange)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, (playerTransform.position - transform.position).normalized, detectionRange, ~LayerMask.GetMask("Enemy", "Ignore Raycast"));
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
            _animator.SetBool(IsWalkingHash, false);
        }
    }

    void FacePlayer()
    {
        if (playerTransform.position.x < transform.position.x) // Player is to the left
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else // Player is to the right
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void DecideNextAction(float distanceToPlayer)
    {
        // Priority: Block > Attack > Move

        // 1. Try to Block
        if (Time.time >= _lastBlockTime + shieldBlockCooldown && Random.value < shieldBlockChance && distanceToPlayer <= detectionRange) // Can block if player is generally close
        {
            StartCoroutine(ShieldBlockCoroutine());
            return;
        }

        // 2. Try to Attack
        List<System.Action> availableAttacks = new List<System.Action>();
        if (Time.time >= _lastAttack1Time + attack1Cooldown && distanceToPlayer <= attack1Range)
            availableAttacks.Add(() => StartCoroutine(PerformAttack1()));
        if (Time.time >= _lastAttack2Time + attack2Cooldown && distanceToPlayer <= attack2Range)
            availableAttacks.Add(() => StartCoroutine(PerformAttack2()));
        if (Time.time >= _lastAttack3Time + attack3Cooldown && distanceToPlayer <= attack3Range)
            availableAttacks.Add(() => StartCoroutine(PerformAttack3()));

        if (availableAttacks.Count > 0)
        {
            int choice = Random.Range(0, availableAttacks.Count);
            availableAttacks[choice].Invoke(); // Execute a random available attack
            return;
        }

        // 3. Move or Stay Still
        if (distanceToPlayer > preferredStoppingDistance)
        {
            MoveTowardsPlayer();
            _animator.SetBool(IsWalkingHash, true);
        }
        else // Close enough, but attacks might be on cooldown
        {
            _animator.SetBool(IsWalkingHash, false);
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop
        }
    }

    void MoveTowardsPlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        _rb.linearVelocity = new Vector2(direction.x * moveSpeed, _rb.linearVelocity.y);
    }

    IEnumerator PerformAttack1()
    {
        _isAttacking = true;
        _lastAttack1Time = Time.time;
        _animator.SetTrigger(Attack1TriggerHash);
        PlaySound(attack1Sfx);
        _rb.linearVelocity = Vector2.zero; // Halt movement during attack

        yield return new WaitForSeconds(attack1HitFrameDelay);
        if (_isAttacking && !_isDead && !_isTakingHit) DealDamageToPlayer(attack1Damage); // Check flags before dealing damage

        yield return new WaitForSeconds(attack1RecoveryTime);
        _isAttacking = false;
    }

    IEnumerator PerformAttack2()
    {
        _isAttacking = true;
        _lastAttack2Time = Time.time;
        _animator.SetTrigger(Attack2TriggerHash);
        PlaySound(attack2Sfx);
        _rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(attack2HitFrameDelay);
        if (_isAttacking && !_isDead && !_isTakingHit) DealDamageToPlayer(attack2Damage);

        yield return new WaitForSeconds(attack2RecoveryTime);
        _isAttacking = false;
    }

    IEnumerator PerformAttack3() // Shield Push
    {
        _isAttacking = true;
        _lastAttack3Time = Time.time;
        _animator.SetTrigger(Attack3TriggerHash);
        PlaySound(attack3Sfx);
        _rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(attack3HitFrameDelay);
        if (_isAttacking && !_isDead && !_isTakingHit)
        {
            DealDamageToPlayer(attack3Damage);
            // Optional: Add knockback to player here if DealDamageToPlayer doesn't handle it
            // Player player = playerTransform.GetComponent<Player>();
            // if (player != null) player.ApplyKnockback(...);
        }

        yield return new WaitForSeconds(attack3RecoveryTime);
        _isAttacking = false;
    }

    IEnumerator ShieldBlockCoroutine()
    {
        _isBlocking = true;
        _lastBlockTime = Time.time;
        _animator.SetTrigger(ShieldBlockTriggerHash); // Or _animator.SetBool("IsBlocking", true);
        PlaySound(shieldBlockActivateSfx);
        _rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(shieldBlockDuration);

        // _animator.SetBool("IsBlocking", false); // If using a bool parameter
        _isBlocking = false;
    }


    void DealDamageToPlayer(int damage)
    {
        if (attackPoint == null)
        {
            Debug.LogError($"{gameObject.name}: AttackPoint is not set!");
            return;
        }

        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxSize, playerLayer);
        foreach (Collider2D playerCollider in hitPlayers)
        {
            Player player = playerCollider.GetComponent<Player>();
            if (player != null && !player._IsDeath)
            {
                Debug.Log($"{gameObject.name} hit Player for {damage} damage.");
                player.TakePlayerDamage(damage);
                // Add knockback or other effects here if desired, specific to the attack
                break; // Usually hit one player
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (_isDead) return;

        if (_isBlocking)
        {
            int damageAfterBlock = Mathf.RoundToInt(damageAmount * blockDamageMultiplier);
            currentHealth -= damageAfterBlock;
            PlaySound(shieldBlockImpactSfx);
            Debug.Log($"{gameObject.name} blocked! Took {damageAfterBlock} reduced damage.");
            // Optional: Trigger a "block hit" animation/effect if you have one
        }
        else
        {
            if (_isTakingHit) return; // Already in hit stun
            currentHealth -= damageAmount;
            PlaySound(takeHitSfx);
            Debug.Log($"{gameObject.name} took {damageAmount} damage. Health: {currentHealth}/{maxHealth}");
        }


        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        else if (!_isBlocking) // Only play hit stun if not blocking (block has its own animation)
        {
            StartCoroutine(TakeHitStunCoroutine());
        }
    }

    IEnumerator TakeHitStunCoroutine()
    {
        _isTakingHit = true;

        // Interrupt current actions
        if (_isAttacking)
        {
            StopCoroutine("PerformAttack1"); // Stop specific coroutines by name
            StopCoroutine("PerformAttack2");
            StopCoroutine("PerformAttack3");
            _isAttacking = false;
        }
        if (_isBlocking)
        {
            StopCoroutine("ShieldBlockCoroutine");
            _isBlocking = false;
            // _animator.SetBool("IsBlocking", false); // If using bool for block anim
        }

        _animator.SetTrigger(TakeHitTriggerHash);
        _rb.linearVelocity = Vector2.zero; // Briefly stop

        // Duration of hit stun (adjust to match your TakeHit animation length)
        yield return new WaitForSeconds(0.5f); // TODO: Make this configurable or match animation

        _isTakingHit = false;
    }

    void Die()
    {
        _isDead = true;
        _animator.SetTrigger(DeathTriggerHash);
        PlaySound(deathSfx);
        _rb.linearVelocity = Vector2.zero;
        if (_rb != null) _rb.isKinematic = true; // Stop physics interactions

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false; // Disable collider

        // Destroy GameObject after death animation (adjust delay)
        Destroy(gameObject, 3f); // TODO: Make this configurable or match animation
    }

    private void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip, volume);
        }
    }

    public void PlayWalkSound() // Called by animation event on walk animation frames
    {
        if (walkSfx != null && walkSfx.Length > 0)
        {
            PlaySound(walkSfx[Random.Range(0, walkSfx.Length)], 0.7f);
        }
    }

    // Gizmos for visualizing ranges in Editor
    void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;

        // Detection & Sight
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseSightRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, preferredStoppingDistance);


        // Attack Ranges (you can make these more distinct if needed)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Red for Attack 1
        Gizmos.DrawWireSphere(transform.position, attack1Range);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange for Attack 2
        Gizmos.DrawWireSphere(transform.position, attack2Range);
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Magenta for Attack 3
        Gizmos.DrawWireSphere(transform.position, attack3Range);

        // Attack Hitbox
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackHitboxSize);
        }
    }
}