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
    public float pistolShoulderOffset = 0.85f;
    [Tooltip("How fast firing kick eases back out, in 1/seconds")]
    public float recoilRecovery = 9f;

    [Header("Smoothing")]
    public float smoothTime = 0.12f;

    [Header("Collision")]
    [Tooltip("Radius for camera collision spherecast (prevents clipping into trees/terrain)")]
    public float collisionRadius = 0.35f;
    [Tooltip("Layers to treat as obstacles for camera (default = everything)")]
    public LayerMask collisionMask = -1;

    private float currentYaw = 0f;
    private float currentPitch = 25f;
    private float recoilPitch;
    private float recoilYaw;
    private Vector3 currentVelocity = Vector3.zero;
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

        float recoilBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, recoilRecovery) * Time.deltaTime);
        recoilPitch = Mathf.Lerp(recoilPitch, 0f, recoilBlend);
        recoilYaw = Mathf.Lerp(recoilYaw, 0f, recoilBlend);

        // Positive pitch looks down. Recoil subtracts so a shot kicks the view up.
        float pitched = Mathf.Clamp(currentPitch - recoilPitch, minVerticalAngle, maxVerticalAngle);
        Quaternion orbit = Quaternion.Euler(pitched, currentYaw + recoilYaw, 0f);

        // The pistol keeps this same orbit and only slides it onto the character's right.
        // The look point takes a third of that slide, so the back sits left of center.
        Vector3 shift = ShoulderShift(pistolShoulderOffset * PistolPose());
        Vector3 pivot = target.position + Vector3.up * height + shift;
        Vector3 desiredPosition = pivot - (orbit * Vector3.forward * distance);
        desiredPosition = PushOutOfWalls(pivot, desiredPosition, distance);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);

        Vector3 lookAtCharacter = target.position + Vector3.up * 1.5f + shift * (1f / 3f);
        transform.LookAt(lookAtCharacter);
    }

    public void AddRecoil(float pitchDegrees, float yawDegrees)
    {
        recoilPitch = Mathf.Clamp(recoilPitch + pitchDegrees, 0f, 7f);
        recoilYaw = Mathf.Clamp(recoilYaw + yawDegrees, -3.5f, 3.5f);
    }

    private float PistolPose()
    {
        if (heldItems == null)
        {
            if (player == null && target != null)
                player = target.GetComponentInParent<PlayerController>();
            if (player != null)
                heldItems = player.GetComponent<HeldItemVisual>();
        }

        if (heldItems == null)
            return 0f;

        return Mathf.Clamp01(heldItems.PistolPoseWeight);
    }

    private Vector3 ShoulderShift(float meters)
    {
        if (Mathf.Abs(meters) < 0.001f)
            return Vector3.zero;

        Transform body = player != null ? player.transform : target;
        Vector3 right = Vector3.ProjectOnPlane(body.right, Vector3.up);
        if (right.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return right.normalized * meters;
    }

    private Vector3 PushOutOfWalls(Vector3 pivot, Vector3 desiredPosition, float camDistance)
    {
        Vector3 offset = desiredPosition - pivot;
        float dist = offset.magnitude;
        if (dist < 0.001f)
            return desiredPosition;

        Vector3 direction = offset / dist;
        RaycastHit[] hits = Physics.SphereCastAll(pivot, collisionRadius, direction, dist, collisionMask, QueryTriggerInteraction.Ignore);
        float nearest = dist;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || IsPlayer(hitTransform))
                continue;
            if (hits[i].distance < nearest)
                nearest = hits[i].distance;
        }

        if (nearest >= dist - 0.001f)
            return desiredPosition;

        float pulled = Mathf.Clamp(nearest - 0.2f, 0.45f, camDistance);
        return pivot + direction * pulled;
    }

    private bool IsPlayer(Transform hitTransform)
    {
        if (player == null)
            return false;
        return hitTransform == player.transform || hitTransform.IsChildOf(player.transform);
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

        // When the mouse is still, ease the body back inside the window instead of snapping.
        if (Mathf.Abs(yawDelta) < 0.02f)
        {
            float maxStep = 100f * Time.deltaTime;
            excess = Mathf.Clamp(excess, -maxStep, maxStep);
            clamped = next - excess;
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