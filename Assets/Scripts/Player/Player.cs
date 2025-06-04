using UnityEngine;
using System.Collections;

public class Player : MonoBehaviour
{
    public int Health = 10;
    public int maxHealth = 10; // Added for respawn
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
    public AudioClip deathSfx; // Added
    public AudioClip checkpointReachedSfx; // Added

    private AudioSource _audioSource;

    private float _Horizontal;
    private Rigidbody2D _R2D;
    private Animator _animator;
    private SpriteRenderer _renderer;
    private CapsuleCollider2D _collider;

    // --- MODIFIED ---
    public bool _IsDeath { get; private set; } // Public getter, private setter for controlled access
    // --- END MODIFIED ---

    private Vector3 _initialStartPosition; // For initial checkpoint

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

        // --- ADDED FOR CHECKPOINT ---
        _initialStartPosition = transform.position;
        // Ensure Checkpoint system is initialized with player's starting spot if no other initial is set
        if (Checkpoint.LastCheckpointPosition == Vector3.zero || !Checkpoint._initialCheckpointSet_InternalUseOnly) // Using a more specific flag from Checkpoint script
        {
            Checkpoint.ResetToInitialCheckpoint(_initialStartPosition);
        }
        // --- END ADDED ---
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
            // For Rigidbody based movement, setting velocity to zero is better than linearVelocity
            _R2D.linearVelocity = new Vector2(0, _R2D.linearVelocity.y);
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
        // Using Rigidbody.velocity for physics based movement
        _R2D.linearVelocity = new Vector2(_Horizontal * Speed, _R2D.linearVelocity.y);
        _animator.SetBool(RunHash, Mathf.Abs(_Horizontal) > 0.01f);
    }

   private void FlipX()
{
    if (_Horizontal < 0 && !_renderer.flipX)
    {
        _renderer.flipX = true;
        FlipChildTransformLocalPositionX(arrowSpawnPoint);
        FlipChildTransformLocalPositionX(dustSpawnPoint);
        FlipChildTransformLocalPositionX(attackHitbox.transform); // <<< ADDED THIS (using .transform)
    }
    else if (_Horizontal > 0 && _renderer.flipX)
    {
        _renderer.flipX = false;
        FlipChildTransformLocalPositionX(arrowSpawnPoint); // No need for 'true' here, it just inverts
        FlipChildTransformLocalPositionX(dustSpawnPoint);
        FlipChildTransformLocalPositionX(attackHitbox.transform); // <<< ADDED THIS (using .transform)
    }
}

