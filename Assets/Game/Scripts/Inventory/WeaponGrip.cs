using UnityEngine;

/// <summary>
/// Hold point stored on the weapon prefab. Grip sits where the right hand closes.
/// LeftHandGrip is optional and only used by two-handed weapons.
/// </summary>
public class WeaponGrip : MonoBehaviour
{
    [SerializeField] private Transform grip;
    [SerializeField] private Transform leftHandGrip;

    public Transform Grip
    {
        get
        {
            Resolve();
            return grip;
        }
    }

    public Transform LeftHandGrip
    {
        get
        {
            Resolve();
            return leftHandGrip;
        }
    }

    public bool HasLeftHandGrip
    {
        get { return LeftHandGrip != null; }
    }

    private void Awake()
    {
        Resolve();
    }

    public void Resolve()
    {
        if (grip == null)
            grip = FindNamed(transform, "Grip");
        if (leftHandGrip == null)
            leftHandGrip = FindNamed(transform, "LeftHandGrip");
    }

    /// <summary>
    /// Moves this weapon so Grip lands on the parent origin.
    /// Rotation is the inverse of the grip rotation. Position is that rotation
    /// applied to the grip position, then negated. Both stay in local space.
    /// The weapon root scale is part of the offset: the pistol root is 0.1, and
    /// without it the handle would miss the hand by that factor.
    /// </summary>
    public void AlignToParentOrigin()
    {
        Resolve();
        if (grip == null)
        {
            Debug.LogError("[WeaponGrip] " + name + " needs a child named Grip.");
            return;
        }

        AlignToParentOrigin(transform, grip);
    }

    public static void AlignToParentOrigin(Transform weapon, Transform gripPoint)
    {
        if (weapon == null || gripPoint == null || gripPoint == weapon)
            return;

        Vector3 gripPosition = gripPoint.localPosition;
        Quaternion gripRotation = gripPoint.localRotation;
        Transform cursor = gripPoint.parent;
        while (cursor != null && cursor != weapon)
        {
            gripPosition = cursor.localPosition + cursor.localRotation * Vector3.Scale(cursor.localScale, gripPosition);
            gripRotation = cursor.localRotation * gripRotation;
            cursor = cursor.parent;
        }

        if (cursor != weapon)
        {
            Debug.LogError("[WeaponGrip] Grip must live under " + weapon.name + ".");
            return;
        }

        Quaternion localRotation = Quaternion.Inverse(gripRotation);
        Vector3 scaledGrip = Vector3.Scale(weapon.localScale, gripPosition);
        weapon.localRotation = localRotation;
        weapon.localPosition = -(localRotation * scaledGrip);
    }

    private static Transform FindNamed(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != root && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        DrawAxes(grip != null ? grip : FindNamed(transform, "Grip"), 0.08f);
        DrawAxes(leftHandGrip != null ? leftHandGrip : FindNamed(transform, "LeftHandGrip"), 0.06f);
    }

    private static void DrawAxes(Transform point, float length)
    {
        if (point == null)
            return;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(point.position, point.forward * length);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(point.position, point.up * length);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(point.position, point.right * length);
    }
}
