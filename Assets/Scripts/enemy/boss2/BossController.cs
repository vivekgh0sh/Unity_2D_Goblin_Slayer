using UnityEngine;
using System.Collections;

public class BossController : MonoBehaviour
{
    // Public variables for easy adjustment in the Inspector
    public float moveSpeed = 2f; // Speed at which the boss moves
    public float attackRange = 5f; // Distance within which the boss can attack the player
    public float attackCooldown = 3f; // Time between attacks
    public int maxHealth = 100; // Maximum health of the boss
    public GameObject player; // Reference to the player object

    // Private variables
    private Animator animator; // Reference to the Animator component
    private Rigidbody2D rb; // Reference to the Rigidbody2D component
    private float health; // Current health of the boss
    private bool isAttacking; // Flag to track if the boss is currently attacking
    private float attackTimer; // Timer to control attack cooldown

    void Start()
    {
        // Initialize components
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        health = maxHealth;

        // Ensure the player reference is set
        if (player == null)
        {
            Debug.LogError("Player reference is not set in the Inspector!");
        }
    }

    void Update()
    {
        // Check if the boss is alive
        if (health <= 0)
        {
            Die();
            return;
        }

        // Get the distance to the player
        float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

        // Update attack timer
        attackTimer -= Time.deltaTime;

        // Decide what the boss should do based on distance to the player
        if (distanceToPlayer > attackRange)
        {
            // Move towards the player
            MoveTowardsPlayer();
        }
        else
        {
            // Attack the player if within range and cooldown is ready
            if (!isAttacking && attackTimer <= 0)
            {
                Attack();
            }
        }
    }

    void MoveTowardsPlayer()
    {
        // Calculate the direction to the player
        Vector2 direction = (player.transform.position - transform.position).normalized;

        // Move the boss towards the player
        rb.linearVelocity = direction * moveSpeed;

        // Switch to "walk" animation
        animator.SetTrigger("walk");
    }

    void Attack()
    {
        // Set the boss to an attacking state
        isAttacking = true;
        attackTimer = attackCooldown;

        // Switch to "attack" animation
        animator.SetTrigger("attack");

        // After the attack animation, reset the attacking state
        StartCoroutine(ResetAttackState());
    }

    IEnumerator ResetAttackState()
    {
        yield return new WaitForSeconds(1f); // Wait for the attack animation to finish
        isAttacking = false;
    }

    void Die()
    {
        // Stop all movements and animations
        rb.linearVelocity = Vector2.zero;
        animator.SetTrigger("die");

        // Destroy the boss after the death animation
        Destroy(gameObject, 2f); // Adjust the delay based on the death animation length
    }

    public void TakeDamage(int damage)
    {
        // Reduce the boss's health
        health -= damage;

        // Debug log for testing
        Debug.Log("Boss took " + damage + " damage. Remaining health: " + health);

        // If health reaches zero, trigger the death sequence
        if (health <= 0)
        {
            Die();
        }
    }
}