// NEW Helper method to flip localPosition.x
private void FlipChildTransformLocalPositionX(Transform child)
{
    if (child != null)
    {
        // Invert the current localPosition.x
        child.localPosition = new Vector3(-child.localPosition.x, child.localPosition.y, child.localPosition.z);
    }
}


    private void HandleJumpAndFall()
    {
        bool isGroundedThisFrame = _IsGrounded;

        if (isGroundedThisFrame && !_wasGroundedLastFrame)
        {
            _jumpCount = 0;
            if (_R2D.linearVelocity.y < -1f) // Check for actual fall velocity, not just touching ground
            {
                PlayDustImpactFX();
                PlaySound(landSfx);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) && !_IsDeath)
        {
            if (_jumpCount == 0)
            {
                if (isGroundedThisFrame || _wasGroundedLastFrame)
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
        if (Input.GetMouseButtonDown(0) && !_IsDeath)
        {
            bool currentFrameGrounded = _IsGrounded;
            if (!currentFrameGrounded)
            {
                if (_R2D.linearVelocity.y > 0)
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
                if (_attackComboIndex > 4) _attackComboIndex = 1;
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
        if (Input.GetMouseButtonDown(1) && !_IsDeath)
        {
            _animator.SetBool(BlockHash, true);
            PlaySound(blockActivateSfx);
        }
        if (Input.GetMouseButtonUp(1) && !_IsDeath)
        {
            _animator.SetBool(BlockHash, false);
        }
    }

    private void HandleSlam()
    {
        if (Input.GetKeyDown(KeyCode.S) && !_IsGrounded && !_isSlamming && !_IsDeath)
        {
            _isSlamming = true;
            _animator.SetTrigger(SlamHash);
            _R2D.linearVelocity = new Vector2(_R2D.linearVelocity.x, 0);
            _R2D.AddForce(Vector2.down * SlamForce, ForceMode2D.Impulse);
        }
    }

    private void HandleArrowShooting()
    {
        if (Input.GetKeyDown(KeyCode.E) && !_IsDeath)
        {
            _animator.SetTrigger(ShootArrowHash);
        }
    }

    public void FireArrow()
    {
        PlaySound(shootSfx);
        if (arrowPrefab == null || arrowSpawnPoint == null)
        {
            Debug.LogError("Arrow prefab or spawn point not set on Player.");
            return;
        }

        GameObject arrowGO = Instantiate(arrowPrefab, arrowSpawnPoint.position, arrowSpawnPoint.rotation);
        Arrow arrowScript = arrowGO.GetComponent<Arrow>();

        if (arrowScript != null)
        {
            arrowScript.direction = _renderer.flipX ? Vector2.left : Vector2.right;
            if (_renderer.flipX)
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

    // --- ADDED FOR CHECKPOINT ---
    public void PlayCheckpointReachedSound() // Public method to be called by Checkpoint script if needed
    {
        PlaySound(checkpointReachedSfx);
    }

    // --- END ADDED ---

    // --- MODIFIED FOR RESPAWN ---
    [System.Obsolete]
    public void Die()
    {
        if (_IsDeath) return;

        Debug.Log("Player Died!");
        _IsDeath = true; // Set death flag
        _animator.Play("Death"); // Play death animation
        PlaySound(deathSfx);   // Play death sound

        _R2D.linearVelocity = Vector2.zero;     // Stop all movement
        _R2D.isKinematic = true;          // Make Rigidbody not affected by physics
        _collider.enabled = false;        // Disable collider to prevent interactions

        // Consider disabling other input/control scripts if you have them
        // e.g., if (TryGetComponent<YourPlayerInputScript>(out var inputScript)) inputScript.enabled = false;

        StartCoroutine(RespawnPlayerCoroutine(2f)); // Start respawn process after a delay
    }

    [System.Obsolete]
    private IEnumerator RespawnPlayerCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log("Respawning player at: " + Checkpoint.LastCheckpointPosition);
        transform.position = Checkpoint.LastCheckpointPosition; // Move to checkpoint

        // Reset player state
        Health = maxHealth; // Reset to max health
        _IsDeath = false;   // Clear death flag
        _jumpCount = 0;     // Reset jump count
        _isSlamming = false; // Reset slam state
        _wasGroundedLastFrame = true; // Assume grounded on respawn for immediate jump

        _animator.Play("Idle"); // Or a specific respawn/idle animation
        _R2D.isKinematic = false; // Re-enable physics
        _collider.enabled = true;  // Re-enable collider

        // Re-enable input/control scripts if they were disabled
        // e.g., if (TryGetComponent<YourPlayerInputScript>(out var inputScript)) inputScript.enabled = true;

        // Update health UI if applicable
        // UpdateHealthUI();
    }

    [System.Obsolete]
    public void TakePlayerDamage(int amount = 1)
    {
        if (_IsDeath) return;

        if (_animator.GetBool(BlockHash))
        {
            PlaySound(blockImpactSfx);
            return;
        }

        Health -= amount;
        Debug.Log($"Player took {amount} damage. Current Health: {Health}");

        // UpdateHealthUI();

        if (Health <= 0 && !_IsDeath) // Ensure Die is only called once
        {
            Health = 0; // Clamp health
            Die();      // Call new Die method
        }
    }

}