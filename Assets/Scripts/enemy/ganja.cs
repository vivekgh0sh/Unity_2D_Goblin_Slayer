// ganja.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ganja : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 5;
    public int currentHealth;
    public float moveSpeed = 2f;
    public int attackDamage = 1;
    public float attackRange = 1.5f;
    public float attackCooldown = 2f;
    public float detectionRange = 8f;
    public float loseSightRange = 12f;

    [Header("References")]
    public Transform playerTransform;
    public LayerMask playerLayer;
    public Transform attackPoint;
    public float attackHitboxSize = 0.5f;

    [Header("UI Setup")]
    public GameObject healthBarPrefab;
    public Vector3 healthBarOffset = new Vector3(0, 1.2f, 0);

    private Image _healthBarFillImage;
    private Canvas _healthBarCanvasInstance;
    private Camera _mainCamera;

    [Header("SFX")]
    public AudioClip attackSfx;
    public AudioClip takeHitSfx;
    public AudioClip deathSfx;

    private Animator _animator;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private AudioSource _audioSource;

    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private float _lastAttackTime = -Mathf.Infinity;
    private bool _isTakingHit = false;
    private bool _isDead = false;

    private Coroutine _attackCoroutine; // <<< VARIABLE TO HOLD THE ATTACK COROUTINE

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
        _mainCamera = Camera.main;
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
                Debug.LogError($"Ganja ({gameObject.name}): Player not found! Assign Player Transform or ensure Player is tagged 'Player'.");
                enabled = false;
                return;
            }
        }

        SetupHealthBar();
        UpdateHealthBarVisuals();
    }

    void SetupHealthBar()
    {
        if (healthBarPrefab == null) return;

        GameObject canvasGO = Instantiate(healthBarPrefab, transform.position + healthBarOffset, Quaternion.identity);
        _healthBarCanvasInstance = canvasGO.GetComponent<Canvas>();

        if (_healthBarCanvasInstance == null)
        {
            Destroy(canvasGO);
            return;
        }

        _healthBarCanvasInstance.transform.SetParent(transform);
        _healthBarCanvasInstance.transform.localPosition = healthBarOffset;

        Transform fillTransform = _healthBarCanvasInstance.transform.Find("HealthBar_Background_Enemy/HealthBar_Fill_Enemy");
        if (fillTransform != null)
        {
            _healthBarFillImage = fillTransform.GetComponent<Image>();
        }
    }

    private void LateUpdate()
    {
        if (_healthBarCanvasInstance != null && _mainCamera != null)
        {
            _healthBarCanvasInstance.transform.LookAt(transform.position + _mainCamera.transform.rotation * Vector3.forward,
                                                      _mainCamera.transform.rotation * Vector3.up);
        }
    }

    void Update()
    {
        if (_isDead || _isTakingHit || playerTransform == null)
        {
            if (_animator != null) _animator.SetBool(IsRunningHash, false);
            if (_rb != null && !_isDead) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        HandlePlayerDetection(distanceToPlayer);

        if (_isPlayerDetected)
        {
            FacePlayer();
            HandleActions(distanceToPlayer);
        }
        else
        {
            if (_animator != null) _animator.SetBool(IsRunningHash, false);
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
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

    void HandleActions(float distanceToPlayer)
    {
        if (distanceToPlayer <= attackRange && Time.time >= _lastAttackTime + attackCooldown && !_isAttacking)
        {
            _attackCoroutine = StartCoroutine(Attack()); // <<< STORE THE COROUTINE
        }
        else if (distanceToPlayer > attackRange && !_isAttacking)
        {
            MoveTowardsPlayer();
            if (_animator != null) _animator.SetBool(IsRunningHash, true);
        }
        else if (_isAttacking)
        {
            if (_animator != null) _animator.SetBool(IsRunningHash, false);
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        }
        else
        {
            if (_animator != null) _animator.SetBool(IsRunningHash, false);
            if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
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
        if (_rb != null) _rb.linearVelocity = new Vector2(direction.x * moveSpeed, _rb.linearVelocity.y);
    }

    IEnumerator Attack()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        if (_animator != null) _animator.SetTrigger(AttackTriggerHash);
        PlaySound(attackSfx);

        yield return new WaitForSeconds(0.3f);

        if (_isAttacking && !_isDead && !_isTakingHit)
        {
            DealDamageToPlayer();
        }

        yield return new WaitForSeconds(0.4f);

        _isAttacking = false;
        _attackCoroutine = null; // <<< CLEAR THE REFERENCE WHEN DONE
    }

    void DealDamageToPlayer()
    {
        if (attackPoint == null) return;
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxSize, playerLayer);
        foreach (Collider2D playerCollider in hitPlayers)
        {
            Player player = playerCollider.GetComponent<Player>();
            if (player != null && !player._IsDeath)
            {
                player.TakePlayerDamage(attackDamage);
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

        // --- FIX: STOP SPECIFIC COROUTINE, NOT ALL ---
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        // --- END FIX ---

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

        if (_healthBarCanvasInstance != null)
        {
            Destroy(_healthBarCanvasInstance.gameObject);
        }

        Destroy(gameObject, 2f);
    }

    private void UpdateHealthBarVisuals()
    {
        if (_healthBarFillImage == null) return;

        if (maxHealth > 0)
        {
            _healthBarFillImage.fillAmount = (float)currentHealth / maxHealth;
        }

        if (_healthBarCanvasInstance != null)
        {
            bool shouldBeActive = currentHealth > 0 && currentHealth < maxHealth;
            if (_healthBarCanvasInstance.gameObject.activeSelf != shouldBeActive)
            {
                _healthBarCanvasInstance.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    private void OnDestroy()
    {
        if (_healthBarCanvasInstance != null)
        {
            Destroy(_healthBarCanvasInstance.gameObject);
        }
    }

    private void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip, volume);
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