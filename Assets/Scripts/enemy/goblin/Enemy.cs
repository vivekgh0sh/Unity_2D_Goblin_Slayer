
using UnityEngine;
using UnityEngine.UI; // Required for UI elements like Image if you use it for health bar fill

public class Enemy : MonoBehaviour
{
    public float Distance;
    public int Speed;
    public float AttackCooldown = 1f;
    public int Damage = 1; // Damage this enemy deals
    private float _lastAttackTime;

    public int maxHealth = 10; // Max health for the enemy
    public int Health;         // Current health
    public GameObject HealthBarUI; // The parent UI element for the health bar (e.g., a Canvas or Panel)
    public Transform HealthBarFill; // The UI Image Transform used as the fill part of the health bar

    private Transform _player;

    public Transform Target;
    public Transform AttackTarget;

    private Animator _animator;
    private bool _isRight = true;
    private bool _isAttacking = false;
    private int AttackHash = Animator.StringToHash("IsAttacking");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        Health = maxHealth; // Initialize health
    }

    private void Start()
    {
        UpdateHealthBar(); // Initial health bar update
    }

    private void Update()
    {
        if (!_isAttacking)
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        transform.Translate(Vector3.right * Speed * Time.deltaTime);

        RaycastHit2D groundHit = Physics2D.Raycast(Target.position, Vector2.down, Distance);
        RaycastHit2D wallHit = Physics2D.Raycast(Target.position, transform.right, 0.3f);

        if (!groundHit.collider || wallHit.collider)
        {
            Flip();
        }

        Debug.DrawRay(Target.position, Vector2.down * Distance, Color.green);
        Debug.DrawRay(Target.position, transform.right * 0.3f, Color.red);
    }

    private void Flip()
    {
        if (_isRight)
        {
            transform.rotation = Quaternion.Euler(0, 180, 0);
            _isRight = false;
        }
        else
        {
            transform.rotation = Quaternion.identity;
            _isRight = true;
        }
        // Flip the health bar as well if it's a child and world space UI
        if (HealthBarUI != null && HealthBarUI.transform.parent == transform)
        {
            HealthBarUI.transform.localScale = new Vector3(
                _isRight ? Mathf.Abs(HealthBarUI.transform.localScale.x) : -Mathf.Abs(HealthBarUI.transform.localScale.x),
                HealthBarUI.transform.localScale.y,
                HealthBarUI.transform.localScale.z
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _player = collision.transform;
            _isAttacking = true;
            _animator.SetBool(AttackHash, true);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            FacePlayer();

            if (Time.time - _lastAttackTime >= AttackCooldown)
            {
                _animator.SetTrigger("Attack");
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
            _animator.SetBool(AttackHash, false);
        }
    }
    private void FacePlayer()
    {
        if (_player == null) return;

        if (_player.position.x > transform.position.x && !_isRight)
        {
            Flip();
        }
        else if (_player.position.x < transform.position.x && _isRight)
        {
            Flip();
        }
    }

    public void DealDamage()
    {
        // This is called by animation event for enemy's attack
        // You would check if _player is still in range and deal damage to player
        if (_player != null)
        {
            // Example: Check distance or use a small OverlapCircle for attack hit detection
            float attackRange = 1.5f; // Define an attack range
            if (Vector2.Distance(AttackTarget.position, _player.position) < attackRange)
            {
                Player playerScript = _player.GetComponent<Player>();
                if (playerScript != null)
                {
                    playerScript.TakePlayerDamage(Damage); // Enemy deals damage to player
                }
            }
        }
    }

    public void TakeDamage(int amount)
    {
        Health -= amount;
        Debug.Log($"{gameObject.name} took {amount} damage. Current Health: {Health}/{maxHealth}"); // Console log

        UpdateHealthBar();

        if (Health <= 0)
        {
            Health = 0; // Ensure health doesn't go negative
            Die();
        }
    }

    private void UpdateHealthBar()
    {
        if (HealthBarFill != null)
        {
            // Assuming HealthBarFill is an Image with Fill Method set to Horizontal
            // If it's a direct transform scale:
            HealthBarFill.localScale = new Vector3((float)Health / maxHealth, 1f, 1f);

            // If HealthBarFill is an Image component (UnityEngine.UI.Image):
            // Image fillImage = HealthBarFill.GetComponent<Image>();
            // if (fillImage != null)
            // {
            //    fillImage.fillAmount = (float)Health / maxHealth;
            // }
        }
        if (HealthBarUI != null)
        {
            // Keep health bar facing the camera if it's world space UI
            // Or handle visibility based on health
            HealthBarUI.SetActive(Health > 0 && Health < maxHealth); // Show only when damaged but not dead
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} has died.");
        // Optional: Play death animation
        // _animator.SetTrigger("Die");
        // Destroy(gameObject, deathAnimationDuration);
        Destroy(gameObject);
    }
}