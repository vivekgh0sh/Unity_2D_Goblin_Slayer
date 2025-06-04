using UnityEngine;
using System.Collections.Generic; // For HashSet

public class PlayerAttackHitbox : MonoBehaviour
{
    public int damageAmount = 2; // Default damage for melee, can be set by Player script
    private Player _playerScript;
    private HashSet<Collider2D> _hitObjectsThisSwing; // To ensure one hit per enemy per swing

    void Awake()
    {
        _playerScript = GetComponentInParent<Player>();
        if (_playerScript == null)
        {
            // It's good practice to have this, but let's add a Debug.Log for clarity too.
            Debug.LogError("PlayerAttackHitbox cannot find Player script in parent!", gameObject);
        }
        _hitObjectsThisSwing = new HashSet<Collider2D>();
        // Collider should be disabled by default (managed by Player animations)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }
        else
        {
            Debug.LogError("PlayerAttackHitbox is missing its Collider2D component!", gameObject);
        }
        Debug.Log($"{gameObject.name} (Hitbox) Awake. Collider initially disabled."); // For debugging
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // For debugging, always log what it collides with
        Debug.Log($"{gameObject.name} (Hitbox) OnTriggerEnter2D with: {other.gameObject.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");

        if (_hitObjectsThisSwing.Contains(other))
        {
            Debug.Log($"{other.gameObject.name} already hit by this swing."); // For debugging
            return; // Already hit this object in the current swing
        }

        // --- START OF MODIFIED SECTION ---
        bool damageDealt = false;

        // First, try to get the Ganja component
        ganja ganjaEnemy = other.GetComponent<ganja>();
        if (ganjaEnemy != null)
        {
            Debug.Log($"Ganja component found on '{other.gameObject.name}'. Applying {damageAmount} damage.");
            ganjaEnemy.TakeDamage(damageAmount); // Call TakeDamage on Ganja script
            _hitObjectsThisSwing.Add(other); // Register as hit
            damageDealt = true;
        }
        else
        {
            // If not Ganja, try to get the generic Enemy component (for your older enemies)
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                Debug.Log($"Generic Enemy component found on '{other.gameObject.name}'. Applying {damageAmount} damage.");
                enemy.TakeDamage(damageAmount); // Call TakeDamage on generic Enemy script
                _hitObjectsThisSwing.Add(other); // Register as hit
                damageDealt = true;
            }
        }

        if (!damageDealt)
        {
            Debug.LogWarning($"No Ganja or Enemy component found on '{other.gameObject.name}'. No damage dealt by melee from {gameObject.name}.");
        }
        // --- END OF MODIFIED SECTION ---
    }

    // Called by Player script (via animation event) to enable the hitbox
    public void Activate(int dmg)
    {
        damageAmount = dmg;
        _hitObjectsThisSwing.Clear(); // Clear previously hit objects for the new swing
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
            Debug.Log($"{gameObject.name} (Hitbox) Activated with {dmg} damage. Collider enabled."); // For debugging
        }
    }

    // Called by Player script (via animation event) to disable the hitbox
    public void Deactivate()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
            Debug.Log($"{gameObject.name} (Hitbox) Deactivated. Collider disabled."); // For debugging
        }
    }
}