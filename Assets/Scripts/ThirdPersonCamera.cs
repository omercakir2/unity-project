using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The CameraTarget child object inside the Player prefab")]
    [SerializeField] private Transform target;

    [Header("Camera Settings")]
    [Tooltip("Distance from the target (increased for forest view and idle comfort)")]
    public float distance = 8.5f;  
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
    private float currentPitch = 25f; 
    private Vector3 currentVelocity = Vector3.zero; // Veya Vector3.zero
    private Vector2 lookInput;
    private InputAction lookAction;

    private void Start()
    {
        if (target == null)
        {
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
        }

        // Look aksiyonunu güvenli bir şekilde bulma
        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            lookAction = playerInput.actions.FindAction("Look");
        }

        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
        }

        // Fareyi kilitleyelim
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Input System Action Map kullanılıyorsa buradan da okunabilir
        if (lookAction != null)
        {
            lookInput = lookAction.ReadValue<Vector2>();
        }

        // Fare hareketini uygula (Hassasiyet için Time.deltaTime kullanılabilir veya bağımsız tutulabilir)
        currentYaw += lookInput.x * rotationSpeed * 0.02f; // Mouse delta değerleri genelde deltaTime gerektirmez ama hassasiyete göre ayarlanabilir
        currentPitch -= lookInput.y * rotationSpeed * 0.02f;
        currentPitch = Mathf.Clamp(currentPitch, minVerticalAngle, maxVerticalAngle);

        // Fare girdisini işledikten hemen sonra sıfırlıyoruz ki durduğunda kamera kaymasın
        // (Eğer OnLook kullanıyorsanız ve bu satır girdiyi kesiyorsa silebilirsiniz, ancak Update tabanlı okuma için önemlidir)
        // lookInput = Vector2.zero; 

        // Hedef rotasyon ve pozisyon
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 targetFocusPosition = target.position + Vector3.up * height;
        
        // Kamera ideal pozisyonu
        Vector3 desiredPosition = targetFocusPosition - (rotation * Vector3.forward * distance);

        // Kamera Çarpışma Kontrolü (Camera Collision)
        float targetDistance = distance;
        Vector3 directionToCamera = (desiredPosition - targetFocusPosition).normalized;

        if (Physics.SphereCast(targetFocusPosition, collisionRadius, directionToCamera, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Clamp(hit.distance - 0.2f, 0.5f, distance);
            desiredPosition = targetFocusPosition + (directionToCamera * targetDistance);
        }

        // Yumuşak geçiş (SmoothDamp)
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);

        // Kameranın hedefe bakması
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    public void OnMouseLookToggle(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (context.canceled)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}