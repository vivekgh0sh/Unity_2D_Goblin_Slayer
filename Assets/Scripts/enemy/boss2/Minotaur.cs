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
    public float attackStoppingDistance = 2.5f;

    [Header("Attack (Heavy Swing)")]
    public int attackDamage = 25;
    public float attackRange = 3.0f;
    public float attackCooldown = 2.0f;
    public AudioClip attackSfx;

    [Header("Charge Attack")]
    [Tooltip("How much faster the Minotaur moves when charging.")]
    public float chargeSpeedMultiplier = 2.5f;
    [Tooltip("How long the charge lasts in seconds.")]
    public float chargeDuration = 1.5f;
    [Tooltip("How often the Minotaur can charge in seconds.")]
    public float chargeCooldown = 8f;
    [Tooltip("The distance at which the Minotaur will consider charging the player.")]
    public float chargeTriggerDistance = 10f;

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
    private bool _isCharging = false;
    private float _lastAttackTime = -Mathf.Infinity;
    private float _lastChargeTime = -Mathf.Infinity;

    // Animator Hashes
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int TakeHitTriggerHash = Animator.StringToHash("TakeHit");
    private static readonly int DeathTriggerHash = Animator.StringToHash("Death");

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) { _audioSource = gameObject.AddComponent<AudioSource>(); }
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) { playerTransform = playerObject.transform; }
            else { Debug.LogError($"Minotaur ({gameObject.name}): Player not found!"); enabled = false; return; }
        }
        _lastAttackTime = -attackCooldown;
        _lastChargeTime = -chargeCooldown; // Initialize charge cooldown
        if (bossHealthBarObject != null) { bossHealthBarObject.SetActive(false); }
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
            // If an action is in progress, let it finish.
            if (_isAttacking || _isCharging)
            {
                return; // Let the respective coroutines handle the logic
            }
            DecideNextAction(distanceToPlayer);
        }
        else
        {
            _animator.SetBool(IsWalkingHash, false);
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        }
    }

    void DecideNextAction(float distanceToPlayer)
    {
        // Priority 1: Charge if player is far away and charge is ready
        if (distanceToPlayer > chargeTriggerDistance && Time.time >= _lastChargeTime + chargeCooldown)
        {
            StartCoroutine(ChargeCoroutine());
            return;
        }

        // Priority 2: Attack if in range and cooldown is ready
        if (distanceToPlayer <= attackRange && Time.time >= _lastAttackTime + attackCooldown)
        {
            StartCoroutine(PerformAttackCoroutine());
        }
        // Priority 3: Wait if in attack range but on cooldown
        else if (distanceToPlayer <= attackStoppingDistance)
        {
            _animator.SetBool(IsWalkingHash, false);
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        }
        // Priority 4: Walk towards player
        else
        {
            MoveTowardsPlayer();
            _animator.SetBool(IsWalkingHash, true);
        }
    }

    IEnumerator ChargeCoroutine()
    {
        Debug.Log("Minotaur is charging!");
        _isCharging = true;
        _lastChargeTime = Time.time;

        _animator.speed = chargeSpeedMultiplier;
        _animator.SetBool(IsWalkingHash, true);

        float chargeEndTime = Time.time + chargeDuration;

        while (Time.time < chargeEndTime)
        {
            if (_isDead || _isTakingHit) break;

            Vector2 direction = (playerTransform.position - transform.position).normalized;
            _rb.linearVelocity = new Vector2(direction.x * moveSpeed * chargeSpeedMultiplier, _rb.linearVelocity.y);

            yield return null;
        }

        _animator.speed = 1f;
        _isCharging = false;
    }

    // --- Other methods remain the same ---
    IEnumerator TakeHitStunCoroutine()
    {
        _isTakingHit = true; _isAttacking = false; _animator.SetTrigger(TakeHitTriggerHash); _rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(3f); if (_isTakingHit) { _isTakingHit = false; }
    }
    public void OnTakeHitFinished() { _isTakingHit = false; }
    IEnumerator PerformAttackCoroutine()
    {
        _isAttacking = true; _lastAttackTime = Time.time; _animator.SetTrigger(AttackTriggerHash); PlaySound(attackSfx); _rb.linearVelocity = Vector2.zero;
        yield return new WaitUntil(() => !_isAttacking || _isTakingHit || _isDead);
    }
    public void TakeDamage(int damageAmount)
    {
        if (_isDead || _isTakingHit) return;
        currentHealth -= damageAmount; PlaySound(takeHitSfx); UpdateHealthBar();
        if (currentHealth <= 0) { currentHealth = 0; Die(); } else { StartCoroutine(TakeHitStunCoroutine()); }
    }
    public void OnAttackHitFrame() { if (_isAttacking && !_isDead && !_isTakingHit) { DealDamageToPlayer(attackDamage); } }
    public void OnAttackFinished() { _isAttacking = false; }
    void HandlePlayerDetection(float distanceToPlayer)
    {
        if (!_isPlayerDetected && distanceToPlayer <= detectionRange)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, (playerTransform.position - transform.position).normalized, detectionRange, ~LayerMask.GetMask("Enemy", "Ignore Raycast"));
            if (hit.collider != null && hit.collider.CompareTag("Player")) { _isPlayerDetected = true; if (bossHealthBarObject != null) { bossHealthBarObject.SetActive(true); UpdateHealthBar(); } }
        }
        else if (_isPlayerDetected && distanceToPlayer > loseSightRange) { _isPlayerDetected = false; _animator.SetBool(IsWalkingHash, false); }
    }
    void FacePlayer() { if (playerTransform.position.x < transform.position.x) { transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z); } else { transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z); } }
    void MoveTowardsPlayer() { Vector2 direction = (playerTransform.position - transform.position).normalized; _rb.linearVelocity = new Vector2(direction.x * moveSpeed, _rb.linearVelocity.y); }
    void DealDamageToPlayer(int damage) { if (attackPoint == null) return; Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxSize, playerLayer); foreach (Collider2D playerCollider in hitPlayers) { Player player = playerCollider.GetComponent<Player>(); if (player != null && !player._IsDeath) { player.TakePlayerDamage(damage); break; } } }
    void Die() { if (_isDead) return; _isDead = true; if (endGameTextTMP != null) { endGameTextTMP.text = "THE MINOTAUR IS DEFEATED"; endGameTextTMP.gameObject.SetActive(true); } if (bossHealthBarObject != null) { bossHealthBarObject.SetActive(false); } _animator.SetTrigger(DeathTriggerHash); PlaySound(deathSfx); _rb.linearVelocity = Vector2.zero; _rb.isKinematic = true; GetComponent<Collider2D>().enabled = false; Destroy(gameObject, deathCleanupDelay); }
    private void PlaySound(AudioClip clip, float volume = 1.0f) { if (_audioSource != null && clip != null) { _audioSource.PlayOneShot(clip, volume); } }
    public void PlayWalkSound() { if (walkSfx != null && walkSfx.Length > 0) { PlaySound(walkSfx[Random.Range(0, walkSfx.Length)], 0.8f); } }
    void UpdateHealthBar() { if (bossHealthFillImage != null) { bossHealthFillImage.fillAmount = (float)currentHealth / maxHealth; } }
    void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRange); Gizmos.color = Color.gray; Gizmos.DrawWireSphere(transform.position, loseSightRange); Gizmos.color = Color.blue; Gizmos.DrawWireSphere(transform.position, attackStoppingDistance); Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange); Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, chargeTriggerDistance); if (attackPoint != null) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(attackPoint.position, attackHitboxSize); } }
}