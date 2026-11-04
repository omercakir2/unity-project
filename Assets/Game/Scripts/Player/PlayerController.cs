using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField, Range(0.02f, 0.3f)] private float rotationSmoothTime = 0.08f;

    [Header("Vault / Climb (LedgeMarker)")]
    [Tooltip("How far from the feet a marker edge can be and still be grabbed.")]
    [SerializeField] private float ledgeReach = 1.1f;
    [Tooltip("How far off the character's facing a marker edge can be, in degrees.")]
    [SerializeField] private float ledgeMaxAngle = 60f;
    [Tooltip("Climb Up ledges up to this fraction of the controller height use the waist-high climb; taller ones jump to a hang first.")]
    [SerializeField] private float waistHeightFactor = 0.65f;
    [Tooltip("Highest edge the character can reach, as a multiple of the controller height.")]
    [SerializeField] private float climbMaxHeightFactor = 2f;

    [Header("Stairs")]
    [Tooltip("Uphill is the tagged object's forward, flattened on the ground.")]
    [SerializeField] private string stairsTag = "Stairs";

    [Header("Physics")]
    [SerializeField] private float gravity = -40f;
    [SerializeField] private LayerMask traversalMask = ~0;

    [Header("Animation Blend")]
    [SerializeField] private float speedBlendStart = 0.22f;
    [SerializeField] private float speedBlendStop = 0.48f;
    [SerializeField] private float tiredBlend = 0.55f;

    [Header("References")]
    [SerializeField, Tooltip("Main Camera Transform (auto-assigned if not set)")]
    private Transform cameraTransform;
    [SerializeField] private Animator animator;

    [SerializeField] private CharacterController controller;
    private Vector3 velocity;
    private Vector2 moveInput;
    private bool isSprinting;
    private bool initialized;
    private Vector3 worldMoveDir;

    private PlayerVitals vitals;
    private HeldItemVisual heldItems;

    private CharacterController Controller
    {
        get
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
            return controller;
        }
    }

    public bool IsSprintingNow =>
        traversal == TraversalKind.None && Exerting && (IsOnStairs() || MostlyForward(worldMoveDir));
    public bool IsMoving => initialized && moveInput.magnitude > 0.1f;
    public bool IsGrounded => initialized && Controller != null && Controller.isGrounded;
    public bool IsTraversing => traversal != TraversalKind.None;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;

    private bool sprintRecoverLatch;

    private bool Exerting
    {
        get
        {
            if (!initialized || moveInput.magnitude <= 0.1f || !isSprinting)
            {
                sprintRecoverLatch = false;
                return false;
            }

            // Stay in the walk until Shift is released, so a full bar does not
            // tick back under 20% on the next sprint frame.
            if (vitals != null && !vitals.CanSprint)
                sprintRecoverLatch = true;
            if (sprintRecoverLatch)
                return false;

            return true;
        }
    }

    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int moveXHash = Animator.StringToHash("MoveX");
    private readonly int moveYHash = Animator.StringToHash("MoveY");
    private readonly int locomotionRateHash = Animator.StringToHash("LocomotionRate");
    private readonly int tiredHash = Animator.StringToHash("Tired");
    private readonly int pistolRateHash = Animator.StringToHash("PistolRate");
    private readonly int isSprintingHash = Animator.StringToHash("IsSprinting");
    private readonly int climbHash = Animator.StringToHash("Climb");
    private readonly int onStairsHash = Animator.StringToHash("OnStairs");
    private readonly int stairDirectionHash = Animator.StringToHash("StairDirection");
    private readonly int walkUpStairsHash = Animator.StringToHash("Walk Up Stairs");
    private readonly int runUpStairsHash = Animator.StringToHash("Run Up Stairs");
    private readonly int descendStairsHash = Animator.StringToHash("Descend Stairs");

    private float visualSpeed;
    private float visualMoveX;
    private float visualMoveY;
    private float visualTired;
    private float visualTiredVelocity;
    private float planarSpeed;
    private float planarSpeedVelocity;
    private float blendAngle;
    private float blendAngleVelocity;
    private float blendMagnitude;
    private float blendMagnitudeVelocity;
    private Vector3 lastPlanarDir = Vector3.forward;
    private const float GaitSmooth = 0.08f;

    private enum TraversalKind { None, Climb, Vault, HangUp, Drop }

    private const string ClimbStateName = "Climb Over";
    private const string VaultStateName = "Vault Over";
    private const string HangStateName = "Hang Up";
    private const string LandStateName = "Hard Landing";

    // Normalized times in "Vault Over" (Vault Over Box): hands reach the top, leave it,
    // the feet are down on the far side, and the clip is standing again.
    private const float VaultEntryTime = 0.1f;
    private const float VaultPlantTime = 0.18f;
    private const float VaultReleaseTime = 0.3f;
    private const float VaultLandTime = 0.42f;
    private const float VaultExitTime = 0.5f;

    // Climb Over leaves the crouch on top for the drop this far through "Climb Over",
    // before that state blends back to locomotion.
    private const float ClimbOverDropTime = 0.88f;
    // "Hard Landing" starts about 2 m up, is on the ground here, and stands up by the exit.
    private const float DropLandTime = 0.25f;
    private const float DropExitTime = 0.9f;

    // The hands leave the hips and reach for the ledge between these times in "Hang Up".
    private const float HangReachStart = 0.28f;
    private const float HangReachEnd = 0.5f;

    private struct Ledge
    {
        public float Height;
        public float Top;
        public Vector3 Lip;
        public Vector3 Into;
        public Vector3 TopFeet;
        public Vector3 FarFeet;
    }
    private TraversalKind traversal;
    private bool inspectLocked;

    public bool IsInspecting => inspectLocked;

    public void SetInspectLock(bool locked)
    {
        inspectLocked = locked;
        if (!locked)
            return;

        moveInput = Vector2.zero;
        isSprinting = false;
        velocity.x = 0f;
        velocity.z = 0f;
        planarSpeed = 0f;
        planarSpeedVelocity = 0f;
        visualSpeed = 0f;
        if (animator == null)
            return;

        animator.SetFloat(speedHash, 0f);
        animator.SetFloat(moveXHash, 0f);
        animator.SetFloat(moveYHash, 0f);
        animator.SetBool(isSprintingHash, false);
        animator.ResetTrigger(climbHash);
    }
    private float traversalStartTime;
    private float traversalDuration;
    private Vector3 traversalStart;
    private Vector3 traversalEnd;
    private float traversalClearance;
    private Vector3 traversalFacing;

    private Transform stairRoot;
    private float stairContactTimer;
    private bool stairPoseLocked;
    private bool stairMetricsValid;
    private Transform measuredStairRoot;
    private float stairTreadSpacing = 0.35f;
    private float stairRiser = 0.2f;
    private float stairFrontAlong;
    private readonly StairClipMatch walkStairMatch = new StairClipMatch();
    private readonly StairClipMatch runStairMatch = new StairClipMatch();
    private readonly StairClipMatch descendStairMatch = new StairClipMatch();
    private bool stairClipsBaked;
    private bool samplingStairClip;

    private Transform leftHand;
    private Transform rightHand;
    private Transform leftFingertip;
    private Transform rightFingertip;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform leftToes;
    private Transform rightToes;
    private Vector3 climbLip;
    private Vector3 climbLandingFeet;
    private Vector3 climbMountFeet;
    private float climbObstacleTop;
    private float climbGroundY;
    private Vector3 vaultCarry;
    private bool vaultReleased;
    private bool climbOverPending;
    private Vector3 dropFarFeet;
    private float vaultLandY;

    private int ProbeMask => traversalMask.value == 0 ? Physics.DefaultRaycastLayers : traversalMask.value;

    private void Awake()
    {
        if (controller == null)
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
            {
                animator.applyRootMotion = false;
                Debug.Log("[PlayerController] Animator automatically found on child: " + animator.gameObject.name);
            }
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

        vitals = GetComponent<PlayerVitals>();
        CacheLimbs();
        if (animator != null)
        {
            animator.SetFloat(locomotionRateHash, 1f);
            animator.SetFloat(pistolRateHash, 1f);
        }
        if (animator != null && animator.GetComponent<StairFootIk>() == null)
            animator.gameObject.AddComponent<StairFootIk>();
        WeaponHandIk.Ensure(animator);
        if (animator != null && animator.GetComponent<HeadLook>() == null)
            animator.gameObject.AddComponent<HeadLook>();
        if (GetComponent<RainFootsteps>() == null)
            gameObject.AddComponent<RainFootsteps>();
        if (GetComponent<PlayerBreath>() == null)
            gameObject.AddComponent<PlayerBreath>();
        if (GetComponent<CorpseInspect>() == null)
            gameObject.AddComponent<CorpseInspect>();
        initialized = true;
        Debug.Log("[PlayerController] Successfully initialized.");
    }

    public void RecacheLimbs()
    {
        leftHand = null;
        rightHand = null;
        leftFingertip = null;
        rightFingertip = null;
        leftFoot = null;
        rightFoot = null;
        leftToes = null;
        rightToes = null;
        stairClipsBaked = false;
        walkStairMatch.ready = false;
        walkStairMatch.plants.Clear();
        runStairMatch.ready = false;
        runStairMatch.plants.Clear();
        descendStairMatch.ready = false;
        descendStairMatch.plants.Clear();

        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            if (Application.isPlaying && animator.GetComponent<StairFootIk>() == null)
                animator.gameObject.AddComponent<StairFootIk>();
            if (Application.isPlaying)
                WeaponHandIk.Ensure(animator);
            if (Application.isPlaying && animator.GetComponent<HeadLook>() == null)
                animator.gameObject.AddComponent<HeadLook>();
        }

        CacheLimbs();
    }

    private void CacheLimbs()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string bone = all[i].name;
            if (bone.EndsWith("LeftHandIndex4")) leftFingertip = all[i];
            else if (bone.EndsWith("RightHandIndex4")) rightFingertip = all[i];
            else if (bone.EndsWith("LeftHand")) leftHand = all[i];
            else if (bone.EndsWith("RightHand")) rightHand = all[i];
            else if (bone.EndsWith("LeftFoot")) leftFoot = all[i];
            else if (bone.EndsWith("RightFoot")) rightFoot = all[i];
        }

        if (animator != null)
        {
            if (leftFingertip == null)
                leftFingertip = animator.GetBoneTransform(HumanBodyBones.LeftIndexDistal);
            if (rightFingertip == null)
                rightFingertip = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
            if (leftFoot == null)
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (rightFoot == null)
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
        }
    }

    private bool TryCameraPlanar(out Vector3 forward, out Vector3 right)
    {
        if (cameraTransform == null)
        {
            forward = Vector3.zero;
            right = Vector3.zero;
            return false;
        }

        forward = cameraTransform.forward;
        forward.y = 0f;
        right = cameraTransform.right;
        right.y = 0f;
        if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.zero;
            right = Vector3.zero;
            return false;
        }

        forward.Normalize();
        right.Normalize();
        return true;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!initialized) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (!initialized) return;
        isSprinting = context.ReadValue<float>() > 0.5f;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!initialized || !context.performed) return;
        TryPerformJump();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!initialized || !context.performed) return;

        Debug.Log("[PlayerController] Interact performed.");
    }

    private void Update()
    {
        if (!initialized) return;

        if (inspectLocked)
        {
            velocity.x = 0f;
            velocity.z = 0f;
            planarSpeed = 0f;
            HandleGravity();
            return;
        }

        // Traversals place the body from its bones in LateUpdate.
        if (traversal != TraversalKind.None)
        {
            UpdateClimbFacing();
            return;
        }

        RefreshStairs();
        HandleMovement();
        HandleGravity();
        HandleStairs();
        HandleAnimation();
    }

    private void LateUpdate()
    {
        if (!initialized || inspectLocked) return;

        EnsureAnimator();

        if (traversal == TraversalKind.Vault)
        {
            ApplyVaultWarp();
            return;
        }

        if (traversal == TraversalKind.HangUp)
        {
            ApplyHangWarp();
            return;
        }

        if (traversal == TraversalKind.Climb)
        {
            float elapsed = Time.time - traversalStartTime;
            float climbTime = Mathf.Clamp01(elapsed / Mathf.Max(0.4f, traversalDuration));
            ApplyClimbWarp(climbTime);
            if (climbOverPending && climbTime >= ClimbOverDropTime)
                BeginDrop();
            else if (climbTime >= 0.98f || elapsed > traversalDuration + 0.35f)
                FinishTraversal();
            return;
        }

        if (traversal == TraversalKind.Drop)
        {
            ApplyDropWarp();
            return;
        }

        SyncStairPhase();
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (traversal != TraversalKind.None) return;
        if (hit.normal.y < 0.5f) return;

        Transform root = FindStairsRoot(hit.collider.transform);
        if (root == null) return;

        stairRoot = root;
        stairContactTimer = 0.2f;
    }

    private void RefreshStairs()
    {
        if (Controller == null) return;

        Vector3 origin = FeetWorld() + Vector3.up * 0.3f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.8f, ProbeMask, QueryTriggerInteraction.Ignore))
            return;

        Transform root = FindStairsRoot(hit.collider.transform);
        if (root == null) return;

        stairRoot = root;
        stairContactTimer = 0.2f;
    }

    /// <summary>
    /// The vault or climb Space would start right now, if any. Drives the on-screen prompt.
    /// </summary>
    public bool TryGetAvailableTraversal(out LedgeType type)
    {
        type = LedgeType.Vault;
        return CanStartTraversal() && TryGetLedge(out _, out type);
    }

    private bool CanStartTraversal()
    {
        if (!initialized || inspectLocked) return false;
        if (Controller == null || !Controller.isGrounded) return false;
        if (traversal != TraversalKind.None) return false;
        return vitals == null || vitals.CanJump;
    }

    private void TryPerformJump()
    {
        if (!CanStartTraversal()) return;

        // There is no free jump: Space only vaults or climbs at a LedgeMarker.
        if (!TryGetLedge(out Ledge ledge, out LedgeType type))
            return;

        vitals?.SpendJumpEnergy();
        switch (type)
        {
            case LedgeType.Vault:
                BeginVault(ledge);
                break;
            case LedgeType.ClimbUp:
                BeginLedgeClimb(ledge, ledge.Height <= Controller.height * waistHeightFactor);
                break;
            case LedgeType.ClimbOver:
                BeginLedgeClimb(ledge, ledge.Height <= Controller.height * waistHeightFactor);
                climbOverPending = true;
                dropFarFeet = ledge.FarFeet;
                break;
        }
    }

    private void HandleMovement()
    {
        worldMoveDir = Vector3.zero;

        if (cameraTransform == null)
        {
            Debug.LogWarning("CameraTransform is null - cannot move");
            return;
        }
        if (Controller == null) return;

        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        float inputMagnitude = Mathf.Clamp01(input.magnitude);
        bool onStairs = false;
        float targetSpeed = 0f;

        if (inputMagnitude > 0.01f)
        {
            // The pistol pose faces the aim, so movement stays on the body.
            // Otherwise WASD follows the Cinemachine camera.
            bool aimOwnsFacing = PistolAimOwnsFacing();
            Vector3 moveForward;
            Vector3 moveRight;
            if (aimOwnsFacing || !TryCameraPlanar(out moveForward, out moveRight))
            {
                moveForward = aimOwnsFacing ? transform.forward : cameraTransform.forward;
                moveRight = aimOwnsFacing ? transform.right : cameraTransform.right;
            }

            moveForward.y = 0f;
            moveRight.y = 0f;
            if (moveForward.sqrMagnitude < 0.0001f)
                moveForward = Vector3.forward;
            if (moveRight.sqrMagnitude < 0.0001f)
                moveRight = Vector3.right;
            moveForward.Normalize();
            moveRight.Normalize();

            Vector3 desiredMoveDir = (moveForward * input.z + moveRight * input.x).normalized;
            if (desiredMoveDir.sqrMagnitude > 0.01f)
                lastPlanarDir = desiredMoveDir;

            // Stairs still turn toward travel so the stair clips stay aligned.
            // Off the stairs, WASD does not yaw the body. Sideways and backward
            // input plays the strafe and walk-back clips instead.
            if (IsOnStairs() && desiredMoveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Pow(0.1f, Time.deltaTime * (1f / rotationSmoothTime)));
            }

            onStairs = IsOnStairs();
            if (onStairs)
                EnsureStairMetrics();
            float gait = onStairs
                ? StairMatchedSpeed()
                : (MostlyForward(desiredMoveDir) && Exerting ? sprintSpeed : walkSpeed);
            targetSpeed = gait * inputMagnitude;
        }

        // Ease the body in and out with the step cycle. A hard start at full
        // speed plays idle feet while the capsule is already gliding.
        if (onStairs)
        {
            planarSpeed = targetSpeed;
            planarSpeedVelocity = 0f;
        }
        else
            planarSpeed = Mathf.SmoothDamp(planarSpeed, targetSpeed, ref planarSpeedVelocity, GaitSmooth);

        if (planarSpeed > 0.02f && lastPlanarDir.sqrMagnitude > 0.01f)
        {
            worldMoveDir = lastPlanarDir;
            velocity.x = lastPlanarDir.x * planarSpeed;
            velocity.z = lastPlanarDir.z * planarSpeed;
        }
        else
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }
    }

    private void HandleGravity()
    {
        if (IsOnStairs() && StairDirectionValue() < 0f)
        {
            float horizontal = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            velocity.y = -horizontal * (stairRiser / Mathf.Max(0.15f, stairTreadSpacing));
        }
        else if (IsOnStairs())
        {
            if (Controller.isGrounded && velocity.y < 0f)
                velocity.y = -2f;
            else if (velocity.y < -3.5f)
                velocity.y = -3.5f;
            else if (!Controller.isGrounded)
                velocity.y += gravity * Time.deltaTime;
        }
        else if (Controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        Controller.Move(velocity * Time.deltaTime);

        if (Controller.isGrounded)
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }
    }

    private void HandleStairs()
    {
        if (stairContactTimer > 0f)
            stairContactTimer -= Time.deltaTime;

        if (animator == null) return;

        bool onStairs = IsOnStairs();
        float direction = onStairs ? StairDirectionValue() : 0f;

        animator.SetBool(onStairsHash, onStairs);
        animator.SetFloat(stairDirectionHash, direction);
    }

    private void HandleAnimation()
    {
        if (animator == null) return;

        bool isMoving = moveInput.magnitude > 0.1f || planarSpeed > 0.35f;
        bool sprinting = Exerting;
        if (walkSpeed > 0.01f && planarSpeed > 0.05f)
            visualSpeed = Mathf.Clamp(planarSpeed / walkSpeed, 0f, sprintSpeed / Mathf.Max(0.01f, walkSpeed)) * 0.5f;
        else
            visualSpeed = 0f;
        animator.SetFloat(speedHash, visualSpeed);
        animator.SetBool(isSprintingHash, sprinting);

        Vector2 moveBlend = LocomotionBlend(isMoving);
        float targetMag = moveBlend.magnitude;
        float targetAngle = targetMag > 0.001f ? Mathf.Atan2(moveBlend.x, moveBlend.y) * Mathf.Rad2Deg : blendAngle;
        blendAngle = Mathf.SmoothDampAngle(blendAngle, targetAngle, ref blendAngleVelocity, GaitSmooth);
        // Enter the step at full clip weight so idle does not dilute the feet.
        // Leave it smoothly once the body has nearly stopped.
        if (targetMag > 0.2f && blendMagnitude < 0.2f)
        {
            blendMagnitude = targetMag;
            blendMagnitudeVelocity = 0f;
        }
        else
            blendMagnitude = Mathf.SmoothDamp(blendMagnitude, targetMag, ref blendMagnitudeVelocity, GaitSmooth);
        float rad = blendAngle * Mathf.Deg2Rad;
        visualMoveX = Mathf.Sin(rad) * blendMagnitude;
        visualMoveY = Mathf.Cos(rad) * blendMagnitude;
        animator.SetFloat(moveXHash, visualMoveX);
        animator.SetFloat(moveYHash, visualMoveY);

        // Move speed only picks the walk or run pose. Playback stays at the clip rate.
        animator.SetFloat(locomotionRateHash, 1f);
        animator.SetFloat(pistolRateHash, 1f);

        bool energyLow = vitals != null && vitals.IsEnergyLow;
        visualTired = Mathf.SmoothDamp(visualTired, energyLow ? 1f : 0f, ref visualTiredVelocity, tiredBlend);
        animator.SetFloat(tiredHash, visualTired);
    }

    private bool MostlyForward(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return false;

        float along = Vector3.Dot(direction, FlatForward());
        float side = Vector3.Dot(direction, FlatRight());
        return along > 0.65f && along >= Mathf.Abs(side);
    }

    private Vector2 LocomotionBlend(bool stepping)
    {
        if (!stepping || worldMoveDir.sqrMagnitude < 0.01f)
            return Vector2.zero;

        float along = Vector3.Dot(worldMoveDir, FlatForward());
        float side = Vector3.Dot(worldMoveDir, FlatRight());
        Vector2 local = new Vector2(side, along);
        if (local.sqrMagnitude < 0.0001f)
            return Vector2.zero;
        local.Normalize();

        // Walk, strafe, and back sit on the 0.5 ring. Forward sprint opens that
        // out to the run clip as the body actually speeds up.
        float radius = 0.5f;
        if (along > 0.65f && planarSpeed > walkSpeed && sprintSpeed > walkSpeed)
            radius = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(walkSpeed, sprintSpeed, planarSpeed));
        return local * radius;
    }

    private Vector3 FlatRight()
    {
        Vector3 right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            return Vector3.Cross(Vector3.up, FlatForward());
        return right.normalized;
    }

    private bool PistolAimOwnsFacing()
    {
        if (IsOnStairs())
            return false;

        if (heldItems == null)
            heldItems = GetComponent<HeldItemVisual>();
        return heldItems != null && heldItems.PistolPoseWeight > 0.001f;
    }

    // True while stair contact is still counted. The camera keeps its free orbit then.
    public bool IsOnStairs()
    {
        return Controller != null && stairRoot != null && stairContactTimer > 0f;
    }

    private Transform FindStairsRoot(Transform node)
    {
        while (node != null)
        {
            if (node.CompareTag(stairsTag))
                return node;
            node = node.parent;
        }
        return null;
    }

    private Vector3 FeetWorld()
    {
        return transform.position + transform.TransformVector(Controller.center) - Vector3.up * (Controller.height * 0.5f);
    }

    private Vector3 TransformFromFeet(Vector3 feetWorld)
    {
        return transform.position + (feetWorld - FeetWorld());
    }

    private Vector3 FlatForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;
        return forward.normalized;
    }

    private void BeginVault(Ledge ledge)
    {
        climbLip = ledge.Lip;
        climbObstacleTop = ledge.Top;
        climbGroundY = FeetWorld().y;
        vaultReleased = false;
        vaultLandY = transform.position.y + (ledge.FarFeet.y - climbGroundY);
        BeginTraversal(TraversalKind.Vault, TransformFromFeet(ledge.FarFeet), ledge.Height + 0.15f, VaultStateName, 0, VaultEntryTime);
        Debug.Log("[PlayerController] Vault performed.");
    }

    // Waist-high ledges go straight into the braced-hang climb; taller ones jump up to the hang first.
    private void BeginLedgeClimb(Ledge ledge, bool fromWaist)
    {
        climbOverPending = false;
        climbLip = ledge.Lip;
        climbLandingFeet = ledge.TopFeet;
        climbObstacleTop = ledge.Top;
        climbGroundY = FeetWorld().y;
        float footLift = 0.17f;
        if (leftFoot != null && rightFoot != null)
        {
            float boneY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            float lift = boneY - climbGroundY;
            if (lift > 0.02f && lift < 0.45f)
                footLift = lift;
        }
        climbMountFeet = ledge.TopFeet + Vector3.up * footLift;

        Vector3 end = TransformFromFeet(ledge.TopFeet);
        if (fromWaist)
        {
            BeginTraversal(TraversalKind.Climb, end, ledge.Height + 0.15f, ClimbStateName, climbHash);
            Debug.Log("[PlayerController] Climb over performed.");
        }
        else
        {
            BeginTraversal(TraversalKind.HangUp, end, ledge.Height + 0.15f, HangStateName, 0);
            Debug.Log("[PlayerController] Ledge grab performed.");
        }
    }

    private bool TryGetLedge(out Ledge ledge, out LedgeType type)
    {
        ledge = default;
        type = LedgeType.Vault;

        var cc = Controller;
        Vector3 feet = FeetWorld();
        if (!LedgeMarker.TryFindNearest(feet, FlatForward(), ledgeReach, cc.height * climbMaxHeightFactor, ledgeMaxAngle, out LedgeMarker.Grab grab))
            return false;

        type = grab.Type;
        Vector3 into = grab.Into;
        ledge.Lip = grab.Lip + into * 0.05f;
        ledge.Top = grab.Lip.y;
        ledge.Height = grab.Lip.y - feet.y;
        ledge.Into = into;

        // Crouch spot on top, just past the lip, but not off the far side of a thin top.
        Vector3 topFeet = grab.Lip + into * Mathf.Min(cc.radius + 0.35f, grab.Depth * 0.5f);
        ledge.TopFeet = GroundBelow(topFeet + Vector3.up * 0.3f, 0.6f, topFeet);

        // The vault clip comes down about 1.2 m past the hands; the hard landing about 1 m past the top.
        float farDistance = type == LedgeType.ClimbOver ? grab.Depth * 0.5f + 1f : 1.2f;
        Vector3 farFeet = grab.Lip + into * Mathf.Max(grab.Depth + cc.radius + 0.1f, farDistance);
        farFeet.y = feet.y;
        ledge.FarFeet = GroundBelow(farFeet + Vector3.up * (ledge.Height + 0.3f), ledge.Height + 2f, farFeet);
        return true;
    }

    private Vector3 GroundBelow(Vector3 origin, float distance, Vector3 fallback)
    {
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, ProbeMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return fallback;
    }

    // trigger 0 cross-fades straight into the clipName state, starting startNormalized into it.
    private void BeginTraversal(TraversalKind kind, Vector3 endPosition, float clearance, string clipName, int trigger, float startNormalized = 0f)
    {
        traversal = kind;
        traversalStart = transform.position;
        traversalEnd = endPosition;
        traversalClearance = clearance;
        traversalStartTime = Time.time;
        traversalDuration = ClipDuration(clipName, 1.13f);
        traversalFacing = traversalEnd - traversalStart;
        traversalFacing.y = 0f;
        if (traversalFacing.sqrMagnitude < 0.0001f)
            traversalFacing = FlatForward();
        else
            traversalFacing.Normalize();

        velocity = Vector3.zero;
        planarSpeed = 0f;
        planarSpeedVelocity = 0f;
        blendMagnitude = 0f;
        blendMagnitudeVelocity = 0f;
        stairContactTimer = 0f;
        stairRoot = null;
        ReleaseStairPose();
        if (traversalFacing.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(traversalFacing);
        // Keep the motion from sticking to the ground or catching the obstacle.
        Controller.enabled = false;

        if (animator != null)
        {
            animator.speed = 1f;
            animator.applyRootMotion = false;
            animator.SetBool(onStairsHash, false);
            animator.SetFloat(stairDirectionHash, 0f);
            animator.ResetTrigger(climbHash);
            if (trigger != 0)
                animator.SetTrigger(trigger);
            else
                animator.CrossFadeInFixedTime(clipName, 0.15f, 0, startNormalized * traversalDuration);
        }
    }

    private void UpdateClimbFacing()
    {
        if (traversalFacing.sqrMagnitude < 0.001f) return;
        Quaternion targetRotation = Quaternion.LookRotation(traversalFacing);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Pow(0.05f, Time.deltaTime));
    }

    private void FinishTraversal()
    {
        if (traversal == TraversalKind.Climb || traversal == TraversalKind.HangUp)
            AlignClimbToLanding();
        if (!Controller.enabled)
            Controller.enabled = true;

        // A long downward move falls through the thin ground plane.
        Vector3 feet = FeetWorld() + Vector3.up * 0.05f;
        if (Physics.Raycast(feet, Vector3.down, out RaycastHit ground, 0.4f, ProbeMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 settle = TransformFromFeet(ground.point) - transform.position;
            if (settle.y < 0f && settle.y > -0.3f)
                Controller.Move(settle);
        }

        traversal = TraversalKind.None;
        velocity = Vector3.zero;
        velocity.y = -2f;
        planarSpeed = 0f;
        planarSpeedVelocity = 0f;
    }

    private bool TryGetStateNormalized(string stateName, out float normalized)
    {
        normalized = 0f;
        if (animator == null) return false;

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsTag("Action") && next.IsName(stateName))
            {
                normalized = next.normalizedTime;
                return true;
            }
        }

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsTag("Action") && info.IsName(stateName))
        {
            normalized = info.normalizedTime;
            return true;
        }

        return false;
    }

    private void ApplyClimbWarp(float t)
    {
        if (leftHand == null || rightHand == null || leftFoot == null || rightFoot == null)
        {
            transform.position = EvaluateClimb(t);
            return;
        }

        if (!TryGetStateNormalized(ClimbStateName, out float poseT))
            return;

        poseT = Mathf.Repeat(poseT, 1f);
        float poseWeight = PoseWeight();
        float lowestFoot = LowestFootOffset();
        Vector3 held = HeldOnLip(lowestFoot);

        // The braced hang keeps the fingertips on the lip while the body pulls up.
        // After that, the clip climbs over the edge and the feet settle on the landing.
        Vector3 target = held;
        if (poseT > 0.62f)
        {
            Vector3 footMid = (leftFoot.position + rightFoot.position) * 0.5f;
            Vector3 footOffset = footMid - transform.position;
            Vector3 mounted = climbMountFeet - footOffset;
            float blend = Mathf.SmoothStep(0.62f, 0.96f, poseT);
            target = Vector3.Lerp(held, mounted, blend);
            float plantedFoot = target.y + lowestFoot;
            if (plantedFoot < climbGroundY + 0.02f)
                target.y += climbGroundY + 0.02f - plantedFoot;
        }

        transform.position = Vector3.Lerp(transform.position, target, poseWeight);
    }

    private bool HasWarpBones => leftHand != null && rightHand != null && leftFoot != null && rightFoot != null;

    // Blends the warp in over the cross-fade, so the body does not jump on the first frame.
    private float PoseWeight()
    {
        return animator != null && animator.IsInTransition(0)
            ? animator.GetAnimatorTransitionInfo(0).normalizedTime
            : 1f;
    }

    private float LowestFootOffset()
    {
        return Mathf.Min(leftFoot.position.y, rightFoot.position.y) - transform.position.y;
    }

    // Transform position that puts the fingertips on the lip, without pushing the feet into the ground.
    private Vector3 HeldOnLip(float lowestFoot)
    {
        Transform gripL = leftFingertip != null ? leftFingertip : leftHand;
        Transform gripR = rightFingertip != null ? rightFingertip : rightHand;
        Vector3 gripMid = (gripL.position + gripR.position) * 0.5f;
        Vector3 held = climbLip - (gripMid - transform.position);

        float minHeldY = climbGroundY + 0.02f - lowestFoot;
        if (held.y < minHeldY)
            held.y = minHeldY;
        return held;
    }

    private void ApplyVaultWarp()
    {
        float elapsed = Time.time - traversalStartTime;
        if (!HasWarpBones || !TryGetStateNormalized(VaultStateName, out float poseT))
        {
            if (elapsed > 0.3f)
                FinishTraversal();
            return;
        }

        if (poseT >= VaultExitTime || elapsed > traversalDuration)
        {
            FinishTraversal();
            return;
        }

        float poseWeight = PoseWeight();
        Vector3 target;
        if (poseT < VaultReleaseTime)
        {
            // Run in to the obstacle and keep the planted hands on its top edge.
            Vector3 held = HeldOnLip(LowestFootOffset());
            target = Vector3.Lerp(traversalStart, held, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VaultEntryTime, VaultPlantTime, poseT)));
        }
        else
        {
            // Off the hands: carry the body over to the landing spot the probe checked is clear.
            if (!vaultReleased)
            {
                vaultReleased = true;
                vaultCarry = transform.position;
            }

            float land = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VaultReleaseTime, VaultLandTime, poseT));
            target = Vector3.Lerp(vaultCarry, traversalEnd, land);
            target.y = Mathf.Lerp(vaultCarry.y, vaultLandY, land);
        }

        transform.position = Vector3.Lerp(transform.position, target, poseWeight);
    }

    private void ApplyHangWarp()
    {
        float elapsed = Time.time - traversalStartTime;

        // "Hang Up" hands over to "Climb Over", which pulls the body onto the ledge.
        if (TryGetStateNormalized(ClimbStateName, out _))
        {
            traversal = TraversalKind.Climb;
            traversalStartTime = Time.time;
            traversalDuration = ClipDuration(ClimbStateName, 1.13f);
            ApplyClimbWarp(0f);
            return;
        }

        if (!HasWarpBones || elapsed > traversalDuration + 0.6f)
        {
            FinishTraversal();
            return;
        }

        if (!TryGetStateNormalized(HangStateName, out float poseT))
            return;

        float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HangReachStart, HangReachEnd, poseT));
        Vector3 target = Vector3.Lerp(traversalStart, HeldOnLip(LowestFootOffset()), reach);
        transform.position = Vector3.Lerp(transform.position, target, PoseWeight());
    }

    // Climb Over: from the crouch on top, drop off the far side.
    private void BeginDrop()
    {
        climbOverPending = false;
        traversal = TraversalKind.Drop;
        traversalStart = transform.position;
        traversalEnd = TransformFromFeet(dropFarFeet);
        traversalStartTime = Time.time;
        traversalDuration = ClipDuration(LandStateName, 2.5f);
        if (animator != null)
            animator.CrossFadeInFixedTime(LandStateName, 0.15f, 0, 0f);
        Debug.Log("[PlayerController] Climb over drop performed.");
    }

    private void ApplyDropWarp()
    {
        float elapsed = Time.time - traversalStartTime;
        if (!HasWarpBones || !TryGetStateNormalized(LandStateName, out float poseT))
        {
            if (elapsed > 0.4f)
                FinishTraversal();
            return;
        }

        if (poseT >= DropExitTime || elapsed > traversalDuration + 0.3f)
        {
            FinishTraversal();
            return;
        }

        float fall = Mathf.InverseLerp(0f, DropLandTime, poseT);
        Vector3 target = Vector3.Lerp(traversalStart, traversalEnd, Mathf.SmoothStep(0f, 1f, fall));
        if (fall < 1f)
        {
            // The feet come down from the fence top to the far ground, speeding up like a fall.
            float feetY = Mathf.Lerp(climbObstacleTop, dropFarFeet.y, fall * fall);
            target.y = feetY + 0.02f - LowestFootOffset();
        }
        else
        {
            target.y = traversalEnd.y;
        }

        transform.position = Vector3.Lerp(transform.position, target, PoseWeight());
    }

    private float StairMatchedSpeed()
    {
        bool down = StairDirectionValue() < 0f;
        bool runningUp = !down && Exerting;
        string clipName = down ? "Descend Stairs" : runningUp ? "Run Up Stairs" : "Walk Up Stairs";
        float fallback = down ? 0.933f : runningUp ? 0.6f : 1.2f;
        float cycle = ClipDuration(clipName, fallback);
        // Each stair clip is two footfalls. One footfall advances one tread, at the clip's own pace.
        return Mathf.Max(0.15f, stairTreadSpacing) * 2f / Mathf.Max(0.2f, cycle);
    }

    private float StairDirectionValue()
    {
        if (stairRoot == null || worldMoveDir.sqrMagnitude <= 0.01f)
            return 0f;

        Vector3 uphill = stairRoot.forward;
        uphill.y = 0f;
        if (uphill.sqrMagnitude < 0.0001f)
            return 0f;

        uphill.Normalize();
        float dot = Vector3.Dot(worldMoveDir, uphill);
        if (dot > 0.35f) return 1f;
        if (dot < -0.35f) return -1f;
        return 0f;
    }

    private void EnsureAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.GetComponent<StairFootIk>() == null)
            animator.gameObject.AddComponent<StairFootIk>();
        WeaponHandIk.Ensure(animator);
    }

    private void SyncStairPhase()
    {
        EnsureAnimator();
        if (animator == null) return;
        if (!IsOnStairs() || stairRoot == null || worldMoveDir.sqrMagnitude <= 0.01f)
        {
            ReleaseStairPose();
            return;
        }

        EnsureStairMetrics();
        float direction = animator.GetFloat(stairDirectionHash);
        if (Mathf.Abs(direction) < 0.5f)
        {
            ReleaseStairPose();
            return;
        }

        int state = direction > 0f
            ? (Exerting ? runUpStairsHash : walkUpStairsHash)
            : descendStairsHash;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0) || info.shortNameHash != state)
        {
            ReleaseStairPose();
            return;
        }

        Vector3 uphill = stairRoot.forward;
        uphill.y = 0f;
        if (uphill.sqrMagnitude < 0.0001f)
            uphill = Vector3.forward;
        else
            uphill.Normalize();

        float face = Vector3.Dot(FlatForward(), uphill);
        if (Mathf.Abs(face) < 0.5f)
        {
            ReleaseStairPose();
            return;
        }

        if (!stairClipsBaked)
            BakeStairClips();

        float facingUp = face >= 0f ? 1f : -1f;
        float rootAlong = Vector3.Dot(transform.position - stairRoot.position, uphill);
        StairClipMatch match = state == runUpStairsHash
            ? runStairMatch
            : state == descendStairsHash ? descendStairMatch : walkStairMatch;
        float phase = match.ready && match.plants.Count >= 1
            ? PhaseForFeetOnTreads(match, rootAlong, facingUp)
            : FallbackStairPhase(rootAlong, facingUp, state);

        animator.speed = 0f;
        stairPoseLocked = true;
        animator.Play(state, 0, phase);
        animator.Update(0f);
    }

    private void BakeStairClips()
    {
        EnsureAnimator();
        if (animator == null) return;

        float savedSpeed = animator.speed;
        samplingStairClip = true;
        BakeStairClip(walkStairMatch, walkUpStairsHash);
        BakeStairClip(runStairMatch, runUpStairsHash);
        BakeStairClip(descendStairMatch, descendStairsHash);
        samplingStairClip = false;
        animator.speed = savedSpeed;
        stairClipsBaked = true;
    }

    private void BakeStairClip(StairClipMatch match, int stateHash)
    {
        match.ready = false;
        match.drop = 0f;
        match.plants.Clear();
        if (leftFoot == null || rightFoot == null) return;

        const int sampleCount = 48;
        var leftHeight = new float[sampleCount];
        var rightHeight = new float[sampleCount];
        var leftZ = new float[sampleCount];
        var rightZ = new float[sampleCount];

        animator.speed = 0f;
        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != stateHash)
            return;

        for (int i = 0; i < sampleCount; i++)
        {
            float phase = i / (float)sampleCount;
            animator.Play(stateHash, 0, phase);
            animator.Update(0f);
            SampleFoot(leftToes != null ? leftToes : leftFoot, out leftHeight[i], out leftZ[i]);
            SampleFoot(rightToes != null ? rightToes : rightFoot, out rightHeight[i], out rightZ[i]);
        }

        var found = new System.Collections.Generic.List<StairPlant>(4);
        CollectPlants(found, leftHeight, leftZ, sampleCount);
        CollectPlants(found, rightHeight, rightZ, sampleCount);
        if (found.Count == 0) return;

        float lowest = found[0].height;
        for (int i = 1; i < found.Count; i++)
            if (found[i].height < lowest) lowest = found[i].height;

        found.Sort((a, b) => a.phase.CompareTo(b.phase));
        for (int i = 0; i < found.Count; i++)
        {
            if (found[i].height > lowest + 0.04f) continue;
            if (match.plants.Count > 0)
            {
                StairPlant previous = match.plants[match.plants.Count - 1];
                float gap = Mathf.Repeat(found[i].phase - previous.phase, 1f);
                if (gap < 0.12f && found[i].height >= previous.height)
                    continue;
            }

            match.plants.Add(found[i]);
            if (match.plants.Count == 4) break;
        }

        if (match.plants.Count == 0) return;
        match.drop = 0f;
        match.ready = true;
    }

    private void SampleFoot(Transform bone, out float height, out float localZ)
    {
        height = bone.position.y - FeetWorld().y;
        localZ = transform.InverseTransformPoint(bone.position).z;
    }

    private static void CollectPlants(System.Collections.Generic.List<StairPlant> plants, float[] height, float[] localZ, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            int previous = (i + sampleCount - 1) % sampleCount;
            int next = (i + 1) % sampleCount;
            bool isLowest = height[i] <= height[previous] && height[i] <= height[next]
                && (height[i] < height[previous] || height[i] < height[next]);
            if (!isLowest) continue;

            plants.Add(new StairPlant
            {
                phase = i / (float)sampleCount,
                localZ = localZ[i],
                height = height[i]
            });
        }
    }

    public void UpdateStairFeet(Animator ikAnimator)
    {
        if (ikAnimator == null)
            return;

        if (samplingStairClip || inspectLocked || !IsOnStairs() || stairRoot == null || traversal != TraversalKind.None)
        {
            ikAnimator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            ikAnimator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            ikAnimator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            ikAnimator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            return;
        }

        float capsuleFeet = FeetWorld().y;
        PlantStairFoot(ikAnimator, AvatarIKGoal.LeftFoot, leftToes != null ? leftToes : leftFoot, capsuleFeet);
        PlantStairFoot(ikAnimator, AvatarIKGoal.RightFoot, rightToes != null ? rightToes : rightFoot, capsuleFeet);
    }

    private void PlantStairFoot(Animator ikAnimator, AvatarIKGoal goal, Transform contactBone, float capsuleFeet)
    {
        if (contactBone == null)
        {
            ikAnimator.SetIKPositionWeight(goal, 0f);
            ikAnimator.SetIKRotationWeight(goal, 0f);
            return;
        }

        Vector3 contact = contactBone.position;
        float aboveCapsule = contact.y - capsuleFeet;
        float weight = Mathf.Clamp01(1f - Mathf.InverseLerp(0.1f, 0.22f, aboveCapsule));
        if (weight <= 0.001f)
        {
            ikAnimator.SetIKPositionWeight(goal, 0f);
            ikAnimator.SetIKRotationWeight(goal, 0f);
            return;
        }

        Vector3 origin = contact + Vector3.up * 0.45f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, ProbeMask, QueryTriggerInteraction.Ignore)
            || FindStairsRoot(hit.collider.transform) == null)
        {
            ikAnimator.SetIKPositionWeight(goal, 0f);
            ikAnimator.SetIKRotationWeight(goal, 0f);
            return;
        }

        // Phase lock already puts the foot on the right tread. IK only settles the sole on that surface.
        Vector3 foot = ikAnimator.GetIKPosition(goal);
        float sole = Mathf.Clamp(foot.y - contact.y, 0.02f, 0.22f);
        Vector3 target = foot;
        target.y = hit.point.y + sole + 0.005f;
        ikAnimator.SetIKPositionWeight(goal, weight);
        ikAnimator.SetIKRotationWeight(goal, weight);
        ikAnimator.SetIKPosition(goal, target);
        ikAnimator.SetIKRotation(goal, ikAnimator.GetIKRotation(goal));
    }

    private float PhaseForFeetOnTreads(StairClipMatch match, float rootAlong, float facingUp)
    {
        int count = match.plants.Count;
        float spacing = Mathf.Max(0.15f, stairTreadSpacing);
        float center0 = stairFrontAlong + spacing * 0.5f;
        StairPlant first = match.plants[0];
        float rootAtPlant = center0 - first.localZ * facingUp;
        float span = count * spacing;
        float rel = Mathf.Repeat((rootAlong - rootAtPlant) * facingUp, span);

        float traveled = 0f;
        for (int i = 0; i < count; i++)
        {
            StairPlant current = match.plants[i];
            StairPlant next = match.plants[(i + 1) % count];
            float stepDist = spacing + current.localZ - next.localZ;
            if (stepDist < 0.05f) stepDist = spacing;
            float phaseSpan = Mathf.Repeat(next.phase - current.phase, 1f);
            if (phaseSpan < 0.0001f) phaseSpan = 1f;

            if (rel <= traveled + stepDist || i == count - 1)
            {
                float blend = Mathf.Clamp01((rel - traveled) / stepDist);
                return Mathf.Repeat(current.phase + phaseSpan * blend, 1f);
            }

            traveled += stepDist;
        }

        return Mathf.Repeat(first.phase, 1f);
    }

    private float FallbackStairPhase(float rootAlong, float facingUp, int state)
    {
        float footBack = state == runUpStairsHash ? 0.03f : state == descendStairsHash ? -0.05f : 0.06f;
        float spacing = Mathf.Max(0.15f, stairTreadSpacing);
        float along = rootAlong - footBack * facingUp - stairFrontAlong;
        float stepPos = along / spacing;
        if (state == descendStairsHash)
            return Mathf.Repeat(0.875f - stepPos, 1f);

        float phase = Mathf.Repeat(stepPos * 0.5f, 1f);
        if (state == runUpStairsHash)
            phase = Mathf.Repeat(phase + 0.15f - 0.5f, 1f);
        return phase;
    }

    private void AlignClimbToLanding()
    {
        if (animator != null)
        {
            animator.speed = 1f;
            animator.ResetTrigger(climbHash);
            visualSpeed = 0f;
            planarSpeed = 0f;
            planarSpeedVelocity = 0f;
            blendMagnitude = 0f;
            blendMagnitudeVelocity = 0f;
            animator.SetFloat(speedHash, 0f);
            animator.SetFloat(moveXHash, 0f);
            animator.SetFloat(moveYHash, 0f);
            animator.SetFloat(locomotionRateHash, 1f);
            animator.CrossFadeInFixedTime("Blend Tree", 0.3f, 0, 0f);
        }

        Vector3 standOn = climbLandingFeet;
        if (Physics.Raycast(standOn + Vector3.up * 0.4f, Vector3.down, out RaycastHit ground, 1.5f, ProbeMask, QueryTriggerInteraction.Ignore))
            standOn = ground.point;

        Vector3 delta = standOn - FeetWorld();
        if (delta.sqrMagnitude < 9f)
            transform.position += delta;
    }

    private void ReleaseStairPose()
    {
        if (!stairPoseLocked || animator == null) return;
        animator.speed = 1f;
        stairPoseLocked = false;
    }

    private bool TryMeasureStepsFromRenderers(Vector3 uphill)
    {
        float[] centers = new float[24];
        float[] tops = new float[24];
        int count = 0;
        for (int i = 0; i < stairRoot.childCount && count < centers.Length; i++)
        {
            Renderer rend = stairRoot.GetChild(i).GetComponent<Renderer>();
            if (rend == null)
                continue;

            Vector3 size = rend.bounds.size;
            float depth = Mathf.Abs(uphill.x) * size.x + Mathf.Abs(uphill.z) * size.z;
            if (depth < 0.15f || depth > 0.8f)
                continue;

            centers[count] = Vector3.Dot(rend.bounds.center - stairRoot.position, uphill);
            tops[count] = rend.bounds.max.y;
            count++;
        }

        if (count < 3)
            return false;

        for (int i = 1; i < count; i++)
        {
            float center = centers[i];
            float top = tops[i];
            int j = i;
            while (j > 0 && centers[j - 1] > center)
            {
                centers[j] = centers[j - 1];
                tops[j] = tops[j - 1];
                j--;
            }

            centers[j] = center;
            tops[j] = top;
        }

        float sum = 0f;
        float riserSum = 0f;
        int spacingCount = 0;
        for (int i = 1; i < count; i++)
        {
            float spacing = centers[i] - centers[i - 1];
            float riser = tops[i] - tops[i - 1];
            if (spacing > 0.15f && spacing < 0.8f && riser > 0.05f && riser < 0.5f)
            {
                sum += spacing;
                riserSum += riser;
                spacingCount++;
            }
        }

        if (spacingCount < 2)
            return false;

        stairTreadSpacing = sum / spacingCount;
        stairRiser = Mathf.Clamp(riserSum / spacingCount, 0.05f, 0.45f);
        stairFrontAlong = centers[0] - stairTreadSpacing * 0.5f;
        return true;
    }

    private void EnsureStairMetrics()
    {
        if (stairRoot == null) return;
        if (stairMetricsValid && measuredStairRoot == stairRoot) return;

        measuredStairRoot = stairRoot;
        stairMetricsValid = true;
        stairTreadSpacing = 0.35f;
        stairFrontAlong = 0f;

        Vector3 uphill = stairRoot.forward;
        uphill.y = 0f;
        if (uphill.sqrMagnitude < 0.0001f)
            return;
        uphill.Normalize();
        if (TryMeasureStepsFromRenderers(uphill))
            return;

        const int count = 180;
        const float step = 0.05f;
        const float start = -1f;
        var height = new float[count];
        var hit = new bool[count];
        int mask = ProbeMask;
        for (int i = 0; i < count; i++)
        {
            float distance = start + i * step;
            Vector3 origin = stairRoot.position + uphill * distance + Vector3.up * 5f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit rh, 10f, mask, QueryTriggerInteraction.Ignore))
                continue;
            if (FindStairsRoot(rh.collider.transform) != stairRoot)
                continue;
            hit[i] = true;
            height[i] = rh.point.y;
        }

        float spacingSum = 0f;
        float riserSum = 0f;
        int spacingCount = 0;
        float firstFront = 0f;
        bool haveFront = false;
        float prevFront = 0f;
        float prevHeight = 0f;
        bool havePrev = false;

        int index = 0;
        while (index < count)
        {
            if (!hit[index])
            {
                index++;
                continue;
            }

            int end = index;
            while (end + 1 < count && hit[end + 1] && Mathf.Abs(height[end + 1] - height[index]) < 0.03f)
                end++;

            float front = start + index * step;
            float depth = (end - index) * step + step;
            float y = height[index];
            bool stepLike = depth >= 0.18f && depth <= 0.8f;
            if (stepLike && havePrev && y > prevHeight + 0.08f && y < prevHeight + 0.45f)
            {
                float spacing = front - prevFront;
                if (spacing > 0.15f && spacing < 0.9f)
                {
                    spacingSum += spacing;
                    riserSum += y - prevHeight;
                    spacingCount++;
                    if (!haveFront)
                    {
                        firstFront = prevFront;
                        haveFront = true;
                    }
                }
            }

            if (stepLike)
            {
                prevFront = front;
                prevHeight = y;
                havePrev = true;
            }
            else
            {
                havePrev = false;
            }

            index = end + 1;
        }

        if (spacingCount >= 2)
        {
            stairTreadSpacing = spacingSum / spacingCount;
            stairRiser = Mathf.Clamp(riserSum / spacingCount, 0.05f, 0.45f);
            stairFrontAlong = firstFront;
        }
    }

    private Vector3 EvaluateClimb(float t)
    {
        // Cross after the body is already up, then come down on the far side.
        float cross = Mathf.SmoothStep(0.28f, 0.72f, t);
        float height = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * traversalClearance;
        Vector3 pos = Vector3.Lerp(traversalStart, traversalEnd, cross);
        pos.y = Mathf.Lerp(traversalStart.y, traversalEnd.y, cross) + height;
        return pos;
    }

    private float ClipDuration(string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return fallback;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clipName)
                return Mathf.Max(0.2f, clips[i].length);
        }

        return fallback;
    }

    private struct StairPlant
    {
        public float phase;
        public float localZ;
        public float height;
    }

    private sealed class StairClipMatch
    {
        public bool ready;
        public float drop;
        public readonly System.Collections.Generic.List<StairPlant> plants = new System.Collections.Generic.List<StairPlant>(4);
    }
}
