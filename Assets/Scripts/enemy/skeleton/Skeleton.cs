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

    [Header("Combat Phases")]
    [Tooltip("Health percentage threshold for Phase 2")]
    [Range(0, 100)]
    public int phase2ThresholdPercent = 70;
    [Tooltip("Health percentage threshold for Phase 3")]
    [Range(0, 100)]
    public int phase3ThresholdPercent = 30;
    [Tooltip("Multiplier for attack speed in Phase 2")]
    public float phase2AttackSpeedMultiplier = 1.2f;
    [Tooltip("Multiplier for attack speed in Phase 3")]
    public float phase3AttackSpeedMultiplier = 1.5f;
    [Tooltip("Multiplier for movement speed in Phase 3")]
    public float phase3MoveSpeedMultiplier = 1.3f;

    [Header("Advanced AI")]
    [Tooltip("Chance (0-1) to perform a combo when an attack finishes successfully")]
    public float comboChance = 0.4f;
    [Tooltip("Time window (seconds) after player attack where boss can attempt to block reactively")]
    public float reactiveBlockWindow = 0.8f;
    [Tooltip("Cooldown for reactive blocking")]
    public float reactiveBlockCooldown = 3f;
    [Tooltip("Distance within which boss might strafe")]
    public float strafeDistanceThreshold = 3f;
    [Tooltip("Chance (0-1) to strafe each decision tick")]
    public float strafeChance = 0.3f;
    [Tooltip("Force to apply when backing away")]
    public float backAwayForce = 3f;

    [Header("References")]
    public Transform playerTransform;   // Assign the Player's transform
    public LayerMask playerLayer;       // Set this to the layer your Player is on
    public Transform attackPoint;       // Empty child GameObject where attacks originate
    public float attackHitboxSize = 0.7f; // General radius/size of the attack hitbox (can be overridden per attack logic if complex)

    [Header("Attack 1 (Basic Swing)")]
    public int attack1Damage = 10;
    public float attack1Range = 2.0f;
    public float attack1Cooldown = 2.5f;
    public float attack1HitFrameDelay = 0.5f; // Time from animation start to damage frame (now mainly for Coroutine safety)
    public float attack1RecoveryTime = 0.7f; // Time for animation to finish after hit frame (now mainly for Coroutine safety)
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
    public float shieldBlockChance = 0.3f; // Chance to block when eligible (proactive)
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

    [Header("Feedback & Polish")]
    [Tooltip("Duration of hit stun when taking damage (not blocking)")]
    public float hitStunDuration = 0.5f; // Configurable hit stun
    [Tooltip("Time to wait before destroying the object after death animation starts")]
    public float deathCleanupDelay = 3f; // Configurable death delay

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
    private float _lastReactiveBlockAttemptTime = -Mathf.Infinity;
    private float _lastPlayerAttackTime = -Mathf.Infinity; // Track when player last attacked
    private int _currentPhase = 1;
    private int _lastAttackUsed = 0; // 0 = none, 1, 2, 3 = attack type

    // Animator Hashes
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int Attack1TriggerHash = Animator.StringToHash("Attack1");
    private static readonly int Attack2TriggerHash = Animator.StringToHash("Attack2");
    private static readonly int Attack3TriggerHash = Animator.StringToHash("Attack3");
    private static readonly int ShieldBlockTriggerHash = Animator.StringToHash("ShieldBlock"); // Or "IsBlocking" (bool)
    private static readonly int TakeHitTriggerHash = Animator.StringToHash("TakeHit");
    private static readonly int DeathTriggerHash = Animator.StringToHash("Death");
    // private static readonly int TauntTriggerHash = Animator.StringToHash("Taunt"); // REMOVED

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
        _lastReactiveBlockAttemptTime = -reactiveBlockCooldown;
    }

    void Update()
    {
        if (_isDead || _isTakingHit || playerTransform == null)
        {
            _animator.SetBool(IsWalkingHash, false);
            if (_rb != null && !_isDead) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            return;
        }

        UpdatePhase(); // Check and update combat phase

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

    void UpdatePhase()
    {
        float healthPercent = (float)currentHealth / maxHealth * 100;
        if (healthPercent <= phase3ThresholdPercent)
        {
            _currentPhase = 3;
        }
        else if (healthPercent <= phase2ThresholdPercent)
        {
            _currentPhase = 2;
        }
        else
        {
            _currentPhase = 1;
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
        // Priority: Reactive Block > Proactive Block > Attack Combo > Attack > Move/Strafe
        // 1. Try Reactive Block (if player recently attacked)
        if (Time.time < _lastPlayerAttackTime + reactiveBlockWindow &&
            Time.time >= _lastReactiveBlockAttemptTime + reactiveBlockCooldown &&
            distanceToPlayer <= attack1Range + 1f) // Only block if player is reasonably close
        {
            StartCoroutine(ShieldBlockCoroutine(isReactive: true));
            return;
        }

        // 2. Try Proactive Block
        if (Time.time >= _lastBlockTime + shieldBlockCooldown && Random.value < shieldBlockChance && distanceToPlayer <= attack1Range + 2f)
        {
            StartCoroutine(ShieldBlockCoroutine(isReactive: false));
            return;
        }

        // 3. Try Attack Combo (if last attack was successful and not a combo already)
        if (_lastAttackUsed != 0 && Random.value < comboChance)
        {
            // Simple combo logic: Attack1 -> Attack3, Attack2 -> Attack1, Attack3 -> Attack2
            System.Action comboAttack = null;
            if (_lastAttackUsed == 1 && Time.time >= _lastAttack1Time + GetAdjustedCooldown(attack1Cooldown))
                comboAttack = () => StartCoroutine(PerformAttack3());
            else if (_lastAttackUsed == 2 && Time.time >= _lastAttack2Time + GetAdjustedCooldown(attack2Cooldown))
                comboAttack = () => StartCoroutine(PerformAttack1());
            else if (_lastAttackUsed == 3 && Time.time >= _lastAttack3Time + GetAdjustedCooldown(attack3Cooldown))
                comboAttack = () => StartCoroutine(PerformAttack2());

            if (comboAttack != null)
            {
                comboAttack.Invoke();
                _lastAttackUsed = 0; // Reset to prevent chain combos for now
                return;
            }
        }

        // 4. Try to Attack (with weighted selection)
        List<System.Action> availableAttacks = new List<System.Action>();
        List<float> attackWeights = new List<float>();

        float attack1Weight = 1.0f;
        float attack2Weight = 1.0f;
        float attack3Weight = 1.0f;

        // Weight adjustments based on distance
        if (distanceToPlayer > attack1Range * 0.8f) attack1Weight *= 0.5f; // Less likely if far for basic attack
        if (distanceToPlayer < attack2Range * 0.6f) attack2Weight *= 1.5f; // More likely if close for heavy
        if (distanceToPlayer > attack3Range * 0.9f) attack3Weight *= 0.3f; // Less likely if far for push

        if (Time.time >= _lastAttack1Time + GetAdjustedCooldown(attack1Cooldown) && distanceToPlayer <= attack1Range)
        {
            availableAttacks.Add(() => StartCoroutine(PerformAttack1()));
            attackWeights.Add(attack1Weight);
        }
        if (Time.time >= _lastAttack2Time + GetAdjustedCooldown(attack2Cooldown) && distanceToPlayer <= attack2Range)
        {
            availableAttacks.Add(() => StartCoroutine(PerformAttack2()));
            attackWeights.Add(attack2Weight);
        }
        if (Time.time >= _lastAttack3Time + GetAdjustedCooldown(attack3Cooldown) && distanceToPlayer <= attack3Range)
        {
            availableAttacks.Add(() => StartCoroutine(PerformAttack3()));
            attackWeights.Add(attack3Weight);
        }

        if (availableAttacks.Count > 0)
        {
            int choice = WeightedRandom(attackWeights);
            if (choice >= 0 && choice < availableAttacks.Count)
            {
                availableAttacks[choice].Invoke();
                return;
            }
        }

        // 5. Move or Stay Still / Strafe / Back Away
        if (distanceToPlayer > preferredStoppingDistance)
        {
            MoveTowardsPlayer();
            _animator.SetBool(IsWalkingHash, true);
        }
        else // Close enough
        {
            // Potentially strafe or back away
            float rand = Random.value;
            if (rand < strafeChance * 0.5f && distanceToPlayer < strafeDistanceThreshold)
            {
                Strafe();
            }
            else if (rand < strafeChance && distanceToPlayer < strafeDistanceThreshold)
            {
                BackAway();
            }
            else
            {
                _animator.SetBool(IsWalkingHash, false);
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // Stop
            }
        }
    }

    float GetAdjustedCooldown(float baseCooldown)
    {
        if (_currentPhase == 2) return baseCooldown / phase2AttackSpeedMultiplier;
        if (_currentPhase == 3) return baseCooldown / phase3AttackSpeedMultiplier;
        return baseCooldown;
    }

    float GetAdjustedMoveSpeed()
    {
        if (_currentPhase == 3) return moveSpeed * phase3MoveSpeedMultiplier;
        return moveSpeed;
    }

    int WeightedRandom(List<float> weights)
    {
        if (weights == null || weights.Count == 0) return -1;

        float totalWeight = 0;
        foreach (float w in weights) totalWeight += w;

        if (totalWeight <= 0) return Random.Range(0, weights.Count);

        float randomValue = Random.Range(0, totalWeight);
        float cumulativeWeight = 0;

        for (int i = 0; i < weights.Count; i++)
        {
            cumulativeWeight += weights[i];
            if (randomValue <= cumulativeWeight)
            {
                return i;
            }
        }
        return weights.Count - 1; // Fallback
    }

    void MoveTowardsPlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        _rb.linearVelocity = new Vector2(direction.x * GetAdjustedMoveSpeed(), _rb.linearVelocity.y);
    }

    void Strafe()
    {
        Vector2 direction = new Vector2(Random.Range(-1f, 1f), 0).normalized;
        if (direction.x != 0) // Ensure we have a direction
        {
            // Ensure strafe direction is somewhat perpendicular to player direction
            Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
            if (Vector2.Dot(direction, toPlayer) > 0.7f) // Too parallel, flip
            {
                direction = -direction;
            }
            _rb.AddForce(direction * GetAdjustedMoveSpeed() * 1.5f, ForceMode2D.Impulse);
            _animator.SetBool(IsWalkingHash, true);
        }
    }

    void BackAway()
    {
        Vector2 direction = (transform.position - playerTransform.position).normalized;
        _rb.AddForce(direction * backAwayForce, ForceMode2D.Impulse);
        _animator.SetBool(IsWalkingHash, true);
    }


    // --- Attack Coroutines (Timing mainly handled by Animation Events now) ---
    IEnumerator PerformAttack1()
    {
        _isAttacking = true;
        _lastAttack1Time = Time.time;
        _lastAttackUsed = 1;
        _animator.SetTrigger(Attack1TriggerHash);
        PlaySound(attack1Sfx);
        _rb.linearVelocity = Vector2.zero; // Halt movement during attack

        // Safety net timeout - animation events should stop the coroutine
        float maxDuration = attack1HitFrameDelay + attack1RecoveryTime + 1f;
        float elapsedTime = 0f;
        while (_isAttacking && !_isDead && !_isTakingHit && elapsedTime < maxDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _isAttacking = false; // Ensure it's reset if animation event fails
    }

    IEnumerator PerformAttack2()
    {
        _isAttacking = true;
        _lastAttack2Time = Time.time;
        _lastAttackUsed = 2;
        _animator.SetTrigger(Attack2TriggerHash);
        PlaySound(attack2Sfx);
        _rb.linearVelocity = Vector2.zero;

        float maxDuration = attack2HitFrameDelay + attack2RecoveryTime + 1f;
        float elapsedTime = 0f;
        while (_isAttacking && !_isDead && !_isTakingHit && elapsedTime < maxDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _isAttacking = false;
    }

    IEnumerator PerformAttack3()
    {
        _isAttacking = true;
        _lastAttack3Time = Time.time;
        _lastAttackUsed = 3;
        _animator.SetTrigger(Attack3TriggerHash);
        PlaySound(attack3Sfx);
        _rb.linearVelocity = Vector2.zero;

        float maxDuration = attack3HitFrameDelay + attack3RecoveryTime + 1f;
        float elapsedTime = 0f;
        while (_isAttacking && !_isDead && !_isTakingHit && elapsedTime < maxDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _isAttacking = false;
    }

    // --- Animation Events for Attacks ---
    // These methods MUST be called by Animation Events in your attack animations
    public void OnAttack1HitFrame()
    {
        if (_isAttacking && !_isDead && !_isTakingHit) DealDamageToPlayer(attack1Damage);
    }
    public void OnAttack1Finished()
    {
        _isAttacking = false; // This is the primary way to end the attack state
    }
    public void OnAttack2HitFrame()
    {
        if (_isAttacking && !_isDead && !_isTakingHit) DealDamageToPlayer(attack2Damage);
    }
    public void OnAttack2Finished()
    {
        _isAttacking = false;
    }
    public void OnAttack3HitFrame()
    {
        if (_isAttacking && !_isDead && !_isTakingHit) DealDamageToPlayer(attack3Damage);
    }
    public void OnAttack3Finished()
    {
        _isAttacking = false;
    }

    IEnumerator ShieldBlockCoroutine(bool isReactive = false)
    {
        _isBlocking = true;
        if (isReactive)
        {
            _lastReactiveBlockAttemptTime = Time.time;
            // Debug.Log($"{gameObject.name} performed reactive block!");
        }
        else
        {
            _lastBlockTime = Time.time;
            // Debug.Log($"{gameObject.name} performed proactive block!");
        }
        _animator.SetTrigger(ShieldBlockTriggerHash);
        PlaySound(shieldBlockActivateSfx);
        _rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(shieldBlockDuration);

        _isBlocking = false;
    }

    // --- Animation Event for Block End ---
    // This method MUST be called by Animation Event at the end of the block animation
    public void OnBlockFinished()
    {
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
                // Record player attack time for reactive block (assuming player attacks trigger this)
                // This is a simplification - ideally, the Player script would notify enemies
                // Or you could check for a specific "PlayerAttack" tag/layer on player's attack collider
                // For now, we'll assume any hit from the boss means the player is attacking back
                // A better way: Have Player script call a method on enemies when they attack
                _lastPlayerAttackTime = Time.time;
                break; // Usually hit one player
            }
        }
    }

    // Call this method from the Player script when the player attacks (e.g., on sword swing)
    public void OnPlayerAttack()
    {
        _lastPlayerAttackTime = Time.time;
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
        // Interrupt current actions (less critical now with Animation Events, but good safety)
        // StopCoroutines are tricky by name, but we rely on Animation Events primarily
        _isAttacking = false;
        _isBlocking = false;
        _animator.ResetTrigger(Attack1TriggerHash); // Try to cancel queued attacks
        _animator.ResetTrigger(Attack2TriggerHash);
        _animator.ResetTrigger(Attack3TriggerHash);
        _animator.SetTrigger(TakeHitTriggerHash);
        _rb.linearVelocity = Vector2.zero; // Briefly stop

        yield return new WaitForSeconds(hitStunDuration); // Use configurable duration
        _isTakingHit = false;
    }

    // --- Animation Event for Hit Stun End ---
    // This method MUST be called by Animation Event at the end of the take hit animation
    public void OnTakeHitFinished()
    {
        _isTakingHit = false;
    }

    void Die()
    {
        if (_isDead) return; // Prevent multiple deaths
        _isDead = true;
        _animator.SetTrigger(DeathTriggerHash);
        PlaySound(deathSfx);
        _rb.linearVelocity = Vector2.zero;
        if (_rb != null) _rb.freezeRotation = true; // Often better than isKinematic for 2D
        if (_rb != null) _rb.constraints = RigidbodyConstraints2D.FreezeAll; // Freeze all movement
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false; // Disable collider

        // Use configurable delay
        Invoke("CleanupAfterDeath", deathCleanupDelay);
    }

    void CleanupAfterDeath()
    {
        Destroy(gameObject);
    }

    // --- Animation Event for Death End ---
    // This method CAN be called by Animation Event at the end of the death animation instead of Invoke
    public void OnDeathAnimationFinished()
    {
        CancelInvoke("CleanupAfterDeath"); // Cancel the Invoke if animation event happens first
        CleanupAfterDeath();
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