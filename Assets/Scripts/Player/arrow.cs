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
            enabled = false; // Disable script if no Rigidbody
            return;
        }
        rb.linearVelocity = direction * speed; // Changed from linearVelocity to velocity
        Destroy(gameObject, lifetime);
        // Debug.Log($"Arrow fired. Direction: {direction}, Speed: {speed}, Damage: {damage}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // For debugging, always log what it collides with
        Debug.Log($"Arrow collided with: {other.gameObject.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");

        bool enemyHit = false;

        // --- START OF CORRECTED SECTION ---
        // 1. Try to get the SkeletonKnight component FIRST
        SkeletonKnight skeletonKnightEnemy = other.GetComponent<SkeletonKnight>();
        if (skeletonKnightEnemy != null)
        {
            Debug.Log($"'SkeletonKnight' component found on '{other.gameObject.name}' by arrow. Applying {damage} damage.");
            skeletonKnightEnemy.TakeDamage(damage); // Assuming SkeletonKnight.cs has TakeDamage(int)
            enemyHit = true;
        }
        else
        {
            // 2. If not SkeletonKnight, try to get the 'ganja' component
            ganja ganjaEnemy = other.GetComponent<ganja>();
            if (ganjaEnemy != null)
            {
                Debug.Log($"'ganja' component found on '{other.gameObject.name}' by arrow. Applying {damage} damage.");
                ganjaEnemy.TakeDamage(damage); // Assuming ganja.cs has TakeDamage(int)
                enemyHit = true;
            }
            else
            {
                // 3. If not 'ganja', try to get the generic 'Enemy' component
                Enemy genericEnemy = other.GetComponent<Enemy>();
                if (genericEnemy != null)
                {
                    Debug.Log($"Generic 'Enemy' component found on '{other.gameObject.name}' by arrow. Applying {damage} damage.");
                    genericEnemy.TakeDamage(damage); // Assuming Enemy.cs has TakeDamage(int)
                    enemyHit = true;
                }
            }
        }
        // --- END OF CORRECTED SECTION ---

        if (enemyHit)
        {
            Debug.Log($"Arrow hit an enemy ({other.gameObject.name}) and dealt damage. Destroying arrow.");
            Destroy(gameObject); // Destroy arrow after hitting any recognized enemy
            return; // Stop further processing for this collision
        }

        // If it's not an enemy, decide if it should be destroyed
        bool isPlayer = other.CompareTag("Player") || other.GetComponent<Player>() != null;
        bool isAnotherArrow = other.GetComponent<Arrow>() != null;

        if (!isPlayer && !isAnotherArrow)
        {
            // This log was the one you were seeing:
            Debug.Log($"Arrow hit non-enemy, non-player, non-arrow object: '{other.gameObject.name}'. Destroying arrow.");
            Destroy(gameObject);
        }
        else if (isPlayer)
        {
            // Debug.Log($"Arrow collided with Player: {other.gameObject.name}. Arrow not destroyed (to prevent self-destruction at spawn).");
        }
        else if (isAnotherArrow)
        {
            // Debug.Log($"Arrow collided with another Arrow: {other.gameObject.name}. Arrow not destroyed (arrows pass through each other).");
        }
    }
}