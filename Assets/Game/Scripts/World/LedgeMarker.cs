using System.Collections.Generic;
using UnityEngine;

public enum LedgeType
{
    Vault,
    ClimbUp,
    ClimbOver
}

/// <summary>
/// Invisible marker on top of an obstacle the player can vault, climb up or climb over.
/// The transform sits on the middle of the top surface, which is length (local X) by depth (local Z).
/// Vault and Climb Over cross along local Z only, from either side; Climb Up can be grabbed from all four sides.
/// </summary>
public class LedgeMarker : MonoBehaviour
{
    private static readonly List<LedgeMarker> active = new List<LedgeMarker>();

    // Keeps the hands off the very corners.
    private const float CornerMargin = 0.1f;

    [SerializeField] private LedgeType type = LedgeType.Vault;
    [Tooltip("Top surface size along local X.")]
    [SerializeField] private float length = 1f;
    [Tooltip("Top surface size along local Z, the way Vault and Climb Over cross it.")]
    [SerializeField] private float depth = 0.5f;

    public LedgeType Type => type;

    public struct Grab
    {
        public LedgeMarker Marker;
        public LedgeType Type;
        // Top of the near face, where the hands go.
        public Vector3 Lip;
        // Flat direction across the obstacle, away from the player.
        public Vector3 Into;
        public float Depth;
        public float Distance;
    }

    public void Configure(LedgeType ledgeType, float topLength, float topDepth)
    {
        type = ledgeType;
        length = topLength;
        depth = topDepth;
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    /// <summary>
    /// Nearest marker edge in front of the player, within reach and the given height above the feet.
    /// </summary>
    public static bool TryFindNearest(Vector3 feet, Vector3 facing, float reach, float maxHeight, float maxAngle, out Grab best)
    {
        best = default;
        bool found = false;
        float minDot = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
        for (int i = 0; i < active.Count; i++)
        {
            LedgeMarker marker = active[i];
            int sides = marker.type == LedgeType.ClimbUp ? 2 : 1;
            for (int side = 0; side < sides; side++)
            {
                if (!marker.TryGetGrab(feet, side == 1, out Grab grab))
                    continue;

                float height = grab.Lip.y - feet.y;
                if (height < 0.1f || height > maxHeight)
                    continue;
                if (grab.Distance > reach || Vector3.Dot(facing, grab.Into) < minDot)
                    continue;
                if (found && grab.Distance >= best.Distance)
                    continue;

                best = grab;
                found = true;
            }
        }

        return found;
    }

    // acrossLength false: grab a long edge and cross along local Z. True: grab an end edge and cross along local X.
    private bool TryGetGrab(Vector3 feet, bool acrossLength, out Grab grab)
    {
        grab = default;
        Vector3 localX = transform.right;
        Vector3 localZ = transform.forward;
        localX.y = 0f;
        localZ.y = 0f;
        if (localX.sqrMagnitude < 0.0001f || localZ.sqrMagnitude < 0.0001f)
            return false;
        localX.Normalize();
        localZ.Normalize();

        float sizeX = length * Mathf.Abs(transform.lossyScale.x);
        float sizeZ = depth * Mathf.Abs(transform.lossyScale.z);
        Vector3 edgeAxis = acrossLength ? localZ : localX;
        Vector3 crossAxis = acrossLength ? localX : localZ;
        float edgeLength = acrossLength ? sizeZ : sizeX;
        float thickness = acrossLength ? sizeX : sizeZ;

        // The player crosses away from whichever side they stand on.
        Vector3 toFeet = feet - transform.position;
        toFeet.y = 0f;
        Vector3 into = Vector3.Dot(toFeet, crossAxis) <= 0f ? crossAxis : -crossAxis;

        float halfEdge = Mathf.Max(0f, edgeLength * 0.5f - CornerMargin);
        float along = Mathf.Clamp(Vector3.Dot(toFeet, edgeAxis), -halfEdge, halfEdge);
        Vector3 lip = transform.position + edgeAxis * along - into * (thickness * 0.5f);

        Vector3 flat = lip - feet;
        flat.y = 0f;
        grab.Marker = this;
        grab.Type = type;
        grab.Lip = lip;
        grab.Into = into;
        grab.Depth = thickness;
        grab.Distance = flat.magnitude;
        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = type == LedgeType.Vault ? new Color(0.3f, 0.9f, 0.4f)
            : type == LedgeType.ClimbUp ? new Color(0.3f, 0.6f, 1f)
            : new Color(1f, 0.6f, 0.2f);

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, 0.02f, depth));
        // The crossing direction: one arrow for Vault and Climb Over, both axes for Climb Up.
        Gizmos.DrawLine(new Vector3(0f, 0f, -depth * 0.5f), new Vector3(0f, 0f, depth * 0.5f));
        if (type == LedgeType.ClimbUp)
            Gizmos.DrawLine(new Vector3(-length * 0.5f, 0f, 0f), new Vector3(length * 0.5f, 0f, 0f));
        Gizmos.matrix = previous;
    }
}
