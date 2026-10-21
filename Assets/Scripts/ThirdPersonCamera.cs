using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
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
    [Tooltip("How far the view may swing to either side of a straight rear view. Further horizontal look turns the character.")]
    public float maxYawFromBehind = 135f;
    [Tooltip("How far the view shifts onto the character's right while the pistol pose is fully in")]
    public float pistolShoulderOffset = 0.55f;

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
    private PlayerController player;
    private HeldItemVisual heldItems;

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
            player = target.GetComponentInParent<PlayerController>();
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
        if (Time.timeScale <= 0f) return;
        if (target == null) return;

        // Input System Action Map kullanılıyorsa buradan da okunabilir
        if (lookAction != null)
        {
            lookInput = lookAction.ReadValue<Vector2>();
        }

        // Fare hareketini uygula (Hassasiyet için Time.deltaTime kullanılabilir veya bağımsız tutulabilir)
        float yawDelta = lookInput.x * rotationSpeed * 0.02f;
        currentPitch -= lookInput.y * rotationSpeed * 0.02f;
        currentPitch = Mathf.Clamp(currentPitch, minVerticalAngle, maxVerticalAngle);
        ApplyYawLimit(yawDelta);

        // Fare girdisini işledikten hemen sonra sıfırlıyoruz ki durduğunda kamera kaymasın
        // (Eğer OnLook kullanıyorsanız ve bu satır girdiyi kesiyorsa silebilirsiniz, ancak Update tabanlı okuma için önemlidir)
        // lookInput = Vector2.zero; 

        // Hedef rotasyon ve pozisyon
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 targetFocusPosition = target.position + Vector3.up * height + PistolShoulderShift();
        
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

        // Kameranın hedefe bakması. The shoulder shift keeps the player left of the crosshair.
        transform.LookAt(target.position + Vector3.up * 1.5f + PistolShoulderShift());
    }

    private Vector3 PistolShoulderShift()
    {
        if (heldItems == null)
        {
            if (player == null && target != null)
                player = target.GetComponentInParent<PlayerController>();
            if (player != null)
                heldItems = player.GetComponent<HeldItemVisual>();
        }

        if (heldItems == null || pistolShoulderOffset <= 0f)
            return Vector3.zero;

        float weight = heldItems.PistolPoseWeight;
        if (weight <= 0.001f)
            return Vector3.zero;

        Transform body = player != null ? player.transform : target;
        Vector3 right = Vector3.ProjectOnPlane(body.right, Vector3.up);
        if (right.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return right.normalized * (pistolShoulderOffset * weight);
    }

    private void ApplyYawLimit(float yawDelta)
    {
        if (player == null && target != null)
            player = target.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            currentYaw += yawDelta;
            return;
        }

        // Add the mouse delta onto the current offset. DeltaAngle alone would
        // flip a fast swing that crosses 180° onto the opposite side.
        float characterYaw = player.transform.eulerAngles.y;
        float relative = Mathf.DeltaAngle(characterYaw, currentYaw);
        float limit = Mathf.Max(0f, maxYawFromBehind);
        float next = relative + yawDelta;
        float clamped = Mathf.Clamp(next, -limit, limit);
        float excess = next - clamped;

        // Climb and jump-down own facing. Extra mouse is dropped so the view stays beside them.
        if (player.IsTraversing)
        {
            currentYaw = characterYaw + clamped;
            return;
        }

        if (Mathf.Abs(excess) < 0.01f)
            excess = 0f;
        else
            player.transform.Rotate(0f, excess, 0f, Space.World);

        currentYaw = characterYaw + excess + clamped;
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