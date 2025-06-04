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
        rb.linearVelocity = direction * speed; // Using velocity is generally preferred over linearVelocity for direct setting
        Destroy(gameObject, lifetime);
        // Debug.Log($"Arrow fired. Direction: {direction}, Speed: {speed}, Damage: {damage}"); // For testing
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // For debugging, always log what it collides with
        Debug.Log($"Arrow collided with: {other.gameObject.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");

        bool enemyHit = false;

        // --- START OF MODIFIED SECTION ---
        // First, try to get the 'ganja' component (assuming class name is 'ganja')
        ganja ganjaEnemy = other.GetComponent<ganja>(); // IMPORTANT: Class name 'ganja' all lowercase
        if (ganjaEnemy != null)
        {
            Debug.Log($"'ganja' component found on '{other.gameObject.name}' by arrow. Applying {damage} damage.");
            ganjaEnemy.TakeDamage(damage); // Assuming ganja.cs has TakeDamage(int)
            enemyHit = true;
        }
        else
        {
            // If not 'ganja', try to get the generic 'Enemy' component (for your older enemies)
            Enemy genericEnemy = other.GetComponent<Enemy>();
            if (genericEnemy != null)
            {
                Debug.Log($"Generic 'Enemy' component found on '{other.gameObject.name}' by arrow. Applying {damage} damage.");
                genericEnemy.TakeDamage(damage); // Assuming Enemy.cs has TakeDamage(int)
                enemyHit = true;
            }
        }
        // --- END OF MODIFIED SECTION ---

        if (enemyHit)
        {
            Debug.Log($"Arrow hit an enemy ({other.gameObject.name}) and dealt damage. Destroying arrow.");
            Destroy(gameObject); // Destroy arrow after hitting any recognized enemy
            return; // Stop further processing for this collision
        }

        // If it's not an enemy, decide if it should be destroyed
        // (e.g., on hitting a wall or anything that's not the player or another arrow)
        // This logic helps prevent the arrow from being destroyed if it briefly touches the player who fired it at spawn.
        bool isPlayer = other.CompareTag("Player") || other.GetComponent<Player>() != null;
        bool isAnotherArrow = other.GetComponent<Arrow>() != null;

        if (!isPlayer && !isAnotherArrow)
        {
            // You might want to add more specific checks here, e.g., only destroy on objects with a "Wall" tag or on a specific "Environment" layer.
            // For now, it destroys on anything that's not the player or another arrow.
            Debug.Log($"Arrow hit non-enemy, non-player, non-arrow object: '{other.gameObject.name}'. Destroying arrow.");
            Destroy(gameObject);
        }
        else if (isPlayer)
        {
            Debug.Log($"Arrow collided with Player: {other.gameObject.name}. Arrow not destroyed (to prevent self-destruction at spawn).");
        }
        else if (isAnotherArrow)
        {
            Debug.Log($"Arrow collided with another Arrow: {other.gameObject.name}. Arrow not destroyed (arrows pass through each other).");
            // If you want arrows to destroy each other, you'd add Destroy(gameObject) here.
        }
    }
}