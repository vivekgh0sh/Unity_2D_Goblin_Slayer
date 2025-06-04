// FallDetector.cs
using UnityEngine;

public class FallDetector : MonoBehaviour
{
    // This script should be placed on a large trigger collider below your playable level area.

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) // Make sure your Player GameObject is tagged "Player"
        {
            Player player = other.GetComponent<Player>();
            if (player != null)
            {
                // Check the public _IsDeath property (or a getter method) from Player.cs
                if (!player._IsDeath) // Accessing the public property
                {
                    Debug.Log("Player entered fall death trigger. Initiating death sequence.");
                    player.Die(); // Call the public Die method on the Player script
                }
            }
            else
            {
                Debug.LogWarning($"FallDetector collided with an object tagged 'Player' but couldn't find Player script on: {other.gameObject.name}");
            }
        }
    }

    // Optional: Visual gizmo to see the death zone in the editor
    void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f); // Semi-transparent red
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}