using UnityEngine;

public class MovingPlatform2D : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    public float moveSpeed = 2f;

    [HideInInspector]
    public bool playerOnPlatform = false;

    void Update()
    {
        Vector3 target = playerOnPlatform ? endPoint.position : startPoint.position;
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
    }
}
