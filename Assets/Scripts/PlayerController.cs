using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField, Range(0.02f, 0.3f)] private float rotationSmoothTime = 0.08f;
    [SerializeField] private float gravity = -40f;

    [Header("References")]
    [SerializeField, Tooltip("Main Camera Transform (auto-assigned if not set)")]
    private Transform cameraTransform;
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector2 moveInput;
    private bool isSprinting;

    // Animator parameter hashes for performance
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isSprintingHash = Animator.StringToHash("IsSprinting");

    private float rotationVelocity;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("[PlayerController] CharacterController not found on this object!");
            return;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator != null)
                Debug.Log("[PlayerController] Animator automatically found on child: " + animator.gameObject.name);
            else
                Debug.LogError("[PlayerController] Animator could NOT be found on any child object!");
        }

        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                Debug.Log("[PlayerController] CameraTransform auto-assigned from Main Camera.");
            }
            else
            {
                Debug.LogWarning("[PlayerController] No Main Camera found!");
            }
        }

        Debug.Log("[PlayerController] ✅ Successfully initialized! Controller and camera ready.");
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        // For a holdable sprint button, we check if the action is currently active
        isSprinting = context.ReadValue<float>() > 0.5f;
    }

    private void Update()
    {
        HandleMovement();
        HandleGravity();
        HandleAnimation();

        // DEBUG - Remove after fixing
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[DEBUG] Grounded: {controller.isGrounded} | Velocity Y: {velocity.y:F2} | Position Y: {transform.position.y:F2}");
        }
    }

    private void HandleMovement()
    {
        if (cameraTransform == null)
        {
            Debug.LogWarning("CameraTransform is null - cannot move");
            return;
        }
        if (controller == null) return;

        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        float inputMagnitude = input.magnitude;

        if (inputMagnitude > 0.01f)
        {
            // === BETTER CAMERA-RELATIVE MOVEMENT (works with diagonal camera angles) ===
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 desiredMoveDir = (camForward * input.z + camRight * input.x).normalized;

            // Rotate character to face movement direction
            if (desiredMoveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Pow(0.1f, Time.deltaTime * (1f / rotationSmoothTime)));
            }

            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;
            velocity.x = desiredMoveDir.x * currentSpeed;
            velocity.z = desiredMoveDir.z * currentSpeed;
        }
        else
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }
    }

    private void HandleGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        // Apply movement (horizontal from HandleMovement + vertical gravity)
        controller.Move(velocity * Time.deltaTime);

        // Reset horizontal velocity after move (prevents sliding)
        if (controller.isGrounded)
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }
    }

    private void HandleAnimation()
    {
        if (animator == null) return;

        float inputMagnitude = moveInput.magnitude;
        bool isMoving = inputMagnitude > 0.1f;
        float targetSpeed = isMoving ? (isSprinting ? 1.0f : 0.5f) : 0f;

        animator.SetFloat(speedHash, targetSpeed, 0.1f, Time.deltaTime);
        animator.SetBool(isSprintingHash, isSprinting && isMoving);
    }
}
