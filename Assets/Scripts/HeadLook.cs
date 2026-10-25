using UnityEngine;

/// <summary>
/// Adds a small neck and head turn toward the camera look direction.
/// Runs after <see cref="ThirdPersonCamera"/> so it uses this frame's view.
/// </summary>
[DefaultExecutionOrder(220)]
public class HeadLook : MonoBehaviour
{
    [SerializeField] private float yawShare = 0.5f;
    [SerializeField] private float pitchShare = 0.45f;
    [SerializeField] private float maxYaw = 42f;
    [SerializeField] private float maxPitchUp = 22f;
    [SerializeField] private float maxPitchDown = 18f;
    [SerializeField, Range(0f, 0.6f)] private float neckShare = 0.28f;
    [SerializeField] private float smoothTime = 0.1f;

    private Animator animator;
    private PlayerController player;
    private Transform view;
    private ThirdPersonCamera orbit;
    private float yaw;
    private float pitch;
    private float yawVelocity;
    private float pitchVelocity;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        player = GetComponentInParent<PlayerController>();
    }

    private void LateUpdate()
    {
        if (animator == null || !animator.isHuman)
            return;

        if (view == null)
        {
            if (Camera.main != null)
                view = Camera.main.transform;
            if (view == null)
                return;
            orbit = view.GetComponent<ThirdPersonCamera>();
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return;

        Transform body = player != null ? player.transform : transform;
        bool holdStill = player != null && player.IsTraversing;
        float targetYaw = 0f;
        float targetPitch = 0f;
        if (!holdStill)
        {
            // Orbit forward stays level when the view is level. Camera.forward points
            // down at the body because the camera sits above and behind it.
            Vector3 look = orbit != null ? orbit.LookDirection : view.forward;
            Vector3 flatLook = Vector3.ProjectOnPlane(look, Vector3.up);
            float bodyYaw = body.eulerAngles.y;
            float lookYaw = flatLook.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(flatLook.x, flatLook.z) * Mathf.Rad2Deg
                : bodyYaw;
            // look.y is negative while the orbit looks down.
            float lookDown = Mathf.Asin(Mathf.Clamp(-look.y, -1f, 1f)) * Mathf.Rad2Deg;

            targetYaw = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, lookYaw) * yawShare, -maxYaw, maxYaw);
            targetPitch = Mathf.Clamp(lookDown * pitchShare, -maxPitchUp, maxPitchDown);
        }

        float damp = Mathf.Max(0.01f, smoothTime);
        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, damp);
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, damp);
        if (Mathf.Abs(yaw) < 0.05f && Mathf.Abs(pitch) < 0.05f)
            return;

        Vector3 right = body.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(Vector3.up, body.forward);
        right.Normalize();

        Quaternion full = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, right);
        Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        if (neck != null && head.IsChildOf(neck) && neckShare > 0.001f)
        {
            Quaternion neckAdd = Quaternion.Slerp(Quaternion.identity, full, neckShare);
            neck.rotation = neckAdd * neck.rotation;
            // The neck turn already reached the head. Add only the rest.
            Quaternion headAdd = full * Quaternion.Inverse(neckAdd);
            head.rotation = headAdd * head.rotation;
        }
        else
        {
            head.rotation = full * head.rotation;
        }
    }
}
