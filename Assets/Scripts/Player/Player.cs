// Player.cs
using UnityEngine;
using UnityEngine.UI; // <<< REQUIRED for UI elements
using System.Collections;

public class Player : MonoBehaviour
{
    public int Health = 10;
    public int maxHealth = 10;
    public float Speed = 5f;
    public float JumpStrength = 10f;
    public LayerMask LayerMask;
    private int _jumpCount = 0;
    public int MaxJumps = 2;
    private bool _wasGroundedLastFrame = true;
    private int _attackComboIndex = 0;
    private float _lastAttackTime;
    public float ComboResetTime = 1f;
    private bool _isSlamming = false;
    public float SlamForce = 20f;

    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;

    public PlayerAttackHitbox attackHitbox;
    public int baseMeleeDamage = 2;

    public GameObject dustImpactPrefab;
    public Transform dustSpawnPoint;

    [Header("Audio Clips")]
    public AudioClip jumpSfx;
    public AudioClip landSfx;
    public AudioClip[] footstepSfx;
    public AudioClip blockActivateSfx;
    public AudioClip blockImpactSfx;
    public AudioClip[] attackSfx;
    public AudioClip shootSfx;
    public AudioClip deathSfx;
    public AudioClip checkpointReachedSfx;

    [Header("UI")] // <<< NEW SECTION FOR UI
    public Image healthBarFillImage; // <<< REFERENCE TO THE HEALTH BAR FILL IMAGE

    private AudioSource _audioSource;
    private float _Horizontal;
    private Rigidbody2D _R2D;
    private Animator _animator;
    private SpriteRenderer _renderer;
    private CapsuleCollider2D _collider;

    public bool _IsDeath { get; private set; }
    private Vector3 _initialStartPosition;

    // Animator Hashes
    private int RunHash = Animator.StringToHash("IsRunning");
    private int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private int IsFallingHash = Animator.StringToHash("IsFalling");
    private int JumpHash = Animator.StringToHash("IsJump");
    private int DoubleJumpHash = Animator.StringToHash("IsDoubleJump");
    private int ShootArrowHash = Animator.StringToHash("ShootArrow");
    private int SlamHash = Animator.StringToHash("Slam");
    private int BlockHash = Animator.StringToHash("Block");

    private bool _IsGrounded => Physics2D.BoxCast(
        _collider.bounds.center,
        new Vector2(_collider.size.x * 0.8f, _collider.bounds.size.y),
        0f,
        Vector2.down,
        0.1f,
        LayerMask);

    private void Awake()
    {
        _R2D = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _renderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<CapsuleCollider2D>();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            Debug.LogWarning("Player is missing an AudioSource component! Adding one.");
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        if (attackHitbox == null)
        {
            attackHitbox = GetComponentInChildren<PlayerAttackHitbox>();
            if (attackHitbox == null)
            {
                Debug.LogError("PlayerAttackHitbox not found on player or its children. Melee attacks will not work.");
            }
        }

        _initialStartPosition = transform.position;
        if (Checkpoint.LastCheckpointPosition == Vector3.zero || !Checkpoint._initialCheckpointSet_InternalUseOnly)
        {
            Checkpoint.ResetToInitialCheckpoint(_initialStartPosition);
        }

        UpdateHealthUI(); // <<< CALL TO INITIALIZE HEALTH BAR
    }

    private void Update()
    {
        if (_IsDeath) return;

        HandleMovementInput();
        HandleJumpAndFall();
        HandleAttack();
        HandleBlock();
        HandleSlam();
        HandleArrowShooting();
    }

    private void FixedUpdate()
    {
        if (!_IsDeath)
        {
            ApplyMovement();
            FlipX();
        }
        else
        {
            if (_R2D != null) // Null check for safety
            {
                _R2D.linearVelocity = new Vector2(0, _R2D.linearVelocity.y); // Changed to .velocity
            }
        }
    }

