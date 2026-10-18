using UnityEngine;

public class SearchlightAim : MonoBehaviour
{
    [SerializeField] private Transform lamp;
    [SerializeField] private float maxDistance = 40f;

    private Transform ownerRoot;

    public void SetOwner(Transform owner)
    {
        ownerRoot = owner;
    }

    private void LateUpdate()
    {
        if (lamp == null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = ray.GetPoint(maxDistance);
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

        Vector3 direction = aimPoint - lamp.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
        lamp.rotation = Quaternion.LookRotation(direction, up);
    }

    private bool IsOwner(Transform hitTransform)
    {
        if (ownerRoot == null)
            return false;
        return hitTransform == ownerRoot || hitTransform.IsChildOf(ownerRoot);
    }
}
