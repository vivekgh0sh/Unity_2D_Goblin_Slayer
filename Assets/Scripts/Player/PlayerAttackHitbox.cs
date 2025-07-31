// PlayerAttackHitbox.cs (IMPROVED VERSION)

using UnityEngine;
using System.Collections.Generic; // For HashSet

public class PlayerAttackHitbox : MonoBehaviour
{
    public int damageAmount = 2;
    private HashSet<Collider2D> _hitObjectsThisSwing;

    void Awake()
    {
        _hitObjectsThisSwing = new HashSet<Collider2D>();
        GetComponent<Collider2D>().enabled = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore if we've already hit this object during this swing
        if (_hitObjectsThisSwing.Contains(other))
        {
            return;
        }

        // Check if the object we hit can take damage by looking for the IDamageable interface
        IDamageable damageableObject = other.GetComponent<IDamageable>();

        if (damageableObject != null)
        {
            // If it can, deal damage to it!
            Debug.Log($"Hit an IDamageable object: '{other.gameObject.name}'. Applying {damageAmount} damage.");
            damageableObject.TakeDamage(damageAmount);

            // Add it to the list of things we've hit on this swing
            _hitObjectsThisSwing.Add(other);
        }
    }

    public void Activate(int dmg)
    {
        damageAmount = dmg;
        _hitObjectsThisSwing.Clear();
        GetComponent<Collider2D>().enabled = true;
    }

    public void Deactivate()
    {
        GetComponent<Collider2D>().enabled = false;
    }
}