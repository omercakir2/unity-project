using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The CameraTarget child object inside the Player prefab")]
    [SerializeField] private Transform target;

    [Header("Camera Settings")]
    [Tooltip("Distance from the target (increased for forest view and idle comfort)")]
    public float distance = 8.5f;  // Larger default so camera isn't too close when idle
    [Tooltip("Height offset from target")]
    public float height = 4f;
    [Tooltip("How sensitive the mouse rotation is")]
    public float rotationSpeed = 120f;
    [Tooltip("Minimum vertical angle (down)")]
    public float minVerticalAngle = -30f;
    [Tooltip("Maximum vertical angle (up)")]
    public float maxVerticalAngle = 60f;

    [Header("Smoothing")]
    public float smoothTime = 0.12f;

    [Header("Collision")]
    [Tooltip("Radius for camera collision spherecast (prevents clipping into trees/terrain)")]
    public float collisionRadius = 0.35f;
    [Tooltip("Layers to treat as obstacles for camera (default = everything)")]
    public LayerMask collisionMask = -1;

    private float currentYaw = 0f;
    private float currentPitch = 25f; // Slightly higher default for better forest overview
    private Vector3 currentVelocity = Vector3.zero;
    private Vector2 lookInput;

    private void Start()
    {
        if (target == null)
        {
            // Auto-find CameraTarget inside Player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform.Find("CameraTarget");
            }

            if (target == null)
            {
                target = FindAnyObjectByType<PlayerController>()?.transform.Find("CameraTarget");
            }

            if (target == null)
            {
                Debug.LogWarning("ThirdPersonCamera: Could not find CameraTarget automatically. Please assign it manually.");
                return;
            }

            Debug.Log("ThirdPersonCamera: Target automatically assigned -> " + target.name);
        }

        // Initial position behind the player
        currentYaw = target.eulerAngles.y;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Apply mouse rotation
        currentYaw += lookInput.x * rotationSpeed * Time.deltaTime;
        currentPitch -= lookInput.y * rotationSpeed * Time.deltaTime;
        currentPitch = Mathf.Clamp(currentPitch, minVerticalAngle, maxVerticalAngle);

        // Calculate desired camera position
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 desiredPosition = target.position + rotation * new Vector3(0, height, -distance);

        // Camera collision avoidance (critical for forest with many trees)
        RaycastHit hit;
        Vector3 direction = (desiredPosition - target.position).normalized;
        float targetDistance = distance;
        if (Physics.SphereCast(target.position + Vector3.up * 0.5f, collisionRadius, direction, out hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            targetDistance = hit.distance - 0.2f; // Pull camera in
            desiredPosition = target.position + direction * targetDistance;
        }

        // Smooth movement
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref currentVelocity, 
            smoothTime
        );

        // Always look at target (slightly above for better framing)
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    // Optional: Lock cursor when right mouse is held (you can call this from PlayerInput)
    public void OnMouseLookToggle(InputAction.CallbackContext context)
    {
        if (context.performed)
            Cursor.lockState = CursorLockMode.Locked;
        else if (context.canceled)
            Cursor.lockState = CursorLockMode.None;
    }
}
