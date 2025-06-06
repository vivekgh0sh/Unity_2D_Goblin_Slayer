// Enemy.cs
using UnityEngine;
using UnityEngine.UI; // REQUIRED for UI elements

public class Enemy : MonoBehaviour
{
    public float Distance;
    public int Speed;
    public float AttackCooldown = 1f;
    public int Damage = 1;

    public int maxHealth = 10;
    public int Health;

    [Header("UI Setup")]
    public GameObject healthBarPrefab;
    public Vector3 healthBarOffset = new Vector3(0, 1.5f, 0);

    private Image _healthBarFillImage;
    private Canvas _healthBarCanvasInstance;
    private Camera _mainCamera;

    private float _lastAttackTime;
    private Transform _player;
    public Transform Target; // For patrol raycasts
    public Transform AttackTarget; // For attack hit detection
    private Animator _animator;

    // --- State Booleans ---
    private bool _isRight = true;
    private bool _isAttacking = false;
    private bool _isPatrolling = false; // <<< NEW: To control patrolling state

    // --- Animator Hashes ---
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack"); // Assuming "Attack" is a Trigger
    private static readonly int IsAttackingBoolHash = Animator.StringToHash("IsAttacking"); // If you also have a bool for attack loop
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking"); // For patrol/walk animation

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        Health = maxHealth;
        _mainCamera = Camera.main;
    }

    private void Start()
    {
        SetupHealthBar();
        UpdateHealthBarVisuals();

        // Start patrolling immediately
        _isPatrolling = true;
        if (_animator != null)
        {
            _animator.SetBool(IsWalkingHash, true); // Tell animator to play walk/patrol animation
        }
    }

    void SetupHealthBar()
    {
        if (healthBarPrefab == null)
        {
            Debug.LogWarning($"Health Bar Prefab not assigned for {gameObject.name}. UI will not be displayed.");
            return;
        }

        GameObject canvasGO = Instantiate(healthBarPrefab, transform.position + healthBarOffset, Quaternion.identity);
        _healthBarCanvasInstance = canvasGO.GetComponent<Canvas>();

        if (_healthBarCanvasInstance == null)
        {
            Debug.LogError($"Instantiated Health Bar Prefab for {gameObject.name} is missing a Canvas component.");
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

        if (_healthBarFillImage == null)
        {
            Debug.LogError($"Could not find 'HealthBar_Fill_Enemy' Image component for {gameObject.name}. Check prefab names.");
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

    private void Update()
    {
        if (_isPatrolling && !_isAttacking)
        {
            Patrol();
            // Ensure walk animation is playing if animator exists and isn't already set
            if (_animator != null && !_animator.GetBool(IsWalkingHash))
            {
                _animator.SetBool(IsWalkingHash, true);
            }
        }
        else if (_isAttacking)
        {
            // If there's specific logic for while attacking (besides OnTriggerStay), it goes here.
            // For example, ensuring walk animation is off:
            if (_animator != null && _animator.GetBool(IsWalkingHash))
            {
                _animator.SetBool(IsWalkingHash, false);
            }
        }
        else // Not patrolling and not attacking (e.g., could be idle)
        {
            if (_animator != null && _animator.GetBool(IsWalkingHash))
            {
                _animator.SetBool(IsWalkingHash, false); // Turn off walking if idle
            }
            // Add idle behavior if needed
        }
    }

    private void Patrol()
    {
        if (Target == null)
        {
            // Debug.LogWarning($"{gameObject.name}: Target for patrol raycasts not assigned.");
            return;
        }

        transform.Translate(Vector3.right * Speed * Time.deltaTime, Space.Self); // Move in local space

        RaycastHit2D groundHit = Physics2D.Raycast(Target.position, Vector2.down, Distance);
        RaycastHit2D wallHit = Physics2D.Raycast(Target.position, transform.right, 0.3f);

        if (!groundHit.collider || (wallHit.collider && !wallHit.collider.isTrigger))
        {
            Flip();
        }
    }

    private void Flip()
    {
        _isRight = !_isRight;
        transform.Rotate(0f, 180f, 0f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _player = collision.transform;
            _isAttacking = true;
            _isPatrolling = false; // Stop patrolling
            if (_animator != null)
            {
                _animator.SetBool(IsAttackingBoolHash, true); // Use your actual attack bool
                _animator.SetBool(IsWalkingHash, false);    // Stop walking animation
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            FacePlayer(); // Keep facing player while in trigger

            if (Time.time - _lastAttackTime >= AttackCooldown)
            {
                if (_animator != null) _animator.SetTrigger(AttackTriggerHash); // Fire attack animation
                _lastAttackTime = Time.time;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _player = null;
            _isAttacking = false;
            _isPatrolling = true; // Resume patrolling
            if (_animator != null)
            {
                _animator.SetBool(IsAttackingBoolHash, false);
                _animator.SetBool(IsWalkingHash, true); // Start walking animation
            }
        }
    }

    private void FacePlayer()
    {
        if (_player == null) return;

        // Determine direction to player
        bool playerIsToTheRight = _player.position.x > transform.position.x;

        // Flip if current direction (_isRight) doesn't match direction to player
        if (playerIsToTheRight && !_isRight)
        {
            Flip();
        }
        else if (!playerIsToTheRight && _isRight)
        {
            Flip();
        }
    }

    public void DealDamage()
    {
        if (_player != null && AttackTarget != null)
        {
            float attackRange = 1.5f;
            if (Vector2.Distance(AttackTarget.position, _player.position) < attackRange)
            {
                Player playerScript = _player.GetComponent<Player>();
                if (playerScript != null && !playerScript._IsDeath)
                {
                    playerScript.TakePlayerDamage(Damage);
                }
            }
        }
    }

    public void TakeDamage(int amount)
    {
        Health -= amount;
        Health = Mathf.Max(Health, 0);
        Debug.Log($"{gameObject.name} took {amount} damage. Current Health: {Health}/{maxHealth}");

        UpdateHealthBarVisuals();

        if (Health <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthBarVisuals()
    {
        if (_healthBarFillImage != null)
        {
            if (maxHealth > 0)
            {
                _healthBarFillImage.fillAmount = (float)Health / maxHealth;
            }
            else
            {
                _healthBarFillImage.fillAmount = 0;
            }
        }

        if (_healthBarCanvasInstance != null)
        {

            bool shouldBeActive = Health > 0 && Health < maxHealth;

            if (_healthBarCanvasInstance.gameObject.activeSelf != shouldBeActive)
            {
                _healthBarCanvasInstance.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} has died.");
        _isPatrolling = false; // Stop all activities
        _isAttacking = false;

        if (_healthBarCanvasInstance != null)
        {
            Destroy(_healthBarCanvasInstance.gameObject);
        }
        // if (_animator != null) _animator.SetTrigger("DieTrigger");
        Destroy(gameObject /*, optionalDelayForAnimation */);
    }

    private void OnDestroy()
    {
        if (_healthBarCanvasInstance != null)
        {
            Destroy(_healthBarCanvasInstance.gameObject);
        }
    }
}