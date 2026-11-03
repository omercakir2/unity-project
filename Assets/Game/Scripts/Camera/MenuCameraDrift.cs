using UnityEngine;

/// <summary>
/// Slow arc and breathing push around a subject, for the title screen backdrop.
/// </summary>
[DisallowMultipleComponent]
public class MenuCameraDrift : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.9f, 0f);
    [Tooltip("Aims this far to the subject's left so it sits right of centre, leaving room for the menu.")]
    [SerializeField] private float frameShift = 1.7f;
    [SerializeField] private float radius = 7f;
    [SerializeField] private float height = 1.6f;
    [Tooltip("Centre of the arc in degrees around the target. 0 is in front of it.")]
    [SerializeField] private float centerAngle = -28f;
    [SerializeField] private float arcDegrees = 22f;
    [SerializeField] private float arcPeriod = 40f;
    [SerializeField] private float pushAmount = 0.5f;
    [SerializeField] private float pushPeriod = 17f;

    private void LateUpdate()
    {
        if (target == null)
            return;

        float time = Time.unscaledTime;
        float angle = centerAngle + Mathf.Sin(time * Mathf.PI * 2f / arcPeriod) * arcDegrees * 0.5f;
        float r = radius - pushAmount * 0.5f * (1f + Mathf.Sin(time * Mathf.PI * 2f / pushPeriod));
        Quaternion around = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3 forward = target.forward;
        forward.y = 0f;
        Vector3 offset = around * (forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward) * r;

        transform.position = target.position + offset + Vector3.up * height;
        Vector3 look = target.position + lookOffset - transform.position;
        Vector3 right = Vector3.Cross(Vector3.up, look).normalized;
        transform.rotation = Quaternion.LookRotation(look - right * frameShift, Vector3.up);
    }
}
