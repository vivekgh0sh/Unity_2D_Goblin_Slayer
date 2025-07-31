// Arrow.cs
using UnityEngine;

public class Arrow : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 3f;
    public Vector2 direction = Vector2.right;
    public int damage = 1; // Damage the arrow deals

    void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Arrow is missing a Rigidbody2D!", gameObject);
            enabled = false;
            return;
        }
        rb.linearVelocity = direction * speed; // Use .velocity for consistent movement
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // First, check if the object we hit can take damage.
        // This works for Minotaur, SkeletonKnight, ganja, and any other enemy
        // that has the IDamageable interface.
        IDamageable damageableObject = other.GetComponent<IDamageable>();

        if (damageableObject != null)
        {
            // If it's a damageable enemy, deal damage and destroy the arrow.
            Debug.Log($"Arrow hit an IDamageable object: '{other.gameObject.name}'. Dealing {damage} damage.");
            damageableObject.TakeDamage(damage);
            Destroy(gameObject);
            return; // Stop further checks
        }

        // If it wasn't a damageable enemy, check if it was the player or another arrow.
        // We don't want the arrow to destroy itself on the player right after it spawns.
        if (other.CompareTag("Player") || other.GetComponent<Arrow>() != null)
        {
            // It hit the player or another arrow, so just ignore it and continue flying.
            return;
        }

        // If it's not a damageable enemy, not the player, and not another arrow,
        // it must have hit a wall or the ground. Destroy the arrow.
        Debug.Log($"Arrow hit a non-damageable object: '{other.gameObject.name}'. Destroying arrow.");
        Destroy(gameObject);
    }
}