using UnityEngine;

/// <summary>
/// Slows time as a running zombie closes in. Inside startDistance the time scale falls
/// exponentially, scale = minScale ^ closeness, reaching minScale at contactDistance.
/// Back to normal speed when the zombie dies.
/// </summary>
[DisallowMultipleComponent]
public class ZombieSlowMotion : MonoBehaviour
{
    [SerializeField] private float startDistance = 8f;
    [SerializeField] private float contactDistance = 0.8f;
    [SerializeField, Range(0.05f, 1f)] private float minScale = 0.2f;
    [Tooltip("Real seconds to ease back to normal speed after the zombie dies.")]
    [SerializeField] private float recoverTime = 0.35f;

    private ZombieController controller;
    private PlayerVitals vitals;
    private float scale = 1f;
    private bool requesting;

    public float CurrentScale => scale;

    private void Awake()
    {
        controller = GetComponent<ZombieController>();
    }

    private void OnDisable()
    {
        StopRequest();
    }

    private void Update()
    {
        if (controller == null)
            return;

        Transform player = controller.Player;
        if (vitals == null && player != null)
            vitals = player.GetComponent<PlayerVitals>();
        // Death freezes time on its own. Leave it alone.
        if (vitals != null && vitals.IsDead)
            return;

        float target = 1f;
        if (controller.IsChasing && player != null)
            target = ScaleAt(PlanarDistance(transform.position, player.position));

        // Approach follows distance exactly; recovery eases in real time.
        if (target < scale)
            scale = target;
        else
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, recoverTime) * (1f - minScale));

        if (scale < 0.999f)
        {
            GameTime.SetSlowMotion(this, scale);
            requesting = true;
        }
        else
        {
            scale = 1f;
            StopRequest();
        }
    }

    public float ScaleAt(float distance)
    {
        float span = Mathf.Max(0.01f, startDistance - contactDistance);
        float closeness = Mathf.Clamp01((startDistance - distance) / span);
        return Mathf.Pow(minScale, closeness);
    }

    private void StopRequest()
    {
        if (!requesting)
            return;

        requesting = false;
        GameTime.ClearSlowMotion(this);
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