    private void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip, volume);
        }
        else
        {
            if (clip == null) Debug.LogWarning($"PlaySound: AudioClip is null. SFX will not play.");
            if (_audioSource == null) Debug.LogWarning($"PlaySound: AudioSource is null on {gameObject.name}. SFX will not play.");
        }
    }

    private void PlayRandomSound(AudioClip[] clips, float volume = 1.0f)
    {
        if (_audioSource != null && clips != null && clips.Length > 0)
        {
            int randomIndex = Random.Range(0, clips.Length);
            AudioClip clipToPlay = clips[randomIndex];
            if (clipToPlay != null)
            {
                _audioSource.PlayOneShot(clipToPlay, volume);
            }
            else
            {
                Debug.LogWarning($"PlayRandomSound: AudioClip at index {randomIndex} in the array is null. SFX will not play.");
            }
        }
        else
        {
            if (clips == null || clips.Length == 0) Debug.LogWarning($"PlayRandomSound: AudioClip array is null or empty. SFX will not play.");
            if (_audioSource == null) Debug.LogWarning($"PlayRandomSound: AudioSource is null on {gameObject.name}. SFX will not play.");
        }
    }

    private void HandleMovementInput()
    {
        _Horizontal = Input.GetAxisRaw("Horizontal");
    }

    private void ApplyMovement()
    {
        if (_R2D != null) // Null check
        {
            _R2D.linearVelocity = new Vector2(_Horizontal * Speed, _R2D.linearVelocity.y); // Changed to .velocity
        }
        if (_animator != null) // Null check
        {
            _animator.SetBool(RunHash, Mathf.Abs(_Horizontal) > 0.01f);
        }
    }

    private void FlipX()
    {
        if (_renderer == null) return; // Null check

        if (_Horizontal < 0 && !_renderer.flipX)
        {
            _renderer.flipX = true;
            FlipChildTransformLocalPositionX(arrowSpawnPoint);
            FlipChildTransformLocalPositionX(dustSpawnPoint);
            if (attackHitbox != null) FlipChildTransformLocalPositionX(attackHitbox.transform);
        }
        else if (_Horizontal > 0 && _renderer.flipX)
        {
            _renderer.flipX = false;
            FlipChildTransformLocalPositionX(arrowSpawnPoint);
            FlipChildTransformLocalPositionX(dustSpawnPoint);
            if (attackHitbox != null) FlipChildTransformLocalPositionX(attackHitbox.transform);
        }
    }

    private void FlipChildTransformLocalPositionX(Transform child)
    {
        if (child != null)
        {
            child.localPosition = new Vector3(-child.localPosition.x, child.localPosition.y, child.localPosition.z);
        }
    }

    private void HandleJumpAndFall()
    {
        if (_R2D == null || _animator == null) return; // Null checks

        bool isGroundedThisFrame = _IsGrounded;

        if (isGroundedThisFrame && !_wasGroundedLastFrame)
        {
            _jumpCount = 0;
            if (_R2D.linearVelocity.y < -1f)
            {
                PlayDustImpactFX();
                PlaySound(landSfx);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) && !_IsDeath)
        {
            if (_jumpCount == 0)
            {
                if (isGroundedThisFrame || _wasGroundedLastFrame) // Allow jump slightly after leaving ground
                {
                    _R2D.linearVelocity = new Vector2(_R2D.linearVelocity.x, 0f);
                    _R2D.AddForce(Vector2.up * JumpStrength, ForceMode2D.Impulse);
                    _animator.SetTrigger(JumpHash);
                    _jumpCount = 1;
                    PlaySound(jumpSfx);
                }
            }
            else if (_jumpCount == 1 && MaxJumps > 1)
            {
                if (!isGroundedThisFrame)
                {
                    _R2D.linearVelocity = new Vector2(_R2D.linearVelocity.x, 0f);
                    _R2D.AddForce(Vector2.up * JumpStrength, ForceMode2D.Impulse);
                    _animator.SetTrigger(DoubleJumpHash);
                    _jumpCount = 2;
                    PlaySound(jumpSfx, 0.45f);
                }
            }
        }

        _animator.SetBool(IsGroundedHash, isGroundedThisFrame);
        if (!isGroundedThisFrame && _R2D.linearVelocity.y < -0.1f)
        {
            _animator.SetBool(IsFallingHash, true);
        }
        else
        {
            _animator.SetBool(IsFallingHash, false);
        }

        if (isGroundedThisFrame && _isSlamming)
        {
            _isSlamming = false;
            PlayDustImpactFX();
            PlaySound(landSfx, 1.2f);
        }
        _wasGroundedLastFrame = isGroundedThisFrame;
    }

    public void PlayFootstepSound()
    {
        PlayRandomSound(footstepSfx, 0.6f);
    }

    private void HandleAttack()
    {
        if (_animator == null || _R2D == null) return; // Null checks

        if (Input.GetMouseButtonDown(0) && !_IsDeath)
        {
            bool currentFrameGrounded = _IsGrounded;
            if (!currentFrameGrounded)
            {
                if (_R2D.linearVelocity.y > 0) // Check velocity for jump attack type
                    _animator.SetTrigger("JumpAttack1");
                else
                    _animator.SetTrigger("JumpAttack2");
            }
            else
            {
                if (Time.time - _lastAttackTime > ComboResetTime)
                {
                    _attackComboIndex = 0;
                }
                _attackComboIndex++;
                if (_attackComboIndex > 4) _attackComboIndex = 1; // Assuming 4 attack animations
                _animator.SetTrigger($"Attack{_attackComboIndex}");
                _lastAttackTime = Time.time;
            }
        }
    }

    public void PlayAttackSwingSound()
    {
        PlayRandomSound(attackSfx);
    }

    public void ActivateAttackHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.Activate(baseMeleeDamage);
        }
    }

    public void DeactivateAttackHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.Deactivate();
        }
    }

    private void HandleBlock()
    {
        if (_animator == null) return; // Null check

        if (Input.GetMouseButtonDown(1) && !_IsDeath)
        {
            _animator.SetBool(BlockHash, true);
            PlaySound(blockActivateSfx);
        }
        if (Input.GetMouseButtonUp(1) && !_IsDeath) // Consider checking _animator.GetBool(BlockHash) if problems occur
        {
            _animator.SetBool(BlockHash, false);
        }
    }

    private void HandleSlam()
    {
        if (_animator == null || _R2D == null) return; // Null checks

        if (Input.GetKeyDown(KeyCode.S) && !_IsGrounded && !_isSlamming && !_IsDeath)
        {
            _isSlamming = true;
            _animator.SetTrigger(SlamHash);
            _R2D.linearVelocity = new Vector2(_R2D.linearVelocity.x, 0); // Reset Y velocity for consistent slam
            _R2D.AddForce(Vector2.down * SlamForce, ForceMode2D.Impulse);
        }
    }

    private void HandleArrowShooting()
    {
        if (_animator == null) return; // Null check

        if (Input.GetKeyDown(KeyCode.E) && !_IsDeath)
        {
            _animator.SetTrigger(ShootArrowHash);
        }
    }

    public void FireArrow()
    {
        PlaySound(shootSfx);
        if (arrowPrefab == null || arrowSpawnPoint == null || _renderer == null)
        {
            Debug.LogError("Arrow prefab, spawn point, or renderer not set on Player.");
            return;
        }

        GameObject arrowGO = Instantiate(arrowPrefab, arrowSpawnPoint.position, arrowSpawnPoint.rotation);
        Arrow arrowScript = arrowGO.GetComponent<Arrow>();

        if (arrowScript != null)
        {
            arrowScript.direction = _renderer.flipX ? Vector2.left : Vector2.right;
            if (_renderer.flipX) // Adjust arrow visual flip if needed
            {
                arrowGO.transform.localScale = new Vector3(-Mathf.Abs(arrowGO.transform.localScale.x), arrowGO.transform.localScale.y, arrowGO.transform.localScale.z);
            }
        }
        else
        {
            Debug.LogError("Arrow prefab is missing the Arrow script component!");
        }
    }

    public void PlayDustImpactFX()
    {
        if (dustImpactPrefab != null)
        {
            Vector3 spawnPosition = dustSpawnPoint != null ? dustSpawnPoint.position : transform.position;
            Quaternion spawnRotation = dustSpawnPoint != null ? dustSpawnPoint.rotation : Quaternion.identity;
            Instantiate(dustImpactPrefab, spawnPosition, spawnRotation);
        }
    }

    public void PlayCheckpointReachedSound()
    {
        PlaySound(checkpointReachedSfx);
    }

    // Mark as Obsolete if you plan to replace this system later
    // [System.Obsolete("Consider a more robust death/respawn manager.")]
    public void Die()
    {
        if (_IsDeath) return;

        Debug.Log("Player Died!");
        _IsDeath = true;
        if (_animator != null) _animator.Play("Death");
        PlaySound(deathSfx);

        if (_R2D != null)
        {
            _R2D.linearVelocity = Vector2.zero;
            _R2D.isKinematic = true;
        }
        if (_collider != null) _collider.enabled = false;

        StartCoroutine(RespawnPlayerCoroutine(2f));
        UpdateHealthUI(); // <<< UPDATE UI ON DEATH
    }

    // Mark as Obsolete if you plan to replace this system later
    // [System.Obsolete("Consider a more robust death/respawn manager.")]
    private IEnumerator RespawnPlayerCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log("Respawning player at: " + Checkpoint.LastCheckpointPosition);
        transform.position = Checkpoint.LastCheckpointPosition;

        Health = maxHealth;
        _IsDeath = false;
        _jumpCount = 0;
        _isSlamming = false;
        _wasGroundedLastFrame = true;

        if (_animator != null) _animator.Play("Idle");
        if (_R2D != null) _R2D.isKinematic = false;
        if (_collider != null) _collider.enabled = true;

        UpdateHealthUI(); // <<< UPDATE UI ON RESPAWN
    }

    // Mark as Obsolete if you plan to replace this system later
    // [System.Obsolete("Consider a more robust damage system.")]
    public void TakePlayerDamage(int amount = 1)
    {
        if (_IsDeath) return;

        if (_animator != null && _animator.GetBool(BlockHash))
        {
            PlaySound(blockImpactSfx);
            return;
        }

        Health -= amount;
        Health = Mathf.Max(Health, 0); // Ensure health doesn't go below 0
        Debug.Log($"Player took {amount} damage. Current Health: {Health}/{maxHealth}");

        UpdateHealthUI(); // <<< UPDATE UI ON TAKING DAMAGE

        if (Health <= 0 && !_IsDeath)
        {
            // Health = 0; // Already clamped by Mathf.Max
            Die();
        }
    }

    // <<< NEW METHOD TO UPDATE HEALTH UI >>>
    public void UpdateHealthUI()
    {
        if (healthBarFillImage != null)
        {
            if (maxHealth > 0) // Prevent division by zero if maxHealth isn't set properly
            {
                healthBarFillImage.fillAmount = (float)Health / maxHealth;
            }
            else
            {
                healthBarFillImage.fillAmount = 0; // Or 1, depending on desired behavior for 0 max health
                Debug.LogWarning("Player MaxHealth is 0 or less. Health UI may not display correctly.");
            }
        }
        else
        {
            // This warning is helpful during setup but can be commented out later if you're sure it's always assigned.
            // Debug.LogWarning("Player: HealthBarFillImage is not assigned in the Inspector!");
        }
    }
}