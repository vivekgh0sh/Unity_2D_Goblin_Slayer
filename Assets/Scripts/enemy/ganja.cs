// ganja.cs (Corrected)
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ganja : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 5;
    public float moveSpeed = 2f; // Ensure this is > 0 in the Inspector
    public int attackDamage = 1;
    public float attackRange = 1.5f;
    public float attackCooldown = 2f;
    public float detectionRange = 8f;
    public float loseSightRange = 12f;

    // <<< NEW KNOCKBACK STATS >>>
    [Tooltip("How hard the player is pushed back on hit.")]
    public float attackKnockbackForce = 15f;
    [Tooltip("How long the player is stunned during knockback (in seconds).")]
    public float knockbackDuration = 0.2f;

    [Header("References")]
    public Transform playerTransform;
    public LayerMask playerLayer;
    public Transform attackPoint;
    public float attackHitboxSize = 0.5f;

    [Header("UI Setup")]
    public GameObject healthBarPrefab;
    public Vector3 healthBarOffset = new Vector3(0, 1.2f, 0);

    [Header("SFX")]
    public AudioClip attackSfx;
    public AudioClip takeHitSfx;
    public AudioClip deathSfx;
    public AudioClip[] footstepSfx;

    // --- Private Variables ---
    private int currentHealth;
    private Animator _animator;
    private Rigidbody2D _rb;
    private AudioSource _audioSource;
    private Camera _mainCamera;
    private Image _healthBarFillImage;
    private Canvas _healthBarCanvasInstance;

    // --- State Management ---
    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private float _lastAttackTime = -Mathf.Infinity;
    private bool _isTakingHit = false;
    private bool _isDead = false;
    private Coroutine _attackCoroutine;

    // --- Animator Hashes ---
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int TakeHitTriggerHash = Animator.StringToHash("TakeHit");
    private static readonly int DeathTriggerHash = Animator.StringToHash("Death");

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _mainCamera = Camera.main;
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) playerTransform = playerObject.transform;
            else { Debug.LogError($"Ganja ({gameObject.name}): Player not found!"); enabled = false; return; }
        }
        SetupHealthBar();
        UpdateHealthBarVisuals();
    }

    void Update()
    {
        if (_isDead || _isTakingHit || playerTransform == null)
        {
            // If frozen, ensure running animation is off and velocity is zeroed.
            if (_animator != null) _animator.SetBool(IsRunningHash, false);
            if (_rb != null && _rb.bodyType == RigidbodyType2D.Dynamic) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        HandlePlayerDetection(distanceToPlayer);

        if (_isPlayerDetected)
        {
            FacePlayer();
            // --- ACTION LOGIC (directly from your working script) ---
            if (distanceToPlayer <= attackRange && Time.time >= _lastAttackTime + attackCooldown && !_isAttacking)
            {
                // In attack range and ready to attack
                _attackCoroutine = StartCoroutine(Attack());
            }
            else if (distanceToPlayer > attackRange && !_isAttacking)
            {
                // Outside attack range, chase player
                MoveTowardsPlayer();
                if (_animator != null) _animator.SetBool(IsRunningHash, true);
            }
            else
            {
                // In attack range but on cooldown, or currently attacking. Stop moving.
                if (_animator != null) _animator.SetBool(IsRunningHash, false);
                if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            }
        }
        else // Player is not detected
        {
            // Stop moving and animating
            if (_animator != null) _animator.SetBool(IsRunningHash, false);
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            // Optional: Add idle or patrol logic here
        }
    }

    private void LateUpdate()
    {
        if (_healthBarCanvasInstance != null && _mainCamera != null)
        {
            _healthBarCanvasInstance.transform.LookAt(transform.position + _mainCamera.transform.rotation * Vector3.forward, _mainCamera.transform.rotation * Vector3.up);
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
            }
        }
        else if (_isPlayerDetected && distanceToPlayer > loseSightRange)
        {
            _isPlayerDetected = false;
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
        if (playerTransform == null) return;
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        // Use Rigidbody.velocity for smooth, physics-based movement.
        if (_rb != null) _rb.linearVelocity = new Vector2(direction.x * moveSpeed, _rb.linearVelocity.y);
    }

    IEnumerator Attack()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        if (_animator != null) _animator.SetTrigger(AttackTriggerHash);
        PlaySound(attackSfx);
        yield return new WaitForSeconds(0.3f);
        if (_isAttacking && !_isDead && !_isTakingHit) DealDamageToPlayer();
        yield return new WaitForSeconds(0.4f);
        _isAttacking = false;
        _attackCoroutine = null;
    }

    void DealDamageToPlayer()
    {
        if (attackPoint == null) return;
        foreach (Collider2D playerCollider in Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxSize, playerLayer))
        {
            if (playerCollider.TryGetComponent<Player>(out Player player) && !player._IsDeath)
            {
                player.TakePlayerDamage(attackDamage);
                // --- APPLY KNOCKBACK ---
                // Calculate direction from enemy to player
                Vector2 knockbackDirection = (player.transform.position - transform.position).normalized;

                // Add a slight upward push to make it feel better (optional)
                knockbackDirection.y += 0.1f;

                // Call the new method on the Player script
                player.ApplyKnockback(knockbackDirection, attackKnockbackForce, knockbackDuration);
                // --- END KNOCKBACK ---
                break;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (_isDead || _isTakingHit) return;
        currentHealth -= damage;
        PlaySound(takeHitSfx);
        UpdateHealthBarVisuals();
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        else
        {
            StartCoroutine(TakeHitStun());
        }
    }

    IEnumerator TakeHitStun()
    {
        _isTakingHit = true;
        _isAttacking = false;
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        if (_animator != null) _animator.SetTrigger(TakeHitTriggerHash);
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.5f);
        _isTakingHit = false;
    }

    void Die()
    {
        _isDead = true;
        if (_animator != null) _animator.SetTrigger(DeathTriggerHash);
        PlaySound(deathSfx);
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.isKinematic = true;
        }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (_healthBarCanvasInstance != null) Destroy(_healthBarCanvasInstance.gameObject);
        Destroy(gameObject, 2f);
    }

    // --- UI and Sound Methods ---
    void SetupHealthBar()
    {
        if (healthBarPrefab == null) return;
        GameObject canvasGO = Instantiate(healthBarPrefab, transform.position + healthBarOffset, Quaternion.identity);
        _healthBarCanvasInstance = canvasGO.GetComponent<Canvas>();
        if (_healthBarCanvasInstance == null) { Destroy(canvasGO); return; }
        _healthBarCanvasInstance.transform.SetParent(transform);
        _healthBarCanvasInstance.transform.localPosition = healthBarOffset;
        Transform fillTransform = _healthBarCanvasInstance.transform.Find("HealthBar_Background_Enemy/HealthBar_Fill_Enemy");
        if (fillTransform != null) _healthBarFillImage = fillTransform.GetComponent<Image>();
    }

    private void UpdateHealthBarVisuals()
    {
        if (_healthBarFillImage == null) return;
        if (maxHealth > 0) _healthBarFillImage.fillAmount = (float)currentHealth / maxHealth;
        if (_healthBarCanvasInstance != null)
        {
            bool shouldBeActive = currentHealth > 0 && currentHealth < maxHealth;
            _healthBarCanvasInstance.gameObject.SetActive(shouldBeActive);
        }
    }

    private void OnDestroy()
    {
        if (_healthBarCanvasInstance != null) Destroy(_healthBarCanvasInstance.gameObject);
    }

    private void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (_audioSource != null && clip != null) _audioSource.PlayOneShot(clip, volume);
    }

    // This function receives the Animation Event from the "run" animation to prevent console warnings.
    public void PlayFootstepSound()
    {
        if (footstepSfx != null && footstepSfx.Length > 0 && _audioSource != null)
        {
            _audioSource.PlayOneShot(footstepSfx[Random.Range(0, footstepSfx.Length)], 0.5f);
        }
    }

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