using UnityEngine;

/// <summary>
/// Turns the head toward the camera. While the pistol is up, the upper body,
/// head, and barrel all aim at the crosshair.
/// </summary>
[DefaultExecutionOrder(260)]
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
    private HeldItemVisual heldItems;
    private Transform view;
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
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return;

        Transform body = player != null ? player.transform : transform;
        bool holdStill = player != null && player.IsTraversing;
        if (!holdStill && AimPistolAtCrosshair(head))
            return;

        float targetYaw = 0f;
        float targetPitch = 0f;
        if (!holdStill)
        {
            Vector3 look = view.forward;
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

    private bool AimPistolAtCrosshair(Transform head)
    {
        if (player == null)
            return false;
        if (heldItems == null)
            heldItems = player.GetComponent<HeldItemVisual>();
        if (heldItems == null || heldItems.PistolPoseWeight <= 0.02f)
            return false;

        Camera cam = view.GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 aimPoint = CrosshairPoint(cam, head.position);
        float weight = Mathf.Clamp01(heldItems.PistolPoseWeight);
        ShareBone(HumanBodyBones.Spine, aimPoint, 0.3f * weight);
        ShareBone(HumanBodyBones.Chest, aimPoint, 0.45f * weight);
        ShareBone(HumanBodyBones.UpperChest, aimPoint, 0.6f * weight);
        ShareBone(HumanBodyBones.Neck, aimPoint, 0.4f * weight);
        AimBone(head, aimPoint, weight, 80f);
        heldItems.AimPistolAt(aimPoint, weight);
        return true;
    }

    private void ShareBone(HumanBodyBones bone, Vector3 aimPoint, float share)
    {
        if (share <= 0.001f)
            return;

        Transform joint = animator.GetBoneTransform(bone);
        AimBone(joint, aimPoint, share, 65f);
    }

    private static void AimBone(Transform joint, Vector3 aimPoint, float weight, float maxDegrees)
    {
        if (joint == null || weight <= 0.001f)
            return;

        Vector3 direction = aimPoint - joint.position;
        if (direction.sqrMagnitude < 0.04f)
            return;

        Quaternion correction = Quaternion.FromToRotation(joint.forward, direction.normalized);
        correction = ClampRotation(correction, maxDegrees);
        joint.rotation = Quaternion.Slerp(Quaternion.identity, correction, weight) * joint.rotation;
    }

    private Vector3 CrosshairPoint(Camera cam, Vector3 from)
    {
        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;
        float fallback = 40f;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null)
                continue;
            if (player != null && (hitTransform == player.transform || hitTransform.IsChildOf(player.transform)))
                continue;
            if (hitTransform.CompareTag("Player"))
                continue;

            Vector3 point = hits[i].point;
            if (Vector3.Dot(point - from, player.transform.forward) > 0.35f)
                return point;
        }

        return origin + direction * fallback;
    }

    private static Quaternion ClampRotation(Quaternion rotation, float maxDegrees)
    {
        rotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 0.0001f)
            return Quaternion.identity;
        if (angle > 180f)
            angle -= 360f;
        angle = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
        return Quaternion.AngleAxis(angle, axis);
    }
}
