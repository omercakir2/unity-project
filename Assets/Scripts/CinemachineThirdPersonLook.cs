using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse aim for Cinemachine Third Person Follow. The built-in follow places the
/// camera on the shoulder of whatever this transform is facing.
/// </summary>
[DefaultExecutionOrder(-50)]
public class CinemachineThirdPersonLook : MonoBehaviour, IInputAxisOwner
{
    [SerializeField] private float mouseGain = 0.45f;

    public InputAxis HorizontalLook = new InputAxis
    {
        Range = new Vector2(-180f, 180f),
        Wrap = true,
        Center = 0f,
        Value = 0f
    };

    public InputAxis VerticalLook = new InputAxis
    {
        Range = new Vector2(-55f, 65f),
        Wrap = false,
        Center = 16f,
        Value = 16f
    };

    private const string NormalCameraName = "Third Person Aim Camera";
    private const string PistolCameraName = "Pistol Aim Camera";
    private const float PistolOutThreshold = 0.05f;
    private const float SprintFovScale = 1.3f;
    private const float AimFovScale = 0.7f;
    private const float FovSmoothTime = 0.16f;

    private CinemachineInputAxisController axisController;
    private PlayerController player;
    private HeldItemVisual heldItems;
    private CinemachineCamera normalCamera;
    private CinemachineCamera pistolCamera;
    private InputActionReference lookReference;
    private InputAction aimAction;
    private bool lookBound;
    private float baseFov = 60f;
    private float currentFov = 60f;
    private float fovVelocity;
    private bool baseFovCaptured;

    void IInputAxisOwner.GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new IInputAxisOwner.AxisDescriptor
        {
            DrivenAxis = () => ref HorizontalLook,
            Name = "Horizontal Look",
            Hint = IInputAxisOwner.AxisDescriptor.Hints.X
        });
        axes.Add(new IInputAxisOwner.AxisDescriptor
        {
            DrivenAxis = () => ref VerticalLook,
            Name = "Vertical Look",
            Hint = IInputAxisOwner.AxisDescriptor.Hints.Y
        });
    }

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        DisableRecentering(ref HorizontalLook);
        DisableRecentering(ref VerticalLook);

        axisController = GetComponent<CinemachineInputAxisController>();
        if (axisController == null)
            axisController = gameObject.AddComponent<CinemachineInputAxisController>();
        axisController.SuppressInputWhileBlending = false;
        axisController.SynchronizeControllers();
    }

    private void Start()
    {
        BindLook();
        BindAim();
        AttachVirtualCamera();
        TagPlayerColliders();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        if (lookReference != null)
            Destroy(lookReference);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;
        if (!lookBound)
            BindLook();

        float pitch = Mathf.Clamp(VerticalLook.Value, VerticalLook.Range.x, VerticalLook.Range.y);
        VerticalLook.Value = pitch;
        float yaw = HorizontalLook.Value;

        bool holdBody = player != null && player.IsTraversing;
        if (!holdBody && player != null)
        {
            player.transform.Rotate(0f, yaw, 0f, Space.World);
            HorizontalLook.Value = 0f;
            yaw = 0f;
        }

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        UpdatePistolCamera();
        UpdateFieldOfView();
    }

    private void AttachVirtualCamera()
    {
        CinemachineThirdPersonFollow[] follows = FindObjectsByType<CinemachineThirdPersonFollow>(FindObjectsSortMode.None);
        for (int i = 0; i < follows.Length; i++)
        {
            CinemachineCamera vcam = follows[i].GetComponent<CinemachineCamera>();
            if (vcam == null)
                continue;
            vcam.Follow = transform;
            vcam.LookAt = transform;
            if (vcam.name == NormalCameraName)
                normalCamera = vcam;
            else if (vcam.name == PistolCameraName)
                pistolCamera = vcam;
        }
    }

    private void UpdatePistolCamera()
    {
        if (pistolCamera == null)
            return;

        if (heldItems == null && player != null)
            heldItems = player.GetComponent<HeldItemVisual>();

        bool aiming = heldItems != null && heldItems.PistolPoseWeight > PistolOutThreshold;
        pistolCamera.Priority = aiming ? 20 : 0;
        if (normalCamera != null)
            normalCamera.Priority = 10;
    }

    private void UpdateFieldOfView()
    {
        if (!baseFovCaptured)
        {
            CinemachineCamera source = normalCamera != null ? normalCamera : pistolCamera;
            if (source == null)
                return;

            float authored = source.Lens.FieldOfView;
            if (authored < 1f)
                return;

            baseFov = authored;
            currentFov = authored;
            baseFovCaptured = true;
        }

        // Aim replaces sprint. The two scales stay relative to the authored shot.
        float scale = 1f;
        if (AimingDownSights())
            scale = AimFovScale;
        else if (player != null && player.IsSprintingNow)
            scale = SprintFovScale;

        currentFov = Mathf.SmoothDamp(currentFov, baseFov * scale, ref fovVelocity, FovSmoothTime);
        ApplyFov(normalCamera, currentFov);
        ApplyFov(pistolCamera, currentFov);
    }

    private bool AimingDownSights()
    {
        if (heldItems == null || heldItems.PistolPoseWeight <= PistolOutThreshold)
            return false;

        if (aimAction == null)
            BindAim();
        if (aimAction != null && aimAction.IsPressed())
            return true;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.isPressed;
    }

    private static void ApplyFov(CinemachineCamera vcam, float fov)
    {
        if (vcam == null)
            return;

        LensSettings lens = vcam.Lens;
        lens.FieldOfView = fov;
        vcam.Lens = lens;
    }

    private void BindAim()
    {
        PlayerInput playerInput = player != null
            ? player.GetComponent<PlayerInput>()
            : FindAnyObjectByType<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
            return;

        aimAction = playerInput.actions.FindAction("Aim", false);
    }

    private void BindLook()
    {
        lookBound = false;
        if (axisController == null)
            return;

        axisController.SynchronizeControllers();
        PlayerInput playerInput = player != null
            ? player.GetComponent<PlayerInput>()
            : FindAnyObjectByType<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
            return;

        InputAction look = playerInput.actions.FindAction("Look", false);
        if (look == null)
            return;

        if (lookReference != null)
            Destroy(lookReference);
        lookReference = InputActionReference.Create(look);

        for (int i = 0; i < axisController.Controllers.Count; i++)
        {
            CinemachineInputAxisController.Controller controller = axisController.Controllers[i];
            if (controller.Name == "Horizontal Look")
                Arm(controller, mouseGain);
            else if (controller.Name == "Vertical Look")
                Arm(controller, -mouseGain);
            else
                controller.Enabled = false;
        }

        lookBound = true;
    }

    private void Arm(CinemachineInputAxisController.Controller controller, float gain)
    {
        controller.Enabled = true;
        controller.Input.InputAction = lookReference;
        controller.Input.Gain = gain;
        controller.Input.CancelDeltaTime = true;
        controller.Driver.AccelTime = 0f;
        controller.Driver.DecelTime = 0f;
    }

    private static void DisableRecentering(ref InputAxis axis)
    {
        InputAxis.RecenteringSettings recentering = axis.Recentering;
        recentering.Enabled = false;
        axis.Recentering = recentering;
    }

    private void TagPlayerColliders()
    {
        if (player == null)
            return;

        Collider[] cols = player.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null)
                continue;
            try
            {
                cols[i].gameObject.tag = "Player";
            }
            catch (UnityException)
            {
                return;
            }
        }
    }
}
