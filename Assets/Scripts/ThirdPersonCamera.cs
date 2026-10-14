using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The CameraTarget child object inside the Player prefab")]
    [SerializeField] private Transform target;

    [Header("Camera Settings")]
    [Tooltip("Distance from the target")]
    public float distance = 5f;
    [Tooltip("Height offset from target")]
    public float height = 2f;
    [Tooltip("How sensitive the mouse rotation is")]
    public float rotationSpeed = 120f;
    [Tooltip("Minimum vertical angle (down)")]
    public float minVerticalAngle = -30f;
    [Tooltip("Maximum vertical angle (up)")]
    public float maxVerticalAngle = 60f;

    [Header("Smoothing")]
    public float smoothTime = 0.12f;

    private float currentYaw = 0f;
    private float currentPitch = 20f;
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

        // Smooth movement
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref currentVelocity, 
            smoothTime
        );

        // Always look at target
        transform.LookAt(target.position + Vector3.up * 1.2f);
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
