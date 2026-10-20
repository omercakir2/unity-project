using UnityEngine;

public class SearchlightAim : MonoBehaviour
{
    [SerializeField] private Transform lamp;
    [SerializeField] private float maxDistance = 40f;
    [SerializeField] private float maxAimAngle = 22f;
    [SerializeField] private float aimSharpness = 18f;

    private Transform ownerRoot;

    public void SetOwner(Transform owner)
    {
        ownerRoot = owner;
    }

    public bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = transform.position + transform.forward * maxDistance;
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        aimPoint = ray.GetPoint(maxDistance);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || IsOwner(hitTransform))
                continue;
            aimPoint = hits[i].point;
            break;
        }

        return true;
    }

    private void LateUpdate()
    {
        AimNow();
    }

    public void AimNow()
    {
        if (lamp == null || !TryGetAimPoint(out Vector3 aimPoint))
            return;

        Vector3 direction = aimPoint - lamp.position;
        if (direction.sqrMagnitude < 0.0001f || lamp.parent == null)
            return;

        Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
        Quaternion desiredWorld = Quaternion.LookRotation(direction, up);
        Quaternion delta = Quaternion.Inverse(lamp.parent.rotation) * desiredWorld;
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (axis.sqrMagnitude < 0.0001f || float.IsNaN(axis.x))
            delta = Quaternion.identity;
        else if (angle > maxAimAngle)
            delta = Quaternion.AngleAxis(maxAimAngle, axis);

        float blend = 1f - Mathf.Exp(-aimSharpness * Time.deltaTime);
        lamp.localRotation = Quaternion.Slerp(lamp.localRotation, delta, blend);
    }

    private bool IsOwner(Transform hitTransform)
    {
        if (ownerRoot == null)
            return false;
        return hitTransform == ownerRoot || hitTransform.IsChildOf(ownerRoot);
    }
}
