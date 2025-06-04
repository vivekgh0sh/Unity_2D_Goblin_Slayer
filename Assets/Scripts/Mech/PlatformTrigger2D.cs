using UnityEngine;

public class PlatformTrigger2D : MonoBehaviour
{
    private MovingPlatform2D platform;

    void Start()
    {
        platform = GetComponentInParent<MovingPlatform2D>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            platform.playerOnPlatform = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            platform.playerOnPlatform = false;
        }
    }
}
