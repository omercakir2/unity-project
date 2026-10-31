using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("The target transform to follow (typically an empty 'CameraTarget' child of the player)")]
    public Transform target;
    
    [Tooltip("Camera offset position relative to the target's local rotation")]
    public Vector3 offset = new Vector3(0f, 2f, -5f);
    
    [Tooltip("How quickly the camera catches up to the target position")]
    [Range(5f, 30f)]
    public float smoothSpeed = 12f;

    private Vector3 currentVelocity = Vector3.zero;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        
        // Use SmoothDamp for more natural camera movement than Lerp
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref currentVelocity, 
            1f / smoothSpeed
        );
        
        // Look at a point slightly above the target for better framing
        Vector3 lookAtPoint = target.position + Vector3.up * 1.2f;
        transform.LookAt(lookAtPoint);
    }
}
